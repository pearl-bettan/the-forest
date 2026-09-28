using System.Text;


//  סידור טקסט בשילוב של עברית ואנגלית
public static class HebrewText
{
    // סוג התו לצורך החישוב
    private const int Neutral = 0;
    private const int Rtl = 1;
    private const int Latin = 2;
    private const int Number = 3;


    public static string Fix(string text)
    {
        if (string.IsNullOrEmpty(text) == true) return text;

        int length = text.Length;

        int[] kinds = new int[length];

        for (int i = 0; i < length; i++)
        {
            kinds[i] = KindOf(text[i]);
        }

        // מפריד את התו כספרה, כדי שלא ייפרק את רצף הספרות
        for (int i = 1; i < length - 1; i++)
        {
            if (IsNumberSeparator(text[i]) == false) continue;

            if (kinds[i - 1] == Number && kinds[i + 1] == Number) kinds[i] = Number;
        }

        bool baseIsRtl = BaseIsRtl(text);

        int[] levels = BuildLevels(kinds, baseIsRtl);

        char[] letters = text.ToCharArray();

        // סוגר בעברית מוצג הפוך
        for (int i = 0; i < length; i++)
        {
            if (levels[i] % 2 == 1) letters[i] = Mirror(letters[i]);
        }

        Reorder(letters, levels);

        return new string(letters);
    }


    // מפצלת טקסט ארוך לשורות ומסדרת כל שורה בנפרד
    public static string FixLines(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) == true) return text;
        if (maxLength <= 0) return Fix(text);
        if (text.Length <= maxLength) return Fix(text);

        StringBuilder result = new StringBuilder();
        string line = "";

        string[] words = text.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];

            // במצב שבו המילה אינה נכנסת בשורה הנוכחית- פותחים שורה חדשה
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


    private static void AddLine(StringBuilder result, string line)
    {
        if (line == "") return;

        if (result.Length > 0) result.Append('\n');

        result.Append(Fix(line));
    }


    private static bool BaseIsRtl(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            int kind = KindOf(text[i]);

            if (kind == Rtl) return true;
            if (kind == Latin) return false;
        }

        return true;
    }


    // כיוון הבסיס. אם אין תו משני הצדדים, כיוון הבסיס הוא מה שקובע
    private static int[] BuildLevels(int[] kinds, bool baseIsRtl)
    {
        int length = kinds.Length;
        int evenLevel = baseIsRtl ? 2 : 0;

        int[] levels = new int[length];

        int i = 0;

        while (i < length)
        {
            if (kinds[i] != Neutral)
            {
                if (kinds[i] == Rtl) levels[i] = 1;
                else levels[i] = evenLevel;

                i = i + 1;
                continue;
            }

            int end = i;

            while (end < length && kinds[end] == Neutral) end = end + 1;

            bool beforeIsRtl = i > 0 ? StrongIsRtl(kinds[i - 1], baseIsRtl) : baseIsRtl;
            bool afterIsRtl = end < length ? StrongIsRtl(kinds[end], baseIsRtl) : baseIsRtl;

            bool takeRtl = beforeIsRtl == afterIsRtl ? beforeIsRtl : baseIsRtl;

            for (int k = i; k < end; k++)
            {
                levels[k] = takeRtl ? 1 : evenLevel;
            }

            i = end;
        }

        return levels;
    }


    // ספרות נחשבות לספרות בעברית לצורך החלטת הכיוון
    
    private static bool StrongIsRtl(int kind, bool baseIsRtl)
    {
        if (kind == Rtl) return true;
        if (kind == Number) return baseIsRtl;

        return false;
    }



    // הופכת את הסדר, מהרמה הגבוהה ביותר ומטה
    private static void Reorder(char[] letters, int[] levels)
    {
        int highest = 0;

        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] > highest) highest = levels[i];
        }

        for (int level = highest; level >= 1; level--)
        {
            int i = 0;

            while (i < levels.Length)
            {
                if (levels[i] < level)
                {
                    i = i + 1;
                    continue;
                }

                int end = i;

                while (end < levels.Length && levels[end] >= level) end = end + 1;

                ReverseRange(letters, i, end - 1);

                i = end;
            }
        }
    }


    private static void ReverseRange(char[] letters, int from, int to)
    {
        while (from < to)
        {
            char keep = letters[from];
            letters[from] = letters[to];
            letters[to] = keep;

            from = from + 1;
            to = to - 1;
        }
    }


    // סוגר שמוצג הפוך בטקסט בעברית
    private static char Mirror(char c)
    {
        if (c == '(') return ')';
        if (c == ')') return '(';
        if (c == '[') return ']';
        if (c == ']') return '[';
        if (c == '{') return '}';
        if (c == '}') return '{';
        if (c == '<') return '>';
        if (c == '>') return '<';

        return c;
    }


    private static bool IsNumberSeparator(char c)
    {
        return c == ',' || c == '.' || c == ':';
    }


    private static int KindOf(char c)
    {
        // טווח האותיות בעברית בטבלת התווים
        if (c >= '֐' && c <= '׿') return Rtl;

        if (c >= 'a' && c <= 'z') return Latin;
        if (c >= 'A' && c <= 'Z') return Latin;

        if (c >= '0' && c <= '9') return Number;

        return Neutral;
    }
}
