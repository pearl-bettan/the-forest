using UnityEngine;

public class SpriteButtonScript : MonoBehaviour
{
    //מחלקה המרכזת את כל כפתורי המשחק
    
    [Header("What To Do")]
    [SerializeField] string action = "pause";

    [Header("Objects")]
    //  pause
    public GameManager gameManager;

    // checkCode
    public ServerManager serverManager;

    //  start / backToGame / restart / home
    public MenuScript menu;

    //  lookAtLake ו-lookAtPlayer
    public CameraScript gameCamera;

    [Header("Sound Button Only")]
    // ספרייט אחד שמתחלף בין דלוק לכבוי.
    // אין אובייקטים שנדלקים ונכבים, ולכן אין מה להשתבש.
    // אם משאירים את השדה ריק, הסקריפט מוצא את ה-Sprite Renderer לבד
    [SerializeField] SpriteRenderer soundRenderer;

    // Musicon
    [SerializeField] Sprite soundOnSprite;

    // Musicoff
    [SerializeField] Sprite soundOffSprite;

    // אם לא חוברו ספרייטים, הכפתור נהיה דהוי כשהסאונד כבוי
    [SerializeField] Color mutedTint = new Color(1f, 1f, 1f, 0.4f);

    [Header("Look")]
    // כמה הכפתור גדל כשמרחפים עליו
    [SerializeField] float bigScale = 1.1f;

    private Vector3 startScale;

    private RockScript myRock;

    void Awake()
    {
        startScale = transform.localScale;

        // "Sound" או " sound " יעבדו בדיוק כמו "sound".
        // חשוב: אחרי ההמרה הזאת כל ההשוואות למטה חייבות להיות
        // באותיות קטנות בלבד - גם lookatlake וגם checkcode
        if (action != null) action = action.Trim().ToLower();

        // בלי קוליידר על האובייקט עצמו, OnMouseDown פשוט לא נקרא
        // והכפתור נראה תקין אבל לא לחיץ
        EnsureCollider();

        // כפתור הסאונד מוצא את הרנדרר שלו לבד, כדי שיישאר שדה אחד פחות למלא
        if (action == "sound" && soundRenderer == null)
        {
            soundRenderer = GetComponent<SpriteRenderer>();

            if (soundRenderer == null) soundRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (menu == null) menu = Object.FindFirstObjectByType<MenuScript>();
        if (gameManager == null) gameManager = Object.FindFirstObjectByType<GameManager>();
        if (serverManager == null) serverManager = Object.FindFirstObjectByType<ServerManager>();
        if (gameCamera == null) gameCamera = Object.FindFirstObjectByType<CameraScript>();
    }

    void Start()
    {
        ReportSetup();

        if (action == "sound") ShowRightSoundImage();
    }

    // מדפיס ל-Console מה הכפתור הזה יודע על עצמו.
    // אם כפתור לא מופיע ברשימה - הסקריפט לא מחובר אליו בכלל
    private void ReportSetup()
    {
        Collider2D hit = GetComponent<Collider2D>();

        string state = "Sprite Button '" + name + "' | action = '" + action + "'";

        if (hit == null)
        {
            state = state + " | NO COLLIDER - not clickable";
        }
        else if (hit.enabled == false)
        {
            state = state + " | collider is disabled - not clickable";
        }
        else
        {
            state = state + " | collider ok, size " + hit.bounds.size;
        }

        if (action == "sound")
        {
            if (soundRenderer == null)
            {
                state = state + " | NO Sprite Renderer found - the image cannot change";
            }
            else if (soundOnSprite == null || soundOffSprite == null)
            {
                state = state + " | sprites missing (drag Musicon and Musicoff) - " +
                                "the button will only dim when muted";
            }
            else
            {
                state = state + " | sprite swap ready";
            }
        }

        Debug.Log(state);
    }

    public void SetRock(RockScript rock)
    {
        myRock = rock;
    }

    // מוודא שיש קוליידר שאפשר ללחוץ עליו.
    // אם הספרייט יושב על אובייקט בן, הקוליידר עדיין נבנה על ההורה,
    // כדי שהחלפת תמונות (למשל סאונד דלוק/כבוי) לא תבטל את הלחיצה
    private void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null) return;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = GetComponentInChildren<SpriteRenderer>(true);

        if (renderer == null || renderer.sprite == null)
        {
            Debug.LogWarning("Sprite Button '" + name + "' has no Collider2D and no sprite to " +
                             "measure. Add a Box Collider 2D to it or it will not be clickable");
            return;
        }

        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();

        // גודל הספרייט ביחידות מקומיות של האובייקט הזה
        Vector3 size = renderer.bounds.size;
        Vector3 scale = transform.lossyScale;

        float width = scale.x != 0 ? size.x / Mathf.Abs(scale.x) : size.x;
        float height = scale.y != 0 ? size.y / Mathf.Abs(scale.y) : size.y;

        box.size = new Vector2(width, height);
        box.offset = transform.InverseTransformPoint(renderer.bounds.center);

        Debug.Log("Sprite Button '" + name + "' had no collider, so one was added automatically");
    }

    // כפתור UI רגיל יכול לקרוא לפונקציה הזאת ישירות מ-On Click
    public void Press()
    {
        DoAction();
    }

    private void OnMouseDown()
    {
        DoAction();
    }

    private void DoAction()
    {

        if (action == "pause")
        {
            if (gameManager == null)
            {
                Debug.LogWarning("No GameManager found - the pause button will not work");
                return;
            }

            gameManager.PauseGame();
            return;
        }

        if (action == "lookatlake" || action == "lookatplayer")
        {
            if (gameCamera == null)
            {
                Debug.LogWarning("No CameraScript in this scene - the arrows will not work");
                return;
            }

            if (action == "lookatlake") gameCamera.LookAtLake();
            else gameCamera.LookAtPlayer();

            return;
        }

        if (action == "zoom")
        {
            if (myRock == null)
            {
                Debug.LogWarning("The magnifier " + name + " is not linked to a rock. " +
                                 "Drag it into GameManager > Magnifiers");
                return;
            }

            myRock.ShowBigImage();
            return;
        }


        if (action == "sound")
        {
            // הפיכת המצבים: דלוק הופך לכבוי ולהפך
            bool turnOn = !DataPass.soundOn;

            // מנהל הסאונד אחראי על ההשתקה, גם של מוזיקת הרקע
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetSoundOn(turnOn);
            }
            else
            {
                DataPass.soundOn = turnOn;
            }

            ShowRightSoundImage();
            return;
        }

        if (action == "checkcode")
        {
            if (serverManager == null)
            {
                Debug.LogWarning("No ServerManager in this scene. Create an empty object and add Server Manager to it");
                return;
            }

            serverManager.CheckCode();
            return;
        }

        //  מעברים בין סצנות 

        if (menu == null)
        {
            Debug.LogWarning("No MenuScript in this scene. Create an empty object and add Menu Script to it");
            return;
        }

        if (action == "start") menu.StartGame();
        else if (action == "backtogame") menu.BackToGame();
        else if (action == "restart") menu.RestartGame();
        else if (action == "home") menu.GoHome();
        else Debug.LogWarning("Unknown Action: " + action);
    }
    
    // מחליף את הספרייט של הכפתור לפי מצב הסאונד.
    // אותו אובייקט, אותו קוליידר, רק התמונה משתנה - ולכן הכפתור
    // אף פעם לא נעלם ואף פעם לא מפסיק להיות לחיץ
    private void ShowRightSoundImage()
    {
        bool on = DataPass.soundOn;

        // השתקת סאונד
        AudioListener.volume = on ? 1 : 0;

        if (soundRenderer == null) return;

        // שתי התמונות חוברו - מחליפים ספרייט
        if (soundOnSprite != null && soundOffSprite != null)
        {
            soundRenderer.sprite = on ? soundOnSprite : soundOffSprite;
            soundRenderer.color = Color.white;
            return;
        }

        // לא חוברו ספרייטים - לפחות נותנים חיווי בשקיפות
        soundRenderer.color = on ? Color.white : mutedTint;
    }

    private void OnMouseEnter()
    {
        transform.localScale = startScale * bigScale;
    }

    private void OnMouseExit()
    {
        transform.localScale = startScale;
    }
}
