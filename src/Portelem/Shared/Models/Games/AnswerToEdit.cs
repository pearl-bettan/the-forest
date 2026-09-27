using System.ComponentModel.DataAnnotations;

namespace AuthTemplate.Shared.Models.Games
{
    //  אבן אחת שהשחקן יסדר
   
    public class AnswerToEdit
    {

        public int ID { get; set; }

        // תוכן הפריט: טקסט או תמונה
        [Required(ErrorMessage = "חובה להזין תוכן")]
        [StringLength(100, ErrorMessage = "התוכן ארוך מדי")]
        public string Content { get; set; }

        // האם תוכן הסלע היא תמונה
        public bool IsImage { get; set; }
    }
}
