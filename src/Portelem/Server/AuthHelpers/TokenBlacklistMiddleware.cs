namespace UsersManager.Server
{
    // ============================================================
    // שכבת ביניים שחוסמת בקשות שמגיעות עם טוקן שנפסל.
    //
    // רשומה ב-Program.cs לפני האימות, ולכן היא רצה על כל בקשה
    // ומחזירה 401 עוד לפני שהבקשה מגיעה ל-Controller.
    //
    // לא לגעת - אבטחת משתמשים
    // ============================================================
    public class TokenBlacklistMiddleware
    {
        private readonly RequestDelegate _next;

        // next הוא השלב הבא בשרשרת. שכבת ביניים חייבת להחזיק
        // אותו כדי להעביר הלאה בקשה שעברה את הבדיקה
        public TokenBlacklistMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // רץ על כל בקשה: שולף את הטוקן מכותרת Authorization,
        // ואם הוא ברשימה השחורה עוצר מיד ב-401.
        // שירות הרשימה מוזרק לפי בקשה ולא בבנאי, כי הוא scoped
        public async Task Invoke(HttpContext context, ITokenBlacklistService tokenBlacklistService)
        {
            var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (!string.IsNullOrEmpty(token) && tokenBlacklistService.IsBlacklisted(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await _next(context);
        }
    }
}
