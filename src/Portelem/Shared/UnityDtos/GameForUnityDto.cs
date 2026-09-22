using System.Collections.Generic;

namespace ForestGame.Shared.UnityDtos
{
    // מכיל את המידע עבור משחק אחד מלא
    public class GameForUnityDto
    {
        // שם המשחק
        public string GameName { get; set; }

        // כמה פסילות יש לשחקן לכל המשחק
        public int StartingLives { get; set; }

        // כל השלבים של המשחק
        public List<StageForUnityDto> Stages { get; set; }
    }
}
