using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// ============================================================
//  ניגון סרטון מלא-מסך, עם כפתור דילוג.
//
//  שני סרטונים במשחק:
//      Intro - אחרי שהמשחק נטען מהשרת, לפני שנכנסים לסצנת המשחק
//      Win   - בסצנת הסיום, לפני שמוצג מסך הניצחון
//
//  למה הכול נבנה כאן בקוד ולא בסצנה:
//  הסרטון צריך להופיע מעל שתי סצנות שונות, שאין להן מבנה משותף.
//  בנייה בקוד חוסכת הרכבה ידנית בכל סצנה בנפרד, ומבטיחה ששתיהן
//  יתנהגו אותו דבר. אין מה לחבר באינספקטור - קוראים ל-Play ודי.
//
//  למה URL ולא VideoClip:
//  יוניטי אינה תומכת ב-VideoClip מוטמע בבנייה ל-WebGL, והיא מדפיסה
//  על כך אזהרה מפורשת. הדרך היחידה היא URL אל קובץ שמוגש בנפרד,
//  ולכן הסרטונים יושבים ב-StreamingAssets ולא ב-Assets/Videos.
//
//  שימוש:
//      await CutscenePlayer.Play("Intro.mp4");
//      CutscenePlayer.PlayThen("Win.mp4", () => ShowWinScreen());
// ============================================================
public class CutscenePlayer : MonoBehaviour
{
    // ---------- הגדרות תצוגה ----------

    // שם קובץ התמונה של כפתור הדילוג, בתוך Assets/Resources
    private const string SkipSpriteName = "EmptyRock";

    private const string SkipLabel = "דלג";

    // גודל כפתור הדילוג בפיקסלים
    private const float SkipWidth = 190f;
    private const float SkipHeight = 110f;

    // המרחק של הכפתור מפינת המסך
    private const float SkipMargin = 40f;

    // כמה שניות ממתינים להכנת הסרטון לפני שמוותרים עליו.
    // בלי התקרה הזאת קובץ חסר או חסום היה משאיר את השחקן
    // מול מסך שחור בלי דרך להמשיך
    private const float PrepareTimeout = 8f;

    // כמה שניות הכיסוי השחור מחכה להחלפת סצנה לפני שהוא מוותר
    private const float BlackHoldTimeout = 15f;

    // ---------- מצב ----------

    // הסרטון שרץ כרגע, אם יש כזה.
    // סטטי בכוונה: זו ההגנה מפני שני סרטונים במקביל
    private static CutscenePlayer current;

    private VideoPlayer video;
    private RenderTexture screen;
    private bool finished;

    // כל מי שמחכה לסיום הסרטון. יכולים להיות כמה, כשכמה
    // רכיבים באותה סצנה ביקשו את אותו סרטון
    private Action waiting;

    // האם להשאיר כיסוי שחור אחרי הסרטון, עד שהסצנה מתחלפת
    private bool holdBlack;

    private Image blackCover;
    private RawImage videoFrame;
    private GameObject skipButton;

    // מערכת אירועים שנוצרה כאן, רק כשלא הייתה כזו בסצנה
    private GameObject ownEventSystem;

    // ============================================================
    //  נקודות הכניסה
    // ============================================================

    // מנגן סרטון וממתין לסיומו. הגרסה שנוחה לקוד אסינכרוני,
    // למשל ב-ServerManager שכבר ממתין לשרת
    // holdBlack משאיר מסך שחור אחרי סוף הסרטון, עד שהסצנה
    // מתחלפת. בלעדיו הסצנה שמאחור נחשפת לרגע בין סוף הסרטון
    // לבין טעינת הסצנה הבאה - למשל מסך הקלדת הקוד שמהבהב שוב
    // אחרי סרטון הפתיחה
    public static Task Play(string fileName, bool holdBlack = false)
    {
        TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();

        PlayThen(fileName, () => done.TrySetResult(true), holdBlack);

        return done.Task;
    }

    // מנגן סרטון וקורא ל-onFinished בסיומו, בדילוג או בתקלה.
    // onFinished מובטח להיקרא בדיוק פעם אחת, כדי שהמשחק לעולם
    // לא ייתקע בגלל סרטון שלא הצליח לרוץ
    public static void PlayThen(string fileName, Action onFinished, bool holdBlack = false)
    {
        // ============================================================
        // הגנה מפני ניגון כפול.
        //
        // בסצנת הסיום יושבים כמה רכיבי MenuScript - אחד על המנהל
        // ואחד על כל מסך תוצאה - וכל אחד מהם שרץ מבקש את הסרטון
        // בעצמו. בלי ההגנה הזאת הסרטון היה מתנגן פעמיים זה על גבי
        // זה.
        //
        // מי שמאחר אינו מנוגן שוב, אלא רק מצרף את עצמו לתור
        // ההמתנה. כך כל הקוראים עדיין מקבלים את הקריאה שלהם
        // בסיום, ורק הסרטון עצמו רץ פעם אחת
        // ============================================================
        if (current != null)
        {
            current.waiting = current.waiting + onFinished;

            // מספיק שאחד הקוראים ביקש כיסוי שחור
            if (holdBlack == true) current.holdBlack = true;

            return;
        }

        GameObject host = new GameObject("CutscenePlayer");

        CutscenePlayer player = host.AddComponent<CutscenePlayer>();

        player.waiting = onFinished;
        player.holdBlack = holdBlack;

        current = player;

        player.StartCoroutine(player.Run(fileName));
    }

    // ============================================================
    //  הריצה עצמה
    // ============================================================

    private IEnumerator Run(string fileName)
    {
        // מוזיקת הרקע מושתקת לזמן הסרטון, אחרת שני הפסקולים
        // מתנגנים יחד
        if (AudioManager.Instance != null) AudioManager.Instance.PauseMusic();

        BuildScreen();
        BuildSkipButton();

        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        video = gameObject.AddComponent<VideoPlayer>();
        video.source = VideoSource.Url;
        video.url = path;
        video.playOnAwake = false;
        video.isLooping = false;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = screen;

        // הקול עובר דרך AudioSource ולא ישירות לחומרה, כדי שכפתור
        // ההשתקה של המשחק ישפיע גם על הסרטון
        AudioSource speaker = gameObject.AddComponent<AudioSource>();
        video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        video.SetTargetAudioSource(0, speaker);

        video.errorReceived += OnError;
        video.loopPointReached += OnEnded;

        video.Prepare();

        // המתנה להכנה, עם תקרת זמן
        float waited = 0f;

        while (video.isPrepared == false && finished == false && waited < PrepareTimeout)
        {
            waited = waited + Time.unscaledDeltaTime;
            yield return null;
        }

        if (finished == false && video.isPrepared == false)
        {
            Debug.LogWarning("[CutscenePlayer] הסרטון לא נטען בזמן: " + path);
            finished = true;
        }

        if (finished == false)
        {
            video.Play();

            // הסרטון רץ. יוצאים מכאן כשהוא נגמר, כשלוחצים על דילוג
            // או כשמתקבלת שגיאה - שלושתם מסמנים finished
            while (finished == false)
            {
                yield return null;
            }
        }

        // מכאן הסרטון נגמר. משחררים את התור עוד לפני שמנקים,
        // כדי שקריאה חדשה שתגיע אחרי זה תתחיל סרטון חדש כרגיל
        Action callbacks = waiting;
        waiting = null;
        current = null;

        if (AudioManager.Instance != null) AudioManager.Instance.ResumeMusic();

        if (holdBlack == true)
        {
            HoldBlackUntilSceneChange();
        }
        else
        {
            Cleanup();
        }

        if (callbacks != null) callbacks();
    }

    // ============================================================
    // משאיר על המסך רק את הכיסוי השחור, ומעביר את האובייקט
    // לשרוד את החלפת הסצנה.
    //
    // בלי זה נוצר רצף מכוער: הסרטון נגמר, הכיסוי נמחק, הסצנה
    // הנוכחית נחשפת שוב לכמה פריימים, ורק אז הסצנה הבאה נטענת.
    // כך השחקן רואה שחור רצוף עד שהמשחק מוכן.
    //
    // הפסק זמן הוא רשת ביטחון: אם משום מה לא תתבצע החלפת סצנה,
    // הכיסוי לא יישאר תקוע על המסך לנצח
    // ============================================================
    private void HoldBlackUntilSceneChange()
    {
        ReleaseVideo();

        if (videoFrame != null) videoFrame.gameObject.SetActive(false);
        if (skipButton != null) skipButton.SetActive(false);

        // מערכת האירועים שלנו אינה נחוצה יותר, ואסור שתשרוד
        // את החלפת הסצנה: שתי מערכות אירועים בסצנה אחת מייצרות
        // אזהרה ומשבשות את הקלט
        if (ownEventSystem != null)
        {
            Destroy(ownEventSystem);
            ownEventSystem = null;
        }

        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        StartCoroutine(BlackTimeout());
    }

    // מסירים את הכיסוי רק פריים אחרי שהסצנה החדשה נטענה,
    // כדי שהיא תספיק להצטייר לפני שהיא נחשפת
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        StartCoroutine(RemoveCoverNextFrame());
    }

    private IEnumerator RemoveCoverNextFrame()
    {
        yield return null;
        yield return null;

        Cleanup();
    }

    private IEnumerator BlackTimeout()
    {
        yield return new WaitForSecondsRealtime(BlackHoldTimeout);

        Debug.LogWarning("[CutscenePlayer] הסצנה לא התחלפה, מסירים את הכיסוי השחור");

        SceneManager.sceneLoaded -= OnSceneLoaded;

        Cleanup();
    }

    // כפתור הדילוג ולחיצת Escape מסיימים את הסרטון מיד
    public void Skip()
    {
        finished = true;
    }

    private void Update()
    {
        if (finished == false && Input.GetKeyDown(KeyCode.Escape) == true)
        {
            Skip();
        }
    }

    // שגיאת ניגון אינה עוצרת את המשחק. מדלגים הלאה, כדי ששחקן
    // לא יישאר תקוע בגלל קובץ שלא נמצא או פורמט שהדפדפן דחה
    private void OnError(VideoPlayer source, string message)
    {
        Debug.LogWarning("[CutscenePlayer] שגיאה בניגון הסרטון: " + message);
        finished = true;
    }

    private void OnEnded(VideoPlayer source)
    {
        finished = true;
    }

    // ============================================================
    //  בניית התצוגה
    // ============================================================

    // בונה בד ציור מעל כל השאר, ובתוכו רקע שחור ותמונת הסרטון
    private void BuildScreen()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // מספר גבוה, כדי שהסרטון יכסה כל ממשק אחר בסצנה
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        gameObject.AddComponent<GraphicRaycaster>();

        // בלי מערכת אירועים בסצנה לחיצות על כפתורי ממשק אינן נקלטות
        if (EventSystem.current == null)
        {
            GameObject events = new GameObject("EventSystem");
            events.transform.SetParent(transform, false);
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();

            ownEventSystem = events;
        }

        // רקע שחור, כדי שפסי השוליים לא יראו את הסצנה שמאחור
        blackCover = NewFullScreen("Background").gameObject.AddComponent<Image>();
        blackCover.color = Color.black;

        screen = new RenderTexture(1280, 720, 0);

        videoFrame = NewFullScreen("Frame").gameObject.AddComponent<RawImage>();
        videoFrame.texture = screen;
    }

    // יוצר אובייקט ממשק שנמתח על כל המסך
    private RectTransform NewFullScreen(string objectName)
    {
        GameObject item = new GameObject(objectName, typeof(RectTransform));
        item.transform.SetParent(transform, false);

        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rect;
    }

    // בונה את כפתור הדילוג בפינה השמאלית התחתונה, על גבי הסרטון
    private void BuildSkipButton()
    {
        GameObject item = new GameObject("SkipButton", typeof(RectTransform));
        item.transform.SetParent(transform, false);

        skipButton = item;

        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(SkipMargin, SkipMargin);
        rect.sizeDelta = new Vector2(SkipWidth, SkipHeight);

        Image face = item.AddComponent<Image>();

        Sprite rock = LoadSkipSprite();

        if (rock != null)
        {
            face.sprite = rock;
            face.color = Color.white;
        }
        else
        {
            // בלי התמונה עדיין יהיה כפתור, רק בלי הסלע
            Debug.LogWarning("[CutscenePlayer] לא נמצאה התמונה " +
                             SkipSpriteName + " תחת Assets/Resources");
            face.color = new Color(0f, 0f, 0f, 0.55f);
        }

        Button button = item.AddComponent<Button>();
        button.targetGraphic = face;
        button.onClick.AddListener(Skip);

        BuildSkipLabel(item.transform);
    }

    // ============================================================
    // טוענת את תמונת הסלע מתוך Assets/Resources.
    //
    // EmptyRock.png מיובא כ-Sprite במצב Multiple, כלומר הנכס
    // הראשי הוא הטקסטורה והספרייט עצמו הוא תת-נכס בשם
    // EmptyRock_0. לכן טעינה ישירה של Sprite לפי שם הקובץ
    // מחזירה null, ו-LoadAll הוא מה שמגיע גם לתת-נכסים
    // ============================================================
    private static Sprite LoadSkipSprite()
    {
        Sprite direct = Resources.Load<Sprite>(SkipSpriteName);

        if (direct != null) return direct;

        Sprite[] all = Resources.LoadAll<Sprite>(SkipSpriteName);

        if (all != null && all.Length > 0) return all[0];

        return null;
    }

    // הכיתוב שעל הסלע
    private void BuildSkipLabel(Transform parent)
    {
        GameObject item = new GameObject("Label", typeof(RectTransform));
        item.transform.SetParent(parent, false);

        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = item.AddComponent<TextMeshProUGUI>();

        // HebrewText מחזיר את הטקסט בסדר תצוגה נכון, ולכן ההיפוך
        // המובנה של TextMeshPro חייב להישאר מכובה
        label.isRightToLeftText = false;
        label.text = HebrewText.Fix(SkipLabel);
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16;
        label.fontSizeMax = 40;
        label.color = Color.white;

        // הכיתוב אינו חוסם את הלחיצה על הכפתור שמתחתיו
        label.raycastTarget = false;
    }

    // ============================================================
    //  ניקוי
    // ============================================================

    // ============================================================
    // משחרר את משאבי הסרטון: מנתק מהאירועים, עוצר את הניגון
    // ומשחרר את הטקסטורה.
    //
    // מופרד מ-Cleanup כי במצב הכיסוי השחור צריך לשחרר את הסרטון
    // כבר עכשיו, אבל את האובייקט עצמו למחוק רק אחרי החלפת הסצנה
    // ============================================================
    private void ReleaseVideo()
    {
        if (video != null)
        {
            video.errorReceived -= OnError;
            video.loopPointReached -= OnEnded;
            video.Stop();
            video = null;
        }

        if (screen != null)
        {
            if (videoFrame != null) videoFrame.texture = null;

            screen.Release();
            Destroy(screen);
            screen = null;
        }
    }

    // מוחק את כל מה שנבנה. חייב לרוץ בכל מסלול יציאה, אחרת
    // ה-RenderTexture נשאר תפוס בזיכרון והכיסוי נשאר על המסך
    private void Cleanup()
    {
        ReleaseVideo();

        Destroy(gameObject);
    }

    // ============================================================
    // רשת ביטחון אחרונה.
    //
    // אם האובייקט נמחק מסיבה שלא צפינו - החלפת סצנה בזמן הסרטון,
    // למשל - צריך לשחרר את הנעילה הסטטית, אחרת שום סרטון לא
    // יוכל להתנגן שוב עד סוף ההרצה
    // ============================================================
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (current == this) current = null;

        // מי שחיכה ולא קיבל את הקריאה בגלל מחיקה פתאומית -
        // מקבל אותה עכשיו, כדי שהמשחק לא ייתקע
        Action callbacks = waiting;
        waiting = null;

        if (callbacks != null) callbacks();
    }
}
