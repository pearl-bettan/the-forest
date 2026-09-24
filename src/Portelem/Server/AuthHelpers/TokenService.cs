using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace UsersManager.Server
{
    // ============================================================
    // ייצור ואימות של טוקני JWT.
    //
    // הטוקן נחתם במפתח סימטרי שיושב ב-appsettings, ולכן השרת
    // יכול לאמת אותו בלי לשמור שום מצב. כל המידע על המשתמש
    // יושב בתוך הטוקן עצמו, בתביעות.
    //
    // לא לגעת - ניהול טוקנים
    // ============================================================
    public class TokenService
    {
        private readonly IConfiguration _configuration;
        private readonly string _securityKey;
        private readonly string _validIssuer;
        // המפתח והמנפיק נקראים פעם אחת מקובץ ההגדרות.
        // שינוי המפתח פוסל מיידית את כל הטוקנים הקיימים
        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            _securityKey = _configuration["JWTSettings:securityKey"];
            _validIssuer = _configuration["JWTSettings:validIssuer"];
        }

        // בונה טוקן חתום מרשימת תביעות, בתוקף ליומיים.
        // מוחזר ללקוח בהתחברות ונשלח בכל בקשה בכותרת Authorization
        public string GenerateToken(List<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_securityKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _validIssuer,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(2),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        // ============================================================
        // מאמת טוקן ומחזיר את זהות המשתמש שבו, או null אם נכשל.
        //
        // נבדקים המנפיק, תוקף החתימה ותאריך התפוגה. הקהל אינו
        // נבדק כי לשרת הזה יש צרכן אחד בלבד.
        // ClockSkew אופס: ברירת המחדל מאפשרת חמש דקות חריגה,
        // וכאן רוצים שתוקף שפג ייפסל מיד.
        //
        // כל חריגה נבלעת ומוחזר null, כי מבחינת הקורא כישלון
        // אימות הוא תוצאה ולא תקלה
        // ============================================================
        public ClaimsPrincipal ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            
            // Basic JWT format validation
            if (!tokenHandler.CanReadToken(token))
            {
                return null;
            }

            var key = Encoding.UTF8.GetBytes(_securityKey);
            try
            {
                var claimsPrincipal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _validIssuer,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return claimsPrincipal;
            }
            catch
            {
                return null;
            }
        }

        // בודק אם תוקף הטוקן פג, בלי לבדוק את החתימה
        public bool IsTokenExpired(string token)
        {
            var jwtToken = new JwtSecurityTokenHandler().ReadToken(token) as JwtSecurityToken;
            return jwtToken.ValidTo < DateTime.UtcNow;
        }

        // מחדש טוקן שעומד לפוג בחמש הדקות הקרובות, ומחזיר אותו
        // כמות שהוא אם עוד יש לו זמן. טוקן לא תקין מחזיר null.
        // התביעות מועתקות כפי שהן, ולכן המשתמש נשאר אותו משתמש
        public string RefreshToken(string token)
        {
            var principal = ValidateToken(token);
            if (principal == null)
            {
                return null;
            }

            var jwtToken = new JwtSecurityTokenHandler().ReadToken(token) as JwtSecurityToken;
            var expirationTime = jwtToken.ValidTo;
            var currentTime = DateTime.UtcNow;

            // Refresh the token if it's about to expire in the next 5 minutes
            if (expirationTime > currentTime && expirationTime <= currentTime.AddMinutes(5))
            {
                var claims = principal.Claims.ToList();
                return GenerateToken(claims);
            }

            return token; 
        }

        // ============================================================
        // שולף את התביעות מתוך טוקן בלי לאמת אותו.
        //
        // משמש לקריאת הטוקן של פורטלם, שנחתם במפתח של פורטלם
        // ולא במפתח של המחולל ולכן אינו עובר כאן אימות חתימה.
        // רשימה ריקה מוחזרת על כל תקלה, כדי שהקורא לא יצטרך
        // לטפל בחריגות
        // ============================================================
        public List<Claim> GetClaims(string jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt))
            {
                return new List<Claim>();
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                
                // Basic JWT format validation
                if (!handler.CanReadToken(jwt) || jwt.Split('.').Length != 3)
                {
                    return new List<Claim>();
                }

                var token = handler.ReadJwtToken(jwt);
                return token.Claims.ToList();
            }
            catch
            {
                return new List<Claim>();
            }
        }

    }
}
