namespace UsersManager.Server
{
    // מחלקה שמכילה את המידע המינימלי על משתמש, כפי שהוא מוצג ברשימות  
    public class MinimalUser
    {
        public string Email { get; set; }

        // הסיסמה המוצפנת כפי שהיא שמורה בבסיס הנתונים.
        public string PasswordHash { get; set; }

    }
}
