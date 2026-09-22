using System.Collections.Generic;
using UnityEngine;

// מחלקות הנתונים של המשחק, בנפרד מהמנהל עצמו.
// אף אחת מהן אינה MonoBehaviour: הן המבנה של המשחק שמגיע מהשרת,
// ו-DataPass היא הגשר שמעביר מידע בין הסצנות.

[System.Serializable]
public class AnswerData
{
    // הטקסט שיופיע על האבן
    public string answerContent;

    // תמונה שתופיע על האבן במקום הטקסט
    public Sprite answerImage;
}

[System.Serializable]
public class StageData
{
    [Header("Texts")]
    // נושא השאלה
    public string topic;

    // התגית שמופיעה משמאל לאגם (הסוף)
    public string leftTag;

    // התגית שמופיעה מימין לאגם (ההתחלה)
    public string rightTag;

    [Header("Answers")]
    public List<AnswerData> answersList;

    [Header("Time")]
    // כמה שניות יש לשאלה. 0 = ללא הגבלת זמן
    public int stageTime = 60;

    // סומנה כשגויה בעבר (נענתה לא נכון או שנגמר הזמן) וחזרה למאגר
    [System.NonSerialized] public bool markedWrong;

    // נענתה נכון ולכן היא כבר לא חוזרת למאגר.
    // נשמר על השאלה עצמה כדי שההתקדמות תישרד גם מעבר לסצנת ההשהייה
    [System.NonSerialized] public bool answeredCorrectly;
}

[System.Serializable]
public class GameData
{
    // שם המשחק. לא מוצג על המסך
    public string gameName;

    // כמה פסילות יש לשחקן. תמיד 3 - נקבע ב-GameRules
    public int startingLives;

    // כל השאלות במשחק
    public List<StageData> stagesList;
}

// חוקי המשחק שנקבעו באפיון ואסור לשנות אותם מהמחולל
public static class GameRules
{
    // מספר החיים קבוע על 3
    public const int Lives = 3;
}

public class DataPass
{
    // המשחק שהתקבל מהשרת, לפי הקוד שהשחקן הזין בסצנת הפתיחה.
    public static GameData Game;

    // מסך סיום משחק: win / timeout / nolives
    public static string result = "";

    // הציון הסופי, מתוך 100
    public static int score;

    // כמה שניות לקח כל המשחק
    public static float totalTime;

    // כמה שגיאות היו בכל המשחק
    public static int mistakes;

    // כמה שאלות נענו נכון וכמה שאלות היו בסך הכל
    public static int questionsAnswered;
    public static int questionsTotal;

    // כמה חיים נשארו לשחקן. נשמר גם כשעוברים לסצנת ההשהייה
    public static int livesLeft;
    public static bool keepLives;

    // חזרה מהשהייה - צריך להגריל שאלה חדשה והניסיון הקודם לא נספר
    public static bool returningFromPause;

    // מצב המשחק נשמר בזמן ההשהייה כדי להמשיך אותו אחר כך
    public static float savedGameTime;
    public static float savedScore;
    public static int savedMistakes;
    public static int savedQuestionsAnswered;

    // המשחק כולו הוגדר ללא הגבלת זמן. מסכי הסיום מסתירים לפי זה
    // את תיבת הזמן, כי אין מה להציג בה
    public static bool unlimitedTime;

    // הפעלת סאונד
    public static bool soundOn = true;

    // איפוס הכל למשחק חדש
    public static void NewGame()
    {
        result = "";
        score = 0;
        totalTime = 0;
        mistakes = 0;
        questionsAnswered = 0;
        questionsTotal = 0;
        livesLeft = 0;
        keepLives = false;
        returningFromPause = false;
        savedGameTime = 0;
        savedScore = 0;
        savedMistakes = 0;
        savedQuestionsAnswered = 0;
        unlimitedTime = false;
    }

    public static string TimeText()
    {
        int minutes = Mathf.FloorToInt(totalTime / 60);
        int seconds = Mathf.FloorToInt(totalTime % 60);

        if (seconds < 10) return minutes + ":0" + seconds;

        return minutes + ":" + seconds;
    }
}
