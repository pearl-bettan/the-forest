using System.Collections.Generic;

namespace ForestGame.Shared.UnityDtos
{
    // שלב בודד במשחק 
    public class StageForUnityDto
    {
        // תגיות 
        public string LeftTag { get; set; }
        public string RightTag { get; set; }

        // זמן לסיום השלב
        public int StageTime { get; set; }

        // התשובות בסדר הנכון.
        public List<AnswerForUnityDto> Answers { get; set; }
    }
}
