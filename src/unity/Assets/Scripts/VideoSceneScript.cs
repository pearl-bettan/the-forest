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

    [Header("ניגון אוטומטי")]
    // ============================================================
    // דפדפנים חוסמים ניגון אוטומטי של סרטון עם קול, עד שהמשתמש
    // ביצע פעולה. במחשב זה כמעט תמיד עובר, כי כבר לחצו בעמוד.
    // בנייד - ובעיקר ב-iOS - החסימה מחמירה יותר, והסרטון פשוט
    // עומד ומחכה ללחיצה.
    //
    // סרטון מושתק מותר בניגון אוטומטי בכל הדפדפנים. לכן אם
    // הניגון לא התחיל תוך זמן קצר, מנסים שוב בלי קול.
    //
    // כיבוי כאן יחזיר את ההתנהגות הקודמת: או שיש קול, או
    // שהשחקן צריך ללחוץ
    // ============================================================
    [SerializeField] bool retryWithoutSound = true;

    // ============================================================
    // התחלה בלי קול מלכתחילה, בלי לנסות קודם עם קול.
    //
    // סרטון מושתק מותר בניגון אוטומטי בכל דפדפן ובכל מכשיר, ולכן
    // זו הדרך היחידה שמבטיחה התחלה מיידית תמיד. המחיר הוא פסקול
    // שקט בסרטון עצמו.
    //
    // נועד לסרטון שרץ רחוק מהנגיעה האחרונה של השחקן - למשל סרטון
    // הניצחון, שמגיע אחרי אנימציית הסיום ולא מיד אחרי לחיצה
    // ============================================================
    [SerializeField] bool alwaysWithoutSound = false;

    // כמה שניות מחכים לתחילת הניגון לפני שמסיקים שהוא נחסם
    [SerializeField] float autoplayGrace = 1.5f;

    [Header("פסקול נפרד")]
    // ============================================================
    // הפסקול מנוגן בנפרד מהסרטון, דרך מערכת הקול של יוניטי.
    //
    // למה: דפדפני נייד חוסמים ניגון אוטומטי של סרטון שיש בו קול,
    // וסרטון מושתק מותר תמיד. לעומת זאת מערכת הקול של יוניטי כבר
    // פתוחה מרגע הנגיעה הראשונה של השחקן - מוזיקת הרקע של המשחק
    // נשמעת בנייד לכל אורכו - ולכן קול שעובר דרכה אינו נחסם.
    //
    // התוצאה: הסרטון מתחיל לבד, ועם קול, בלי שום לחיצה.
    //
    // הקובץ נטען מ-Assets/Resources לפי שם קובץ הסרטון:
    // Win.mp4 -> Audio/Win. אין מה לחבר בעורך.
    // אם לא נמצא פסקול, הקול מנוגן מתוך הסרטון כמו קודם
    // ============================================================
    [SerializeField] bool separateSoundtrack = true;

    // התיקייה בתוך Resources שבה יושבים הפסקולים
    [SerializeField] string soundtrackFolder = "Audio";

    // סטייה בשניות שמעליה מיישרים את הקול בחזרה אל התמונה
    [SerializeField] float syncTolerance = 0.25f;

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

    // הנגן של הפסקול הנפרד, כשהוא בשימוש
    private AudioSource soundtrackSpeaker;

    // האם הקול מגיע מקובץ נפרד ולא מתוך הסרטון
    private bool usingSeparateSoundtrack;

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

        AudioClip soundtrack = null;

        if (separateSoundtrack == true && alwaysWithoutSound == false)
        {
            soundtrack = LoadSoundtrack();
        }

        if (soundtrack != null)
        {
            // הסרטון עצמו מושתק, וסרטון מושתק מותר בניגון אוטומטי
            // בכל דפדפן ובכל מכשיר. הקול מגיע מהקובץ הנפרד
            video.audioOutputMode = VideoAudioOutputMode.None;
            usingSeparateSoundtrack = true;

            soundtrackSpeaker = GetComponent<AudioSource>();

            if (soundtrackSpeaker == null)
            {
                soundtrackSpeaker = gameObject.AddComponent<AudioSource>();
            }

            // כפתור ההשתקה של המשחק עובד דרך AudioListener.volume,
            // ולכן הוא משתיק גם את הפסקול הזה בלי טיפול מיוחד
            soundtrackSpeaker.clip = soundtrack;
            soundtrackSpeaker.playOnAwake = false;
            soundtrackSpeaker.loop = false;
        }
        else if (alwaysWithoutSound == true)
        {
            video.audioOutputMode = VideoAudioOutputMode.None;
        }
        else
        {
            // הקול עובר דרך AudioSource ולא ישירות לחומרה, כדי שכפתור
            // ההשתקה של המשחק ישפיע גם על הסרטון
            AudioSource speaker = GetComponent<AudioSource>();
            if (speaker == null) speaker = gameObject.AddComponent<AudioSource>();

            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            video.SetTargetAudioSource(0, speaker);
        }

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

        // ============================================================
        // זיהוי חסימה של ניגון אוטומטי.
        //
        // קודם לכן חיכינו כאן זמן קבוע ורק אחריו בדקנו אם צויר
        // פריים. שתי בעיות היו בזה: ההמתנה רצה עד סופה גם כשהכול
        // תקין, והמדד עצמו - מספר הפריים - אינו סימן אמין לכך
        // שהניגון באמת רץ.
        //
        // עכשיו יוצאים מהלולאה ברגע שהניגון התחיל, ולכן במחשב
        // המחיר הוא שני פריימים. אם הדפדפן סירב, הניסיון בלי קול
        // מתחיל תוך פחות משנייה במקום אחרי שתיים, וזה ההבדל בין
        // סרטון שנראה כאילו הוא מחכה ללחיצה לבין סרטון שמתחיל לבד
        // ============================================================
        // עם פסקול נפרד הסרטון כבר מושתק, ולכן אין מה להיחסם
        if (usingSeparateSoundtrack == false &&
            retryWithoutSound == true && alwaysWithoutSound == false)
        {
            float waitedForStart = 0f;

            while (finished == false && HasStarted() == false &&
                   waitedForStart < autoplayGrace)
            {
                waitedForStart = waitedForStart + Time.unscaledDeltaTime;
                yield return null;
            }

            if (finished == false && HasStarted() == false)
            {
                yield return StartCoroutine(RetryMuted());
                yield break;
            }
        }

        yield return StartCoroutine(ShowWhenFirstFrameReady());

        // הקול יוצא לדרך באותו רגע שבו התמונה עולה למסך, ולא רגע
        // אחרי ההפעלה, כדי ששניהם יתחילו יחד
        if (usingSeparateSoundtrack == true && finished == false &&
            soundtrackSpeaker != null)
        {
            SeekSoundtrackTo((float)video.time);
            soundtrackSpeaker.Play();

            StartCoroutine(KeepSoundInSync());
        }
    }


    // ============================================================
    // טוען את הפסקול של הסרטון מתוך Resources.
    //
    // Resources.Load נבחר ולא שדה בעורך ולא הורדה מ-StreamingAssets:
    // הוא נתמך במלואו ב-WebGL, הקובץ עובר את צינור הייבוא של יוניטי
    // ולכן הוא נארז בפורמט שהדפדפן יודע לנגן, ואין מה לחבר ידנית
    // ============================================================
    private AudioClip LoadSoundtrack()
    {
        string baseName = Path.GetFileNameWithoutExtension(videoFileName);

        if (string.IsNullOrEmpty(baseName) == true) return null;

        string path = baseName;

        if (string.IsNullOrEmpty(soundtrackFolder) == false)
        {
            path = soundtrackFolder + "/" + baseName;
        }

        AudioClip clip = Resources.Load<AudioClip>(path);

        if (clip == null)
        {
            Debug.LogWarning("[VideoScene] לא נמצא פסקול ב-Resources/" + path +
                             ". הקול ינוגן מתוך הסרטון עצמו, ובנייד הוא עלול להיחסם");
        }

        return clip;
    }


    // ============================================================
    // מיישר את הקול אל התמונה.
    //
    // שני הנגנים עצמאיים זה מזה, ולאורך הסרטון הם עלולים להיפרד
    // בשבריר שנייה - במיוחד במכשיר שמאט את פענוח הווידאו. בדיקה
    // כל חצי שנייה מספיקה: תיקון תכוף יותר נשמע כקפיצה
    // ============================================================
    private IEnumerator KeepSoundInSync()
    {
        while (finished == false && soundtrackSpeaker != null &&
               soundtrackSpeaker.isPlaying == true)
        {
            float picture = (float)video.time;
            float gap = picture - soundtrackSpeaker.time;

            if (gap < 0f) gap = -gap;

            if (gap > syncTolerance) SeekSoundtrackTo(picture);

            yield return new WaitForSecondsRealtime(0.5f);
        }
    }


    // קפיצה לנקודת זמן בפסקול. חריגה מאורך הקובץ זורקת שגיאה,
    // ולכן היא נחסמת כאן ולא בכל מקום שקורא לשגרה
    private void SeekSoundtrackTo(float seconds)
    {
        if (soundtrackSpeaker == null) return;
        if (soundtrackSpeaker.clip == null) return;

        if (seconds < 0f) seconds = 0f;

        if (seconds >= soundtrackSpeaker.clip.length - 0.05f) return;

        soundtrackSpeaker.time = seconds;
    }


    // ============================================================
    // האם הניגון באמת התחיל.
    //
    // שני סימנים יחד: הנגן מדווח שהוא מנגן, וגם התקדם מעבר לפריים
    // הראשון. חסימת ניגון אוטומטי מפילה לפחות אחד מהם - הדפדפן
    // מרשה לפעמים לפענח את הפריים הפותח אך לא להמשיך ממנו
    // ============================================================
    private bool HasStarted()
    {
        if (video == null) return false;

        return video.isPlaying == true && video.frame >= 1;
    }

    // ============================================================
    // ניסיון שני, בלי קול.
    //
    // audioOutputMode = None הופך את הסרטון למושתק מבחינת
    // הדפדפן, וניגון אוטומטי של סרטון מושתק מותר גם בנייד.
    // המחיר הוא פסקול שקט בסרטון עצמו - מוזיקת הרקע של המשחק
    // חוזרת כרגיל בסיומו
    // ============================================================
    private IEnumerator RetryMuted()
    {
        Debug.Log("[VideoScene] הניגון נחסם. מנסים שוב בלי קול");

        video.Stop();
        video.audioOutputMode = VideoAudioOutputMode.None;
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
            Debug.LogWarning("[VideoScene] גם הניסיון בלי קול נכשל");
            Finish();
            yield break;
        }

        video.Play();

        yield return StartCoroutine(ShowWhenFirstFrameReady());

        // גם זה לא עזר: ממשיכים הלאה במקום להשאיר מסך תקוע
        if (finished == false && video.frame < 1)
        {
            Debug.LogWarning("[VideoScene] הסרטון לא התחיל, ממשיכים הלאה");
            Finish();
        }
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

        // הפסקול נעצר יחד עם התמונה, אחרת הוא היה ממשיך להישמע
        // אל תוך הסצנה הבאה אחרי דילוג
        if (soundtrackSpeaker != null) soundtrackSpeaker.Stop();

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
