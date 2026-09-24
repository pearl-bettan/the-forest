namespace UsersManager.Server
{
    // ============================================================
    // תוצאת שאילתת משתמש מבסיס הנתונים, לפני שהיא הופכת ל-User
    // שנשלח ללקוח.
    //
    // קיימת בנפרד כדי שהשכבה שמדברת עם בסיס הנתונים לא תחזיר
    // ישירות טיפוס שמוגדר ב-Shared
    // ============================================================
    public class UserFromDB
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }

    }
}
