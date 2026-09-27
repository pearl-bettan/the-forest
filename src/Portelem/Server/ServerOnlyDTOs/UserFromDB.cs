namespace UsersManager.Server
{
   // מחלקה שמכילה את המידע על משתמש כפי שהוא שמור בבסיס הנתונים
    public class UserFromDB
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }

    }
}
