using System.Security.Claims;
using System.Text.Json;

namespace UsersManager.Client
{
   
    // ============================================================
    // פענוח תביעות מתוך טוקן JWT, בצד הלקוח.
    //
    // הפענוח כאן הוא קריאה בלבד ולא אימות: הלקוח אינו מחזיק את
    // מפתח החתימה ולכן אינו יכול לוודא את הטוקן. האימות האמיתי
    // נעשה בשרת בכל בקשה. מה שקורה כאן הוא רק שליפת השם והדוא"ל
    // לצורך התצוגה, ולכן אין להסתמך עליו כהחלטת אבטחה
    // ============================================================
    public static class JwtParser
    {
        // מחלץ את התביעות מהחלק האמצעי של הטוקן.
        // טוקן בנוי משלושה חלקים מופרדים בנקודה: כותרת, מטען
        // וחתימה. רק המטען מכיל את פרטי המשתמש
        public static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var claims = new List<Claim>();
            var payload = jwt.Split('.')[1];

            // Decode the payload
            var jsonBytes = ParseBase64WithoutPadding(payload);

            // Deserialize the payload into a dictionary
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            foreach (var kvp in keyValuePairs)
            {
                // Add other claims normally
                claims.Add(new Claim(kvp.Key, kvp.Value.ToString()));
            }

            return claims;
        }
       
        // JWT משתמש ב-base64 בלי תווי ריפוד, ו-Convert דורש
        // שהאורך יתחלק בארבע. כאן מוסיפים את הריפוד החסר
        private static byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }

}
