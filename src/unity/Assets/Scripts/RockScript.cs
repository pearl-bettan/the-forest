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
    
    private void OnMouseDown()
    {
        if (gameManager == null) return;

        if (gameManager.canAnswer == true && isPlaced == false)
        {
            gameManager.RockClicked(this);
        }
    }

    private void OnMouseEnter()
    {
        if (gameManager == null) return;

        if (gameManager.canAnswer == true && isPlaced == false)
        {
            transform.localScale = startScale * bigScale;
        }
    }

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
