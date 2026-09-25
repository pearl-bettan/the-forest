using System.Text;

// כלי עזר אחד לכל הפרויקט לסידור טקסט בעברית.
// לפני כן אותו קוד בדיוק היה משוכפל ב-GameManager וב-ServerManager.
public static class HebrewText
{
    // הופך את סדר האותיות בעברית ומשאיר אנגלית ומספרים כמו שהם
    public static string Fix(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        StringBuilder result = new StringBuilder();
        StringBuilder group = new StringBuilder();
        bool groupIsEnglish = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            bool isHebrew = IsHebrew(c);
            bool isEnglish = IsEnglishOrNumber(c);

            // תווים ניטרליים (רווח, פיסוק) נצמדים לקבוצה הנוכחית
            if (isHebrew == false && isEnglish == false)
            {
                group.Append(c);
                continue;
            }

            // התחלפה השפה - סוגרים את הקבוצה הקודמת
            if (group.Length > 0 && isEnglish != groupIsEnglish)
            {
                CloseGroup(result, group, groupIsEnglish);
                group.Clear();
            }

            groupIsEnglish = isEnglish;
            group.Append(c);
        }

        CloseGroup(result, group, groupIsEnglish);
        return result.ToString();
    }

    // מפצל טקסט ארוך לשורות עד maxLength תווים, ומסדר כל שורה בעברית
    public static string FixLines(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (maxLength <= 0) return Fix(text);
        if (text.Length <= maxLength) return Fix(text);

        StringBuilder result = new StringBuilder();
        string line = "";

        string[] words = text.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];

            // המילה לא נכנסת בשורה הנוכחית - פותחים שורה חדשה
            if (line != "" && line.Length + 1 + word.Length > maxLength)
            {
                AddLine(result, line);
                line = "";
            }

            if (line == "") line = word;
            else line = line + " " + word;
        }

        AddLine(result, line);
        return result.ToString();
    }

    // מוסיפה שורה לתוצאה, אחרי שהפכה אותה לסדר הנכון.
    // ירידת השורה נוספת לפני השורה ולא אחריה, כדי שלא תישאר
    // שורה ריקה בסוף הטקסט
    private static void AddLine(StringBuilder result, string line)
    {
        if (line == "") return;

        if (result.Length > 0) result.Append('\n');
        result.Append(Fix(line));
    }

    // כל קבוצה חדשה נכנסת משמאל לקודמות, ככה מתקבל סדר מימין לשמאל
    private static void CloseGroup(StringBuilder result, StringBuilder group, bool isEnglish)
    {
        if (group.Length == 0) return;

        string text = group.ToString();

        // אנגלית ומספרים נשארים בסדר שלהם
        if (isEnglish == false)
        {
            char[] letters = text.ToCharArray();
            System.Array.Reverse(letters);
            text = new string(letters);
        }

        result.Insert(0, text);
    }

    // טווח האותיות העבריות בטבלת התווים
    private static bool IsHebrew(char c)
    {
        return c >= 0x0590 && c <= 0x05FF;
    }

    // אנגלית וספרות נשארות בסדר המקורי שלהן גם בתוך טקסט עברי,
    // ולכן צריך לזהות אותן בנפרד ולא להפוך אותן
    private static bool IsEnglishOrNumber(char c)
    {
        if (c >= 'a' && c <= 'z') return true;
        if (c >= 'A' && c <= 'Z') return true;
        if (c >= '0' && c <= '9') return true;

        return false;
    }
}
