using System.Collections.Generic;
using UnityEngine;

public class DwarfScript : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;

    // אובייקט ריק על היד של הגמד (להחזקת האבן)
    [SerializeField] Transform holdPoint;

    [Header("Speed")]
    [SerializeField] float walkSpeed = 4;
    [SerializeField] float rockSpeed = 8;

    // מרחק העצירה ליד האבן
    [SerializeField] float stopDistance = 1;

    [Header("Throw")]
    // כמה גבוה האבן עפה באוויר לפני שהיא נוחתת
    [SerializeField] float throwHeight = 3;

    // כמה מעלות בשנייה האבן מסתובבת באוויר
    [SerializeField] float spinSpeed = 180;

    [Header("Lift And Throw")]
    // מתי היד מגיעה לאבן
    [Range(0f, 1f)]
    [SerializeField] float grabPoint = 0.14f;

    // מתי האבן עוזבת את היד, בחלק הכי גבוה של הזריקה
    [Range(0f, 1f)]
    [SerializeField] float releasePoint = 0.33f;

    [Header("Sync With Animation")]
    [SerializeField] bool syncWithAnimation = true;

    // אנימציית ההרמה
    [SerializeField] string liftStateName = "Dwarf_Lift";
    [SerializeField] float fallbackLiftLength = 1.2f;

    // אנימציית האבן
    [SerializeField] AnimationCurve liftCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Reaction")]
    // כמה זמן להראות חיווי חיובי או שלילי אחרי שהאבן נחתה במקום
    [SerializeField] float reactionTime = 0.8f;

    [Header("Sprite Direction")]
    // ספרייט ההליכה הצדדית
    [SerializeField] bool sideSpriteLooksRight = true;

    [Header("Skipping")]
    // ספרייט הדילוג. מופרד מספרייט ההליכה כי הוא גיליון אחר,
    // ולא בהכרח מצויר לאותו כיוון
    [SerializeField] bool skipSpriteLooksRight = true;

    // הדילוג תמיד נע מהתגית הימנית לשמאלית, ולכן הגמד צריך
    // להסתכל שמאלה לכל אורכו. כיבוי מחזיר אותו לכיוון התנועה
    [SerializeField] bool skipAlwaysFacesLeft = true;

    // עד כמה הגמד מתקרב לאבן הראשונה לפני שהוא מתחיל לדלג.
    // עד הנקודה הזאת הוא הולך באנימציית ההליכה הרגילה
    [SerializeField] float skipStartDistance = 1.2f;

    // גובה הקפיצה בין אבן לאבן
    [SerializeField] float skipHeight = 1.1f;

    // כמה להרים את הגמד מעל נקודת האבן.
    // נקודות ה-Slots הן המקום שאליו האבנים עפות, והן יושבות
    // באמצע הסלע. נקודת האחיזה של הגמד היא באמצע הגוף ולא ברגליים,
    // ולכן בלי ההרמה הזאת הוא נראה שקוע בתוך הסלע
    [SerializeField] float skipStoneOffsetY = 1.5f;

    // כמה זמן הגמד עומד על האבן האחרונה לפני שממשיכים
    [SerializeField] float skipEndPause = 0.6f;

    // שם הפרמטר של אנימציית הדילוג ב-Animator
    [SerializeField] string skipBoolName = "IsSkipping";

    // שם מצב הקפיצה ב-Animator. משמש כדי להתחיל את הקליפ
    // מההתחלה בכל קפיצה, כך שהתנועה והציור לא מתפצלים
    [SerializeField] string jumpStateName = "Jump";

    [Header("Jump Clip Phases")]
    // מבנה הקליפ Jump בשניות, כפי שהוא מצויר:
    //   0     - 0.10    התכופפות והתנתקות מהאבן
    //   0.10  - 1.10    באוויר
    //   1.10  - 2.1333  נחיתה והתייצבות על האבן הבאה
    // הקוד מזיז את הגמד לפי החלוקה הזאת, ולכן הרגליים עוזבות
    // את האבן ונוחתות עליה בדיוק כשהציור עושה את זה
    [SerializeField] float clipTakeOffTime = 0.10f;
    [SerializeField] float clipAirTime = 1.00f;
    [SerializeField] float clipLandTime = 1.0333f;

    // כמה זמן תימשך קפיצה שלמה במשחק. הקליפ נמתח או מתכווץ
    // לזמן הזה, ושלושת השלבים נשארים ביחס המקורי ביניהם
    [SerializeField] float jumpCycleTime = 0.7f;

    [Header("Free Walk")]
    // האם מותר לשחקן לשלוח את הגמד לטייל על הדשא
    [SerializeField] bool allowFreeWalk = true;

    public GameManager gameManager;
    public CameraScript gameCamera;

    // המצב הנוכחי של הגמד
    private string state = "wait";

    private RockScript currentRock;
    private Vector2 rockTarget;

    // הנקודה הגבוהה שאליה האבן עפה לפני שהיא נוחתת
    private Vector2 throwUpPoint;

    private bool answerIsCorrect;

    private float waitTimer;
    private Vector2 homePosition;
    private float liftProgress;
    private bool liftStateSeen;
    private Vector2 rockGroundPosition;
    private int liftStateHash;
    private bool turnReported;

    // הליכה חופשית על הדשא
    private Vector2 freeWalkTarget;

    // אנימציית הדילוג
    private List<Vector2> skipPath;
    private int skipIndex;
    private Vector2 skipFrom;
    private float skipEndTimer;
    private System.Action skipFinished;

    // כמה זמן עבר מתחילת הקפיצה הנוכחית
    private float hopTimer;

    // המזהה של מצב הקפיצה ב-Animator
    private int jumpStateHash;

    void Awake()
    {
        homePosition = transform.position;
        state = "wait";
    }

    void Start()
    {
        StopWalkAnimation();
        liftStateHash = Animator.StringToHash(liftStateName);
    }

    // האם הגמד פנוי לקבל פקודה חדשה
    public bool IsIdle()
    {
        return state == "wait";
    }

    void Update()
    {
        // הולך לכיוון האבן שנבחרה
        if (state == "walkToRock")
        {
            Vector2 rockPosition = currentRock.transform.position;
            WalkTowards(rockPosition);

            if (Vector2.Distance(transform.position, rockPosition) <= stopDistance)
            {
                StopWalkAnimation();

                // טריגר שמפעיל את אנימציית הרמה וזריקה יחד
                animator.SetTrigger("Lift");

                liftProgress = 0;
                liftStateSeen = false;
                rockGroundPosition = currentRock.transform.position;
                currentRock.SetCarried(true);
                state = "lift";
            }
        }

        //מרים את האבן וזורק אותה
        else if (state == "lift")
        {
            UpdateLiftProgress();
            MoveRockWithHand();

            if (liftProgress >= releasePoint)
            {
                ThrowTheRock();
            }
        }

        //האבן עפה למעלה
        else if (state == "rockUp")
        {
            SpinRock();

            currentRock.transform.position = Vector2.MoveTowards(
                currentRock.transform.position, throwUpPoint, rockSpeed * Time.deltaTime);

            if (Vector2.Distance(currentRock.transform.position, throwUpPoint) <= 0.05f)
            {
                state = "rockDown";
            }
        }

        //האבן נוחתת ב-Slot (או חוזרת למקומה)
        else if (state == "rockDown")
        {
            SpinRock();

            currentRock.transform.position = Vector2.MoveTowards(
                currentRock.transform.position, rockTarget, rockSpeed * Time.deltaTime);

            if (Vector2.Distance(currentRock.transform.position, rockTarget) <= 0.05f)
            {
                currentRock.transform.position = rockTarget;

                // יישור את האבן בחזרה אחרי הסיבוב
                currentRock.transform.rotation = Quaternion.identity;
                currentRock.SetCarried(false);

                // רק בתשובה נכונה המצלמה נוסעת להראות את האגם, בתשובה שגויה האבן חוזרת
                // למקומה והמצלמה נשארת על מסך אבני התשובה
                if (gameCamera != null && answerIsCorrect == true)
                {
                    gameCamera.ShowLakeThenReturn();
                }

                // תשובה שגויה, האבן חוזרת למקומה
                if (answerIsCorrect == false)
                {
                    ReportTurnFinished();
                }

                waitTimer = reactionTime;
                state = "reaction";
            }
        }

        //הצגת חיווי של הצלחה או כישלון
        else if (state == "reaction")
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0)
            {
                state = "walkHome";
            }
        }

        //הגמד חוזר לעמדת ההתחלה
        else if (state == "walkHome")
        {
            WalkTowards(homePosition);

            if (Vector2.Distance(transform.position, homePosition) <= 0.1f)
            {
                transform.position = homePosition;
                StopWalkAnimation();

                state = "wait";
                currentRock = null;
                ReportTurnFinished();
            }
        }

        // השחקן שלח את הגמד לטייל על הדשא
        else if (state == "freeWalk")
        {
            WalkTowards(freeWalkTarget);

            if (Vector2.Distance(transform.position, freeWalkTarget) <= 0.1f)
            {
                transform.position = freeWalkTarget;
                StopWalkAnimation();
                state = "wait";
            }
        }

        // הולך עד שפת האגם לפני שהוא מתחיל לדלג.
        // כאן עדיין פועלת אנימציית ההליכה הרגילה
        else if (state == "skipWalk")
        {
            // הגנה: אם המסלול אופס באמצע, לא נתקעים
            if (skipPath == null || skipPath.Count == 0)
            {
                StopWalkAnimation();
                state = "wait";
                return;
            }

            Vector2 firstStone = skipPath[0];

            WalkTowards(firstStone);

            if (Vector2.Distance(transform.position, firstStone) <= skipStartDistance)
            {
                BeginSkipping();
            }
        }

        // הגמד מדלג על אבני האגם בסיום שאלה מוצלחת
        else if (state == "skip")
        {
            UpdateSkipping();
        }

        // עומד רגע על האבן האחרונה לפני שממשיכים
        else if (state == "skipEnd")
        {
            skipEndTimer -= Time.deltaTime;

            if (skipEndTimer <= 0)
            {
                StopSkipAnimation();
                state = "wait";

                System.Action callback = skipFinished;
                skipFinished = null;

                if (callback != null) callback();
            }
        }
    }

    // הפונקציה שה-GameManager מפעיל כשהשחקן לוחץ על אבן
    public void GoGetRock(RockScript rock, bool isCorrect, Vector2 target)
    {
        currentRock = rock;
        answerIsCorrect = isCorrect;
        rockTarget = target;
        turnReported = false;

        state = "walkToRock";
    }

    // לחיצה על הדשא שולחת את הגמד לטייל. עובד רק כשהגמד פנוי,
    // ככה זה לא מתנגש עם מנגנון התשובות
    public void WalkFreely(Vector2 target)
    {
        if (allowFreeWalk == false) return;
        if (IsIdle() == false) return;

        freeWalkTarget = target;
        state = "freeWalk";
    }

    // הגמד מדלג על כל אבני התשובה באגם, מהתגית הימנית לתגית השמאלית
    public void SkipOverRocks(List<Vector2> path, System.Action onDone)
    {
        skipFinished = onDone;

        if (path == null || path.Count == 0)
        {
            state = "wait";

            if (onDone != null)
            {
                skipFinished = null;
                onDone();
            }
            return;
        }

        skipPath = path;

        // קודם הולכים עד האגם באנימציית ההליכה, ורק כשמגיעים
        // לאבן הראשונה עוברים לאנימציית הדילוג
        state = "skipWalk";
    }

    // המעבר מהליכה לדילוג, ברגע שהגמד הגיע לאבן הראשונה
    private void BeginSkipping()
    {
        skipIndex = 0;
        skipFrom = transform.position;

        StopWalkAnimation();
        StartSkipAnimation();

        jumpStateHash = Animator.StringToHash(jumpStateName);

        // **לא** נותנים ל-Animator להריץ את הקליפ בעצמו.
        // speed = 0 מקפיא אותו, ואנחנו מזיזים אותו ידנית בכל פריים
        // לפי התקדמות הקפיצה. ככה הציור והתנועה לא יכולים להיפרד,
        // בלי תלות במהירויות של המצב או של המעברים
        if (animator != null) animator.speed = 0;

        StartHop();

        state = "skip";

        Debug.Log("Skip started: stones=" + skipPath.Count +
                  "  hopTime=" + HopTime() + "s  clip=" + ClipLength() + "s");
    }

    // אורך הקליפ, לפי שלושת השלבים שהוגדרו
    private float ClipLength()
    {
        float total = clipTakeOffTime + clipAirTime + clipLandTime;

        if (total <= 0) return 1;

        return total;
    }

    // כמה זמן לוקחת קפיצה אחת. הגנה מערך לא הגיוני באינספקטור,
    // שהיה מקפיא את הגמד או מדלג על כל הקליפ
    private float HopTime()
    {
        if (jumpCycleTime < 0.1f) return 0.7f;

        return jumpCycleTime;
    }

    // מתחילים קפיצה חדשה
    private void StartHop()
    {
        hopTimer = 0;
        ShowJumpFrame(0);
    }

    // מציב את הקליפ באחוז ההתקדמות של הקפיצה.
    // 0 = תחילת ההתכופפות, 1 = סוף הנחיתה
    private void ShowJumpFrame(float progress)
    {
        if (animator == null) return;
        if (jumpStateHash == 0) return;

        if (progress < 0) progress = 0;
        if (progress > 1) progress = 1;

        animator.Play(jumpStateHash, 0, progress);
    }

    // המקום שעליו הגמד באמת עומד: נקודת האבן, מורמת כך
    // שהרגליים ינחתו על ראש הסלע
    private Vector2 StonePoint(Vector2 point)
    {
        return new Vector2(point.x, point.y + skipStoneOffsetY);
    }

    private void UpdateSkipping()
    {
        Vector2 target = StonePoint(skipPath[skipIndex]);

        hopTimer = hopTimer + Time.deltaTime;

        float cycle = HopTime();

        // הקליפ נגרר יד ביד עם הקפיצה - אותו אחוז התקדמות בשניהם
        ShowJumpFrame(hopTimer / cycle);

        // השלבים בזמן המשחק, ביחס המקורי של הקליפ
        float scale = cycle / ClipLength();
        float takeOff = clipTakeOffTime * scale;
        float air = clipAirTime * scale;

        // ---- שלב 1: מתכופף על האבן, עוד לא זז ----
        if (hopTimer < takeOff)
        {
            transform.position = new Vector3(skipFrom.x, skipFrom.y, transform.position.z);
        }

        // ---- שלב 2: באוויר, בקשת, עד האבן הבאה ----
        else if (hopTimer < takeOff + air)
        {
            float t = (hopTimer - takeOff) / air;

            Vector2 flat = Vector2.Lerp(skipFrom, target, t);
            float hop = Mathf.Sin(t * Mathf.PI) * skipHeight;

            transform.position = new Vector3(flat.x, flat.y + hop, transform.position.z);
        }

        // ---- שלב 3: נחת. עומד על האבן ומתייצב ----
        else
        {
            transform.position = new Vector3(target.x, target.y, transform.position.z);
        }

        LookWhileSkipping(target);

        // הקפיצה הסתיימה - ממשיכים לאבן הבאה
        if (hopTimer >= cycle)
        {
            transform.position = new Vector3(target.x, target.y, transform.position.z);

            skipIndex = skipIndex + 1;

            // הגענו לאבן האחרונה
            if (skipIndex >= skipPath.Count)
            {
                skipEndTimer = skipEndPause;
                state = "skipEnd";
                return;
            }

            skipFrom = target;
            StartHop();
        }
    }

    private void StartSkipAnimation()
    {
        if (animator == null) return;
        if (skipBoolName == "") return;

        animator.SetBool(skipBoolName, true);
    }

    private void StopSkipAnimation()
    {
        if (animator == null) return;

        // מחזירים את המהירות הרגילה, אחרת גם ההליכה תרוץ מהר
        animator.speed = 1;

        if (skipBoolName == "") return;

        animator.SetBool(skipBoolName, false);
    }

    // מודיע ל-GameManager שהתור נגמר
    private void ReportTurnFinished()
    {
        if (turnReported == true) return;
        if (gameManager == null) return;

        turnReported = true;
        gameManager.TurnFinished();
    }

    // הפעלת האנימציה
    private void UpdateLiftProgress()
    {
        if (syncWithAnimation == true && animator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

            if (info.shortNameHash == liftStateHash)
            {
                liftProgress = Mathf.Clamp01(info.normalizedTime);
                liftStateSeen = true;
                return;
            }

            //סיום האנימציה
            if (liftStateSeen == true)
            {
                liftProgress = 1;
                return;
            }
        }

        float length = fallbackLiftLength;
        if (length <= 0) length = 0.6f;

        liftProgress = Mathf.Clamp01(liftProgress + Time.deltaTime / length);
    }

    // הזזת האבן יחד עם תנועת ההרמה
    private void MoveRockWithHand()
    {
        if (currentRock == null) return;
        if (holdPoint == null) return;

        if (liftProgress < grabPoint) return;

        float liftLength = releasePoint - grabPoint;
        if (liftLength <= 0) liftLength = 0.01f;

        //סטטוס כמה מההרמה בוצעה
        float t = (liftProgress - grabPoint) / liftLength;
        if (t > 1) t = 1;

        float shaped = liftCurve.Evaluate(t);

        currentRock.transform.position = Vector2.Lerp(
            rockGroundPosition, holdPoint.position, shaped);
    }

    // זריקת האבן
    private void ThrowTheRock()
    {
        // בתשובה נכונה- האבן עפה ל-Slot באגם.
        // בתשובה שגויה- האבן חוזרת למקום שלה
        if (answerIsCorrect == false)
        {
            animator.SetTrigger("Sad");
            currentRock.StartBlink();
        }
        else
        {
            animator.SetTrigger("Happy");
        }

        gameManager.ShowAnswerFeedback(currentRock, answerIsCorrect);

        // מחשבים את הנקודה הגבוהה של הזריקה
        Vector2 rockNow = currentRock.transform.position;
        float middleX = (rockNow.x + rockTarget.x) / 2;
        float topY = Mathf.Max(rockNow.y, rockTarget.y) + throwHeight;
        throwUpPoint = new Vector2(middleX, topY);

        // המצלמה עוקבת אחרי האבן רק כשהיא עפה לאגם בתשובה נכונה
        // בתשובה שגויה האבן רק חוזרת למקומה
        if (gameCamera != null && answerIsCorrect == true)
        {
            gameCamera.Follow(currentRock.transform);
        }

        state = "rockUp";
    }

    // מסובב את האבן תוך כדי הזריקה
    private void SpinRock()
    {
        if (spinSpeed == 0) return;

        currentRock.transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
    }

    // מחזיר את הגמד להתחלה בתחילת שאלה חדשה
    public void ResetDwarf()
    {
        transform.position = homePosition;
        state = "wait";
        currentRock = null;
        turnReported = true;

        skipPath = null;
        skipFinished = null;

        StopSkipAnimation();
        StopWalkAnimation();

        // אחרי הדילוג הגמד נשאר מסתכל שמאלה. בלי איפוס הוא היה
        // חוזר לשאלה הבאה כשהספרייט הפוך
        if (spriteRenderer != null) spriteRenderer.flipX = false;

        if (gameCamera != null) gameCamera.JumpHome();
    }

    // מזיז את הגמד לכיוון היעד ומדליק את אנימציית ההליכה המתאימה
    private void WalkTowards(Vector2 target)
    {
        transform.position = Vector2.MoveTowards(
            transform.position, target, walkSpeed * Time.deltaTime);

        Vector2 direction = target - (Vector2)transform.position;

        // בוחרים אנימציה לפי הכיוון הליכה
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            SetWalkAnimation(true, false, false);
            LookAt(target);
        }
        else if (direction.y < 0)
        {
            SetWalkAnimation(false, true, false);

            // מבטלים ההיפוך שנשאר מההליכה לצדדים
            spriteRenderer.flipX = false;
        }
        else
        {
            // הליכה למעלה
            SetWalkAnimation(false, false, true);
            spriteRenderer.flipX = false;
        }
    }

    private void SetWalkAnimation(bool side, bool forward, bool back)
    {
        if (animator == null) return;

        animator.SetBool("IsWalkingSide", side);
        animator.SetBool("IsWalkingForward", forward);
        animator.SetBool("IsWalkingBack", back);
    }

    private void StopWalkAnimation()
    {
        SetWalkAnimation(false, false, false);
    }

    // כיוון הגמד בזמן הדילוג על אבני האגם.
    // מופרד מ-LookAt כי גיליון הדילוג הוא ספרייט אחר מגיליון ההליכה,
    // ולכן הוא עשוי להיות מצויר לכיוון ההפוך
    private void LookWhileSkipping(Vector2 target)
    {
        if (spriteRenderer == null) return;

        // המסלול כולו נע ימינה-שמאלה, אז אין טעם להתהפך בין אבן לאבן
        if (skipAlwaysFacesLeft == true)
        {
            spriteRenderer.flipX = skipSpriteLooksRight;
            return;
        }

        if (target.x > transform.position.x)
        {
            spriteRenderer.flipX = !skipSpriteLooksRight;
        }
        else
        {
            spriteRenderer.flipX = skipSpriteLooksRight;
        }
    }

    // הפיכת הספרייט כך שהגמד יסתכל לכיוון אבן התשובה
    private void LookAt(Vector2 target)
    {
        if (spriteRenderer == null) return;

        if (target.x > transform.position.x)
        {
            // פנייה ימינה של הגמד
            spriteRenderer.flipX = !sideSpriteLooksRight;
        }
        else
        {
            // פנייה שמאלה של הגמד
            spriteRenderer.flipX = sideSpriteLooksRight;
        }
    }
}
