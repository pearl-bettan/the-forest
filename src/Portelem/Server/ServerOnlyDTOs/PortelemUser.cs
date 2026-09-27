namespace UsersManager.Shared;
// מחלקה שמכילה את המידע על משתמש כפי שהוא מופיע בפורטלם
public class PortelemUser
{
    // המזהה הפנימי של המשתמש, מפתח ראשי בטבלה
    public int Id { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string Email { get; set; }

    // המזהה של המשתמש בפורטלם, כפי שהוא מופיע בטוקן  
    public int PortelemId { get; set; }
}