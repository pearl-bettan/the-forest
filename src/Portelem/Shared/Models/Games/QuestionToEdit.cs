using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AuthTemplate.Shared.Models.Games
{
    // ============================================================
    // שאלה אחת במשחק, על כל הפריטים שלה.
    //
    // במשחק סדר "שאלה" היא רצף אחד שהשחקן צריך לסדר בין שתי
    // תגיות קיצון. נע בשני הכיוונים בין עמוד העריכה לשרת
    // ============================================================
    public class QuestionToEdit
    {
        // 0 לשאלה חדשה שעדיין לא נשמרה
        public int ID { get; set; }

        // המשחק שאליו שייכת השאלה
        public int GameId { get; set; }

        // נוסח השאלה, כפי שהשחקן יראה אותו מעל הרצף
        [StringLength(50, ErrorMessage = "עד 50 תווים")]
        public string Topic { get; set; } = "";

        // ערך הקיצון בצד ימין של הרצף
        [Required(ErrorMessage = "חובה להזין ערך קיצון")]
        [StringLength(10, ErrorMessage = "עד 10 תווים")]
        public string RightTag { get; set; }

        // ערך הקיצון בצד שמאל של הרצף
        [Required(ErrorMessage = "חובה להזין ערך קיצון")]
        [StringLength(10, ErrorMessage = "עד 10 תווים")]
        public string LeftTag { get; set; }

        // סדר השאלה בתוך המשחק
        public int QuestionOrder { get; set; }

        // הפריטים בסדר הנכון. הראשון ברשימה הוא המקום הראשון
        public List<AnswerToEdit> Answers { get; set; } = new List<AnswerToEdit>();
    }
}
