using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TimerScript : MonoBehaviour
{
    [Header("Objects")]
    // הספרייט של השמש
    [SerializeField] SpriteRenderer sunImage;

    // הטקסט של הספירה לאחור
    [SerializeField] TMP_Text timerText;

    // האבן שהשמש יושבת עליה. נסתרת יחד עם הטיימר כשאין הגבלת זמן.
    // אם לא חובר בעורך, מחפשים ילד בשם EmptyRock
    [SerializeField] GameObject emptyRock;

    [Header("Sun Sprites")]
    // שלבי השמש לפי הסדר
    [SerializeField] List<Sprite> sunSprites;

    // הספרייט האחרון (השקיעה) שמור לשניות האחרונות בלבד.
    // בלי זה, ברשימה קצרה השמש מגיעה לשקיעה כשעוד נשאר הרבה זמן
    [SerializeField] float lastSunSeconds = 3f;

    // רושם ב-Console לאיזה שלב השמש עברה ובכמה זמן שנשאר.
    // מכבים אחרי שמוודאים שהסנכרון תקין
    [SerializeField] bool logSunSteps = false;

    // הרשימה בלי תאים ריקים. תא ריק ברשימה שיבש את החישוב
    private List<Sprite> readySprites;

    // השלב שמוצג כרגע, כדי לא להחליף ספרייט בכל פריים
    private int shownSunIndex = -1;

    [Header("Colors")]
    // חום כהה, #843D09. זה הצבע לאורך כל הזמן הרגיל
    [SerializeField] Color normalColor = new Color(0.5176471f, 0.23921569f, 0.03529412f, 1f);

    // מתחת לכמה שניות הטקסט הופך לאדום
    [SerializeField] int warningSeconds = 10;
    [SerializeField] Color warningColor = Color.red;

    [Header("Frozen")]
    // הצבע של הטיימר כשהוא עדיין לא התחיל לרוץ
    [SerializeField] Color frozenColor = new Color(0.65f, 0.65f, 0.65f, 0.75f);

    // כמה השמש דוהה כשהטיימר קפוא
    [Range(0f, 1f)]
    [SerializeField] float frozenSunAlpha = 0.45f;

    // הטקסט שמוצג כשהשלב הוא ללא הגבלת זמן
    [SerializeField] string unlimitedText = "\u221E";

    // הטיימר עדיין לא התחיל לרוץ (תצוגת האגם בתחילת השאלה)
    private bool frozen;

    // השלב הזה הוא ללא הגבלת זמן
    private bool unlimited;

    // הפעימה שמסמנת לשחקן שהזמן התחיל
    private float pulseTimer;
    private Vector3 startScale;

    void Awake()
    {
        // הגודל נשמר כאן ולא ב-Start.
        // ה-GameManager מקפיא את הטיימר כבר ב-Start שלו, ואם הוא רץ ראשון
        // אז startScale עדיין היה אפס - והשמש הייתה מתכווצת ונעלמת
        startScale = transform.localScale;

        if (sunImage == null) sunImage = GetComponent<SpriteRenderer>();
        if (sunImage == null) sunImage = GetComponentInChildren<SpriteRenderer>();
        if (timerText == null) timerText = GetComponentInChildren<TMP_Text>();
    }

    void Start()
    {
        BuildReadySprites();
        CheckSetup();
    }

    // בונה רשימה נקייה בלי תאים ריקים, פעם אחת
    private void BuildReadySprites()
    {
        readySprites = new List<Sprite>();

        if (sunSprites == null) return;

        for (int i = 0; i < sunSprites.Count; i++)
        {
            if (sunSprites[i] != null) readySprites.Add(sunSprites[i]);
        }
    }

    // רשת ביטחון: אם משום מה הגודל שנשמר הוא אפס, לא מכווצים את הטיימר
    private Vector3 SafeScale()
    {
        if (startScale == Vector3.zero) return Vector3.one;

        return startScale;
    }

    void Update()
    {
        // פעימה קצרה ברגע שהטיימר מתחיל לרוץ
        if (pulseTimer <= 0) return;

        pulseTimer -= Time.deltaTime;

        if (pulseTimer <= 0)
        {
            transform.localScale = SafeScale();
            return;
        }

        float t = 1f - (pulseTimer / pulseLength);
        float grow = Mathf.Sin(t * Mathf.PI) * 0.25f;

        transform.localScale = SafeScale() * (1f + grow);
    }

    private const float pulseLength = 0.6f;

    // הטיימר קפוא בזמן שהמצלמה מראה את האגם, ומתחיל לרוץ כשהיא חוזרת.
    // כשהוא מתחיל, הוא פועם רגע כדי שהשחקן יראה שהזמן התחיל.
    public void SetFrozen(bool isFrozen)
    {
        if (frozen == isFrozen) return;

        frozen = isFrozen;

        if (isFrozen == false)
        {
            pulseTimer = pulseLength;
        }
        else
        {
            transform.localScale = SafeScale();
            pulseTimer = 0;
        }

        ApplyFrozenLook();
    }

    // שלב ללא הגבלת זמן.
    // אין מה להציג, ולכן השמש, האבן והמספר יורדים מהמסך לגמרי
    public void SetUnlimited(bool isUnlimited)
    {
        unlimited = isUnlimited;

        // שאלה חדשה מתחילה מהשלב הראשון של השמש
        shownSunIndex = -1;

        ShowTimerObjects(unlimited == false);

        if (unlimited == true && timerText != null)
        {
            timerText.text = unlimitedText;
            timerText.color = normalColor;
        }
    }

    // מדליק או מכבה את שלושת חלקי הטיימר
    private void ShowTimerObjects(bool show)
    {
        if (sunImage != null) sunImage.gameObject.SetActive(show);
        if (timerText != null) timerText.gameObject.SetActive(show);

        GameObject rock = FindEmptyRock();
        if (rock != null) rock.SetActive(show);
    }

    // האבן לא תמיד מחוברת בעורך, ולכן מחפשים אותה גם לפי שם
    private GameObject FindEmptyRock()
    {
        if (emptyRock != null) return emptyRock;

        // השמש והאבן יושבות תחת אותו הורה
        Transform parent = sunImage != null ? sunImage.transform.parent : transform.parent;

        if (parent == null) return null;

        Transform found = parent.Find("EmptyRock");

        if (found != null) emptyRock = found.gameObject;

        return emptyRock;
    }

    private void ApplyFrozenLook()
    {
        if (sunImage != null)
        {
            Color sunColor = sunImage.color;
            sunColor.a = frozen ? frozenSunAlpha : 1f;
            sunImage.color = sunColor;
        }

        if (timerText != null && frozen == true)
        {
            timerText.color = frozenColor;
        }
    }

    private void CheckSetup()
    {
        if (timerText == null)
        {
            Debug.LogWarning("Timer Script: 'Timer Text' is empty. " +
                             "Create a TextMeshPro object for the countdown and drag it in");
        }

        if (sunImage == null)
        {
            Debug.LogWarning("Timer Script: 'Sun Image' is empty. " +
                             "Drag the Sprite Renderer of the sun object in");
        }

        if (sunSprites == null || sunSprites.Count == 0)
        {
            Debug.LogWarning("Timer Script: 'Sun Sprites' list is empty. " +
                             "Set Size to 11 and drag HB_1 .. HB_11 in order");
        }
        else
        {
            int empty = sunSprites.Count - readySprites.Count;

            Debug.Log("Timer Script ready with " + readySprites.Count + " sun sprites" +
                      (empty > 0 ? " (" + empty + " empty slots in the list were ignored)" : ""));
        }
    }

    public void ShowTime(float timeLeft, float totalTime)
    {
        // ללא הגבלת זמן - אין ספירה לאחור ואין שקיעה של השמש
        if (unlimited == true)
        {
            if (timerText != null) timerText.text = unlimitedText;
            return;
        }

        ShowNumbers(timeLeft);
        ShowSun(timeLeft, totalTime);
    }

    private void ShowNumbers(float timeLeft)
    {
        if (timerText == null) return;

        if (timeLeft < 0) timeLeft = 0;

        timerText.text = Mathf.Ceil(timeLeft).ToString();

        // כל עוד הטיימר לא התחיל, המספר דהוי
        if (frozen == true)
        {
            timerText.color = frozenColor;
            return;
        }

        if (timeLeft <= warningSeconds)
        {
            timerText.color = warningColor;
        }
        else
        {
            timerText.color = normalColor;
        }
    }

    // בוחר את שלב השמש לפי הזמן שנשאר.
    // השלב האחרון שמור לשניות האחרונות, וכל שאר השלבים
    // נפרסים באופן שווה על כל הזמן שלפניהן. ככה השמש לא
    // מגיעה לשקיעה בזמן שעוד נשארו עשרות שניות
    private void ShowSun(float timeLeft, float totalTime)
    {
        if (sunImage == null) return;
        if (totalTime <= 0) return;

        if (readySprites == null) BuildReadySprites();
        if (readySprites.Count == 0) return;

        int lastIndex = readySprites.Count - 1;

        if (timeLeft < 0) timeLeft = 0;

        int index;

        if (lastIndex <= 0)
        {
            index = 0;
        }
        else
        {
            // כמה שניות שמורות לשלב האחרון. לא יותר מרבע מזמן השאלה,
            // כדי שגם בשאלה קצרה הפריסה תישאר הגיונית
            float tail = Mathf.Min(lastSunSeconds, totalTime * 0.25f);

            if (timeLeft <= tail)
            {
                index = lastIndex;
            }
            else
            {
                // הזמן שבו נפרסים כל השלבים חוץ מהאחרון
                float usable = totalTime - tail;

                float passed = 1f - ((timeLeft - tail) / usable);

                if (passed < 0) passed = 0;
                if (passed > 1) passed = 1;

                index = Mathf.FloorToInt(passed * lastIndex);

                if (index < 0) index = 0;

                // השלב האחרון מגיע רק דרך התנאי שלמעלה
                if (index > lastIndex - 1) index = lastIndex - 1;
            }
        }

        if (index == shownSunIndex) return;

        shownSunIndex = index;
        sunImage.sprite = readySprites[index];

        if (logSunSteps == true)
        {
            Debug.Log("Sun step " + (index + 1) + "/" + readySprites.Count +
                      " with " + timeLeft.ToString("0.0") + "s left of " + totalTime + "s");
        }
    }
}
