using System.ComponentModel.DataAnnotations;

namespace AuthTemplate.Shared.Models.Games
{
  // מסך לעדכון משחק
    public class GameToEdit
    {
        // מזהה המשחק לעדכון
        public int ID { get; set; }

        [Required(ErrorMessage = "חובה להזין שם")]
        [MinLength(1, ErrorMessage = "שם המשחק יכיל בין 1-30 תווים")]
        [StringLength(30, ErrorMessage = "שם המשחק יכיל בין 1-30 תווים")]
        public string GameName { get; set; }

        // הזמן לשאלה בשניות. 0 = ללא הגבלה
        [Range(0, 300, ErrorMessage = "יש לבחור זמן לשאלה")]
        public int TimePerQuestion { get; set; } = -1;
    }
}
