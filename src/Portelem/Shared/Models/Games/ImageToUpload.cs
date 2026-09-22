namespace AuthTemplate.Shared.Models.Games
{
    // העברת קובץ תמונה מהעורך אל השרת.
    // הקובץ נשלח כמחרוזת base64, והשרת שומר אותו בתיקיית uploadedFiles
    public class ImageToUpload
    {
        // תוכן הקובץ בקידוד base64
        public string ImageBase64 { get; set; }

        // סיומת הקובץ, למשל png או jpg
        public string Extension { get; set; }
    }
}
