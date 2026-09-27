using UnityEngine;

// כל התנהגות המצלמה בתסריט
// מעבר בין מסך אבני התשובה לתצוגת האגם, ומעקב אחרי האבן ואחרי הגמד
[RequireComponent(typeof(Camera))]
public class CameraScript : MonoBehaviour
{
    [Header("View Points")]
    //placeHolder למצלמה שממנה רואים את כל האגם
    [SerializeField] Transform lakeViewPoint;

    [Header("Speed")]
    // זמן ההחלקה: בערך כמה שניות לוקח למצלמה להגיע ליעד.
    [SerializeField] float smoothTime = 0.45f;

    // חסם מהירות, כדי שמרחק גדול לא ייראה כמו קפיצה
    [SerializeField] float maxSpeed = 18f;

    // כמה מהר ההתרחקות של הדילוג נכנסת ויוצאת
    [SerializeField] float zoomSmoothTime = 0.5f;

    // מרחק שנחשב הגעה ליעד
    [SerializeField] float arriveDistance = 0.05f;

    [Header("Times")]
    // כמה שניות נשארים על תצוגת האגם אחרי שהאבן נחתה
    [SerializeField] float stayAtLakeTime = 1;

    [Header("Design Size")]
    // הגודל שאליו עוצב המשחק
    [SerializeField] int referenceWidth = 1280;
    [SerializeField] int referenceHeight = 720;

    [SerializeField] float referenceOrthographicSize = 5f;

    [Header("Screen Fit")]
    // גודל התצוגה המרבי, לפי תמונת הרקע של הסצנה
    [SerializeField] float maxOrthographicSize = 6.6f;

    [SerializeField] float hardMaxOrthographicSize = 7.3f;

    [Header("Background")]
    // מחליף את הרקע הכחול של יוניטי
    [SerializeField] bool overrideBackground = true;

    // צבע הרקע שמאחורי המשחק
    [SerializeField] Color backgroundColor = new Color(0.129f, 0.243f, 0.145f, 1f);

    [Header("Limits")]
    // המצלמה לא יוצאת מהמלבן שבין עמדת הבית לתצוגת האגם
    [SerializeField] bool keepInsideLevel = true;

    [SerializeField] float boundsMargin = 0.5f;

    // התרחקות המצלמה לזמן הדילוג
    private float skipZoom;

    // גודל התצוגה הבסיסי, לפני ההתרחקות של הדילוג
    private float baseSize = -1f;

    // המהירות הנוכחית של המצלמה
    private Vector3 moveVelocity;
    private float sizeVelocity;

    // המצב הנוכחי של הגמד: idle / follow / move / wait
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

    private bool followXOnly;

    // רכיב המצלמה עצמו, להתאמת גודל התצוגה והרקע
    private Camera myCamera;

    private float lastAspect = -1f;
    // המיקום שבו המצלמה הונחה בסצנה הוא ״הבית״ שאליו היא תמיד תחזור אליו
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

    void OnEnable()
    {
        if (myCamera == null) myCamera = GetComponent<Camera>();

        ApplyBackground();
        FitToScreen();
    }

    void Update()
    {
        // המסך שינה גודל- מתאימים את שדה הראייה מחדש
        if (myCamera != null && Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;

            if (Mathf.Abs(screenAspect - lastAspect) > 0.0001f) FitToScreen();
        }


        //  המהירות נשמרת בין המצבים
        Vector3 wanted = transform.position;
        bool moving = false;

        if (state == "follow")
        {
            if (target == null)
            {
                GoHome();
                return;
            }

            float wantedY = followXOnly ? transform.position.y : target.position.y;

            wanted = new Vector3(target.position.x, wantedY, transform.position.z);
            moving = true;
        }
        else if (state == "move")
        {
            wanted = movePoint;
            moving = true;
        }
        else if (state == "wait")
        {
            // המצלמה מחכה על תצוגת האגם
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0) GoHome();
        }

        if (moving == true)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, wanted, ref moveVelocity, smoothTime,
                maxSpeed, Time.deltaTime);

            if (state == "move" &&
                Vector3.Distance(transform.position, movePoint) <= arriveDistance)
            {
                if (waitWhenArrived == true)
                {
                    // הגענו לתצוגת האגם- עוצרים את המצלמה כדי שיראו את הסידור
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
        else
        {
            // דעיכה הדרגתית של המהירות, כדי שעצירה לא תהיה חדה
            moveVelocity = Vector3.Lerp(moveVelocity, Vector3.zero, Time.deltaTime * 5f);
        }

        ApplyZoom();
    }

    // גודל התצוגה הוא הגודל הבסיסי של המצלמה בתוספת ההתרחקות של הדילוג
    private void ApplyZoom()
    {
        if (myCamera == null) return;
        if (myCamera.orthographic == false) return;
        if (baseSize <= 0) return;

        float goal = baseSize + skipZoom;

        if (hardMaxOrthographicSize > 0f && goal > hardMaxOrthographicSize)
        {
            goal = hardMaxOrthographicSize;
        }

        if (Mathf.Abs(myCamera.orthographicSize - goal) < 0.001f)
        {
            myCamera.orthographicSize = goal;
            return;
        }

        myCamera.orthographicSize = Mathf.SmoothDamp(
            myCamera.orthographicSize, goal, ref sizeVelocity, zoomSmoothTime);
    }

    //הגבלת תחומי המצלמה, אחרת המצלמה הייתה יכולה לחרוג מהגבול
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

        // אחרת נראה שחור או כחול מאחורי המשחק
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
        float currentAspect = Screen.height > 0
            ? (float)Screen.width / Screen.height
            : myCamera.aspect;

        if (currentAspect <= 0) return;

        lastAspect = currentAspect;

        // אזור המצלמה הראשי הוא תמיד המסך המלא
        myCamera.rect = new Rect(0f, 0f, 1f, 1f);

        if (currentAspect >= referenceAspect)
        {
            SetBaseSize(CapToBackground(referenceOrthographicSize));
            return;
        }

        float wanted = referenceOrthographicSize * (referenceAspect / currentAspect);

        SetBaseSize(CapToBackground(wanted));
    }

    private float CapToBackground(float size)
    {
        if (maxOrthographicSize <= 0f) return size;

        return size < maxOrthographicSize ? size : maxOrthographicSize;
    }

    // קובע את גודל התצוגה, הזום לא ייראה איטי כשיש התרחקות של המצלמה, הז רק יחליק את מעבר המצלמה המצלמה בויזואל
    private void SetBaseSize(float size)
    {
        baseSize = size;

        if (myCamera == null) return;

        if (Mathf.Approximately(skipZoom, 0f) == true)
        {
            myCamera.orthographicSize = size;
            sizeVelocity = 0f;
        }
    }
    // מחזיר את המצלמה אל תוך המלבן שבין עמוד הבית לתצוגת האגם
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
        skipZoom = 0f;

        target = newTarget;
        followXOnly = false;
        state = "follow";
    }


    // מעקב אופקי בלבד. משמש בדילוג של הגמד על האבנים,
    // כדי שהקפיצות למעלה לא יזיזו את המצלמה בצורה חזקה מדי שלא נעימה לעין
    public void FollowSideways(Transform newTarget)
    {
        skipZoom = 0f;

        target = newTarget;
        followXOnly = true;
        state = "follow";
    }

    // הנקודה הגבוהה ביותר מסומנת כך שלא תהיה תזוזת מצלמה מעליה
    public void FollowSidewaysShowing(Transform newTarget, float topWorldY,
                                      float margin, float maxZoomOut)
    {
        target = newTarget;
        followXOnly = true;
        state = "follow";

        if (myCamera == null) myCamera = GetComponent<Camera>();

        if (myCamera == null || baseSize <= 0)
        {
            skipZoom = 0f;
            return;
        }

        float needed = topWorldY + margin - transform.position.y;

        float extra = needed - baseSize;

        if (extra < 0f) extra = 0f;
        if (extra > maxZoomOut) extra = maxZoomOut;

        skipZoom = extra;
    }

    // מחזיר את גודל התצוגה לגודל הרגיל. נקרא בסיום הדילוג
    public void ClearSkipZoom()
    {
        skipZoom = 0f;
    }


    // המצלמה מחכה כמה שניות במסך התשובות לאחר הנחת אבן תשובה נכונה
    public void ShowLakeThenReturn()
    {
        ShowLakeThenReturn(stayAtLakeTime);
    }
    // יש זמן שהייה מפורש שמתאים לאורך אנימציות
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

    //תזוזת מצלמה עם לחיצה על אייקוני החיצים
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

    // נשארים על תצוגת האגם בלי לחזור אוטומטית
    // משמש כשהגמד מדלג על האבנים בסיום שאלה מוצלחת
    public void WatchLake()
    {
        skipZoom = 0f;

        target = null;
        waitWhenArrived = false;

        if (lakeViewPoint == null) return;

        movePoint = LakePoint();
        state = "move";
    }

    // חזרה חלקה לאיזור המצלמה הקבוע 
    public void GoHome()
    {
        MoveTo(homePosition, false);
    }

    //מעבר חלק של המצלמה לאיזור המרכזי
    public void JumpHome()
    {
        skipZoom = 0f;

        // קפיצה מיידית חייבת לאפס גם את המהירות, אחרת המצלמה ממשיכה לזוז מהמקום החדש לפי המהירות הקודמת
        moveVelocity = Vector3.zero;
        sizeVelocity = 0f;

        if (myCamera != null && baseSize > 0) myCamera.orthographicSize = baseSize;

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

    private void MoveTo(Vector3 point, bool waitThere)
    {
        skipZoom = 0f;

        target = null;
        movePoint = point;
        waitWhenArrived = waitThere;
        state = "move";
    }
}
