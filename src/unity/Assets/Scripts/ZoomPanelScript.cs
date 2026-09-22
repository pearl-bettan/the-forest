using UnityEngine;

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

    void Awake()
    {
        gameCamera = Camera.main;
        BuildPanel();
    }

    void Start()
    {
        Hide();
    }

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

    public void ZoomOut()
    {
        SetZoom(zoom - zoomStep);
    }

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

    private Vector3 MouseWorld()
    {
        if (gameCamera == null) return Vector3.zero;

        Vector3 point = gameCamera.ScreenToWorldPoint(Input.mousePosition);
        point.z = 0;
        return point;
    }

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

    public void Hide()
    {
        isOpen = false;
        isDragging = false;

        if (panel != null) panel.SetActive(false);
    }

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

    private void OnMouseDown()
    {
        if (zoomPanel == null) return;

        zoomPanel.Hide();
    }
}
