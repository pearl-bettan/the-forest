using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// ============================================================
//  סצנת סרטון.
//
//  כל סרטון במשחק יושב בסצנה משלו, שכל תפקידה לנגן אותו
//  ולעבור הלאה:
//
//      IntroVideo  ->  Intro.mp4  ->  SampleScene
//      WinVideo    ->  Win.mp4    ->  End
//
//  למה סצנה נפרדת ולא שכבה מעל סצנה קיימת:
//  שכבה שנפרשת מעל סצנה אחרת חושפת אותה לרגע בכניסה וביציאה,
//  ותלויה בסדר הריצה של סקריפטים אחרים באותה סצנה. סצנה משלה
//  מנתקת את הסרטון מכל זה: יוניטי מחליפה סצנות בלי הבהוב,
//  ואפשר למקם ולעצב את הסרטון ואת כפתור הדילוג בעורך.
//
//  מה צריך להיות בסצנה:
//      מצלמה
//      Canvas ובתוכו RawImage בשם VideoSurface - עליו מוקרן הסרטון
//      כפתור בשם SkipButton
//      אובייקט עם הסקריפט הזה ועם רכיב VideoPlayer
//
//  את הסצנות אפשר לייצר מוכנות דרך התפריט:
//      ForestGame > Create video scenes
//
//  למה URL ולא VideoClip:
//  יוניטי אינה תומכת ב-VideoClip מוטמע בבנייה ל-WebGL. לכן
//  הסרטונים יושבים ב-StreamingAssets ונטענים לפי שם קובץ
// ============================================================
public class VideoSceneScript : MonoBehaviour
{
    [Header("הסרטון")]
    // שם הקובץ בתוך Assets/StreamingAssets
    [SerializeField] string videoFileName = "Intro.mp4";

    // ה-RawImage שעליו מוקרן הסרטון. אפשר למקם ולמתוח אותו בעורך
    [SerializeField] RawImage videoSurface;

    // רכיב הניגון. אם לא חובר, נוצר אחד בזמן ריצה
    [SerializeField] VideoPlayer video;

    [Header("דילוג")]
    // כפתור הדילוג. אפשר להזיז ולעצב אותו בעורך
    [SerializeField] GameObject skipButton;

    // האם מקש Escape מדלג גם הוא
    [SerializeField] bool escapeSkips = true;

    [Header("המשך")]
    // הסצנה שנטענת בסוף הסרטון, בדילוג או בתקלה
    [SerializeField] string nextScene = "SampleScene";

    [Header("הגנות")]
    // כמה שניות ממתינים להכנת הסרטון לפני שממשיכים בלעדיו.
    // בלי התקרה הזאת קובץ חסר או פורמט שהדפדפן דחה היו משאירים
    // את השחקן תקוע מול מסך ריק בלי דרך להמשיך
    [SerializeField] float prepareTimeout = 8f;

    // כמה שניות ממתינים לפריים הראשון לפני שחושפים את המשטח
    // בכל מקרה
    [SerializeField] float firstFrameTimeout = 2f;

    // רזולוציית ההקרנה. אין טעם לחרוג מגודל הסרטון עצמו
    [SerializeField] int surfaceWidth = 1280;
    [SerializeField] int surfaceHeight = 720;

    // הטקסטורה שאליה הסרטון מצויר
    private RenderTexture screen;

    // נעילה: המעבר לסצנה הבאה קורה פעם אחת בלבד, גם אם הסרטון
    // נגמר ובאותו רגע גם נלחץ דילוג
    private bool finished;

    // ============================================================
    //  מחזור החיים
    // ============================================================

    private void Start()
    {
        // מוזיקת הרקע מושתקת לזמן הסרטון, אחרת שני הפסקולים
        // מתנגנים יחד
        if (AudioManager.Instance != null) AudioManager.Instance.PauseMusic();

        WireSkipButton();

        StartCoroutine(PlayVideo());
    }

    private void Update()
    {
        if (finished == true) return;
        if (escapeSkips == false) return;

        if (Input.GetKeyDown(KeyCode.Escape) == true) Skip();
    }

    // ============================================================
    //  הניגון
    // ============================================================

    private IEnumerator PlayVideo()
    {
        string path = Path.Combine(Application.streamingAssetsPath, videoFileName);

        if (video == null) video = gameObject.AddComponent<VideoPlayer>();

        screen = new RenderTexture(surfaceWidth, surfaceHeight, 0);

        // RenderTexture חדשה מכילה זבל מהזיכרון. בלי הניקוי הזה
        // עלול להבזיק פריים אקראי לפני הפריים הראשון של הסרטון
        ClearToBlack(screen);

        // ============================================================
        // המשטח מוסתר עד שיש באמת מה להציג עליו.
        //
        // RawImage בלי טקסטורה מצויר כמלבן לבן אטום. כל עוד הסרטון
        // לא התחיל, משטח גלוי היה מכסה בלבן את כל מה שיש בסצנה -
        // רקע, תמונה, כל דבר שהונח בעורך
        // ============================================================
        if (videoSurface != null) videoSurface.enabled = false;

        video.source = VideoSource.Url;
        video.url = path;
        video.playOnAwake = false;
        video.isLooping = false;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = screen;

        if (videoSurface != null) videoSurface.texture = screen;

        // הקול עובר דרך AudioSource ולא ישירות לחומרה, כדי שכפתור
        // ההשתקה של המשחק ישפיע גם על הסרטון
        AudioSource speaker = GetComponent<AudioSource>();
        if (speaker == null) speaker = gameObject.AddComponent<AudioSource>();

        video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        video.SetTargetAudioSource(0, speaker);

        video.errorReceived += OnError;
        video.loopPointReached += OnEnded;

        video.Prepare();

        float waited = 0f;

        while (video.isPrepared == false && finished == false && waited < prepareTimeout)
        {
            waited = waited + Time.unscaledDeltaTime;
            yield return null;
        }

        if (finished == true) yield break;

        if (video.isPrepared == false)
        {
            Debug.LogWarning("[VideoScene] הסרטון לא נטען בזמן: " + path);
            Finish();
            yield break;
        }

        video.Play();

        yield return StartCoroutine(ShowWhenFirstFrameReady());
    }

    // ============================================================
    // חושף את המשטח רק אחרי שהפריים הראשון צויר אליו.
    //
    // video.frame הוא מספר הפריים המוצג. כל עוד הוא שלילי לא צויר
    // דבר, והמשטח היה מציג לבן. תקרת זמן קצרה מוודאת שגם אם
    // המונה לא מתקדם מסיבה כלשהי, הסרטון עדיין ייראה
    // ============================================================
    private IEnumerator ShowWhenFirstFrameReady()
    {
        float waited = 0f;

        while (finished == false && video.frame < 1 && waited < firstFrameTimeout)
        {
            waited = waited + Time.unscaledDeltaTime;
            yield return null;
        }

        if (finished == true) yield break;

        if (videoSurface != null) videoSurface.enabled = true;
    }

    // מנקה טקסטורה לשחור
    private static void ClearToBlack(RenderTexture texture)
    {
        RenderTexture previous = RenderTexture.active;

        RenderTexture.active = texture;
        GL.Clear(true, true, Color.black);

        RenderTexture.active = previous;
    }

    // ============================================================
    //  סיום
    // ============================================================

    // כפתור הדילוג קורא לשגרה הזאת. ציבורית, כדי שאפשר יהיה
    // לחבר אותה גם ידנית ב-On Click של הכפתור בעורך
    public void Skip()
    {
        Finish();
    }

    private void OnEnded(VideoPlayer source)
    {
        Finish();
    }

    // תקלת ניגון אינה עוצרת את המשחק. ממשיכים לסצנה הבאה, כדי
    // ששחקן לא יישאר תקוע בגלל קובץ שלא נמצא
    private void OnError(VideoPlayer source, string message)
    {
        Debug.LogWarning("[VideoScene] שגיאה בניגון הסרטון: " + message);
        Finish();
    }

    // המעבר לסצנה הבאה. כל מסלולי היציאה מגיעים לכאן, והנעילה
    // מוודאת שהוא קורה פעם אחת בלבד
    private void Finish()
    {
        if (finished == true) return;

        finished = true;

        if (AudioManager.Instance != null) AudioManager.Instance.ResumeMusic();

        // ============================================================
        // עוצרים את הניגון אבל לא נוגעים בתמונה.
        //
        // Pause ולא Stop, ובלי לנתק את הטקסטורה מהמשטח: כך הפריים
        // האחרון נשאר על המסך עד שהסצנה הבאה נטענת. ניתוק
        // הטקסטורה כאן היה משאיר RawImage ריק, כלומר מלבן לבן
        // אטום - וזה בדיוק המסך הלבן שנראה בין הסרטון למשחק.
        //
        // השחרור בפועל קורה ב-OnDestroy, כשהסצנה ממילא נפרקת
        // ============================================================
        if (video != null)
        {
            video.errorReceived -= OnError;
            video.loopPointReached -= OnEnded;
            video.Pause();
        }

        if (string.IsNullOrEmpty(nextScene) == true)
        {
            Debug.LogWarning("[VideoScene] לא הוגדרה סצנה להמשך");
            return;
        }

        SceneManager.LoadScene(nextScene);
    }

    // ============================================================
    //  עזרה
    // ============================================================

    // מחבר את כפתור הדילוג לשגרת הדילוג.
    // החיבור נעשה כאן ולא רק בעורך, כדי שכפתור שהוחלף או הוזז
    // ימשיך לעבוד בלי לזכור לחבר אותו מחדש ב-On Click
    private void WireSkipButton()
    {
        if (skipButton == null) return;

        Button button = skipButton.GetComponent<Button>();

        if (button == null)
        {
            Debug.LogWarning("[VideoScene] לכפתור הדילוג אין רכיב Button");
            return;
        }

        button.onClick.RemoveListener(Skip);
        button.onClick.AddListener(Skip);
    }

    // ============================================================
    // משחרר את משאבי הסרטון.
    //
    // נקרא רק מ-OnDestroy, כלומר כשהסצנה כבר נפרקת. אסור לקרוא
    // לו לפני החלפת סצנה: שחרור הטקסטורה בזמן שהמשטח עדיין מוצג
    // הופך אותו למלבן לבן
    // ============================================================
    private void ReleaseVideo()
    {
        if (video != null)
        {
            video.errorReceived -= OnError;
            video.loopPointReached -= OnEnded;
            video.Stop();
        }

        if (screen != null)
        {
            screen.Release();
            Destroy(screen);
            screen = null;
        }
    }

    private void OnDestroy()
    {
        ReleaseVideo();
    }
}
