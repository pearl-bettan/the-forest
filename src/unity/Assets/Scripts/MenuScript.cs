using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MenuScript : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] string homeScene = "Home";
    [SerializeField] string gameScene = "SampleScene";

    [Header("End Screen Images (סצנת הסיום בלבד)")]
    // כל התשובות הנכונות הוזנו
    [SerializeField] GameObject winImage;

    // נגמר הזמן
    [SerializeField] GameObject timeOutImage;

    // נגמרו הפסילות
    [SerializeField] GameObject noLivesImage;

    // תמונת הפסד 
    [SerializeField] GameObject loseImage;

    [Header("Win Video")]
    // סרטון הניצחון, שרץ בסצנת הסיום לפני מסך הניצחון.
    // הקובץ יושב ב-Assets/StreamingAssets ולא ב-Assets/Videos,
    // כי בנייה ל-WebGL אינה תומכת ב-VideoClip מוטמע
    [SerializeField] bool playWinVideo = true;
    [SerializeField] string winVideoFile = "Win.mp4";

    [Header("End Screen Score And Time")]
    // הציון הסופי
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text timeText;

    // הכיתוב "זמן" שמעל או מתחת לזמן עצמו.
    // אם לא חובר בעורך, הקוד מחפש אותו לבד לפי שם
    [SerializeField] GameObject timeTitle;

    // מספר הפסילות בכל המשחק
    [SerializeField] TMP_Text mistakesText;

    // כמה שאלות נענו נכון מתוך הסך הכל
    [SerializeField] TMP_Text questionsText;

    // שורת סיכום אחת שמרכזת הכל, אם מעדיפים טקסט אחד במקום ארבעה
    [SerializeField] TMP_Text summaryText;

    // ============================================================
    // בסצנת הסיום בלבד: מציג את תוצאת המשחק.
    //
    // בניצחון רץ קודם סרטון הניצחון, ורק בסופו מוצג המסך. עד אז
    // כל תמונות הסיום מכובות, אחרת מסך הניצחון היה מהבהב לרגע
    // מתחת לסרטון לפני שהוא מתחיל.
    //
    // הניקוד והזמן מוצגים מיד ולא מחכים לסרטון: הם יושבים על
    // אותו מסך שממילא מוסתר מאחוריו
    // ============================================================
    void Start()
    { 
        if (IsEndScreen() == false) return;

        ShowScoreAndTime();

        if (playWinVideo == true && DataPass.result == "win")
        {
            HideAll();

            // בסצנה הזאת יושבים כמה רכיבי MenuScript - אחד על
            // המנהל ואחד על כל מסך תוצאה - וכל אחד מהם שפעיל
            // מגיע לשורה הזאת. CutscenePlayer מנגן בכל זאת פעם
            // אחת בלבד, ומחזיר את הקריאה לכל מי שביקש
            CutscenePlayer.PlayThen(winVideoFile, ShowResultImage);
            return;
        }

        ShowResultImage();
    }
    
    // כפתור "התחל משחק"
    public void StartGame()
    {
        DataPass.NewGame();
        SceneManager.LoadScene(gameScene);
    }

    // כפתור "חזור למשחק"
    public void BackToGame()
    {
        SceneManager.LoadScene(gameScene);
    }

    // כפתור "התחל מחדש"
    public void RestartGame()
    {
        DataPass.NewGame();
        SceneManager.LoadScene(gameScene);
    }

    // כפתור "חזרה לתפריט"
    public void GoHome()
    {
        DataPass.NewGame();
        SceneManager.LoadScene(homeScene);
    }
    
    // ============================================================
    // מזהה אם הסקריפט יושב על מסך הסיום או על מסך אחר.
    //
    // הזיהוי נעשה לפי השדות שחוברו באינספקטור ולא לפי שם הסצנה:
    // אותו סקריפט משרת כמה מסכים, ומסך הסיום הוא היחיד שבו
    // שדות התוצאה ממולאים
    // ============================================================
    private bool IsEndScreen()
    {
        if (mistakesText != null) return true;
        if (questionsText != null) return true;
        if (summaryText != null) return true;
        if (winImage != null) return true;
        if (timeOutImage != null) return true;
        if (noLivesImage != null) return true;
        if (loseImage != null) return true;
        if (scoreText != null) return true;
        if (timeText != null) return true;

        return false;
    }

    // ============================================================
    // מציגה את תמונת הסיום המתאימה לתוצאת המשחק.
    //
    // שלוש תוצאות אפשריות: ניצחון, נגמר הזמן, ונגמרו הפסילות.
    // תוצאה ריקה פירושה שהמסך הופעל ישירות ולא דרך המשחק,
    // ולכן מוצגת תמונת ההפסד עם אזהרה ב-Console
    // ============================================================
    private void ShowResultImage()
    {
        HideAll();

        if (DataPass.result == "win")
        {
            ShowImage(winImage, "Win Image");
        }
        else if (DataPass.result == "timeout")
        {
            ShowImage(timeOutImage, "Time Out Image");
        }
        else if (DataPass.result == "nolives")
        {
            ShowImage(noLivesImage, "No Lives Image");
        }
        else
        {
            Debug.LogWarning("DataPass.result is empty. " +
                             "Test the full path: Home > game > end, " +
                             "not by pressing Play on the End scene");

            ShowImage(loseImage, "Lose Image");
        }
    }

    // מכבה את כל תמונות הסיום. נקרא לפני הצגת אחת מהן, כדי
    // ששתי תמונות לא יוצגו זו מעל זו
    private void HideAll()
    {
        if (winImage != null) winImage.SetActive(false);
        if (timeOutImage != null) timeOutImage.SetActive(false);
        if (noLivesImage != null) noLivesImage.SetActive(false);
        if (loseImage != null) loseImage.SetActive(false);
    }

    // מדליקה תמונה אחת. אם השדה לא חובר באינספקטור, נופלת
    // לתמונת ההפסד ומדפיסה אזהרה עם שם השדה החסר, כדי שאפשר
    // יהיה למצוא אותו מיד
    private void ShowImage(GameObject image, string fieldName)
    {
        if (image == null)
        {
            Debug.LogWarning("The field '" + fieldName + "' is empty in Menu Script. " +
                             "Result was: " + DataPass.result);
            image = loseImage;
        }

        if (image == null)
        {
            Debug.LogWarning("No image to show. Drag the screen objects from the scene " +
                             "into the Menu Script end screen fields (not the PNG files)");
            return;
        }

        image.SetActive(true);
    }
    

    // הכיתוב "זמן" נקרא TimerTitle במסך אחד ו-TimeTitle בשניים האחרים,
    // ולכן מחפשים את שני השמות. החיפוש רקורסיבי, כי הכיתוב יושב
    // תחת Canvas בתוך המסך ולא ישירות עליו
    private static readonly string[] TimeTitleNames = { "TimerTitle", "TimeTitle" };

    // מאתרת את כיתוב הזמן בסצנה, ושומרת אותו לפעם הבאה
    private GameObject FindTimeTitle()
    {
        if (timeTitle != null) return timeTitle;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            foreach (string wanted in TimeTitleNames)
            {
                if (child.name == wanted)
                {
                    timeTitle = child.gameObject;
                    return timeTitle;
                }
            }
        }

        return null;
    }

    // משוב מסכם: זמן כולל, ציון ומספר שגיאות.
    // מוצג גם במסך סיום מוצלח וגם במסך נגמר הזמן
    private void ShowScoreAndTime()
    {
        if (scoreText != null)
        {
            scoreText.isRightToLeftText = false;
            scoreText.text = DataPass.score.ToString();
        }

        // משחק ללא הגבלת זמן: אין זמן להציג, אז גם המספר וגם
        // הכיתוב שלידו יורדים מהמסך
        bool showTime = DataPass.unlimitedTime == false;

        GameObject title = FindTimeTitle();
        if (title != null) title.SetActive(showTime);

        if (timeText != null)
        {
            timeText.gameObject.SetActive(showTime);

            if (showTime == true)
            {
                timeText.isRightToLeftText = false;
                timeText.text = DataPass.TimeText();
            }
        }

        if (mistakesText != null)
        {
            mistakesText.isRightToLeftText = false;
            mistakesText.text = DataPass.mistakes.ToString();
        }

        if (questionsText != null)
        {
            questionsText.isRightToLeftText = false;
            questionsText.text = DataPass.questionsAnswered + " / " + DataPass.questionsTotal;
        }

        if (summaryText != null)
        {
            summaryText.isRightToLeftText = false;
            summaryText.text = BuildSummary();
        }
    }

    // בונה את שורות הסיכום בעברית מסודרת
    private string BuildSummary()
    {
        string lines = "";

        // כל שורה מופכת בשלמותה, כולל המספרים שבסופה.
        // הפיכה של החלק העברי בלבד מקפיצה את המספרים לצד הלא נכון
        lines = lines + HebrewText.Fix("זמן כולל: " + DataPass.TimeText()) + "\n";
        lines = lines + HebrewText.Fix("ציון: " + DataPass.score) + "\n";
        lines = lines + HebrewText.Fix("פסילות: " + DataPass.mistakes) + "\n";
        lines = lines + HebrewText.Fix("שאלות שנענו: " +
                DataPass.questionsAnswered + " / " + DataPass.questionsTotal);

        return lines;
    }
}
