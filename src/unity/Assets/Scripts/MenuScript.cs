using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MenuScript : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] string homeScene = "Home";
    [SerializeField] string gameScene = "SampleScene";

    [Header("End Screen Images")]
    [SerializeField] GameObject winImage;

    // נגמר הזמן
    [SerializeField] GameObject timeOutImage;

    //  פסילות
    [SerializeField] GameObject noLivesImage;


    [Header("End Screen Score And Time")]
    // הציון הסופי
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text timeText;

    [SerializeField] GameObject timeTitle;

    // מספר הפסילות בכל המשחק
    [SerializeField] TMP_Text mistakesText;


    void Start()
    { 
        if (IsEndScreen() == false) return;

        ShowResultImage();
        ShowScoreAndTime();
    }
    
    // התחל משחק
    public void StartGame()
    {
        DataPass.NewGame();
        SceneManager.LoadScene(gameScene);
    }

    //  חזור למשחק
    public void BackToGame()
    {
        SceneManager.LoadScene(gameScene);
    }

    // התחל מחדש
    public void RestartGame()
    {
        DataPass.NewGame();
        SceneManager.LoadScene(gameScene);
    }

    // חזרה לתפריט
    public void GoHome()
    {
        DataPass.NewGame();
        SceneManager.LoadScene(homeScene);
    }
    
    // האם המסך הנוכחי הוא מסך סיום
    private bool IsEndScreen()
    {
        if (mistakesText != null) return true;
        if (winImage != null) return true;
        if (timeOutImage != null) return true;
        if (noLivesImage != null) return true;
        if (scoreText != null) return true;
        if (timeText != null) return true;

        return false;
    }

   // מציג את תמונת הסיום המתאימה: ניצחון, נגמר הזמן או הפסילות 
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
        }
    }

    // מכבה את כל תמונות הסיום
    private void HideAll()
    {
        if (winImage != null) winImage.SetActive(false);
        if (timeOutImage != null) timeOutImage.SetActive(false);
        if (noLivesImage != null) noLivesImage.SetActive(false);
    }
    //  מציג את מסך הסיום המתאים
    private void ShowImage(GameObject image, string fieldName)
    {
        if (image == null)
        {
            Debug.LogWarning("The field '" + fieldName + "' is empty in Menu Script. " +
                             "Result was: " + DataPass.result);
            Debug.LogWarning("No image to show. Drag the screen objects from the scene " +
                             "into the Menu Script end screen fields (not the PNG files)");
            return;
        }

        image.SetActive(true);
    }
    

    private static readonly string[] TimeTitleNames = { "TimerTitle", "TimeTitle" };

    //   כיתוב הזמן 
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
    //  מציג את הציון הסופי, הזמן והפסילות 
    private void ShowScoreAndTime()
    {
        if (scoreText != null)
        {
            scoreText.isRightToLeftText = false;
            scoreText.text = DataPass.score.ToString();
        }

        // משחק ללא הגבלת זמן: אין זמן להציג,
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

    }
}
