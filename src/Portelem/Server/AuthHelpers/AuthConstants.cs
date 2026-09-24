namespace UsersManager.Server
{
    // ============================================================
    // מחרוזות קבועות של מנגנון ההתחברות.
    //
    // ריכוז במקום אחד כדי שקוד התחברות, ה-Controller וה-Repository
    // ישוו את אותן מחרוזות בדיוק. שגיאת כתיב בקוד תוצאה הייתה
    // מתגלה רק בזמן ריצה
    // ============================================================
    /// <summary>
    /// Constants for authentication operations
    /// </summary>
    public static class AuthConstants
    {
        // התחילית של כותרת Authorization. נחתכת מהערך כדי
        // להשאיר את הטוקן עצמו
        public const string BearerPrefix = "Bearer ";
        
        // ---------- קודי תוצאה של פעולות התחברות ----------

        // לא נמצא משתמש מתאים
        public const string NoUser = "no user";

        // המשתמש כבר קיים. מאז שהרישום הפך לעדכון-או-הוספה
        // לפי המזהה מפורטלם, הקוד הזה אינו מוחזר יותר
        public const string UserExists = "exist";

        // הרישום נכשל מסיבה שאינה קשורה לבסיס הנתונים
        public const string ErrorSignup = "error signup";

        // ההוספה לבסיס הנתונים נכשלה
        public const string ErrorInsertToDb = "error insert to DB";

        // הטוקן שהתקבל אינו תקין או שפג תוקפו
        public const string TokenInvalid = "token invalid";
        
        // ---------- שמות התביעות בתוך טוקן פורטלם ----------
        // Claim types
        public const string ClaimTypeEmail = "email";
        public const string ClaimTypeSub = "sub";
        public const string ClaimTypeGivenName = "given_name";
        public const string ClaimTypeFamilyName = "family_name";
    }
}

