namespace AuthTemplate.Shared.Models.Games
{
    // ============================================================
    // קובץ תמונה בדרך מהעורך אל השרת.
    //
    // הדפדפן קורא את הקובץ ומעביר אותו כמחרוזת base64 בתוך JSON,
    // כדי לא להסתבך עם multipart. השרת מפענח, מייצר שם חדש מ-GUID
    // ושומר בתיקיית uploadedFiles
    // ============================================================
    public class ImageToUpload
    {
        // תוכן הקובץ בקידוד base64
        public string ImageBase64 { get; set; }

        // סיומת הקובץ, למשל png או jpg
        public string Extension { get; set; }
    }
}
