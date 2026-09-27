using System.Collections.Generic;
using UnityEngine;
using TMPro;

//  התצוגה של מצב המשחק: כמה זמן נשאר וכמה שאלות כבר נענו

public class GameStatus : MonoBehaviour
{
    // הטיימר

    [Header("Timer Objects")]
    // הספרייט של השמש
    [SerializeField] SpriteRenderer sunImage;

    // הטקסט של הספירה לאחור
    [SerializeField] TMP_Text timerText;

    // האבן שהשמש יושבת עליהן
    [SerializeField] GameObject emptyRock;

    // האובייקט  ההגדלה של השמש    
    [SerializeField] Transform pulseTarget;

    [Header("Sun Sprites")]
    // שלבי השמש לפי הסדר
    [SerializeField] List<Sprite> sunSprites;

    // הספרייט האחרון (השקיעה) שמור לשניות האחרונות בלבד
    [SerializeField] float lastSunSeconds = 3f;

    // לאיזה שלב השמש עברה 
    [SerializeField] bool logSunSteps = false;

    [Header("Timer Colors")]
    //  הצבע  למשך הזמן הרגיל
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

    // הרשימה בלי תאים ריקים
    private List<Sprite> readySprites;

    // השלב שמוצג כרגע
    private int shownSunIndex = -1;

    // הטיימר עדיין לא התחיל לרוץ (תצוגת האגם בתחילת השאלה)
    private bool frozen;

    // שלב ללא הגבלת זמן
    private bool unlimited;

    // הפעימה שמסמנת לשחקן שהזמן התחיל
    private float pulseTimer;
    private Vector3 startScale;

    private const float pulseLength = 0.6f;


    //   מד ההתקדמות
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
    // שלושת האובייקטים שמסמנים את מד ההתקדמות

    [SerializeField] SpriteRenderer startView;
    [SerializeField] SpriteRenderer centerView;
    [SerializeField] SpriteRenderer endView;

    // החרוזים לפי סדר ההתקדמות: ראשון, אמצעיים, אחרון
    private readonly List<SpriteRenderer> beads = new List<SpriteRenderer>();

    // השכפולים שנוצרו בזמן ריצה
    private readonly List<GameObject> clones = new List<GameObject>();

    private int total = 0;
    private int filled = 0;


    //  אתחול

    void Awake()
    {

        if (pulseTarget == null && sunImage != null) pulseTarget = sunImage.transform;

        startScale = pulseTarget != null ? pulseTarget.localScale : Vector3.one;

        FindAnchors();
    }

  
    void Start()
    {
        BuildReadySprites();
        CheckSetup();
    }

    // מטפל בפעימה הקצרה של השמש 
   
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

    //  אם משום מה הגודל שנשמר הוא אפס, לא מכווצים את הטיימר
    private Vector3 SafeScale()
    {
        if (startScale == Vector3.zero) return Vector3.one;

        return startScale;
    }


    // הטיימר קפוא בזמן שהמצלמה מראה את האגם, ומתחיל לרוץ כשהיא חוזרת
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

    // שלב ללא הגבלת זמן
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


    public void ShowTime(float timeLeft, float totalTime)
    {
        // ללא הגבלת זמן- אין ספירה לאחור ואין שקיעה של השמש
        if (unlimited == true)
        {
            if (timerText != null) timerText.text = unlimitedText;
            return;
        }

        ShowNumbers(timeLeft);
        ShowSun(timeLeft, totalTime);
    }



    // בונה רשימה  בלי תאים ריקים
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

    // החיפוש אבן לפי שם
    private GameObject FindEmptyRock()
    {
        if (emptyRock != null) return emptyRock;

        Transform parent = sunImage != null ? sunImage.transform.parent : transform;

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

    // הצגת  הספירה לאחור במספרים
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

    // בחירת  שלב השמש לפי הזמן שנשאר
  
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
            //  השניות השמורות לשלב האחרון
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

                // השלב האחרון 
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


    // מד ההתקדמות
    // בניית  המד מחדש לפי מספר השאלות במשחק
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

        // מד התקדמות ראשון
        beads.Add(startView);
        if (startView != null) startView.gameObject.SetActive(true);

        // מדי ההתקדמות שבאמצע
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

        // מד ההתקדמות האחרון. כששאלה אחת בלבד אין קצה שני
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

    // מסמן כמה שאלות כבר נענו. אלה שנענו מקבלות אייקון ירוק
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


    // מפזר את אייקוני האמצע במרווחים שווים בין שני הקצוות
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

    // האייקון הראשון והאחרון מקבלים את ספרייטי הקצה
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

    // חיפוש מד ההתקדמות לפי שם 
    private void FindAnchors()
    {
        if (startView == null) startView = FindByName("StartProgressBar");
        if (centerView == null) centerView = FindByName("CenterProgressBar");
        if (endView == null) endView = FindByName("EndProgressBar");
    }

    // חיםוש רכיב  לפי שם 
    private SpriteRenderer FindByName(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName) return child.GetComponent<SpriteRenderer>();
        }

        return null;
    }

    // מדליקים או מכבים את שלושת מיקומי מד ההתקדמות 
    private void ShowAnchors(bool on)
    {
        if (startView != null) startView.gameObject.SetActive(on);
        if (centerView != null) centerView.gameObject.SetActive(on);
        if (endView != null) endView.gameObject.SetActive(on);
    }

    // ניקוי האייקונים שנוצרו בבנייה הקודמת
    private void ClearClones()
    {
        for (int i = 0; i < clones.Count; i++)
        {
            if (clones[i] == null) continue;

            Destroy(clones[i]);
        }

        clones.Clear();
    }

    // בדיקת האיתחול של אובייקטים בסצנה
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
