namespace AuthTemplate.Shared.Models.Games
{
    // קובץ תמונה שמעלים לשרת
    public class ImageToUpload
    {
        // base64 תוכן הקובץ בקידוד 
        public string ImageBase64 { get; set; }

        // סיומת הקובץ, למשל png או jpg
        public string Extension { get; set; }
    }
}
