using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace UsersManager.Server
{
    // ============================================================
    // מסנן פעולה שמוודא שהבקשה הגיעה ממשתמש מחובר.
    //
    // נרשם על Controller או על פעולה בעזרת [ServiceFilter], ורץ
    // לפני גוף הפעולה. בסוף בדיקה מוצלחת הוא מזריק את מזהה
    // המשתמש לתוך ארגומנטי הפעולה תחת השם authUserId, וכך כל
    // פעולה מקבלת את המזהה בלי לפענח את הטוקן בעצמה.
    //
    // זו נקודת האכיפה המרכזית: משחק שייך למשתמש, וללא המזהה
    // הזה אי אפשר לוודא שמי שמבקש לערוך משחק הוא אכן הבעלים
    // ============================================================
    public class AuthCheck : IAsyncActionFilter
    {
        private const string AuthUserIdKey = "authUserId";
        private readonly ITokenBlacklistService _tokenBlacklistService;
        private readonly TokenService _tokenService;
        private readonly ILogger<AuthCheck> _logger;

        // כל התלויות מוזרקות, ואף אחת מהן אינה רשאית להיות ריקה.
        // בדיקת null בבנאי מבטיחה שתקלת הרשמה ב-Program.cs תתגלה
        // בעליית השרת ולא בבקשה הראשונה
        public AuthCheck(
            ITokenBlacklistService tokenBlacklistService,
            TokenService tokenService,
            ILogger<AuthCheck> logger)
        {
            _tokenBlacklistService =
                tokenBlacklistService ?? throw new ArgumentNullException(nameof(tokenBlacklistService));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ============================================================
        // הבדיקה עצמה, לפי הסדר:
        //   1. יש טוקן בכלל
        //   2. הטוקן חתום ובתוקף
        //   3. יש בו מזהה משתמש מספרי
        //   4. הטוקן לא נפסל בהתנתקות
        //
        // סדר הבדיקות אינו מקרי: אימות החתימה זול יותר מפנייה
        // לבסיס הנתונים, ולכן הרשימה השחורה נבדקת אחרונה.
        // כל כישלון מחזיר 401 ועוצר את הבקשה בלי לקרוא ל-next
        // ============================================================
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var token = ExtractToken(context.HttpContext.Request);

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Authentication failed: Missing token");
                context.Result = new UnauthorizedResult();
                return;
            }

            // Validate token first (cheaper than DB lookup)
            var principal = _tokenService.ValidateToken(token);
            if (principal == null)
            {
                _logger.LogWarning("Authentication failed: Invalid token");
                context.Result = new UnauthorizedResult();
                return;
            }

            // Extract userId early for logging
            var userId = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out int userIdInt))
            {
                _logger.LogWarning("Authentication failed: Invalid user ID in token");
                context.Result = new UnauthorizedResult();
                return;
            }

            // Check blacklist after validation
            if (_tokenBlacklistService.IsBlacklisted(token))
            {
                _logger.LogWarning("Authentication failed: Token blacklisted for user {UserId}", userIdInt);
                context.Result = new UnauthorizedResult();
                return;
            }

            // הזרקת המזהה לפעולה. הפרמטר בחתימת הפעולה חייב
            // להיקרא בדיוק authUserId כדי שההשמה הזו תתפוס
            context.ActionArguments[AuthUserIdKey] = userIdInt;

            // רק כאן הבקשה ממשיכה לגוף הפעולה
            await next();
        }

        // שולף את הטוקן מכותרת Authorization.
        // מקבל גם כותרת בלי התחילית "Bearer ", כדי שלקוח שלא
        // הוסיף אותה לא ייכשל סתם
        private static string ExtractToken(HttpRequest request)
        {
            var authHeader = request.Headers["Authorization"].ToString();
            return authHeader.StartsWith(AuthConstants.BearerPrefix, StringComparison.OrdinalIgnoreCase)
                ? authHeader.Substring(AuthConstants.BearerPrefix.Length)
                : authHeader;
        }
    }
}