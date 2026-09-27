using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AuthTemplate.Shared.Models.Games
{
    // שאלה לעריכה.
   
    public class QuestionToEdit
    {
        public int ID { get; set; }

        // המשחק שאליו שייכת השאלה
        public int GameId { get; set; }

        // ערך הקיצון בצד ימין
        [Required(ErrorMessage = "חובה להזין ערך קיצון")]
        [StringLength(10, ErrorMessage = "עד 10 תווים")]
        public string RightTag { get; set; }

        // ערך הקיצון בצד שמאל
        [Required(ErrorMessage = "חובה להזין ערך קיצון")]
        [StringLength(10, ErrorMessage = "עד 10 תווים")]
        public string LeftTag { get; set; }

        // סדר השאלה בתוך המשחק
        public int QuestionOrder { get; set; }

        // הפריטים בסדר הנכון. הראשון ברשימה הוא המקום הראשון
        public List<AnswerToEdit> Answers { get; set; } = new List<AnswerToEdit>();
    }
}
