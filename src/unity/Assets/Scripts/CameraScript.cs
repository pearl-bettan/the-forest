using UnityEngine;

// כל התנהגות המצלמה במקום אחד:
// 1. מעבר בין מסך אבני התשובה לתצוגת האגם, ומעקב אחרי האבן ואחרי הגמד
// 2. התאמת גודל התצוגה לכל יחס מסך. הבמה עוצבה ל-1280x720
// 3. החלפת רקע ברירת המחדל הכחול של יוניטי בצבע של המשחק
[RequireComponent(typeof(Camera))]
public class CameraScript : MonoBehaviour
{
    [Header("View Points")]
    //placeHolder למצלמה שממנה רואים את כל האגם
    [SerializeField] Transform lakeViewPoint;

    [Header("Speed")]
    // מהירות המצלמה שעוקבת אחרי האבן
    [SerializeField] float followSpeed = 4;

    // מהירות המעבר בין המסכים
    [SerializeField] float moveSpeed = 3;

    [Header("Times")]
    // כמה שניות נשארים על תצוגת האגם אחרי שהאבן נחתה
    [SerializeField] float stayAtLakeTime = 1;

    [Header("Design Size")]
    // הגודל שאליו עוצב המשחק
    [SerializeField] int referenceWidth = 1280;
    [SerializeField] int referenceHeight = 720;

    // ה-Orthographic Size שבו המשחק נראה נכון ביחס המסך המקורי
    [SerializeField] float referenceOrthographicSize = 5f;

    [Header("Background")]
    // מחליף את הרקע הכחול של יוניטי
    [SerializeField] bool overrideBackground = true;

    // צבע הרקע שמאחורי המשחק, בגוון היער
    [SerializeField] Color backgroundColor = new Color(0.129f, 0.243f, 0.145f, 1f);

    [Header("Limits")]
    // המצלמה לא יוצאת מהמלבן שבין עמדת הבית לתצוגת האגם.
    // בלי זה המעקב אחרי הגמד הקופץ מוציא אותה מחוץ לעולם המשחק
    // ואז רואים את רקע היוניטי מאחור
    [SerializeField] bool keepInsideLevel = true;

    // כמה מותר לחרוג מעבר לשתי נקודות התצוגה
    [SerializeField] float boundsMargin = 0.5f;

    // המצב הנוכחי: idle / follow / move / wait
    private string state = "idle";
    private Vector3 homePosition;
    private Vector3 movePoint;
    private Transform target;

    // דגל עצירה לתנועת המצלמה
    private bool waitWhenArrived;

    // ספירה לאחור לפני החזרה
    private float waitTimer;

    // כמה זמן המצלמה נשארת על האגם
    private float stayTimeToUse;

    // במעקב אחרי הגמד הקופץ עוקבים רק על ציר ה-X.
    // הקפיצות למעלה לא אמורות להזיז את המצלמה
    private bool followXOnly;

    // רכיב המצלמה עצמו, להתאמת גודל התצוגה והרקע
    private Camera myCamera;

    // היחס האחרון שחושב, כדי לא לחשב מחדש בכל פריים
    private float lastAspect = -1f;

    // המיקום שבו המצלמה הונחה בסצנה הוא "הבית" שאליו היא תמיד
    // חוזרת. נקבע פעם אחת ב-Awake, לפני ש-Start של סקריפטים
    // אחרים מתחיל להזיז אותה
    void Awake()
    {
        homePosition = transform.position;
        movePoint = homePosition;
        state = "idle";
        stayTimeToUse = stayAtLakeTime;

        myCamera = GetComponent<Camera>();
        ApplyBackground();
        FitToScreen();
    }

    // נקרא גם בהדלקה מחדש של האובייקט, ולא רק בטעינה. חוזר על
    // התאמת הרקע והגודל, כי חזרה מהשהיה עלולה לאפס אותם
    void OnEnable()
    {
        if (myCamera == null) myCamera = GetComponent<Camera>();

        ApplyBackground();
        FitToScreen();
    }

    // ============================================================
    // מכונת המצבים של המצלמה, רצה בכל פריים.
    //
    // ארבעה מצבים: idle - עומדת, move - נעה ליעד, follow - עוקבת
    // אחרי אובייקט, wait - ממתינה במקום לפני חזרה הביתה.
    //
    // כאן גם נבדק בכל פריים אם יחס המסך השתנה. המשחק רץ בתוך
    // iframe במחולל, שגודלו משתנה עם חלון הדפדפן, ולכן אי אפשר
    // להסתפק בחישוב חד-פעמי בטעינה
    // ============================================================
    void Update()
    {
        // המסך שינה גודל - מתאימים את שדה הראייה מחדש
        if (myCamera != null && Mathf.Abs(myCamera.aspect - lastAspect) > 0.0001f)
        {
            FitToScreen();
        }

        // המצלמה עוקבת אחרי האבן
        if (state == "follow")
        {
            if (target == null)
            {
                GoHome();
                return;
            }
            
            float wantedY = followXOnly ? transform.position.y : target.position.y;

            Vector3 wanted = new Vector3(
                target.position.x, wantedY, transform.position.z);

            // תנועת המצלמה
            transform.position = Vector3.Lerp(transform.position, wanted, followSpeed * Time.deltaTime);
        }

        else if (state == "move")
        {
            transform.position = Vector3.Lerp(
                transform.position, movePoint, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, movePoint) <= 0.05f)
            {
                transform.position = movePoint;

                if (waitWhenArrived == true)
                {
                    // הגענו לתצוגת האגם - עוצרים כדי שיראו את הסידור
                    waitWhenArrived = false;
                    waitTimer = stayTimeToUse;
                    state = "wait";
                }
                else
                {
                    state = "idle";
                }
            }
        }

        // המצלמה מחכה על תצוגת האגם
        else if (state == "wait")
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0)
            {
                GoHome();
            }
        }
    }

    // הגבלת המצלמה לתחומי השלב נעשית ב-LateUpdate ולא ב-Update,
    // כדי שהיא תרוץ אחרי שכל התנועות של הפריים כבר בוצעו.
    // אחרת המצלמה הייתה יכולה לחרוג לרגע אחד מהגבול
    void LateUpdate()
    {
        ClampInsideLevel();
    }


    // מחליף את הרקע הכחול של יוניטי בצבע של המשחק
    private void ApplyBackground()
    {
        if (overrideBackground == false) return;
        if (myCamera == null) return;

        myCamera.clearFlags = CameraClearFlags.SolidColor;

        // אלפא מלא, אחרת נראה שחור או כחול מאחורי המשחק
        Color solid = backgroundColor;
        solid.a = 1f;

        myCamera.backgroundColor = solid;
    }


    // מגדיל את שדה הראייה במסך צר, ככה שכל מה שעוצב נשאר בפנים
    private void FitToScreen()
    {
        if (myCamera == null) return;
        if (myCamera.orthographic == false) return;
        if (referenceHeight <= 0 || referenceWidth <= 0) return;

        float referenceAspect = (float)referenceWidth / referenceHeight;
        float currentAspect = myCamera.aspect;

        if (currentAspect <= 0) return;

        lastAspect = currentAspect;

        if (currentAspect < referenceAspect)
        {
            // המסך צר יותר מהתכנון - מרחיבים כדי לא לחתוך את הצדדים
            myCamera.orthographicSize = referenceOrthographicSize * (referenceAspect / currentAspect);
        }
        else
        {
            // המסך רחב יותר - הגובה נשאר כמו שתוכנן
            myCamera.orthographicSize = referenceOrthographicSize;
        }
    }

    // מחזיר את המצלמה אל תוך המלבן שבין עמדת הבית לתצוגת האגם
    private void ClampInsideLevel()
    {
        if (keepInsideLevel == false) return;
        if (lakeViewPoint == null) return;

        Vector3 lake = lakeViewPoint.position;

        float minX = Mathf.Min(homePosition.x, lake.x) - boundsMargin;
        float maxX = Mathf.Max(homePosition.x, lake.x) + boundsMargin;
        float minY = Mathf.Min(homePosition.y, lake.y) - boundsMargin;
        float maxY = Mathf.Max(homePosition.y, lake.y) + boundsMargin;

        Vector3 p = transform.position;

        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.y = Mathf.Clamp(p.y, minY, maxY);

        transform.position = p;
    }


    // מעקב אחרי האבן, מופעל אחרי זריקת אבן

    public void Follow(Transform newTarget)
    {
        target = newTarget;
        followXOnly = false;
        state = "follow";
    }


    // מעקב אופקי בלבד. משמש בדילוג של הגמד על האבנים,
    // כדי שהקפיצות למעלה לא יטלטלו את המצלמה
    public void FollowSideways(Transform newTarget)
    {
        target = newTarget;
        followXOnly = true;
        state = "follow";
    }


    // המצלמה מחכה כמה שניות במסך התשובות לאחר הנחת אבן תשובה נכונה
    public void ShowLakeThenReturn()
    {
        ShowLakeThenReturn(stayAtLakeTime);
    }
    // גרסה שמקבלת זמן שהייה מפורש, לשימוש כשהשהייה צריכה
    // להתאים לאורך אנימציה מסוימת.
    // אם נקודת התצוגה לא חוברה באינספקטור, המצלמה נשארת במקומה
    // וממתינה - עדיף מאשר לקפוץ לנקודה שגויה
    public void ShowLakeThenReturn(float stayTime)
    {
        target = null;
        stayTimeToUse = stayTime;

        if (lakeViewPoint == null)
        {
            Debug.LogWarning("Lake View Point is empty in Camera Script. " +
                             "Create an empty object where the whole lake is visible and drag it in");

            waitTimer = stayTimeToUse;
            state = "wait";
            return;
        }

        MoveTo(LakePoint(), true);
    }

    //תזוזת מצלמה עם לחיצה על החיצים
    // החץ השמאלי- תזוזה לאגם
    public void LookAtLake()
    {
        if (lakeViewPoint == null)
        {
            Debug.LogWarning("Lake View Point is empty in Camera Script");
            return;
        }

        MoveTo(LakePoint(), false);
    }

    //החץ הימני- חוזר למסך אבני התשובה
    public void LookAtPlayer()
    {
        GoHome();
    }

    // נשארים על תצוגת האגם בלי לחזור אוטומטית.
    // משמש כשהגמד מדלג על האבנים בסיום שאלה מוצלחת
    public void WatchLake()
    {
        target = null;
        waitWhenArrived = false;

        if (lakeViewPoint == null) return;

        movePoint = LakePoint();
        state = "move";
    }

    // חזרה חלקה לנקודת הבית
    public void GoHome()
    {
        MoveTo(homePosition, false);
    }

    // קפיצה מיידית הביתה בלי אנימציה, ואיפוס כל מצב המעקב.
    // משמש במעבר בין שלבים, ששם תנועה חלקה הייתה נראית כמו
    // תקלה במקום כמו מעבר
    public void JumpHome()
    {
        target = null;
        followXOnly = false;
        waitWhenArrived = false;
        transform.position = homePosition;
        movePoint = homePosition;
        state = "idle";
    }
    
    // מיקום תצוגת האגם
    private Vector3 LakePoint()
    {
        return new Vector3(
            lakeViewPoint.position.x, lakeViewPoint.position.y, transform.position.z);
    }

    // מעביר את המצלמה למצב תנועה אל נקודה. waitThere קובע אם
    // להמתין שם ואז לחזור הביתה, או להישאר
    private void MoveTo(Vector3 point, bool waitThere)
    {
        target = null;
        movePoint = point;
        waitWhenArrived = waitThere;
        state = "move";
    }
}
