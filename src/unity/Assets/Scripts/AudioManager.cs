using UnityEngine;

// מנהל הסאונד של המשחק.
// אובייקט אחד ששורד בין הסצנות, מנגן מוזיקת רקע רציפה ואפקטים.
// אפשר לשים עותק של ה-Prefab בכל סצנה: הראשון שנוצר שורד, השאר מוחקים את עצמם,
// ככה מוזיקת הרקע לא מתחילה מחדש בכל מעבר מסך.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    // הנגן של מוזיקת הרקע. Loop דלוק
    [SerializeField] AudioSource musicSource;

    // הנגן של האפקטים הקצרים
    [SerializeField] AudioSource sfxSource;

    [Header("Clips")]
    // מוזיקת רקע לאורך כל המשחק
    [SerializeField] AudioClip backgroundMusic;

    // כל פעם שמונחת תשובה נכונה
    [SerializeField] AudioClip correctSound;

    // כל פעם שמונחת תשובה שגויה
    [SerializeField] AudioClip wrongSound;

    // סיום שלב בהצלחה
    [SerializeField] AudioClip stageCompleteSound;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] float musicVolume = 0.35f;

    [Range(0f, 1f)]
    [SerializeField] float sfxVolume = 1f;

    void Awake()
    {
        // כבר יש מנהל סאונד פעיל - העותק הזה מיותר
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        BuildSources();
        ApplyMute();
    }

    // מוזיקת הרקע מתחילה ב-Start ולא ב-Awake, כדי שנגני הסאונד
    // כבר ייבנו ומצב ההשתקה כבר ייקבע לפני שמשמיעים משהו
    void Start()
    {
        PlayMusic();
    }

    // יוצר את נגני הסאונד אם לא חוברו ב-Inspector
    private void BuildSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;

        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;
    }

    // מתחיל את מוזיקת הרקע. אם היא כבר מתנגנת - לא מפריעים לה
    public void PlayMusic()
    {
        if (musicSource == null) return;
        if (backgroundMusic == null)
        {
            Debug.LogWarning("AudioManager: Background Music is empty. " +
                             "Drag Background_Sound into the Background Music field");
            return;
        }

        if (musicSource.isPlaying == true && musicSource.clip == backgroundMusic) return;

        musicSource.clip = backgroundMusic;
        musicSource.Play();
    }

    // תשובה נכונה
    public void PlayCorrect()
    {
        PlaySfx(correctSound);
    }

    // תשובה שגויה
    public void PlayWrong()
    {
        PlaySfx(wrongSound);
    }

    // סיום שלב בהצלחה
    public void PlayStageComplete()
    {
        PlaySfx(stageCompleteSound);
    }

    // משמיע צליל בודד. PlayOneShot ולא Play, כדי ששני צלילים
    // שנופלים יחד לא יקטעו זה את זה
    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null) return;
        if (clip == null) return;

        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // ============================================================
    // משהה את מוזיקת הרקע בלי לאבד את מקום הניגון.
    //
    // נקרא לפני סרטון, כדי שהמוזיקה והפסקול של הסרטון לא
    // יתנגנו זה על גבי זה. Pause ולא Stop, כדי שהחזרה תמשיך
    // מאותה נקודה ולא תתחיל את השיר מחדש
    // ============================================================
    public void PauseMusic()
    {
        if (musicSource == null) return;
        if (musicSource.isPlaying == false) return;

        musicSource.Pause();
    }

    // מחזיר את המוזיקה אחרי הסרטון
    public void ResumeMusic()
    {
        if (musicSource == null) return;
        if (backgroundMusic == null) return;

        musicSource.UnPause();

        // אם מסיבה כלשהי היא נעצרה לגמרי ולא רק הושהתה
        if (musicSource.isPlaying == false) PlayMusic();
    }

    // כפתור הסאונד קורא לפונקציה הזאת
    public void SetSoundOn(bool on)
    {
        DataPass.soundOn = on;
        ApplyMute();
    }

    // מחיל את מצב הסאונד על עוצמת המאזין הכללית, ולכן הוא
    // משתיק גם את המוזיקה וגם את האפקטים במכה אחת
    private void ApplyMute()
    {
        AudioListener.volume = DataPass.soundOn ? 1f : 0f;
    }

    // גישה בטוחה מכל מקום, גם אם אין מנהל סאונד בסצנה
    public static void Correct()
    {
        if (Instance != null) Instance.PlayCorrect();
    }

    // תשובה שגויה, מכל מקום בקוד
    public static void Wrong()
    {
        if (Instance != null) Instance.PlayWrong();
    }

    // סיום שלב בהצלחה, מכל מקום בקוד
    public static void StageComplete()
    {
        if (Instance != null) Instance.PlayStageComplete();
    }
}
