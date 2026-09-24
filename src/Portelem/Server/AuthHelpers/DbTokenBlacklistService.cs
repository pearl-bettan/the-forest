using System.IdentityModel.Tokens.Jwt;
using Data;


namespace UsersManager.Server
{
    // ============================================================
    // מימוש הרשימה השחורה מעל טבלת BlackList בבסיס הנתונים.
    //
    // בחירה בבסיס נתונים ולא בזיכרון נועדה לכך שהתנתקות תישאר
    // בתוקף גם אחרי הפעלה מחדש של השרת. כל שורה מחזיקה את
    // תאריך התפוגה של הטוקן, וכך שירות הרקע יודע מה למחוק
    // ============================================================
    public class DbTokenBlacklistService : ITokenBlacklistService
    {
        private readonly DbRepository _db;
        private readonly ILogger<DbTokenBlacklistService> _logger;

        public DbTokenBlacklistService(DbRepository db, ILogger<DbTokenBlacklistService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ============================================================
        // מוסיף טוקן לרשימה בהתנתקות.
        //
        // תאריך התפוגה נשלף מתוך הטוקן עצמו ולא מחושב מחדש, כדי
        // שהשורה תימחק בדיוק כשהטוקן ממילא כבר לא תקף.
        // ה-INSERT מותנה ב-NOT EXISTS כדי שהתנתקות כפולה לא
        // תיצור שתי שורות לאותו טוקן.
        //
        // כישלון כאן נזרק הלאה: אם לא הצלחנו לפסול טוקן, אסור
        // לדווח למשתמש שההתנתקות הצליחה
        // ============================================================
        public void AddToBlacklist(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("AddToBlacklist: Attempted to blacklist null or empty token");
                return;
            }

            try
            {
                // ✅ Extract expiration date from the token itself
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
        
                // Get the 'exp' claim (expiration time in Unix timestamp)
                var expiresAt = jwtToken.ValidTo;

                string query = @"
                    INSERT INTO BlackList (Token, BlacklistedAt, ExpiresAt) 
                    SELECT @Token, @BlacklistedAt, @ExpiresAt
                    WHERE NOT EXISTS (
                        SELECT 1 FROM BlackList WHERE Token = @Token
                    )";

                var result = _db.SaveDataAsync(query, new
                {
                    Token = token,
                    BlacklistedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                }).Result;

                _logger.LogInformation("Token added to database blacklist");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add token to blacklist");
                throw;
            }
        }

        // ============================================================
        // בודק אם הטוקן נפסל. נקרא בכל בקשה מאומתת.
        //
        // התנאי על ExpiresAt מוודא ששורות ישנות שטרם נוקו אינן
        // משפיעות על התוצאה.
        //
        // בשגיאה מוחזר false ולא true: תקלה בבסיס הנתונים לא
        // תנעל את כל המשתמשים מחוץ למערכת
        // ============================================================
        public bool IsBlacklisted(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            try
            {
                // SQLite uses datetime('now') for current time comparison
                string query = @"
                    SELECT COUNT(*) 
                    FROM BlackList 
                    WHERE Token = @Token 
                    AND ExpiresAt > @CurrentTime";

                var count = _db.GetRecordsAsync<int>(query, new
                {
                    Token = token,
                    CurrentTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                }).Result.FirstOrDefault();

                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if token is blacklisted");
                // On error, don't block legitimate users
                return false;
            }
        }

        // מוחק מהרשימה שורות שתוקפן פג. מופעל פעם ביום על ידי
        // TokenCleanupBackgroundService. שגיאה נרשמת ביומן ולא
        // נזרקת, כדי שתקלה בניקוי לא תפיל את שירות הרקע
        public void RemoveExpiredTokens()
        {
            try
            {
                string query = "DELETE FROM BlackList WHERE ExpiresAt < @CurrentTime";
                
                int deleted = _db.SaveDataAsync(query, new 
                { 
                    CurrentTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                }).Result;
                
                _logger.LogInformation("Removed {Count} expired tokens from blacklist", deleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove expired tokens");
            }
        }
    }
}