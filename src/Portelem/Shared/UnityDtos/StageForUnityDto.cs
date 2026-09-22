using System.Collections.Generic;

namespace ForestGame.Shared.UnityDtos
{
    // שלב במשחק
    public class StageForUnityDto
    {
        // נושא השלב
        public string Topic { get; set; }

        // תגיות סדר לתשובות
        public string LeftTag { get; set; }
        public string RightTag { get; set; }

        // זמן לסיום השלב
        public int StageTime { get; set; }

        // סדר התשובות של השלב
        public List<AnswerForUnityDto> Answers { get; set; }
    }
}
