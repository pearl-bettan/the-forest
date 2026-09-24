using System.Collections.Generic;

namespace ForestGame.Shared.UnityDtos
{
    // ============================================================
    // שלב בודד במשחק, בדרך מהשרת אל היוניטי.
    //
    // "שלב" כאן הוא מה שנקרא "שאלה" בצד המחולל: רצף אחד שהשחקן
    // צריך לסדר בין שתי תגיות הקיצון
    // ============================================================
    public class StageForUnityDto
    {
        // נושא השלב
        public string Topic { get; set; }

        // תגיות סדר לתשובות
        public string LeftTag { get; set; }
        public string RightTag { get; set; }

        // זמן לסיום השלב
        public int StageTime { get; set; }

        // הפריטים בסדר הנכון. היוניטי מערבבת אותם לפני ההצגה,
        // ומשווה מולם כשהשחקן מסיים לסדר
        public List<AnswerForUnityDto> Answers { get; set; }
    }
}
