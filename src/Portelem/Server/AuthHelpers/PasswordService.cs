using Microsoft.AspNetCore.Identity;

namespace UsersManager.Server
{
    // ============================================================
    // הצפנת סיסמאות ואימותן, מעל PasswordHasher של ASP.NET Identity.
    //
    // הסיסמה עצמה לעולם אינה נשמרת - רק תוצאת ה-hash, שכוללת
    // בתוכה גם את המלח ואת מספר הסיבובים.
    // במסלול ההתחברות דרך פורטלם המחלקה אינה בשימוש, כי הזיהוי
    // נעשה מול הטוקן. היא נשארת עבור רישום מקומי.
    //
    // לא לגעת - הצפנת סיסמאות
    // ============================================================
    public class PasswordService
    {
        private readonly IPasswordHasher<MinimalUser> _passwordHasher;

        // יוצר מצפין חדש עם הגדרות ברירת המחדל של ASP.NET
        public PasswordService()
        {
            _passwordHasher = new PasswordHasher<MinimalUser>();
        }

        // מקבל משתמש וסיסמה גלויה, ומחזיר את המחרוזת המוצפנת
        // שתישמר בבסיס הנתונים
        public string HashPassword(MinimalUser user, string password)
        {
            return _passwordHasher.HashPassword(user, password);
        }

        // משווה סיסמה גלויה מול ה-hash השמור של המשתמש.
        // מחזיר Success, Failed, או SuccessRehashNeeded כשהסיסמה
        // נכונה אבל הוצפנה בשיטה ישנה יותר
        public PasswordVerificationResult VerifyPassword(MinimalUser user, string password)
        {
            return _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        }

    }
}
