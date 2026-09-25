using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ============================================================
//  התצוגה של מצב המשחק: כמה זמן נשאר וכמה שאלות כבר נענו.
//
//  הרכיב הזה מאחד שני דברים שהיו קודם שני סקריפטים נפרדים:
//
//      הטיימר       - השמש ששוקעת והספירה לאחור
//      מד ההתקדמות  - שרשרת החרוזים שמתמלאת שאלה אחר שאלה
//
//  שניהם מוצגים ב-Menu, שניהם מתעדכנים מה-GameManager באותם
//  רגעים בדיוק, ושניהם רק מציגים - אין להם לוגיקת משחק משלהם.
//  לכן הם יושבים ברכיב אחד על אובייקט Menu.
//
//  שימוש מהקוד:
//      gameStatus.SetUnlimited(noTimeLimit);
//      gameStatus.ShowTime(timeLeft, totalTime);
//      gameStatus.Build(totalQuestions);
//      gameStatus.SetProgress(questionsAnswered);
// ============================================================
public class GameStatus : MonoBehaviour
{
    // ============================================================
    //  חלק ראשון: הטיימר
    // ============================================================

    [Header("Timer Objects")]
    // הספרייט של השמש
    [SerializeField] SpriteRenderer sunImage;

    // הטקסט של הספירה לאחור
    [SerializeField] TMP_Text timerText;

    // האבן שהשמש יושבת עליה. נסתרת יחד עם הטיימר כשאין הגבלת זמן.
    // אם לא חובר בעורך, מחפשים ילד בשם EmptyRock
    [SerializeField] GameObject emptyRock;

    // מה פועם כשהטיימר מתחיל לרוץ. הרכיב הזה יושב על Menu,
    // שמחזיק גם את מד ההתקדמות, ולכן הפעימה חייבת להיות
    // מכוונת לאובייקט השמש ולא ל-transform של הרכיב עצמו -
    // אחרת כל התצוגה הייתה גדלה וקטנה יחד עם הטיימר
    [SerializeField] Transform pulseTarget;

    [Header("Sun Sprites")]
    // שלבי השמש לפי הסדר
    [SerializeField] List<Sprite> sunSprites;

    // הספרייט האחרון (השקיעה) שמור לשניות האחרונות בלבד.
    // בלי זה, ברשימה קצרה השמש מגיעה לשקיעה כשעוד נשאר הרבה זמן
    [SerializeField] float lastSunSeconds = 3f;

    // רושם ב-Console לאיזה שלב השמש עברה ובכמה זמן שנשאר.
    // מכבים אחרי שמוודאים שהסנכרון תקין
    [SerializeField] bool logSunSteps = false;

    [Header("Timer Colors")]
    // חום כהה, #843D09. זה הצבע לאורך כל הזמן הרגיל
    [SerializeField] Color normalColor = new Color(0.5176471f, 0.23921569f, 0.03529412f, 1f);

    // מתחת לכמה שניות הטקסט הופך לאדום
    [SerializeField] int warningSeconds = 10;
    [SerializeField] Color warningColor = Color.red;

    [Header("Timer Frozen")]
    // הצבע של הטיימר כשהוא עדיין לא התחיל לרוץ
    [SerializeField] Color frozenColor = new Color(0.65f, 0.65f, 0.65f, 0.75f);

    // כמה השמש דוהה כשהטיימר קפוא
    [Range(0f, 1f)]
    [SerializeField] float frozenSunAlpha = 0.45f;

    // הטקסט שמוצג כשהשלב הוא ללא הגבלת זמן
    [SerializeField] string unlimitedText = "∞";

    // הרשימה בלי תאים ריקים. תא ריק ברשימה שיבש את החישוב
    private List<Sprite> readySprites;

    // השלב שמוצג כרגע, כדי לא להחליף ספרייט בכל פריים
    private int shownSunIndex = -1;

    // הטיימר עדיין לא התחיל לרוץ (תצוגת האגם בתחילת השאלה)
    private bool frozen;

    // השלב הזה הוא ללא הגבלת זמן
    private bool unlimited;

    // הפעימה שמסמנת לשחקן שהזמן התחיל
    private float pulseTimer;
    private Vector3 startScale;

    private const float pulseLength = 0.6f;


    // ============================================================
    //  חלק שני: מד ההתקדמות
    //
    //  המד הוא שרשרת חרוזים על גבי הגבעול: חרוז אחד לכל שאלה.
    //  שלושת האובייקטים שבסצנה משמשים כעוגנים:
    //
    //      StartProgressBar   - החרוז הראשון, בקצה אחד
    //      EndProgressBar     - החרוז האחרון, בקצה השני
    //      CenterProgressBar  - תבנית לחרוזים שבאמצע
    //
    //  החרוזים שבאמצע נוצרים כשכפולים של תבנית המרכז ומפוזרים
    //  במרווחים שווים על הקו שבין שני הקצוות. כך המיקום, הסיבוב
    //  והגודל נקבעים בעורך ולא בקוד.
    //
    //  כל שאלה שנענתה הופכת חרוז אחד מאפור לירוק, מהתחלה לסוף.
    // ============================================================

    [Header("Progress Sprites")]
    // חרוז הקצה הראשון
    [SerializeField] Sprite startGray;
    [SerializeField] Sprite startGreen;

    // החרוזים שבאמצע
    [SerializeField] Sprite middleGray;
    [SerializeField] Sprite middleGreen;

    // חרוז הקצה האחרון
    [SerializeField] Sprite endGray;
    [SerializeField] Sprite endGreen;

    [Header("Progress Anchors")]
    // שלושת האובייקטים שמסמנים את המד בסצנה. אם לא חוברו כאן,
    // הם נמצאים לפי השם בין הצאצאים של האובייקט הזה
    [SerializeField] SpriteRenderer startView;
    [SerializeField] SpriteRenderer centerView;
    [SerializeField] SpriteRenderer endView;

    // החרוזים לפי סדר ההתקדמות: ראשון, אמצעיים, אחרון
    private readonly List<SpriteRenderer> beads = new List<SpriteRenderer>();

    // השכפולים שנוצרו בזמן ריצה, כדי לנקות אותם בבנייה מחדש
    private readonly List<GameObject> clones = new List<GameObject>();

    private int total = 0;
    private int filled = 0;


    // ============================================================
    //  אתחול
    // ============================================================

    void Awake()
    {
        // השמש והטקסט לא מאותרים כאן לפי סוג הרכיב. הרכיב יושב על
        // Menu, שמחזיק גם את מד ההתקדמות, ו-GetComponentInChildren
        // היה מחזיר את החרוז הראשון של המד במקום את השמש.
        // מה שלא חובר בעורך נשאר ריק, ו-CheckSetup מתריע עליו
        if (pulseTarget == null && sunImage != null) pulseTarget = sunImage.transform;

        // הגודל נשמר כאן ולא ב-Start.
        // ה-GameManager מקפיא את הטיימר כבר ב-Start שלו, ואם הוא רץ ראשון
        // אז startScale עדיין היה אפס - והשמש הייתה מתכווצת ונעלמת

        startScale = pulseTarget != null ? pulseTarget.localScale : Vector3.one;

        FindAnchors();
    }

    // רץ אחרי כל ה-Awake בסצנה: מכין את רשימת הספרייטים ובודק
    // שההרכבה באינספקטור שלמה. שלב הבדיקה חייב להיות כאן ולא
    // ב-Awake, כי רק עכשיו כל הרכיבים כבר אותחלו
    void Start()
    {
        BuildReadySprites();
        CheckSetup();
    }

    // ============================================================
    // מטפל בפעימה הקצרה של השמש ברגע שהטיימר משתחרר.
    //
    // יוצא מיד כשאין פעימה פעילה, ולכן הוא זול כמעט בכל פריים.
    // זו הסיבה שאפשר להשאיר אותו ב-Update ולא להעביר לקורוטינה
    // ============================================================
    void Update()
    {
        // פעימה קצרה ברגע שהטיימר מתחיל לרוץ
        if (pulseTimer <= 0) return;
        if (pulseTarget == null) return;

        pulseTimer -= Time.deltaTime;

        if (pulseTimer <= 0)
        {
            pulseTarget.localScale = SafeScale();
            return;
        }

        float t = 1f - (pulseTimer / pulseLength);
        float grow = Mathf.Sin(t * Mathf.PI) * 0.25f;

        pulseTarget.localScale = SafeScale() * (1f + grow);
    }

    // רשת ביטחון: אם משום מה הגודל שנשמר הוא אפס, לא מכווצים את הטיימר
    private Vector3 SafeScale()
    {
        if (startScale == Vector3.zero) return Vector3.one;

        return startScale;
    }


    // ============================================================
    //  הטיימר: ממשק ציבורי
    // ============================================================

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
            if (pulseTarget != null) pulseTarget.localScale = SafeScale();
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

    // ============================================================
    // מעדכנת את תצוגת הזמן. נקראת מה-GameManager בכל פריים.
    //
    // מקבלת גם את הזמן שנותר וגם את הזמן הכולל, כי מיקום השמש
    // הוא היחס ביניהם ולא ערך מוחלט. כך אותו קוד עובד לשאלה
    // בת דקה ולשאלה בת שתי דקות.
    //
    // במצב ללא הגבלת זמן מוצג כיתוב קבוע במקום מספר, והשמש
    // אינה זזה בכלל
    // ============================================================
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


    // ============================================================
    //  הטיימר: פנימי
    // ============================================================

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
        Transform parent = sunImage != null ? sunImage.transform.parent : transform;

        if (parent == null) return null;

        Transform found = parent.Find("EmptyRock");

        if (found != null) emptyRock = found.gameObject;

        return emptyRock;
    }

    // מחילה את המראה הדהוי של טיימר שעוד לא התחיל לרוץ:
    // שמש שקופה למחצה ומספר אפור. זה החיווי לשחקן שהספירה
    // עדיין לא רצה ואפשר להסתכל על השאלה בנחת
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

    // ============================================================
    // מציגה את הספירה לאחור במספרים.
    //
    // Ceil ולא Floor: כך השחקן רואה "1" בשנייה האחרונה ולא "0",
    // והאפס מופיע רק כשהזמן באמת נגמר.
    //
    // הצבע מעביר מידע נוסף - אפור לפני ההתחלה, ואזהרה בשניות
    // האחרונות
    // ============================================================
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


    // ============================================================
    //  מד ההתקדמות: ממשק ציבורי
    // ============================================================

    // בונה את המד מחדש לפי מספר השאלות במשחק
    public void Build(int questionCount)
    {
        FindAnchors();
        ClearClones();

        beads.Clear();
        total = questionCount;

        if (total <= 0)
        {
            ShowAnchors(false);
            return;
        }

        // חרוז ראשון
        beads.Add(startView);
        if (startView != null) startView.gameObject.SetActive(true);

        // חרוזי האמצע: הראשון שבהם הוא תבנית המרכז עצמה,
        // והשאר שכפולים שלה
        int middleCount = total - 2;

        if (middleCount > 0 && centerView != null)
        {
            for (int i = 0; i < middleCount; i++)
            {
                SpriteRenderer bead = centerView;

                if (i > 0)
                {
                    GameObject copy = Instantiate(centerView.gameObject,
                                                  centerView.transform.parent);
                    copy.name = "Bead_" + (i + 2);
                    clones.Add(copy);

                    bead = copy.GetComponent<SpriteRenderer>();
                }

                bead.gameObject.SetActive(true);
                beads.Add(bead);
            }
        }
        else if (centerView != null)
        {
            // פחות משלוש שאלות: אין חרוזי אמצע
            centerView.gameObject.SetActive(false);
        }

        // חרוז אחרון. כששאלה אחת בלבד אין קצה שני
        if (total >= 2)
        {
            beads.Add(endView);
            if (endView != null) endView.gameObject.SetActive(true);
        }
        else if (endView != null)
        {
            endView.gameObject.SetActive(false);
        }

        Spread();
        SetProgress(filled);
    }

    // מסמן כמה שאלות כבר נענו. אלה שנענו מקבלות חרוז ירוק
    public void SetProgress(int answered)
    {
        if (answered < 0) answered = 0;
        if (answered > total) answered = total;

        filled = answered;

        for (int i = 0; i < beads.Count; i++)
        {
            if (beads[i] == null) continue;

            beads[i].sprite = SpriteFor(i, i < filled);
        }
    }


    // ============================================================
    //  מד ההתקדמות: פנימי
    // ============================================================

    // מפזר את חרוזי האמצע במרווחים שווים בין שני הקצוות.
    // הקצוות עצמם נשארים בדיוק במקום שנקבע להם בעורך
    private void Spread()
    {
        if (startView == null || endView == null) return;
        if (beads.Count < 3) return;

        Vector3 from = startView.transform.localPosition;
        Vector3 to = endView.transform.localPosition;

        for (int i = 1; i < beads.Count - 1; i++)
        {
            if (beads[i] == null) continue;

            float t = (float)i / (beads.Count - 1);
            beads[i].transform.localPosition = Vector3.Lerp(from, to, t);
        }
    }

    // החרוז הראשון והאחרון מקבלים את ספרייטי הקצה
    private Sprite SpriteFor(int index, bool green)
    {
        if (index == 0)
        {
            return green == true ? startGreen : startGray;
        }

        if (index == total - 1)
        {
            return green == true ? endGreen : endGray;
        }

        return green == true ? middleGreen : middleGray;
    }

    // אם העוגנים לא חוברו ב-Inspector, מאתרים אותם לפי השם.
    // החיפוש עובר על כל הצאצאים ולא רק על הילדים הישירים, כי
    // החרוזים יושבים תחת ProgressBar ולא ישירות תחת Menu
    private void FindAnchors()
    {
        if (startView == null) startView = FindByName("StartProgressBar");
        if (centerView == null) centerView = FindByName("CenterProgressBar");
        if (endView == null) endView = FindByName("EndProgressBar");
    }

    // מחפשת רכיב בן לפי שם, כולל בנים מכובים. משמשת לאיתור
    // עוגני מד ההתקדמות כשהם לא חוברו ידנית באינספקטור
    private SpriteRenderer FindByName(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName) return child.GetComponent<SpriteRenderer>();
        }

        return null;
    }

    // מדליקה או מכבה את שלושת עוגני מד ההתקדמות יחד
    private void ShowAnchors(bool on)
    {
        if (startView != null) startView.gameObject.SetActive(on);
        if (centerView != null) centerView.gameObject.SetActive(on);
        if (endView != null) endView.gameObject.SetActive(on);
    }

    // מוחקת את החרוזים שנוצרו בבנייה הקודמת. נקראת לפני כל
    // בנייה מחדש, אחרת חרוזים של משחק קודם היו נשארים על המסך
    private void ClearClones()
    {
        for (int i = 0; i < clones.Count; i++)
        {
            if (clones[i] == null) continue;

            Destroy(clones[i]);
        }

        clones.Clear();
    }


    // ============================================================
    //  בדיקת חיווט. רצה פעם אחת ב-Start
    // ============================================================
    private void CheckSetup()
    {
        if (timerText == null)
        {
            Debug.LogWarning("Game Status: 'Timer Text' is empty. " +
                             "Create a TextMeshPro object for the countdown and drag it in");
        }

        if (sunImage == null)
        {
            Debug.LogWarning("Game Status: 'Sun Image' is empty. " +
                             "Drag the Sprite Renderer of the sun object in");
        }

        if (sunSprites == null || sunSprites.Count == 0)
        {
            Debug.LogWarning("Game Status: 'Sun Sprites' list is empty. " +
                             "Set Size to 11 and drag HB_1 .. HB_11 in order");
        }
        else
        {
            int empty = sunSprites.Count - readySprites.Count;

            Debug.Log("Game Status ready with " + readySprites.Count + " sun sprites" +
                      (empty > 0 ? " (" + empty + " empty slots in the list were ignored)" : ""));
        }

        if (startView == null || centerView == null || endView == null)
        {
            Debug.LogWarning("Game Status: the progress bar anchors are missing. " +
                             "Drag StartProgressBar, CenterProgressBar and EndProgressBar in");
        }
    }
}
