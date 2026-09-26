using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// חיבור המשחק לשרת

// המחלקות של השרת
[System.Serializable]
public class ServerGame
{
    public string gameName;
    public int startingLives;
    public List<ServerStage> stages;
}

[System.Serializable]
public class ServerStage
{
    public string topic;
    public string leftTag;
    public string rightTag;
    public int stageTime;
    public List<ServerAnswer> answers;
}

[System.Serializable]
public class ServerAnswer
{
    public string content;
    public bool isImage;
}

public class ServerManager : MonoBehaviour
{
    [Header("Objects")]
    // תיבת הטקסט שבה השחקן מקליד את קוד המשחק
    [SerializeField] private TMP_InputField codeInput;

    // כפתור ההתחלה, מכובה בזמן הטעינה כדי שהיוזר לא יוכל ללחוץ עליו פעמיים
    [SerializeField] private GameObject startButton;

    // הודעות לשחקן: ״טוען...״, ״משחק לא קיים״ וכו׳
    [SerializeField] private TMP_Text messageText;

    // הטקסט שמופיע מעל תיבת הקוד עם הקודים של משחקי הדוגמה
    [SerializeField] private TMP_Text sampleCodesText;

    [Header("Sample Games")]
    // הקודים של שני משחקי הדוגמה
    [SerializeField] private string sampleCodes = "1001,1002";

    [Header("Scenes")]
    [SerializeField] private string gameScene = "SampleScene";

    [Header("Intro Video")]
    // סרטון הפתיחה שרץ אחרי טעינת המשחק. הקובץ יושב
    // ב-Assets/StreamingAssets ולא ב-Assets/Videos, כי בנייה
    // ל-WebGL אינה תומכת ב-VideoClip מוטמע
    [SerializeField] private bool playIntroVideo = true;
    [SerializeField] private string introVideoFile = "Intro.mp4";

    [Header("Server")]
    // הנתיב לפרויקט. בבנייה ל-Web צריך להחליף לכתובת של השרת האמיתי
    [SerializeField] private string projectURL = "https://localhost:7296/";

    // הנתיב לקונטרולר
    [SerializeField] private string apiURL = "api/Unity/";

    // נתיב לתיקיית התמונות
    [SerializeField] private string imagesFolder = "uploadedFiles/";

    [Header("Messages")]
    [SerializeField] private string loadingMessage = "טוען...";
    [SerializeField] private string emptyCodeMessage = "הקלידו קוד משחק";
    [SerializeField] private string noConnectionMessage = "לא הצלחנו להתחבר לשרת";

    // הודעת שגיאה שחזרה מהשרת
    private string lastError = "";

    // כמה אבנים יש בסצנת המשחק
    private const int MaxAnswers = 10;

    // הכי פחות תשובות שאפשר לסדר בשלב, לפי האפיון
    private const int MinAnswers = 3;

    // הגנה מפני שליחה כפולה של אותה בקשה
    private bool isLoading;

    // אתחול מסך הפתיחה: ניקוי הודעות, הצגת קודי הדוגמה, וקביעת
    // כתובת השרת לפי סביבת ההרצה
    void Start()
    {
        ShowMessage("");
        ShowSampleCodes();

        // בעורך משאירים את כתובת ה-localhost. בבנייה ל-Web עוברים
        // לכתובת יחסית, כי המשחק יושב בתוך wwwroot של המחולל
        SetProjectUrl();
    }

    // ============================================================
    // כתובת השרת, לפי מצגת ההנחיות "ייצוא פרויקט".
    //
    // המשחק יושב ב-<המחולל>/wwwroot/Game/index.html, והשרת הוא
    // רמה אחת מעליו. לכן "./../" מוביל בדיוק לשורש המחולל, בלי
    // תלות בשם הדומיין או בתיקייה שבה האתר יושב ב-IIS.
    // ============================================================
    private void SetProjectUrl()
    {
        if (Application.isEditor == true)
        {
            Debug.Log("Running in editor");
            return;
        }

        Debug.Log("Running as WEBGL");

        projectURL = "./../";

        ReadCodeFromUrl();
    }

    // המחולל מטמיע את המשחק כך: Game/index.html?code=1001
    // כאן שולפים את הקוד מהכתובת, ממלאים אותו בתיבה ומתחילים לבד
    private void ReadCodeFromUrl()
    {
        Dictionary<string, string> parameters = GetQueryParams(Application.absoluteURL);

        if (parameters.ContainsKey("code") == false) return;

        string code = parameters["code"];

        if (string.IsNullOrEmpty(code) == true) return;

        // המחולל שולח code=0 כשנכנסים לעמוד בלי לבחור משחק. אסור להעביר
        // את זה הלאה: השרת מחזיר "קוד המשחק חייב להיות מספר חיובי",
        // והשחקן רואה שגיאה עוד לפני שהספיק להקליד משהו
        int number;

        if (int.TryParse(code, out number) == false || number <= 0) return;

        if (codeInput != null) codeInput.text = code;

        // הקוד הגיע מהמחולל, אז אין סיבה להכריח את השחקן ללחוץ
        CheckCode();
    }

    // פירוק מחרוזת השאילתה שבסוף הכתובת
    private Dictionary<string, string> GetQueryParams(string url)
    {
        Dictionary<string, string> result = new Dictionary<string, string>();

        if (string.IsNullOrEmpty(url) == true) return result;

        int mark = url.IndexOf('?');

        if (mark < 0 || mark == url.Length - 1) return result;

        string query = url.Substring(mark + 1);

        // חותכים עוגן, אם יש
        int hash = query.IndexOf('#');
        if (hash >= 0) query = query.Substring(0, hash);

        string[] pairs = query.Split('&');

        foreach (string pair in pairs)
        {
            if (string.IsNullOrEmpty(pair) == true) continue;

            int eq = pair.IndexOf('=');

            if (eq <= 0) continue;

            string key = UnityWebRequest.UnEscapeURL(pair.Substring(0, eq));
            string value = UnityWebRequest.UnEscapeURL(pair.Substring(eq + 1));

            if (result.ContainsKey(key) == false) result.Add(key, value);
        }

        return result;
    }

    // מציג לשחקן את הקודים של משחקי הדוגמה מעל תיבת הקוד
    private void ShowSampleCodes()
    {
        if (sampleCodesText == null) return;

        // חשוב להפוך את המשפט השלם ולא להדביק את המספרים אחרי ההפיכה,
        // אחרת הקודים והנקודתיים נוחתים בצד הלא נכון של הטקסט
        sampleCodesText.isRightToLeftText = false;
        sampleCodesText.text = HebrewText.Fix("קודים לדוגמה: " + sampleCodes);
    }

    // כפתור התחל משחק מפעיל את הפונקציה
    public async void CheckCode()
    {
        // הגנה: לחיצה נוספת בזמן טעינה לא תשלח בקשה שנייה
        if (isLoading == true) return;

        string code = "";
        if (codeInput != null) code = codeInput.text;

        if (code != null) code = code.Trim();

        // במקרה שלא הוזן קוד
        if (string.IsNullOrEmpty(code) == true)
        {
            ShowMessage(emptyCodeMessage);
            return;
        }

        isLoading = true;
        if (startButton != null) startButton.SetActive(false);

        // הודעת המתנה בזמן שליפת המשחק מהשרת
        ShowMessage(loadingMessage);

        lastError = "";

        GameData newGame = await GetGameFromServer(code);

        if (newGame != null)
        {
            // איפוס נתוני המשחק הקודם לפני שנטען המשחק החדש
            DataPass.NewGame();
            DataPass.Game = newGame;

            // סרטון הפתיחה רץ רק כאן: אחרי שהקוד נמצא תקין ואחרי
            // שהמשחק כולו כבר ירד מהשרת. כך שחקן שהקליד קוד שגוי
            // רואה את הודעת השגיאה מיד ולא אחרי סרטון.
            // ההמתנה כאן גם מנצלת את הזמן: הסצנה נטענת רק בסופו
            if (playIntroVideo == true)
            {
                ShowMessage("");

                // holdBlack משאיר מסך שחור מסוף הסרטון ועד שסצנת
                // המשחק נטענת. בלעדיו מסך הקלדת הקוד נחשף שוב
                // לכמה פריימים אחרי הסרטון, ורק אז המשחק מתחיל
                await CutscenePlayer.Play(introVideoFile, true);
            }

            SceneManager.LoadScene(gameScene);
            return;
        }

        // לא חזר משחק - מציגים לשחקן למה
        ShowMessage(ErrorForPlayer());

        isLoading = false;
        if (startButton != null) startButton.SetActive(true);
    }

    // קריאה ל-API
    async Task<GameData> GetGameFromServer(string code)
    {
        string endPoint = projectURL + apiURL + code;
        string gameJson = await GetDataFromServer(endPoint);

        // במקרה שבו: תקלת שרת/ משחק לא קיים/ אינו מפורסם
        if (string.IsNullOrEmpty(gameJson) == true)
        {
            return null;
        }

        ServerGame serverGame = JsonUtility.FromJson<ServerGame>(gameJson);

        //במקרה שבו ה-JSON חזר במבנה אחר מהמצופה
        if (serverGame == null || serverGame.stages == null)
        {
            lastError = "מבנה המידע שחזר מהשרת אינו מתאים";
            Debug.LogError(lastError);
            return null;
        }

        // ההודעה נשארת "טוען..." גם בזמן הורדת התמונות
        return await ParseGame(serverGame);
    }

    // ממיר משחק שלם מהשרת למחלקת המשחק של היוניטי
    async Task<GameData> ParseGame(ServerGame serverGame)
    {
        GameData unityGame = new GameData();
        unityGame.gameName = serverGame.gameName;
        unityGame.stagesList = new List<StageData>();

        // מספר החיים קבוע על 3 לפי האפיון, ולא נלקח מהמחולל
        unityGame.startingLives = GameRules.Lives;

        foreach (ServerStage serverStage in serverGame.stages)
        {
            StageData unityStage = await ParseStage(serverStage);

            if (unityStage != null)
            {
                unityGame.stagesList.Add(unityStage);
            }
        }

        // אין שאלה שאפשר לשחק
        if (unityGame.stagesList.Count == 0)
        {
            lastError = "אין במשחק שאלה תקינה לשחק בה";
            Debug.LogError(lastError);
            return null;
        }

        return unityGame;
    }

    // ============================================================
    // ממירה שלב שהגיע מהשרת למבנה שהמשחק עובד איתו.
    //
    // שלב פסול אינו מפיל את הטעינה אלא מוחזר כ-null ומדולג, עם
    // אזהרה ב-Console. כך משחק שבו שאלה אחת חסרה עדיין ניתן
    // לשחק במקום להיכשל כולו.
    //
    // אסינכרונית כי הפריטים עשויים להיות תמונות, וכל תמונה
    // דורשת הורדה נפרדת מהשרת
    // ============================================================
    async Task<StageData> ParseStage(ServerStage serverStage)
    {
        //מקרה של שלב בלי תשובות
        if (serverStage.answers == null)
        {
            Debug.LogWarning("Stage '" + serverStage.topic + "' has no answers. Skipped");
            return null;
        }

        //מקרה שבו יש פחות מהמינימום תשובות
        if (serverStage.answers.Count < MinAnswers)
        {
            Debug.LogWarning("Stage '" + serverStage.topic + "' has only " +
                             serverStage.answers.Count + " answers, minimum is " +
                             MinAnswers + ". Skipped");
            return null;
        }

        //מקרה שבו יש יותר תשובות ממספר האבנים בסצנה
        if (serverStage.answers.Count > MaxAnswers)
        {
            Debug.LogWarning("Stage '" + serverStage.topic + "' has " +
                             serverStage.answers.Count + " answers but there are only " +
                             MaxAnswers + " rocks in the scene. Skipped");
            return null;
        }

        StageData unityStage = new StageData();
        unityStage.topic = serverStage.topic;
        unityStage.leftTag = serverStage.leftTag;
        unityStage.rightTag = serverStage.rightTag;

        // 0 מהמחולל = ללא הגבלת זמן, וזה מצב חוקי
        unityStage.stageTime = Mathf.Max(0, serverStage.stageTime);

        unityStage.answersList = new List<AnswerData>();

        // התשובות מגיעות מהשרת לפי הסדר הנכון
        foreach (ServerAnswer serverAnswer in serverStage.answers)
        {
            AnswerData unityAnswer = await ParseAnswer(serverAnswer);
            unityStage.answersList.Add(unityAnswer);
        }

        return unityStage;
    }

    // ממיר תשובה אחת- אם היא תמונה- הוא מוריד אותה מהשרת
    async Task<AnswerData> ParseAnswer(ServerAnswer serverAnswer)
    {
        AnswerData unityAnswer = new AnswerData();

        if (serverAnswer.isImage == true)
        {
            string imageEndpoint = projectURL + imagesFolder + serverAnswer.content;
            unityAnswer.answerImage = await LoadImage(imageEndpoint);

            //מקרה שבו התמונה לא קיימת בשרת, האבן תישאר בלי תמונה
            //ולכן יוצג עליה שם הקובץ (ככה העורך ידע לראות מה חסר)
            if (unityAnswer.answerImage == null)
            {
                Debug.LogWarning("Image not found on server: " + serverAnswer.content);
                unityAnswer.answerContent = serverAnswer.content;
            }
            else
            {
                // תמונה מחליפה את הטקסט על האבן
                unityAnswer.answerContent = "";
            }
        }
        else
        {
            unityAnswer.answerContent = serverAnswer.content;
            unityAnswer.answerImage = null;
        }

        return unityAnswer;
    }

    // ============================================================
    // פנייה כללית לשרת, שמחזירה את גוף התשובה כטקסט.
    //
    // ההמתנה נעשית בלולאת Task.Yield ולא בקורוטינה, כדי שאפשר
    // יהיה לקרוא לה מתוך שגרות async רגילות.
    //
    // שים לב: אם השרת מוגש ב-http ולא ב-https, יוניטי חוסמת את
    // הפנייה מברירת מחדל. ההגדרה שמתירה זאת היא
    // insecureHttpOption ב-Player Settings
    // ============================================================
    async Task<string> GetDataFromServer(string url)
    {
        using var http = UnityWebRequest.Get(url);

        var get = http.SendWebRequest();

        while (get.isDone == false)
        {
            await Task.Yield();
        }

        if (http.result == UnityWebRequest.Result.Success)
        {
            return http.downloadHandler.text;
        }

        // השרת מחזיר הודעה מדויקת: משחק לא קיים / המשחק אינו מפורסם / אין שלבים במשחק
        lastError = ReadServerError(http);

        Debug.LogError("Server error " + http.responseCode + ": " + lastError);
        return null;
    }

    // שולף את הודעת השגיאה שהשרת החזיר
    private string ReadServerError(UnityWebRequest http)
    {
        string body = "";

        if (http.downloadHandler != null) body = http.downloadHandler.text;

        // אין תשובה מהשרת בכלל - בעיית חיבור
        if (http.result == UnityWebRequest.Result.ConnectionError)
        {
            return "";
        }

        if (string.IsNullOrEmpty(body) == true)
        {
            // אין גוף להודעה, אז מסתמכים על קוד הסטטוס
            if (http.responseCode == 404) return "משחק לא קיים";
            if (http.responseCode == 400) return "המשחק אינו מפורסם";

            return "";
        }

        return body;
    }

    // מוריד תמונה מהשרת ומחזיר אותה כ-Sprite ואם התמונה לא קיימת- תוחזר שגיאה
    public async Task<Sprite> LoadImage(string endpoint)
    {
        using var http = UnityWebRequestTexture.GetTexture(endpoint);

        var get = http.SendWebRequest();

        while (get.isDone == false)
        {
            await Task.Yield();
        }

        if (http.result != UnityWebRequest.Result.Success)
        {
            return null;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(http);

        if (texture == null) return null;

        Rect spriteRect = new Rect(0, 0, texture.width, texture.height);
        Vector2 spriteCenter = new Vector2(0.5f, 0.5f);

        return Sprite.Create(texture, spriteRect, spriteCenter);
    }

    // מתרגם את הודעת השגיאה של השרת להודעה שתוצג לשחקן
    private string ErrorForPlayer()
    {
        if (lastError == "") return noConnectionMessage;

        // חזר דף HTML ולא הודעה - סימן שהשרת לא זמין
        if (lastError.Contains("<") == true) return noConnectionMessage;

        return lastError;
    }

    // מציגה הודעה לשחקן אחרי שהפכה אותה לסדר תצוגה נכון.
    // isRightToLeftText מכובה בכוונה: HebrewText כבר הפך את
    // הטקסט, והפעלת שתי ההיפוכים יחד הייתה מחזירה אותו לשיבוש
    private void ShowMessage(string message)
    {
        if (messageText == null) return;

        messageText.isRightToLeftText = false;
        messageText.text = HebrewText.Fix(message);
    }
}
