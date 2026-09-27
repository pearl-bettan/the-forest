using UnityEngine;
// מנהל הסאונד של המשחק 
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

    void Start()
    {
        PlayMusic();
    }
   
    //Inspectorיוצר את נגני הסאונד אם לא חוברו ב
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

    // מתחיל את מוזיקת הרקע
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

    // שיהיה אפשר לנגן בו זמנית, והסאונדים לא יקטעו אחד את השני
    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null) return;
        if (clip == null) return;

        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // עוצר את מוזיקת הרקע בלי לאבד את מקום הניגון
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

    // משתיק גם את המוזיקה וגם את האפקטים במכה אחת
    private void ApplyMute()
    {
        AudioListener.volume = DataPass.soundOn ? 1f : 0f;
    }

    public static void Correct()
    {
        if (Instance != null) Instance.PlayCorrect();
    }

    // תשובה שגויה
    public static void Wrong()
    {
        if (Instance != null) Instance.PlayWrong();
    }

    // סיום שלב בהצלחה
    public static void StageComplete()
    {
        if (Instance != null) Instance.PlayStageComplete();
    }
}
