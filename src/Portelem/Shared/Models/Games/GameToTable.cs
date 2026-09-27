namespace AuthTemplate.Shared.Models.Games
{
    // שורה אחת בטבלת "המשחקים שלי",    
    public class GameToTable
    {
        // מזהה המשחק, משמש למעבר לעמוד העריכה ולפעולות על השורה
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
