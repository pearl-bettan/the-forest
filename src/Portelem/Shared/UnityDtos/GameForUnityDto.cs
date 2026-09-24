using System.Collections.Generic;

namespace ForestGame.Shared.UnityDtos
{
    // ============================================================
    // משחק שלם בדרך מהשרת אל משחק היוניטי.
    //
    // זו התשובה של UnityController לקוד משחק שהשחקן הקליד.
    // מכיל רק את מה שהמשחק צריך כדי לרוץ - בלי מזהים, בלי
    // פרטי הבעלים ובלי שאלות שלא פורסמו
    // ============================================================
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
