namespace ForestGame.Shared.UnityDtos
{
    // תשובה

    public class AnswerForUnityDto
    {
        // תוכן התשובה: הטקסט עצמו, או שם קובץ התמונה.
        // uploadedFiles התמונה נשמרת בתקיית  של 
        public string Content { get; set; }

        // תמונה או טקסט
        public bool IsImage { get; set; }
    }
}
