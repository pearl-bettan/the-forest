using UnityEngine;
using TMPro;
public class RockScript : MonoBehaviour
{
    [Header("Objects")]
    public TMP_Text rockText;

    public SpriteRenderer rockImage;

    [Header("Image Answers")]
    // ספרייט של האבן, שמציג תמונת תשובה
    [SerializeField] SpriteRenderer answerImage;

    private Vector3 answerImageOriginalScale = Vector3.one;

    // הגודל שחושב לתמונה הנוכחית. משמש גם להגדלה בזכוכית המגדלת
    private Vector3 answerImageFitScale = Vector3.one;

    // כפתור ההגדלה של האבן
    [System.NonSerialized] public GameObject magnifierButton;

    // המרחק בין הכפתור לאבן בסצנה. שומר עליו כשהאבן עוברת לאגם
    private Vector3 magnifierOffset;
    private bool magnifierOffsetSaved;

    [Header("Blink Settings")]
    [SerializeField] Color wrongColor = Color.red;
    [SerializeField] float blinkTime = 0.15f;
    [SerializeField] int blinkTimes = 3;
    [SerializeField] float bigScale = 1.1f;

    // הרכיבים של השלב
    public GameManager gameManager;

    // פאנל ההגדלה
    public ZoomPanelScript zoomPanel;

    // המקום הנכון של האבן באגם
    public int correctPlace;

    // המקום שבו נמצאות אבני התשובה
    [System.NonSerialized] public Vector2 startPosition;

    // התמונה שעל האבן, אם יש
    [System.NonSerialized] public Sprite currentImage;

    private bool homeSaved;

    private static bool animatorWarningShown;
    private static bool magnifierWarningShown;

    // האבן כבר הונחה באגם ואין אפשות ללחוץ עליה שוב
    public bool isPlaced;

    private Color startColor;
    private Vector3 startScale;

    // מיקום האבן
    private int baseSortingOrder;
    [SerializeField] int carriedSortingBoost = 50;

    // משתנים להבהוב האדום
    private bool isBlinking;
    private float blinkTimer;
    private int blinksLeft;
    private bool isRed;

    // ============================================================
    // אתחול האבן: איתור הרכיבים, כיבוי ה-Animator, הכנת תמונת
    // התשובה ושמירת המקום והצבע ההתחלתיים.
    //
    // סדר הפעולות חשוב: שומרים את המקום והצבע רק אחרי שה-Animator
    // כובה, אחרת היו נשמרים ערכים שהאנימציה כבר שינתה
    // ============================================================
    void Awake()
    {
        if (rockText == null) rockText = GetComponentInChildren<TMP_Text>();
        if (rockImage == null) rockImage = GetComponent<SpriteRenderer>();

        if (rockText == null)
        {
            Debug.LogWarning("Rock " + name + " has no text. Add a child: 3D Object > Text - TextMeshPro");
        }

        StopPositionAnimation();
        BuildAnswerImage();
        SaveAnswerImageScale();
        SaveHome();

        if (rockImage != null)
        {
            startColor = rockImage.color;
            baseSortingOrder = rockImage.sortingOrder;
        }

        isBlinking = false;
    }
    // ============================================================
    // יוצרת בקוד את הרנדרר שיציג תמונת תשובה על האבן.
    //
    // נבנה בקוד ולא בפריפאב, כי לא כל פריט הוא תמונה - פריטי
    // טקסט לא צריכים אותו כלל. הוא נוצר מכובה ונדלק רק כשיש
    // תמונה להציג.
    // שכבת המיון שלו היא של האבן ועוד אחת, כדי שהתמונה תוצג
    // מעל הסלע ולא מאחוריו
    // ============================================================
    private void BuildAnswerImage()
    {
        if (answerImage != null) return;

        GameObject imageObject = new GameObject("AnswerImage");
        imageObject.transform.SetParent(transform, false);
        imageObject.transform.localPosition = Vector3.zero;

        answerImage = imageObject.AddComponent<SpriteRenderer>();
        if (rockImage != null)
        {
            answerImage.sortingLayerID = rockImage.sortingLayerID;
            answerImage.sortingOrder = rockImage.sortingOrder + 1;
        }

        answerImage.gameObject.SetActive(false);
    }

    // שומרת את הגודל המקורי של תמונת התשובה, כדי שאפשר יהיה
    // לחזור אליו אחרי התאמות גודל לתמונות שונות
    private void SaveAnswerImageScale()
    {
        if (answerImage == null) return;

        answerImageOriginalScale = answerImage.transform.localScale;
        answerImage.drawMode = SpriteDrawMode.Simple;
    }

    // ה-GameManager מחבר את כפתור ההגדלה שממקמת בסצנה לאבן הזאת
    public void SetMagnifier(GameObject button)
    {
        magnifierButton = button;

        if (magnifierButton == null) return;

        // נשמר פעם אחת, מהמיקום שהוגדר בסצנה
        if (magnifierOffsetSaved == false)
        {
            magnifierOffset = magnifierButton.transform.position - transform.position;
            magnifierOffsetSaved = true;
        }

        // כל כפתור ולאיזו אבן הוא שייך
        SpriteButtonScript script = magnifierButton.GetComponent<SpriteButtonScript>();

        if (script == null)
        {
            if (magnifierWarningShown == false)
            {
                magnifierWarningShown = true;
                Debug.LogWarning("The magnifier " + magnifierButton.name +
                                 " has no Sprite Button Script. Add it to the prefab and set Action to zoom");
            }
            return;
        }

        script.SetRock(this);
    }
    
    // ============================================================
    // מסמנת שהאבן מורמת על ידי הגמד או הונחה.
    //
    // התפקיד העיקרי הוא שכבות התצוגה: אבן מורמת מקבלת דחיפה
    // בסדר המיון כדי שתעבור מעל שאר האבנים ולא תיעלם מאחוריהן.
    // הטקסט והתמונה שעליה מקבלים דחיפה גדולה באחד, כדי שיישארו
    // מעל האבן עצמה
    // ============================================================
    public void SetCarried(bool carried)
    {
        // הכפתור נעלם בזמן שהאבן באוויר, וחוזר ליד האבן כשהיא נוחתת.
        // ככה אפשר להגדיל את התמונה גם אחרי שהאבן הונחה באגם
        if (magnifierButton != null && currentImage != null)
        {
            if (carried == true)
            {
                magnifierButton.SetActive(false);
            }
            else
            {
                magnifierButton.SetActive(true);
                PlaceMagnifier();
            }
        }

        if (rockImage == null) return;

        int offset = carried ? carriedSortingBoost : 0;

        rockImage.sortingOrder = baseSortingOrder + offset;

        if (rockText != null)
        {
            Renderer textRenderer = rockText.GetComponent<Renderer>();
            if (textRenderer != null)
                textRenderer.sortingOrder = baseSortingOrder + offset + 1;
        }

        if (answerImage != null)
            answerImage.sortingOrder = baseSortingOrder + offset + 1;
    }

    // מצמיד את כפתור ההגדלה לאבן, איפה שהיא לא תהיה
    private void PlaceMagnifier()
    {
        if (magnifierButton == null) return;
        if (magnifierOffsetSaved == false) return;

        magnifierButton.transform.position = transform.position + magnifierOffset;
    }

    // כיבוי הכפתור. לא בשימוש בזמן משחק רגיל, כי לפי האפיון
    // אפשר להגדיל תמונה גם אחרי שהאבן הונחה באגם
    public void HideMagnifier()
    {
        if (magnifierButton != null) magnifierButton.SetActive(false);
    }
    
    // ============================================================
    // מכבה את ה-Animator של האבן.
    //
    // קליפ האנימציה שבפריפאב מנפיש גם את המיקום, ולכן כל האבנים
    // היו נדחפות לאותה נקודה בדיוק ונערמות זו על זו. עד שיוסרו
    // עקומות המיקום מהקליפ, הפתרון הוא לכבות אותו.
    // האזהרה מודפסת פעם אחת בלבד ולא לכל אבן בנפרד
    // ============================================================
    private void StopPositionAnimation()
    {
        Animator animator = GetComponent<Animator>();

        if (animator == null) return;
        if (animator.enabled == false) return;

        animator.enabled = false;

        if (animatorWarningShown == false)
        {
            animatorWarningShown = true;
            Debug.Log("Disabled the rocks Animator - its clip animates Position, " +
                      "which pushed every rock to the same spot. " +
                      "To use it, remove the Position curves from the clip");
        }
    }

    // שומר את המקום של האבן בסצנה
    private void SaveHome()
    {
        if (homeSaved == true) return;

        startPosition = transform.position;
        startScale = transform.localScale;
        homeSaved = true;
    }

    // מטפל בהבהוב האדום של תשובה שגויה. יוצא מיד כשאין הבהוב
    // פעיל, ולכן הוא זול כמעט בכל פריים
    void Update()
    {
        // ההבהוב האדום 
        if (isBlinking == true && rockImage != null)
        {
            blinkTimer -= Time.deltaTime;

            if (blinkTimer <= 0)
            {
                blinkTimer = blinkTime;

                if (isRed == true)
                {
                    rockImage.color = startColor;
                    isRed = false;
                    blinksLeft = blinksLeft - 1;

                    if (blinksLeft <= 0)
                    {
                        isBlinking = false;
                    }
                }
                else
                {
                    rockImage.color = wrongColor;
                    isRed = true;
                }
            }
        }
    }
    
    // לחיצה על האבן. שני התנאים אינם כפילות: canAnswer חוסם
    // לחיצות בזמן שהגמד באמצע פעולה, ו-isPlaced חוסם אבן
    // שכבר הונחה באגם
    private void OnMouseDown()
    {
        if (gameManager == null) return;

        if (gameManager.canAnswer == true && isPlaced == false)
        {
            gameManager.RockClicked(this);
        }
    }

    // הגדלה קלה במעבר עכבר, כחיווי שהאבן לחיצה
    private void OnMouseEnter()
    {
        if (gameManager == null) return;

        if (gameManager.canAnswer == true && isPlaced == false)
        {
            transform.localScale = startScale * bigScale;
        }
    }

    // חזרה לגודל המקורי ביציאת העכבר
    private void OnMouseExit()
    {
        transform.localScale = startScale;
    }

    // כפתור זכוכית המגדלת 
    public void ShowBigImage()
    {
        if (zoomPanel == null) return;
        if (currentImage == null) return;

        zoomPanel.Show(currentImage);
    }

    

    // הפונקציה טוענת לאבן טקסט או תמונה, ומגדירה את המקום הנכון שלה
    public void SetRock(string text, Sprite image, int place)
    {
        SaveHome();

        correctPlace = place;
        isPlaced = false;
        currentImage = image;

        ShowContent(text, image);

        transform.position = startPosition;

        // הכפתור חוזר לצד האבן אחרי שהיא חזרה למקומה
        PlaceMagnifier();

        transform.localScale = startScale;
        transform.rotation = Quaternion.identity;   
        // מיישר את האבן אחרי סיבוב בזריקה

        if (rockImage != null) rockImage.color = startColor;
        isBlinking = false;
    }

    // מחליט מה מוצג על האבן: תמונה או טקסט. אף פעם לא שניהם
    private void ShowContent(string text, Sprite image)
    {
        bool useImage = (image != null);

        //טקסט
        if (rockText != null)
        {
            rockText.gameObject.SetActive(!useImage);
            rockText.isRightToLeftText = false;
            rockText.text = text;
        }

        //  תמונה 
        if (answerImage != null)
        {
            answerImage.gameObject.SetActive(useImage);

            if (useImage == true)
            {
                answerImage.sprite = image;
                FitAnswerImage(image);
            }
        }

        //  זכוכית מגדלת: מופיעה רק כשיש תמונה על האבן 
        if (magnifierButton != null)
        {
            magnifierButton.SetActive(useImage);

            if (useImage == true) PlaceMagnifier();
        }
        else if (useImage == true && magnifierWarningShown == false)
        {
            magnifierWarningShown = true;
            Debug.LogWarning("This stage uses images but no magnifier is linked to " + name +
                             ". Drag the MagnifierPrefab objects into GameManager > Magnifiers");
        }
    }

    // התאמת גודל התמונה
    private void FitAnswerImage(Sprite image)
    {
        if (answerImage == null) return;
        answerImage.transform.localScale = answerImageOriginalScale;

        float containerWidth = answerImageOriginalScale.x;
        float containerHeight = answerImageOriginalScale.y;

        float imageWidth = image.bounds.size.x;
        float imageHeight = image.bounds.size.y;

        if (imageWidth <= 0 || imageHeight <= 0) return;

        float scaleByWidth = containerWidth / imageWidth;
        float scaleByHeight = containerHeight / imageHeight;

        float scale = Mathf.Min(scaleByWidth, scaleByHeight);

        answerImageFitScale = new Vector3(scale, scale, 1f);
        answerImage.transform.localScale = answerImageFitScale;
    }

    // מתחיל את ההבהוב האדום - קורה כשהתשובה שגויה
    public void StartBlink()
    {
        isBlinking = true;
        isRed = false;
        blinkTimer = 0;
        blinksLeft = blinkTimes;
    }
}

// ============================================================
//  פאנל ההגדלה של תמונת תשובה.
//
//  הפאנל היה קודם קובץ נפרד, והוא יושב כאן כי הוא קיים בשביל
//  האבן בלבד: כפתור זכוכית המגדלת שעל האבן הוא הדבר היחיד
//  שפותח אותו, דרך ShowBigImage שלמעלה.
//
//  הוא נשאר מחלקה משלו ולא חלק מ-RockScript, כי יש אבן אחת לכל
//  תשובה אבל פאנל אחד בלבד לכל המשחק. ה-GameManager יוצר אותו
//  פעם אחת ב-BuildZoomPanel, ומחבר אותו לכל האבנים.
// ============================================================

// מסך שמציג תמונת תשובה בגדול.
// אפשר להגדיל ולהקטין בגלגלת העכבר, לגרור את התמונה כשהיא מוגדלת,
// ולסגור בכפתור ה-X, בלחיצה על הרקע או ב-Escape.
public class ZoomPanelScript : MonoBehaviour
{
    [Header("Objects")]
    // האובייקט שמכיל את הרקע ואת התמונה
    [SerializeField] GameObject panel;

    // הספרייט שמציג את התמונה המוגדלת
    [SerializeField] SpriteRenderer bigImage;

    // הרקע הכהה
    [SerializeField] SpriteRenderer background;

    // כפתור הסגירה
    [SerializeField] SpriteRenderer closeButton;

    [Header("Look")]
    // גודל התמונה
    [SerializeField] Vector2 containerSize = new Vector2(6, 4);

    [SerializeField] Color backgroundColor = new Color(0, 0, 0, 0.75f);

    [SerializeField] int sortingOrder = 100;

    [Header("Close Button")]
    // גודל כפתור ה-X ביחידות עולם
    [SerializeField] float closeButtonSize = 0.7f;

    // המרחק של הכפתור מהפינה הימנית העליונה של המסך
    [SerializeField] Vector2 closeButtonMargin = new Vector2(0.9f, 0.9f);

    [SerializeField] Color closeButtonColor = Color.white;

    [Header("Interactive Zoom")]
    // כמה כל צעד של הגלגלת מגדיל
    [SerializeField] float zoomStep = 0.15f;

    // ההגדלה המקסימלית והמינימלית ביחס לגודל ההתחלתי
    [SerializeField] float minZoom = 0.5f;
    [SerializeField] float maxZoom = 4f;

    // האם אפשר לגרור את התמונה כשהיא מוגדלת
    [SerializeField] bool allowDrag = true;

    private Camera gameCamera;

    // הגודל שחושב לתמונה כשהיא נפתחה, לפני הגדלות של השחקן
    private Vector3 fitScale = Vector3.one;

    // כמה השחקן הגדיל, 1 = הגודל ההתחלתי
    private float zoom = 1f;

    private bool isOpen;
    private bool isDragging;
    private bool draggedThisClick;
    private Vector3 dragStartMouse;
    private Vector3 dragStartImage;

    // בונה את הפאנל מיד בטעינה, כדי שהוא יהיה מוכן לפני
    // ההגדלה הראשונה ולא ייבנה תוך כדי לחיצה
    void Awake()
    {
        gameCamera = Camera.main;
        BuildPanel();
    }

    // הפאנל מתחיל סגור. ההסתרה ב-Start ולא ב-Awake, כדי
    // שהבנייה תספיק להסתיים קודם
    void Start()
    {
        Hide();
    }

    // קלט ההגדלה והגרירה, רק כשהפאנל פתוח. היציאה המוקדמת
    // מוודאת שהגלגלת לא תשפיע על שום דבר כשהוא סגור
    void Update()
    {
        if (isOpen == false) return;

        HandleZoomInput();
        HandleDragInput();

        // סגירה במקלדת
        if (Input.GetKeyDown(KeyCode.Escape) == true)
        {
            Hide();
        }
    }

    // גלגלת העכבר מגדילה ומקטינה את התמונה
    private void HandleZoomInput()
    {
        float wheel = Input.mouseScrollDelta.y;

        if (Mathf.Abs(wheel) < 0.01f) return;

        SetZoom(zoom + wheel * zoomStep);
    }

    // כפתורי + ו- על המסך יכולים לקרוא לפונקציות האלה
    public void ZoomIn()
    {
        SetZoom(zoom + zoomStep);
    }

    // הקטנה בצעד אחד, לכפתור המינוס
    public void ZoomOut()
    {
        SetZoom(zoom - zoomStep);
    }

    // ============================================================
    // קובעת את רמת ההגדלה, בתוך הטווח המותר.
    //
    // כשחוזרים לגודל המקורי התמונה מוחזרת למרכז, אחרת שחקן
    // שגרר אותה הצידה והקטין היה נשאר עם תמונה תלויה מחוץ
    // למסגרת בלי דרך ברורה להחזיר אותה
    // ============================================================
    private void SetZoom(float newZoom)
    {
        zoom = Mathf.Clamp(newZoom, minZoom, maxZoom);

        if (bigImage != null)
        {
            bigImage.transform.localScale = fitScale * zoom;
        }

        // בגודל המקורי התמונה חוזרת למרכז
        if (zoom <= 1f && bigImage != null)
        {
            bigImage.transform.localPosition = Vector3.zero;
        }
    }

    // גרירה של התמונה כשהיא מוגדלת
    private void HandleDragInput()
    {
        if (allowDrag == false) return;
        if (bigImage == null) return;
        if (gameCamera == null) return;

        if (Input.GetMouseButtonDown(0) == true && zoom > 1f)
        {
            isDragging = true;
            draggedThisClick = false;
            dragStartMouse = MouseWorld();
            dragStartImage = bigImage.transform.localPosition;
        }

        if (isDragging == true)
        {
            Vector3 delta = MouseWorld() - dragStartMouse;

            // תזוזה קטנה עדיין נחשבת ללחיצה ולא לגרירה
            if (delta.magnitude > 0.05f) draggedThisClick = true;

            bigImage.transform.localPosition = dragStartImage + delta;
        }

        if (Input.GetMouseButtonUp(0) == true)
        {
            isDragging = false;
        }
    }

    // ממירה את מיקום העכבר במסך לנקודה בעולם המשחק.
    // ציר ה-Z מאופס, כי המשחק דו-ממדי
    private Vector3 MouseWorld()
    {
        if (gameCamera == null) return Vector3.zero;

        Vector3 point = gameCamera.ScreenToWorldPoint(Input.mousePosition);
        point.z = 0;
        return point;
    }

    // ============================================================
    // בונה בקוד את כל חלקי פאנל ההגדלה: רקע כהה, תמונה גדולה
    // וכפתור סגירה.
    //
    // הכול נבנה בקוד ולא בסצנה, כדי שההגדלה תעבוד בכל סצנה בלי
    // להרכיב אותה ידנית מחדש. היציאה המוקדמת מאפשרת בכל זאת
    // לחבר חלקים מוכנים באינספקטור, והם לא יידרסו
    // ============================================================
    private void BuildPanel()
    {
        if (panel != null && bigImage != null && closeButton != null) return;

        if (panel == null)
        {
            panel = new GameObject("Panel");
            panel.transform.SetParent(transform, false);
        }

        //  רקע כהה
        if (background == null)
        {
            GameObject backObject = new GameObject("Background");
            backObject.transform.SetParent(panel.transform, false);

            background = backObject.AddComponent<SpriteRenderer>();
            background.sprite = MakeSquareSprite();
            background.color = backgroundColor;
            background.sortingOrder = sortingOrder;

            // קוליידר כדי שאפשר יהיה ללחוץ ולסגור
            backObject.AddComponent<BoxCollider2D>();
            backObject.AddComponent<ZoomBackgroundScript>().zoomPanel = this;
        }

        //  התמונה המוגדלת
        if (bigImage == null)
        {
            GameObject imageObject = new GameObject("BigImage");
            imageObject.transform.SetParent(panel.transform, false);

            bigImage = imageObject.AddComponent<SpriteRenderer>();
            bigImage.sortingOrder = sortingOrder + 1;
            bigImage.drawMode = SpriteDrawMode.Simple;
        }

        //  כפתור ה-X לסגירה
        if (closeButton == null)
        {
            GameObject closeObject = new GameObject("CloseButton");
            closeObject.transform.SetParent(panel.transform, false);

            closeButton = closeObject.AddComponent<SpriteRenderer>();
            closeButton.sprite = MakeCloseSprite();
            closeButton.color = closeButtonColor;
            closeButton.sortingOrder = sortingOrder + 2;

            BoxCollider2D closeCollider = closeObject.AddComponent<BoxCollider2D>();
            closeCollider.size = new Vector2(1f, 1f);

            closeObject.AddComponent<ZoomCloseScript>().zoomPanel = this;

            closeObject.transform.localScale = new Vector3(closeButtonSize, closeButtonSize, 1);
        }
    }

    // מייצרת ספרייט לבן בגודל פיקסל אחד, שמשמש כרקע מתוח.
    // כך אין צורך בקובץ תמונה עבור מלבן בצבע אחיד
    private Sprite MakeSquareSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }

    // מצייר עיגול עם X בתוכו, ככה אין צורך בקובץ תמונה
    private Sprite MakeCloseSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        texture.filterMode = FilterMode.Bilinear;

        float center = (size - 1) / 2f;
        float radius = center - 1f;
        float thickness = size * 0.09f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;

                // מחוץ לעיגול - שקוף
                if (dx * dx + dy * dy > radius * radius)
                {
                    texture.SetPixel(x, y, new Color(0, 0, 0, 0));
                    continue;
                }

                // שני האלכסונים של ה-X
                bool onFirst = Mathf.Abs(dx - dy) < thickness;
                bool onSecond = Mathf.Abs(dx + dy) < thickness;

                // האלכסונים לא נמשכים עד הקצה של העיגול
                bool insideCross = (dx * dx + dy * dy) < (radius * 0.62f) * (radius * 0.62f);

                if ((onFirst || onSecond) && insideCross == true)
                {
                    texture.SetPixel(x, y, Color.white);
                }
                else
                {
                    // גוף הכפתור, כהה ושקוף חלקית
                    texture.SetPixel(x, y, new Color(0.1f, 0.1f, 0.1f, 0.85f));
                }
            }
        }

        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ============================================================
    // פותחת את ההגדלה על תמונה נתונה.
    //
    // הפאנל מועבר קודם למיקום המצלמה, כי המצלמה נעה במהלך
    // המשחק והפאנל חייב להיפתח מול השחקן ולא במקום שבו הוא
    // נבנה. כל פתיחה מתחילה מהגודל ההתחלתי ומהמרכז
    // ============================================================
    public void Show(Sprite image)
    {
        if (panel == null) return;
        if (bigImage == null) return;
        if (image == null) return;

        MoveToCamera();

        bigImage.sprite = image;
        bigImage.transform.localPosition = Vector3.zero;

        FitImage(image);

        // כל פתיחה מתחילה מהגודל ההתחלתי
        zoom = 1f;
        SetZoom(1f);

        isDragging = false;
        draggedThisClick = false;
        isOpen = true;

        panel.SetActive(true);
    }

    // סוגרת את ההגדלה ומאפסת את מצב הגרירה
    public void Hide()
    {
        isOpen = false;
        isDragging = false;

        if (panel != null) panel.SetActive(false);
    }

    // האם ההגדלה פתוחה. נבדק מבחוץ, כדי שלחיצות על המשחק
    // לא ייקלטו כשהפאנל מכסה אותו
    public bool IsOpen()
    {
        return isOpen;
    }

    // לחיצה על הרקע סוגרת, אבל רק אם זו הייתה לחיצה ולא גרירה של התמונה
    public bool ClosingByBackgroundAllowed()
    {
        if (isDragging == true) return false;
        if (draggedThisClick == true) return false;

        return true;
    }

    // מעבירה את הפאנל למרכז תצוגת המצלמה הנוכחית
    private void MoveToCamera()
    {
        if (gameCamera == null) gameCamera = Camera.main;
        if (gameCamera == null) return;

        Vector3 center = gameCamera.transform.position;
        center.z = 0;
        panel.transform.position = center;

        float screenHeight = gameCamera.orthographicSize * 2;
        float screenWidth = screenHeight * gameCamera.aspect;

        if (background != null)
        {
            // הרקע קצת יותר גדול, כדי שלא יישארו פסים בקצוות
            background.transform.localScale = new Vector3(screenWidth + 1, screenHeight + 1, 1);
        }

        // כפתור ה-X יושב בפינה הימנית העליונה של המסך
        if (closeButton != null)
        {
            closeButton.transform.localPosition = new Vector3(
                screenWidth / 2f - closeButtonMargin.x,
                screenHeight / 2f - closeButtonMargin.y,
                0);
        }
    }

    // התאמת גודל לפי פרופורציות. עובד גם לתמונות רחבות וגם לגבוהות
    private void FitImage(Sprite image)
    {
        float imageWidth = image.bounds.size.x;
        float imageHeight = image.bounds.size.y;

        if (imageWidth <= 0 || imageHeight <= 0) return;

        float scaleByWidth = containerSize.x / imageWidth;
        float scaleByHeight = containerSize.y / imageHeight;

        float scale = Mathf.Min(scaleByWidth, scaleByHeight);

        fitScale = new Vector3(scale, scale, 1f);
        bigImage.transform.localScale = fitScale;
    }
}


// הרקע הכהה של הפאנל - לחיצה עליו סוגרת
public class ZoomBackgroundScript : MonoBehaviour
{
    public ZoomPanelScript zoomPanel;

    // OnMouseUpAsButton ולא OnMouseDown: כך לחיצה שהתחילה על
    // הרקע וגררה את התמונה אינה נספרת כלחיצת סגירה
    private void OnMouseUpAsButton()
    {
        if (zoomPanel == null) return;
        if (zoomPanel.ClosingByBackgroundAllowed() == false) return;

        zoomPanel.Hide();
    }
}


// כפתור ה-X לסגירת ההגדלה
public class ZoomCloseScript : MonoBehaviour
{
    public ZoomPanelScript zoomPanel;

    // לחיצה על ה-X סוגרת מיד, בלי התניות
    private void OnMouseDown()
    {
        if (zoomPanel == null) return;

        zoomPanel.Hide();
    }
}
