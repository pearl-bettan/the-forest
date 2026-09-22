using System.ComponentModel.DataAnnotations;

namespace AuthTemplate.Shared.Models.Games
{
    // DTO להעברת פרטי המשחק החדש מהטופס אל ה-Controller.
    // מכיל רק את מה שהעורך מזין בפופאפ - שאר הפרטים נקבעים בשרת
    public class GameToAdd
    {
        [Required(ErrorMessage = "חובה להזין שם")]
        [MinLength(1, ErrorMessage = "שם המשחק יכיל בין 1-30 תווים")]
        [StringLength(30, ErrorMessage = "שם המשחק יכיל בין 1-30 תווים")]
        public string GameName { get; set; }

        // הזמן לשאלה בשניות. 0 = ללא הגבלה.
        // ברירת המחדל היא 1- כדי שהוולידציה תדרוש בחירה מפורשת
        [Range(0, 300, ErrorMessage = "יש לבחור זמן לשאלה")]
        public int TimePerQuestion { get; set; } = -1;
    }
}
