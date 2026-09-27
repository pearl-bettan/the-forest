using System.Collections.Generic;

namespace ForestGame.Shared.UnityDtos
{
    // הגדרות כלליות של המשחק
    public class GameForUnityDto
    {
        // שם המשחק
        public string GameName { get; set; }

        // כמה פסילות יש לשחקן לכל המשחק
        public int StartingLives { get; set; }

        //  השלבים של המשחק
        public List<StageForUnityDto> Stages { get; set; }
    }
}
