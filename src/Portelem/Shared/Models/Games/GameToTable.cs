namespace AuthTemplate.Shared.Models.Games
{
    // DTO להצגת כל משחק בטבלת "המשחקים שלי"
    // מכיל רק את המידע שהטבלה צריכה, ולא את כל המידע שבבסיס הנתונים
    public class GameToTable
    {
        public int ID { get; set; }

        public string GameName { get; set; }

        // קוד המשחק שהשחקן מזין ביוניטי
        public int GameCode { get; set; }

        // כמה שניות יש לשחקן לכל שאלה. 0 = ללא הגבלת זמן
        public int TimePerQuestion { get; set; }

        // האם המשחק מפורסם
        public bool IsPublish { get; set; }

        // האם המשחק עומד בתנאי הפרסום
        public bool CanPublish { get; set; }

        // מספר השאלות במשחק, מוצג בעמודה בטבלה
        public int QuestionsCount { get; set; }
    }
}
