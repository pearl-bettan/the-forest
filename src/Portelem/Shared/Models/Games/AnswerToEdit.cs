using System.ComponentModel.DataAnnotations;

namespace AuthTemplate.Shared.Models.Games
{
    // תשובה בודדת בשאלה - פריט אחד שהשחקן יגרור
    public class AnswerToEdit
    {
        public int ID { get; set; }

        // תוכן הפריט: טקסט או שם קובץ תמונה, לפי IsImage.
        // מגבלת 25 התווים חלה על טקסט בלבד ונאכפת בטופס עצמו.
        // כאן המגבלה רחבה יותר, כי שם קובץ תמונה נוצר מ-GUID
        // ואורכו כ-40 תווים
        [Required(ErrorMessage = "חובה להזין תוכן")]
        [StringLength(100, ErrorMessage = "התוכן ארוך מדי")]
        public string Content { get; set; }

        // האם התוכן הוא שם של תמונה
        public bool IsImage { get; set; }
    }
}
