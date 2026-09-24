using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Data
{
    // ============================================================
    // ניהול קבצי התמונות שהעורכת מעלה לפריטי השאלות.
    //
    // התמונות נשמרות כקבצים בתיקייה uploadedFiles שתחת wwwroot,
    // ולא בבסיס הנתונים. בבסיס הנתונים נשמר רק שם הקובץ, ולכן
    // ה-db נשאר קטן והתמונות מוגשות ישירות כקבצים סטטיים.
    //
    // שם הקובץ נוצר מ-GUID ולא משם הקובץ המקורי: כך אין התנגשות
    // בין שתי עורכות שהעלו קובץ באותו שם, ואי אפשר להסיק מהשם
    // שום דבר על מי העלה אותו
    // ============================================================
    public class FilesManage
    {
        private readonly IWebHostEnvironment _env;

        // מגבלת הגודל של תמונה שמורה, לפי האפיון
        private const int MaxFileBytes = 512 * 1024;

        // הגודל המרבי של התמונה בפיקסלים
        private const int MaxSide = 600;

        // הסביבה דרושה כדי למצוא את נתיב wwwroot בזמן ריצה,
        // שמשתנה בין הרצה מקומית לבין השרת
        public FilesManage(IWebHostEnvironment env)
        {
            _env = env;
        }

        // מחיקת קובץ בודד
        public bool DeleteFile(string fileName, string containerName)
        {
            // הגנה: מקבלים רק שם קובץ, לא נתיב.
            // ככה אי אפשר לצאת מהתיקייה עם ../ ולמחוק קבצים אחרים בשרת
            if (IsSafeFileName(fileName) == false)
            {
                return false;
            }

            string folderPath = Path.Combine(_env.WebRootPath, containerName);
            string savingPath = Path.Combine(folderPath, fileName);

            if (File.Exists(savingPath) == false)
            {
                return false;
            }

            try
            {
                File.Delete(savingPath);
                return true;
            }
            catch (IOException)
            {
                // הקובץ תפוס כרגע. לא מפילים את הבקשה בגלל זה
                return false;
            }
        }

        // מחיקת אוסף קבצים. מחזיר כמה נמחקו בפועל
        public int DeleteFiles(IEnumerable<string> fileNames, string containerName)
        {
            if (fileNames == null)
            {
                return 0;
            }

            int deleted = 0;

            foreach (string fileName in fileNames)
            {
                if (DeleteFile(fileName, containerName) == true)
                {
                    deleted = deleted + 1;
                }
            }

            return deleted;
        }

        // בודק ששם הקובץ הוא שם ולא נתיב
        private bool IsSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            if (fileName.Contains("/") == true || fileName.Contains("\\") == true)
            {
                return false;
            }

            if (fileName.Contains("..") == true)
            {
                return false;
            }

            return fileName == Path.GetFileName(fileName);
        }

        // ============================================================
        // שומרת תמונה שהגיעה כ-base64, ומחזירה את שם הקובץ שנוצר.
        //
        // הסדר: פענוח, הקטנה למידות התצוגה, דחיסה עד שהקובץ
        // נכנס למגבלה, ורק אז כתיבה לדיסק. כך קובץ גדול מדי
        // לעולם אינו נכתב לשרת ולו לרגע.
        //
        // אם הדחיסה נאלצה לעבור מ-PNG ל-JPEG, גם הסיומת משתנה
        // בהתאם, כדי שהקובץ על הדיסק יתאים לתוכנו
        // ============================================================
        // שמירת תמונה. התמונה מוקטנת ונדחסת עד שהיא נכנסת למגבלת 512KB
        public async Task<string> SaveFile(string imageBase64, string extension, string containerName)
        {
            byte[] picture = Convert.FromBase64String(imageBase64);

            using (Image image = Image.Load(picture))
            {
                // הקטנה ראשונית לגודל התצוגה
                image.Mutate(x => x
                    .Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(MaxSide, MaxSide)
                    }));

                string useExtension = CleanExtension(extension);

                byte[] finalBytes = await CompressToLimit(image, useExtension);

                // אם התמונה נשמרה כ-JPEG בגלל הדחיסה, גם הסיומת משתנה
                if (useExtension == "png" && finalBytes.Length > MaxFileBytes)
                {
                    useExtension = "jpg";
                }

                string fileName = $"{Guid.NewGuid()}.{useExtension}";

                string folderPath = Path.Combine(_env.WebRootPath, containerName);

                // התיקייה עלולה להיות חסרה בפריסה חדשה
                if (Directory.Exists(folderPath) == false)
                {
                    Directory.CreateDirectory(folderPath);
                }

                string savingPath = Path.Combine(folderPath, fileName);

                await File.WriteAllBytesAsync(savingPath, finalBytes);

                return fileName;
            }
        }

        // ============================================================
        // דוחסת את התמונה בשלבים עד שהיא נכנסת למגבלת 512KB.
        //
        // סדר הניסיונות מהזול ליקר מבחינת איכות התוצאה:
        //   1. קידוד באיכות 85
        //   2. אם זה PNG - מעבר ל-JPEG, שכמעט תמיד קטן בהרבה
        //   3. הורדת איכות הדרגתית: 70, 55, 45, 35
        //   4. הקטנת המידות ב-20% כל פעם, עד חמישה סיבובים
        //
        // עצירה ב-80 פיקסלים כדי לא להגיע לתמונה בלתי שמישה.
        // אם גם אחרי כל זה הקובץ גדול מהמגבלה, מוחזר מה שיש -
        // כי הוא כבר קטן משמעותית מהמקור, ועדיף מלדחות את ההעלאה
        // ============================================================
        // מוריד את איכות הדחיסה, ואם צריך גם את המידות,
        // עד שהתמונה נכנסת למגבלת הגודל
        private async Task<byte[]> CompressToLimit(Image image, string extension)
        {
            byte[] bytes = await EncodeAsync(image, extension, 85);

            if (bytes.Length <= MaxFileBytes)
            {
                return bytes;
            }

            // PNG של תצלום כמעט תמיד גדול מדי. עוברים ל-JPEG
            if (extension == "png")
            {
                extension = "jpg";
                bytes = await EncodeAsync(image, extension, 85);

                if (bytes.Length <= MaxFileBytes)
                {
                    return bytes;
                }
            }

            // שלב ראשון: מורידים איכות
            int[] qualities = { 70, 55, 45, 35 };

            for (int i = 0; i < qualities.Length; i++)
            {
                bytes = await EncodeAsync(image, extension, qualities[i]);

                if (bytes.Length <= MaxFileBytes)
                {
                    return bytes;
                }
            }

            // שלב שני: מקטינים את המידות, עד חמישה ניסיונות
            for (int i = 0; i < 5; i++)
            {
                int newWidth = (int)(image.Width * 0.8);
                int newHeight = (int)(image.Height * 0.8);

                if (newWidth < 80 || newHeight < 80)
                {
                    break;
                }

                image.Mutate(x => x.Resize(newWidth, newHeight));

                bytes = await EncodeAsync(image, extension, 60);

                if (bytes.Length <= MaxFileBytes)
                {
                    return bytes;
                }
            }

            // הגענו לגבול היכולת. מחזירים את מה שיש, זה כבר קטן משמעותית מהמקור
            return bytes;
        }

        // ממיר את התמונה למערך בתים לפי הסיומת
        private async Task<byte[]> EncodeAsync(Image image, string extension, int quality)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                if (extension == "png")
                {
                    PngEncoder pngEncoder = new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.BestCompression
                    };

                    await image.SaveAsPngAsync(memory, pngEncoder);
                }
                else
                {
                    JpegEncoder jpegEncoder = new JpegEncoder
                    {
                        Quality = quality
                    };

                    await image.SaveAsJpegAsync(memory, jpegEncoder);
                }

                return memory.ToArray();
            }
        }

        // מקבל רק את הסיומות המותרות לפי האפיון
        private string CleanExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return "jpg";
            }

            string clean = extension.Replace(".", "").Trim().ToLower();

            if (clean == "png") return "png";
            if (clean == "jpeg") return "jpg";
            if (clean == "jpg") return "jpg";

            // כל דבר אחר נשמר כ-JPEG
            return "jpg";
        }
    }
}
