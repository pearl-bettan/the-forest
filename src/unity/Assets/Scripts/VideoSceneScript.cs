using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

//  סצנת סרטון.
public class VideoSceneScript : MonoBehaviour
{
    [Header("The video")]
    // שם הקובץ  
    [SerializeField] string videoFileName = "Intro.mp4";

    // המשטח שעליו מוקרן הסרטון
    [SerializeField] RawImage videoSurface;

    // רכיב הניגון. אם לא חובר, נוצר אחד בזמן ריצה
    [SerializeField] VideoPlayer video;

    [Header("Skip")]
    // כפתור הדילוג. אפשר להזיז ולעצב אותו בעורך
    [SerializeField] GameObject skipButton;

    // האם מקש Escape מדלג גם הוא
    [SerializeField] bool escapeSkips = true;

    [Header("Continuation")]
    // הסצנה שנטענת בסוף הסרטון, בדילוג או בתקלה
    [SerializeField] string nextScene = "SampleScene";

    [Header("Autoplay")]
    [SerializeField] bool retryWithoutSound = true;

    // התחלה ללא קול
    [SerializeField] bool alwaysWithoutSound = false;

    // כמה שניות מחכים לתחילת הניגון לפני שמסיקים שהוא נחסם
    [SerializeField] float autoplayGrace = 1.5f;

    [Header("Audio")]
    [SerializeField] bool separateSoundtrack = true;

    // התיקיה של הקבצי קול
    [SerializeField] string soundtrackFolder = "Audio";

    // סנכרון בין תמונה לקול
    [SerializeField] float syncTolerance = 0.25f;

    [Header("Timeouts")]
    [SerializeField] float prepareTimeout = 8f;

    // כמה שניות ממתינים לפריים הראשון 
    [SerializeField] float firstFrameTimeout = 2f;

    // רזולוציית המשחק
    [SerializeField] int surfaceWidth = 1280;
    [SerializeField] int surfaceHeight = 720;

    // המשטח שהסרטון מוקרן
    private RenderTexture screen;

    // הנגן של הפסקול הנפרד
    private AudioSource soundtrackSpeaker;

    // האם הקול מגיע מקובץ נפרד ולא מתוך הסרטון
    private bool usingSeparateSoundtrack;

    // דגל סיום הניגון ומעבר למסך הסיום
    private bool finished;

    private void Start()
    {
        // מוזיקת הרקע מושתקת לזמן הסרטון
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


    private IEnumerator PlayVideo()
    {
        string path = Path.Combine(Application.streamingAssetsPath, videoFileName);

        if (video == null) video = gameObject.AddComponent<VideoPlayer>();

        screen = new RenderTexture(surfaceWidth, surfaceHeight, 0);

       // ניקוי הנגן
        ClearToBlack(screen);

        // המשטח מוסתר עד שיש באמת מה להציג עליו.
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
            // הסרטון מושתק
            MuteVideoAudio();
            usingSeparateSoundtrack = true;

            soundtrackSpeaker = GetComponent<AudioSource>();

            if (soundtrackSpeaker == null)
            {
                soundtrackSpeaker = gameObject.AddComponent<AudioSource>();
            }

            // כפתור ההשתקה
            soundtrackSpeaker.clip = soundtrack;
            soundtrackSpeaker.playOnAwake = false;
            soundtrackSpeaker.loop = false;
        }
        else if (alwaysWithoutSound == true)
        {
            MuteVideoAudio();
        }
        else
        {
            // כפתור ההשתקה
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

        // זיהוי חסימה של ניגון אוטומטי.
       
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

        if (finished == true) yield break;

        // נסיון נוסף לניגון 
        float extraWait = 0f;

        while (finished == false && video.frame < 1 && extraWait < autoplayGrace)
        {
            extraWait = extraWait + Time.unscaledDeltaTime;
            yield return null;
        }

        if (finished == true) yield break;

        // ניגון הסרטון נכשל
   
        if (video.frame < 1)
        {
            Debug.LogWarning("[VideoScene] הסרטון לא התחיל, ממשיכים הלאה");
            Finish();
            yield break;
        }

        // ניגון הקול והתמונה
        if (usingSeparateSoundtrack == true && soundtrackSpeaker != null)
        {
            SeekSoundtrackTo((float)video.time);
            soundtrackSpeaker.Play();

            StartCoroutine(KeepSoundInSync());
        }
    }


    // השתקה  של פס הקול שבתוך הסרטון.
  
    private void MuteVideoAudio()
    {
        video.audioOutputMode = VideoAudioOutputMode.None;

        if (video.controlledAudioTrackCount > 0)
        {
            video.EnableAudioTrack(0, false);
        }
    }


    // טעינת הקול 
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

    // סנכרון בין תמונה לקול
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


    // קפיצה לנקודת זמן בפסקול
    private void SeekSoundtrackTo(float seconds)
    {
        if (soundtrackSpeaker == null) return;
        if (soundtrackSpeaker.clip == null) return;

        if (seconds < 0f) seconds = 0f;

        if (seconds >= soundtrackSpeaker.clip.length - 0.05f) return;

        soundtrackSpeaker.time = seconds;
    }


    // האם הניגון  התחיל
    private bool HasStarted()
    {
        if (video == null) return false;

        return video.isPlaying == true && video.frame >= 1;
    }

    // ניסיון שני, בלי קול

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

    // הצגת הוידאו רק אחרי סיום הטעינה
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

    // ניקוי 
    private static void ClearToBlack(RenderTexture texture)
    {
        RenderTexture previous = RenderTexture.active;

        RenderTexture.active = texture;
        GL.Clear(true, true, Color.black);

        RenderTexture.active = previous;
    }

    // דילוג על הסרטון
    public void Skip()
    {
        Finish();
    }

    private void OnEnded(VideoPlayer source)
    {
        Finish();
    }

    // תקלת ניגון אינה עוצרת את המשחק. ממשיכים לסצנה הבאה, כדי
    // ששחקן לא יישאר תקוע 
    private void OnError(VideoPlayer source, string message)
    {
        Debug.LogWarning("[VideoScene] שגיאה בניגון הסרטון: " + message);
        Finish();
    }

    // מעבר לסצנה הבאה
    private void Finish()
    {
        if (finished == true) return;

        finished = true;

        if (AudioManager.Instance != null) AudioManager.Instance.ResumeMusic();

        // עוצרים את הניגון 
        if (video != null)
        {
            video.errorReceived -= OnError;
            video.loopPointReached -= OnEnded;
            video.Pause();
        }

        // עצירת הניגון
        if (soundtrackSpeaker != null) soundtrackSpeaker.Stop();

        if (string.IsNullOrEmpty(nextScene) == true)
        {
            Debug.LogWarning("[VideoScene] לא הוגדרה סצנה להמשך");
            return;
        }

        SceneManager.LoadScene(nextScene);
    }

    // פונקציות עזר
    // חיבור כפתור דלג 
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

    // משחרר את הזכרון של הסרטון.
    
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
