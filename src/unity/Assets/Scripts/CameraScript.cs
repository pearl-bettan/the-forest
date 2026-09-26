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
    // ============================================================
    // זמן ההחלקה: בערך כמה שניות לוקח למצלמה להגיע ליעד.
    //
    // התנועה מבוססת SmoothDamp ולא Lerp, וההבדל מהותי: SmoothDamp
    // שומר מהירות בין פריימים ובין מצבים, ולכן כשהיעד מתחלף -
    // אחרי הנחת אבן, למשל - המצלמה מסיטה את התנועה בהדרגה במקום
    // לשנות כיוון בבת אחת.
    //
    // ערך גדול יותר = תנועה איטית ורכה יותר
    // ============================================================
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

    // ה-Orthographic Size שבו המשחק נראה נכון ביחס המסך המקורי
    [SerializeField] float referenceOrthographicSize = 5f;

    [Header("Screen Fit")]
    // ============================================================
    // מסגור במקום מתיחה.
    //
    // בלי זה, מסך ביחס שונה מ-1280x720 גורם למצלמה להראות יותר
    // עולם - ומעבר לגבול האיור מתגלה צבע הרקע של המצלמה. במסך
    // מלא זה בולט במיוחד, ונראה כמו שוליים ירוקים סביב המשחק.
    //
    // כשהאפשרות דלוקה, המשחק נשאר תמיד ביחס שאליו הוא עוצב,
    // והשטח העודף נצבע בשחור - כמו סרט בטלוויזיה
    // ============================================================
    [SerializeField] bool letterbox = true;

    // צבע הפסים שמסביב
    [SerializeField] Color letterboxColor = Color.black;

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

    // ============================================================
    // התרחקות לזמן הדילוג.
    //
    // בזמן הדילוג המצלמה עוקבת אחרי הגמד רק אופקית, וכשהאבנים
    // או תגית הסיום יושבות גבוה ראש הגמד יוצא מהמסך.
    //
    // הפתרון הוא להתרחק ולא להתרומם. הרמה מזיזה את המסגרת כלפי
    // מעלה, וכל מה שהיה בתחתית - תגית ההתחלה, למשל - נחתך.
    // התרחקות מגדילה את התמונה סימטרית סביב אותו מרכז, ולכן
    // שום דבר שהיה גלוי אינו נעלם
    // ============================================================
    private float skipZoom;

    // גודל התצוגה הבסיסי, לפני ההתרחקות של הדילוג
    private float baseSize = -1f;

    // המהירות הנוכחית של המצלמה. נשמרת בין המצבים, וזה מה
    // שהופך את המעברים לרציפים במקום לקפיצות
    private Vector3 moveVelocity;
    private float sizeVelocity;

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

    // מצלמת הרקע שצובעת את הפסים
    private Camera letterboxCamera;

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
        // המסך שינה גודל - מתאימים את שדה הראייה מחדש.
        // ההשוואה היא ליחס המסך ולא ליחס המצלמה, כי המסגור משנה
        // את יחס המצלמה בעצמו והבדיקה הייתה מפסיקה להגיב
        if (myCamera != null && Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;

            if (Mathf.Abs(screenAspect - lastAspect) > 0.0001f) FitToScreen();
        }

        // ============================================================
        // כל המצבים מחשבים יעד אחד, וכל התנועה מתבצעת במקום אחד.
        //
        // זה מה שמאפשר רציפות: המהירות נשמרת בין המצבים, ולכן
        // מעבר ממעקב אחרי אבן אל תצוגת האגם, ומשם חזרה הביתה,
        // הוא תנועה אחת מתמשכת ולא שלוש תנועות נפרדות
        // ============================================================
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

            // ההגעה ליעד נבדקת בלי הצמדה למקום המדויק. הצמדה
            // הייתה עוצרת את המצלמה בבת אחת, וזו בדיוק הקפיצה
            // שהתנועה החלקה אמורה למנוע
            if (state == "move" &&
                Vector3.Distance(transform.position, movePoint) <= arriveDistance)
            {
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
        else
        {
            // דעיכה הדרגתית של המהירות, כדי שעצירה לא תהיה חדה
            moveVelocity = Vector3.Lerp(moveVelocity, Vector3.zero, Time.deltaTime * 5f);
        }

        ApplyZoom();
    }

    // ============================================================
    // מחיל את גודל התצוגה: הגודל הבסיסי ועוד ההתרחקות של הדילוג.
    //
    // גם כאן ההחלקה חשובה: קפיצת גודל פתאומית נראית כמו תקלה,
    // והתרחקות הדרגתית נקראת כמו מהלך מכוון
    // ============================================================
    private void ApplyZoom()
    {
        if (myCamera == null) return;
        if (myCamera.orthographic == false) return;
        if (baseSize <= 0) return;

        float goal = baseSize + skipZoom;

        if (Mathf.Abs(myCamera.orthographicSize - goal) < 0.001f)
        {
            myCamera.orthographicSize = goal;
            return;
        }

        myCamera.orthographicSize = Mathf.SmoothDamp(
            myCamera.orthographicSize, goal, ref sizeVelocity, zoomSmoothTime);
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

        // יחס המסך עצמו, ולא myCamera.aspect: ברגע שמצמצמים את
        // אזור הציור של המצלמה, aspect מחזיר את היחס של האזור
        // המצומצם, והבדיקה הייתה מתייצבת על ערך שגוי
        float currentAspect = Screen.height > 0
            ? (float)Screen.width / Screen.height
            : myCamera.aspect;

        if (currentAspect <= 0) return;

        lastAspect = currentAspect;

        if (letterbox == true)
        {
            ApplyLetterbox(referenceAspect, currentAspect);
            return;
        }

        // בלי מסגור: אזור הציור חוזר למסך המלא
        myCamera.rect = new Rect(0f, 0f, 1f, 1f);

        if (currentAspect < referenceAspect)
        {
            // המסך צר יותר מהתכנון - מרחיבים כדי לא לחתוך את הצדדים
            SetBaseSize(referenceOrthographicSize * (referenceAspect / currentAspect));
        }
        else
        {
            // המסך רחב יותר - הגובה נשאר כמו שתוכנן
            SetBaseSize(referenceOrthographicSize);
        }
    }

    // ============================================================
    // קובע את גודל התצוגה הבסיסי.
    //
    // כשאין התרחקות פעילה הגודל מוחל מיד, כדי ששינוי גודל חלון
    // לא ייראה כמו זום איטי. כשיש התרחקות, ApplyZoom הוא שידאג
    // להחליק את המעבר
    // ============================================================
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

    // ============================================================
    // מצמצם את אזור הציור של המצלמה ליחס שאליו המשחק עוצב,
    // וממרכז אותו. השטח שנשאר מחוץ לאזור הוא הפס השחור.
    //
    // מסך רחב מהתכנון מקבל פסים בצדדים, מסך גבוה מהתכנון מקבל
    // פסים למעלה ולמטה. גודל התצוגה נשאר תמיד המתוכנן, ולכן
    // שום שחקן לא רואה יותר עולם משחקן אחר
    // ============================================================
    private void ApplyLetterbox(float referenceAspect, float currentAspect)
    {
        SetBaseSize(referenceOrthographicSize);

        if (currentAspect > referenceAspect)
        {
            float width = referenceAspect / currentAspect;
            myCamera.rect = new Rect((1f - width) / 2f, 0f, width, 1f);
        }
        else
        {
            float height = currentAspect / referenceAspect;
            myCamera.rect = new Rect(0f, (1f - height) / 2f, 1f, height);
        }

        BuildLetterboxCamera();
    }

    // ============================================================
    // מצלמת רקע שמנקה את כל המסך לשחור.
    //
    // מצלמה שאזור הציור שלה מצומצם מנקה רק את האזור הזה, והשטח
    // שמסביב שומר את מה שצויר בו בפריים הקודם - מה שמייצר מריחה.
    // מצלמה נוספת שמציירת כלום ומנקה הכול פותרת את זה.
    //
    // Depth נמוך יותר מבטיח שהיא מציירת ראשונה, ו-cullingMask
    // ריק מבטיח שהיא לא מציירת שום אובייקט בעצמה
    // ============================================================
    private void BuildLetterboxCamera()
    {
        if (letterboxCamera != null)
        {
            letterboxCamera.backgroundColor = letterboxColor;
            return;
        }

        GameObject item = new GameObject("LetterboxCamera");
        item.transform.SetParent(transform, false);

        letterboxCamera = item.AddComponent<Camera>();
        letterboxCamera.clearFlags = CameraClearFlags.SolidColor;
        letterboxCamera.backgroundColor = letterboxColor;
        letterboxCamera.cullingMask = 0;
        letterboxCamera.depth = myCamera.depth - 100;
        letterboxCamera.rect = new Rect(0f, 0f, 1f, 1f);
        letterboxCamera.orthographic = true;
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
        skipZoom = 0f;

        target = newTarget;
        followXOnly = false;
        state = "follow";
    }


    // מעקב אופקי בלבד. משמש בדילוג של הגמד על האבנים,
    // כדי שהקפיצות למעלה לא יטלטלו את המצלמה
    public void FollowSideways(Transform newTarget)
    {
        skipZoom = 0f;

        target = newTarget;
        followXOnly = true;
        state = "follow";
    }

    // ============================================================
    // מעקב אופקי, עם הבטחה שנקודה מסוימת תישאר בתוך המסך.
    //
    // topWorldY היא הנקודה הגבוהה ביותר שחייבת להיראות - למשל
    // ראש הגמד בשיא הקפיצה מעל האבן הגבוהה ביותר במסלול.
    // מכאן נגזר הגובה המינימלי של המצלמה: חצי גובה התצוגה הוא
    // orthographicSize, ולכן המצלמה חייבת לשבת לפחות ב-
    // topWorldY + שוליים - orthographicSize.
    //
    // ההרמה מתבצעת מיד ולא בהדרגה, כי הדילוג מתחיל באותו רגע
    // ============================================================
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

        // ============================================================
        // חצי הגובה הדרוש כדי שהנקודה הגבוהה תיכנס למסך, מהמרכז
        // הנוכחי של המצלמה ומעלה. ההפרש מהגודל הבסיסי הוא
        // ההתרחקות הנחוצה.
        //
        // החסם קיים כדי שנקודה חריגה אחת לא תרחיק את המצלמה עד
        // שהכול נראה קטן ומנותק
        // ============================================================
        float needed = topWorldY + margin - transform.position.y;

        float extra = needed - baseSize;

        if (extra < 0f) extra = 0f;
        if (extra > maxZoomOut) extra = maxZoomOut;

        skipZoom = extra;
    }

    // מחזיר את גודל התצוגה לרגיל. נקרא בסיום הדילוג
    public void ClearSkipZoom()
    {
        skipZoom = 0f;
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
        skipZoom = 0f;

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
        skipZoom = 0f;

        // קפיצה מיידית חייבת לאפס גם את המהירות, אחרת המצלמה
        // ממשיכה לנוע מהמקום החדש לפי המהירות שנצברה
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

    // מעביר את המצלמה למצב תנועה אל נקודה. waitThere קובע אם
    // להמתין שם ואז לחזור הביתה, או להישאר
    private void MoveTo(Vector3 point, bool waitThere)
    {
        skipZoom = 0f;

        target = null;
        movePoint = point;
        waitWhenArrived = waitThere;
        state = "move";
    }
}
