using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// מנהל המשחק. מחלקות הנתונים שלו נמצאות ב-GameData.cs


public class GameManager : MonoBehaviour
{
    [Header("Game")]
    // המשחק שהתקבל מהשרת, לפי הקוד שהשחקן הזין בסצנת הפתיחה
    [SerializeField] GameData game;

    // כמה שניות מחכים אחרי שנגמרה שאלה, לפני שהשאלה הבאה נטענת
    [SerializeField] float nextStageDelay = 2;

    [Header("Scenes")]
    [SerializeField] string pauseScene = "Pause";
    [SerializeField] string endScene = "End";

    //מספר השניות שרואים את המסך האחרון לפני המעבר לסצנת הסיום
    [SerializeField] float endSceneDelay = 1.5f;

    [Header("Intro")]
    [SerializeField] CameraScript gameCamera;

    // כמה שניות רואים את האגם בתחילת כל שאלה, לפני שהטיימר מתחיל
    [SerializeField] float introLakeTime = 3;

    //מספר השניות שרואים את המסך הרגיל לפני שהמצלמה יוצאת לאגם
    [SerializeField] float introStartDelay = 1;

    // ההודעה שמסמנת לשחקן שהטיימר התחיל לרוץ
    [SerializeField] string timerStartMessage = "צאו לדרך!";
    [SerializeField] float timerStartMessageTime = 1.2f;

    [Header("Objects")]
    [SerializeField] DwarfScript dwarf;

    // האבנים שנמצאות בסצנה
    [SerializeField] List<RockScript> rocks;

    //ה-Slots על האגם
    [SerializeField] List<Transform> slots;

    // תצוגת מצב המשחק: הטיימר עם השמש, ומד ההתקדמות
    [SerializeField] GameStatus gameStatus;

    //מסך שמציג תמונת תשובה בגדול
    [SerializeField] ZoomPanelScript zoomPanel;

    // כפתורי ההגדלה
    [SerializeField] List<GameObject> magnifiers;

    [Header("Free Walk")]
    // אזור הדשא. לחיצה בתוכו שולחת את הגמד לטייל לשם.
    // היה סקריפט נפרד על אובייקט הדשא, ועכשיו זה שדה אחד כאן
    [SerializeField] Collider2D grassArea;

    [Header("Skipping")]
    // הנקודה שאליה הגמד מדלג בסוף - התגית השמאלית.
    // בלי זה הוא עוצר על אבן התשובה האחרונה ולא מגיע עד התגית
    [SerializeField] Transform leftTagPoint;

    [Header("Slots Layout")]
    // המיקומים של ה-Slots כפי שסודרו ידנית בסצנה נשמרים בהתחלה.
    // כשיש פחות תשובות ממספר ה-Slots, האבנים מתפזרות במידה שווה
    // לאורך אותו מסלול, בין התגית הימנית לתגית השמאלית
    [SerializeField] bool spreadSlotsEvenly = true;

    [Header("Texts")]
    [SerializeField] TMP_Text topicText;
    [SerializeField] TMP_Text leftTagText;
    [SerializeField] TMP_Text rightTagText;
    [SerializeField] TMP_Text messageText;

    [Header("Screens")]
    [SerializeField] GameObject messagePanel;

    // תמונה אדומה שמכסה את המסך, מכובה בהתחלה
    [SerializeField] GameObject redFlash;
    [SerializeField] float redFlashTime = 0.4f;

    [Header("Lives")]
    [SerializeField] List<GameObject> hearts;

    [Header("Sounds")]
    // גיבוי אם אין AudioManager בסצנה
    [SerializeField] AudioSource feedbackAudio;
    [SerializeField] AudioClip correctSound;
    [SerializeField] AudioClip wrongSound;
    [SerializeField] AudioClip stageCompleteSound;

    [Header("Hebrew")]
    // מסדר את סדר האותיות בעברית
    [SerializeField] bool fixHebrewOrder = true;

    // כמה תווים יש בשורה אחת בנושא השאלה
    [SerializeField] int topicMaxChars = 11;

    // כמה תווים נכנסים בשורה אחת על אבן תשובה.
    // חשוב: בלי שבירת שורות משלנו, TMP שובר את הטקסט אחרי
    // שכבר הפכנו אותו, ואז סדר המילים במשפט יוצא הפוך
    [SerializeField] int rockMaxChars = 12;

    // אותו דבר לתגיות שמשמאל ומימין לאגם
    [SerializeField] int tagLineLength = 7;

    // האם מותר ללחוץ על אבן כרגע
    public bool canAnswer;

    private StageData currentStage;

    // מאגר השאלות שעוד לא נענו נכון
    private List<StageData> questionPool;

    private int totalQuestions;

    // כמה שאלות כבר נענו נכון
    private int questionsAnswered;

    //ה-Slot הבא שצריך למלא
    private int nextSlot;

    private float stageTimer;
    private int livesLeft;
    private float flashTimer;
    private bool gameOver;

    // הטיימר קפוא עד שהתצוגה של האגם נגמרת
    private bool timerRunning;

    // כמה תשובות נכונות וכמה טעויות בשאלה הנוכחית
    private int stageCorrect;
    private int stageMistakes;

    // כמה טעויות בכל המשחק
    private int totalMistakes;

    //הניקוד שנצבר עד עכשיו
    private float scoreSoFar;

    //כמה זמן עבר מתחילת המשחק
    private float gameTime;

    //ספירה לאחור למעבר אוטומטי לשאלה הבאה
    private float nextStageTimer;

    //ספירה לאחור למעבר לסצנת הסיום
    private float endSceneTimer;

    //ספירה לאחור לתצוגת האגם בתחילת כל שאלה
    private float introTimer;

    //ההמתנה על המסך הראשון, לפני שהמצלמה יוצאת לאגם
    private float introDelayTimer;

    //האם המצלמה כבר יצאה לדרך בתצוגת הפתיחה
    private bool introMoved;

    // ספירה לאחור להודעה שמסמנת שהטיימר התחיל
    private float startMessageTimer;

    // הגנה מפני לחיצות חוזרות על כפתור עצירת המשחק
    private bool pauseRequested;

    // הגמד באמצע אנימציית הדילוג בסיום שאלה
    private bool skipping;

    // המיקומים שסודרו ידנית בסצנה, נשמרים לפני שנוגעים בהם
    private List<Vector3> manualSlotPositions;

    void Start()
    {
        BuildZoomPanel();
        SaveManualSlotPositions();
        CheckSetup();
        GetGame();
        StartStage();
    }

    // בתחילת כל שאלה המצלמה נוסעת לאגם ועומדת שם כמה שניות,
    // ככה השחקן רואה לאן הוא אמור לסדר את האבנים ובאיזה סדר.
    // הטיימר קפוא בזמן הזה ומתחיל רק כשהמצלמה חוזרת.
    private void ShowLakeIntro()
    {
        if (gameCamera == null)
        {
            // הטיימר מתחיל מיד אם אין מצלמה שתראה את האגם
            StartTimer();
            return;
        }

        if (introLakeTime <= 0)
        {
            StartTimer();
            return;
        }

        // קודם נותנים לשחקן לראות את המסך הראשון, ורק אחר כך המצלמה יוצאת לאגם
        canAnswer = false;
        timerRunning = false;
        if (gameStatus != null) gameStatus.SetFrozen(true);

        introDelayTimer = introStartDelay;
        introTimer = introStartDelay + introLakeTime + 1;
        introMoved = false;
    }

    // מרגע זה הטיימר רץ. מציגים לשחקן חיווי שהזמן התחיל
    private void StartTimer()
    {
        timerRunning = true;
        if (gameStatus != null) gameStatus.SetFrozen(false);

        if (gameOver == false) canAnswer = true;

        if (timerStartMessage != "" && messagePanel != null && messageText != null)
        {
            messagePanel.SetActive(true);
            messageText.text = FixText(timerStartMessage);
            startMessageTimer = timerStartMessageTime;
        }
    }

    //הגדלת תמונה
    private void BuildZoomPanel()
    {
        if (zoomPanel != null) return;

        GameObject panelObject = new GameObject("ZoomPanel");
        zoomPanel = panelObject.AddComponent<ZoomPanelScript>();
    }

    // הכנת משחק חדש, אתחול חיים, ניקוד וזמן
    private void GetGame()
    {
        if (DataPass.Game != null)
        {
            game = DataPass.Game;
        }

        //מופעל במקרה שבו לא נטען משחק
        if (game == null || game.stagesList == null || game.stagesList.Count == 0)
        {
            Debug.LogError("No game data. Enter a game code in the Home scene, " +
                           "or fill Game in the GameManager Inspector");
            return;
        }

        // חזרה מהשהייה - ממשיכים עם המאגר שהיה, ולא מתחילים משחק חדש
        bool resuming = DataPass.keepLives;

        // מאגר השאלות. שאלה שנענתה נכון יוצאת ממנו ולא חוזרת
        questionPool = new List<StageData>();
        totalQuestions = game.stagesList.Count;

        for (int i = 0; i < game.stagesList.Count; i++)
        {
            StageData question = game.stagesList[i];
            if (question == null) continue;

            // משחק חדש - מנקים את הסימונים מהמשחק הקודם
            if (resuming == false)
            {
                question.markedWrong = false;
                question.answeredCorrectly = false;
            }

            if (question.answeredCorrectly == false)
            {
                questionPool.Add(question);
            }
        }

        questionsAnswered = 0;
        scoreSoFar = 0;
        gameTime = 0;
        totalMistakes = 0;

        // מספר החיים קבוע על 3 ולא ניתן לשינוי מהמחולל
        livesLeft = GameRules.Lives;

        // לא יכולים להציג יותר לבבות ממה שיש בסצנה
        if (hearts != null && hearts.Count > 0 && livesLeft > hearts.Count)
        {
            Debug.LogWarning("There are only " + hearts.Count + " hearts in the scene but the game " +
                             "needs " + GameRules.Lives + ". Add hearts to GameManager > Hearts");
            livesLeft = hearts.Count;
        }

        // חזרה מהשהייה - ממשיכים מאיפה שהפסקנו
        if (DataPass.keepLives == true)
        {
            livesLeft = DataPass.livesLeft;
            gameTime = DataPass.savedGameTime;
            scoreSoFar = DataPass.savedScore;
            totalMistakes = DataPass.savedMistakes;
            questionsAnswered = DataPass.savedQuestionsAnswered;

            DataPass.keepLives = false;
            DataPass.returningFromPause = false;
        }

        UpdateHearts();
        UpdateProgressBar();
    }

    private void CheckSetup()
    {
        string missing = "";

        if (dwarf == null) missing = missing + "Dwarf, ";
        if (rocks == null || rocks.Count == 0) missing = missing + "Rocks, ";
        if (slots == null || slots.Count == 0) missing = missing + "Slots, ";
        if (gameStatus == null) missing = missing + "Game Status, ";
        if (magnifiers == null || magnifiers.Count == 0) missing = missing + "Magnifiers, ";
        if (topicText == null) missing = missing + "Topic Text, ";
        if (leftTagText == null) missing = missing + "Left Tag Text, ";
        if (rightTagText == null) missing = missing + "Right Tag Text, ";
        if (messageText == null) missing = missing + "Message Text, ";
        if (messagePanel == null) missing = missing + "Message Panel, ";
        if (redFlash == null) missing = missing + "Red Flash, ";
        if (hearts == null || hearts.Count == 0) missing = missing + "Hearts, ";

        if (missing != "")
        {
            Debug.LogWarning("Empty fields in GameManager: " + missing);
        }

        // המצלמה מחפשת את עצמה פעם אחת אם היא לא חוברה ב-Inspector.
        // בלי זה תצוגת האגם בתחילת שאלה פשוט לא רצה
        if (gameCamera == null)
        {
            gameCamera = Object.FindFirstObjectByType<CameraScript>();

            if (gameCamera == null)
            {
                Debug.LogWarning("No CameraScript in the scene - the lake intro will not run. " +
                                 "Add Camera Script to the Main Camera and drag it into GameManager > Game Camera");
            }
        }

        // הגמד והאבנים צריכים לדעת מי מנהל אותם. עושים את זה פעם אחת כאן
        // ולא בכל פריים עם Find
        if (dwarf != null)
        {
            dwarf.gameManager = this;
            if (dwarf.gameCamera == null) dwarf.gameCamera = gameCamera;
        }
    }

    void Update()
    {
        //אם השאלה לא התחילה - אין מה לעדכן
        if (currentStage == null) return;

        // לחיצה על הדשא שולחת את הגמד לטייל
        HandleGrassClick();

        // ההודעה שמסמנת שהטיימר התחיל
        if (startMessageTimer > 0)
        {
            startMessageTimer -= Time.deltaTime;

            if (startMessageTimer <= 0 && gameOver == false && messagePanel != null)
            {
                messagePanel.SetActive(false);
            }
        }

        // המצלמה מראה את האגם - הזמן לא רץ ואי אפשר לענות
        if (introTimer > 0)
        {
            introTimer -= Time.deltaTime;

            //המסך נשאר לרגע על המסך הראשון, ורק אז המצלמה יוצאת לאגם
            if (introMoved == false)
            {
                introDelayTimer -= Time.deltaTime;

                if (introDelayTimer <= 0)
                {
                    introMoved = true;
                    gameCamera.ShowLakeThenReturn(introLakeTime);
                }
            }

            if (introTimer <= 0 && gameOver == false)
            {
                StartTimer();
            }
            return;
        }

        //ספירה למעבר לסצנת הסיום
        if (endSceneTimer > 0)
        {
            endSceneTimer -= Time.deltaTime;

            if (endSceneTimer <= 0)
            {
                SceneManager.LoadScene(endScene);
                return;
            }
        }

        //ספירה לשאלה הבאה
        if (nextStageTimer > 0)
        {
            nextStageTimer -= Time.deltaTime;

            if (nextStageTimer <= 0)
            {
                StartStage();
                return;
            }
        }

        //הטיימר. רץ רק אחרי שתצוגת האגם נגמרה
        if (gameOver == false && timerRunning == true)
        {
            gameTime += Time.deltaTime;

            // 0 = ללא הגבלת זמן
            if (currentStage.stageTime > 0)
            {
                stageTimer -= Time.deltaTime;

                if (gameStatus != null) gameStatus.ShowTime(stageTimer, currentStage.stageTime);

                if (stageTimer <= 0)
                {
                    stageTimer = 0;
                    if (gameStatus != null) gameStatus.ShowTime(0, currentStage.stageTime);

                    TimeIsUp();
                }
            }
        }

        //כיבוי ההבהוב האדום
        if (flashTimer > 0)
        {
            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0 && redFlash != null)
            {
                redFlash.SetActive(false);
            }
        }
    }

    // לחיצה על הדשא שולחת את הגמד לטייל לשם.
    // הלחיצה נבדקת כאן ולא בסקריפט נפרד על אובייקט הדשא
    private void HandleGrassClick()
    {
        if (grassArea == null) return;
        if (dwarf == null) return;
        if (gameOver == true) return;

        if (Input.GetMouseButtonDown(0) == false) return;

        // לא מזיזים את הגמד בזמן שהוא באמצע תשובה או דילוג
        if (dwarf.IsIdle() == false) return;

        Camera cameraToUse = Camera.main;
        if (cameraToUse == null) return;

        Vector3 screenPoint = cameraToUse.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new Vector2(screenPoint.x, screenPoint.y);

        // אם לחצו על אבן או על כפתור, זה לא טיול על הדשא
        Collider2D[] under = Physics2D.OverlapPointAll(point);

        for (int i = 0; i < under.Length; i++)
        {
            if (under[i] == null) continue;
            if (under[i] == grassArea) continue;

            if (under[i].GetComponent<RockScript>() != null) return;
            if (under[i].GetComponent<SpriteButtonScript>() != null) return;
        }

        if (grassArea.OverlapPoint(point) == false) return;

        dwarf.WalkFreely(point);
    }


    // נגמר הזמן: השאלה מסומנת כשגויה והמשחק נגמר.
    // השחקן עובר למסך "נגמר הזמן"
    private void TimeIsUp()
    {
        timerRunning = false;
        canAnswer = false;

        // **לא** מוסיפים פסילה כאן. פסילה היא לב שירד, וסיום הזמן
        // לא מוריד לב. הספירה נשארת זהה למה שהשחקן ראה על המסך.
        // השאלה עדיין מסומנת כשגויה לצורך מאגר השאלות
        if (currentStage != null) currentStage.markedWrong = true;

        EndGame("timeout", "נגמר הזמן");
    }

    // מוריד חיים ומעדכן את מונה השגיאות
    private void CountMistake()
    {
        totalMistakes = totalMistakes + 1;
        livesLeft = livesLeft - 1;
        UpdateHearts();
    }

    // מחזיר את השאלה הנוכחית למאגר ומסמן אותה כשגויה
    private void ReturnQuestionToPool()
    {
        if (currentStage == null) return;
        if (questionPool == null) return;

        currentStage.markedWrong = true;

        if (questionPool.Contains(currentStage) == false)
        {
            questionPool.Add(currentStage);
        }
    }

    public void StartStage()
    {
        if (game == null || game.stagesList == null || game.stagesList.Count == 0)
        {
            Debug.LogError("Stages List is empty. Add a stage in GameManager > Game > Stages List, " +
                           "or start the game from the Home scene with a game code");
            return;
        }

        nextStageTimer = 0;
        skipping = false;
        pauseRequested = false;

        if (questionPool == null) GetGame();

        // נגמרו השאלות - השחקן ענה נכון על כולן
        if (questionPool.Count == 0)
        {
            EndGame("win", "סיימת את כל השאלות!");
            return;
        }

        // מגרילים שאלה מתוך המאגר. שאלה שתיענה נכון תצא ממנו,
        // שאלה שתיענה לא נכון תוחזר אליו
        int randomIndex = Random.Range(0, questionPool.Count);
        currentStage = questionPool[randomIndex];
        questionPool.RemoveAt(randomIndex);

        // כותרות
        SetWrappedText(topicText, currentStage.topic, topicMaxChars);
        SetWrappedText(leftTagText, currentStage.leftTag, tagLineLength);
        SetWrappedText(rightTagText, currentStage.rightTag, tagLineLength);

        // טיימר. 0 = ללא הגבלת זמן, וזה מצב חוקי מהמחולל
        stageTimer = currentStage.stageTime;

        bool noTimeLimit = currentStage.stageTime <= 0;

        // מסכי הסיום צריכים לדעת אם היה זמן בכלל
        DataPass.unlimitedTime = noTimeLimit;

        if (gameStatus != null)
        {
            gameStatus.SetUnlimited(noTimeLimit);
            gameStatus.ShowTime(stageTimer, currentStage.stageTime);
        }

        // ניקוד השאלה מתחיל מחדש
        stageCorrect = 0;
        stageMistakes = 0;

        // ניקוי המסך
        nextSlot = 0;
        gameOver = false;
        timerRunning = false;

        if (messagePanel != null) messagePanel.SetActive(false);
        if (redFlash != null) redFlash.SetActive(false);
        if (zoomPanel != null) zoomPanel.Hide();

        LayoutSlots();
        ShowSlots();
        SetupRocks();
        UpdateProgressBar();

        if (dwarf != null) dwarf.ResetDwarf();

        canAnswer = false;
        ShowLakeIntro();
    }

    // שומר את הסידור הידני של ה-Slots בסצנה, פעם אחת בתחילת המשחק
    private void SaveManualSlotPositions()
    {
        manualSlotPositions = new List<Vector3>();

        if (slots == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            manualSlotPositions.Add(slots[i].position);
        }
    }

    // כשיש מספר תשובות מלא - משתמשים בסידור הידני כמו שהוא.
    // כשיש פחות - מפזרים את האבנים במידה שווה לאורך אותו מסלול,
    // כך שהראשונה צמודה לתגית הימנית והאחרונה לתגית השמאלית
    private void LayoutSlots()
    {
        if (slots == null || slots.Count == 0) return;
        if (manualSlotPositions == null || manualSlotPositions.Count == 0) return;

        int answersCount = currentStage.answersList.Count;
        if (answersCount <= 0) return;

        // מספר התשובות המלא, או שהפיזור כבוי - מחזירים את הסידור הידני
        if (spreadSlotsEvenly == false || answersCount >= manualSlotPositions.Count)
        {
            RestoreManualSlots();
            return;
        }

        for (int i = 0; i < answersCount && i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            // t רץ מ-0 (התגית הימנית) עד 1 (התגית השמאלית)
            float t;

            if (answersCount == 1) t = 0.5f;
            else t = (float)i / (answersCount - 1);

            Vector3 point = PointOnManualPath(t);
            point.z = slots[i].position.z;

            slots[i].position = point;
        }
    }

    private void RestoreManualSlots()
    {
        for (int i = 0; i < slots.Count && i < manualSlotPositions.Count; i++)
        {
            if (slots[i] == null) continue;

            slots[i].position = manualSlotPositions[i];
        }
    }

    // מחזיר נקודה על המסלול שסודר ידנית, לפי מרחק יחסי לאורכו.
    // ככה הפיזור נשאר על אותו קו שסידרת, גם כשיש פחות אבנים
    private Vector3 PointOnManualPath(float t)
    {
        int count = manualSlotPositions.Count;

        if (count == 1) return manualSlotPositions[0];

        if (t <= 0) return manualSlotPositions[0];
        if (t >= 1) return manualSlotPositions[count - 1];

        // אורך כל מקטע ואורך המסלול כולו
        float totalLength = 0;

        for (int i = 0; i < count - 1; i++)
        {
            totalLength += Vector3.Distance(manualSlotPositions[i], manualSlotPositions[i + 1]);
        }

        if (totalLength <= 0) return manualSlotPositions[0];

        // מתקדמים לאורך המסלול עד למרחק המבוקש
        float wanted = t * totalLength;
        float walked = 0;

        for (int i = 0; i < count - 1; i++)
        {
            float segment = Vector3.Distance(manualSlotPositions[i], manualSlotPositions[i + 1]);

            if (segment <= 0) continue;

            if (walked + segment >= wanted)
            {
                float inside = (wanted - walked) / segment;
                return Vector3.Lerp(manualSlotPositions[i], manualSlotPositions[i + 1], inside);
            }

            walked += segment;
        }

        return manualSlotPositions[count - 1];
    }

    // מדליק רק את מספר ה-Slots שהוקצבו בשאלה הזאת
    private void ShowSlots()
    {
        int answersCount = currentStage.answersList.Count;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            slots[i].gameObject.SetActive(i < answersCount);
        }
    }

    // מכין את האבנים בסצנה לשאלה החדשה
    private void SetupRocks()
    {
        int answersCount = currentStage.answersList.Count;

        if (answersCount > rocks.Count)
        {
            Debug.LogWarning("Stage has " + answersCount + " answers but only " +
                             rocks.Count + " rocks in the Rocks list");
            answersCount = rocks.Count;
        }

        // הגרלה איזו אבן תקבל איזו תשובה
        List<int> randomPlaces = RandomPlaces(answersCount);

        for (int i = 0; i < rocks.Count; i++)
        {
            if (rocks[i] == null) continue;

            if (i < answersCount)
            {
                // המקום הנכון של האבן הספציפית באגם
                int place = randomPlaces[i];
                AnswerData answer = currentStage.answersList[place];

                rocks[i].gameObject.SetActive(true);
                rocks[i].gameManager = this;
                rocks[i].zoomPanel = zoomPanel;

                // מחברים לאבן את כפתור ההגדלה שממוקם לידה בסצנה
                rocks[i].SetMagnifier(GetMagnifier(i));

                rocks[i].SetRock(FixRockText(answer.answerContent), answer.answerImage, place);
            }
            else
            {
                // האבן מיותרת בשאלה הזאת, מכבים אותה ואת הכפתור שלה
                rocks[i].gameObject.SetActive(false);

                GameObject extraButton = GetMagnifier(i);
                if (extraButton != null) extraButton.SetActive(false);
            }
        }
    }

    // מחזיר את כפתור ההגדלה ששייך לאבן
    private GameObject GetMagnifier(int index)
    {
        if (magnifiers == null) return null;
        if (index < 0 || index >= magnifiers.Count) return null;

        return magnifiers[index];
    }

    // מחזיר רשימה של אבני התשובה בסדר מעורבב
    private List<int> RandomPlaces(int count)
    {
        List<int> result = new List<int>();

        for (int i = 0; i < count; i++)
        {
            result.Add(i);
        }

        // ערבוב פישר-ייטס: מעבר אחד על הרשימה במקום בנייה של רשימה שנייה
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            int temp = result[i];
            result[i] = result[j];
            result[j] = temp;
        }

        return result;
    }

    // האבן מפעילה את הפונקציה כשלוחצים עליה
    public void RockClicked(RockScript rock)
    {
        if (dwarf == null)
        {
            Debug.LogError("Dwarf field is empty in GameManager");
            return;
        }

        if (canAnswer == false) return;

        canAnswer = false;

        // התשובה נכונה אם המקום של האבן הוא בדיוק ה-Slot הבא בתור
        bool isCorrect = (rock.correctPlace == nextSlot);

        Vector2 target;

        if (isCorrect == true && nextSlot < slots.Count && slots[nextSlot] != null)
        {
            // האבן תעוף למקום המתאים לה באגם
            target = slots[nextSlot].position;
        }
        else
        {
            // האבן תחזור למקום שממנו באה
            target = rock.startPosition;
        }

        // שולחים את הגמד להביא את האבן
        dwarf.GoGetRock(rock, isCorrect, target);
    }

    // הגמד מפעיל את הפונקציה ברגע שהוא זורק את האבן
    public void ShowAnswerFeedback(RockScript rock, bool isCorrect)
    {
        if (isCorrect == true)
        {
            rock.isPlaced = true;
            nextSlot = nextSlot + 1;
            stageCorrect = stageCorrect + 1;

            PlayCorrectSound();
        }
        else
        {
            stageMistakes = stageMistakes + 1;
            CountMistake();

            // ההבהוב האדום שמראה שהייתה טעות
            if (redFlash != null)
            {
                redFlash.SetActive(true);
                flashTimer = redFlashTime;
            }

            PlayWrongSound();
        }
    }

    public void TurnFinished()
    {
        if (gameOver == true) return;
        if (skipping == true) return;

        // נגמרו הפסילות
        if (livesLeft <= 0)
        {
            EndGame("nolives", "נגמרו הפסילות");
            return;
        }

        // כל האבנים סודרו
        if (nextSlot >= currentStage.answersList.Count)
        {
            StageWon();
            return;
        }

        canAnswer = true;
    }

    // סיום שאלה בהצלחה
    private void StageWon()
    {
        gameOver = true;
        canAnswer = false;
        timerRunning = false;

        AddStageScore();

        // השאלה נענתה נכון ולכן היא לא חוזרת למאגר, גם לא אחרי השהייה
        currentStage.answeredCorrectly = true;
        questionsAnswered = questionsAnswered + 1;
        UpdateProgressBar();

        PlayStageCompleteSound();

        // הגמד מדלג על כל האבנים באגם מהתגית הימנית לשמאלית
        StartSkipping();
    }

    // אנימציית הדילוג של הגמד על אבני האגם בסיום שאלה מוצלחת
    private void StartSkipping()
    {
        if (dwarf == null)
        {
            AfterSkipping();
            return;
        }

        int answersCount = currentStage.answersList.Count;

        List<Vector2> path = new List<Vector2>();

        for (int i = 0; i < answersCount && i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            path.Add(slots[i].position);
        }

        if (path.Count == 0)
        {
            AfterSkipping();
            return;
        }

        // הדילוג תמיד נע מהתגית הימנית לשמאלית. מיון לפי x יורד מבטיח
        // את זה בלי תלות בסדר שבו חוברו האבנים ברשימת ה-Slots בעורך
        path.Sort((a, b) => b.x.CompareTo(a.x));

        // התחנה האחרונה היא התגית השמאלית, ולא אבן התשובה האחרונה
        path.Add(SkipEndPoint(path));

        skipping = true;

        // מעקב אופקי בלבד: הקפיצות למעלה לא מזיזות את המצלמה,
        // וההצמדה לגבולות מונעת יציאה מחוץ לעולם המשחק
        if (gameCamera != null) gameCamera.FollowSideways(dwarf.transform);

        dwarf.SkipOverRocks(path, AfterSkipping);
    }

    // הנקודה שבה הדילוג נגמר: התגית השמאלית אם חוברה,
    // אחרת ממשיכים צעד אחד בכיוון שבו הלכו האבנים
    private Vector2 SkipEndPoint(List<Vector2> path)
    {
        if (leftTagPoint != null) return leftTagPoint.position;

        Vector2 last = path[path.Count - 1];

        if (path.Count < 2) return last;

        Vector2 before = path[path.Count - 2];

        return last + (last - before);
    }


    // נקרא כשהגמד סיים לדלג
    private void AfterSkipping()
    {
        skipping = false;

        // המצלמה מפסיקה לעקוב וחוזרת למסך אבני התשובה
        if (gameCamera != null) gameCamera.GoHome();

        if (questionPool.Count == 0)
        {
            //השאלה האחרונה
            EndGame("win", "סיימת את כל השאלות!");
            return;
        }

        ShowMessage("כל הכבוד!");

        //תחילת ספירה לשאלה הבאה
        nextStageTimer = nextStageDelay;
    }

    // בתוך השאלה, הניקוד נקבע לפי הדיוק של התשובות.
    // כל שאלה שווה חלק שווה מ-100, ומשחק מושלם בלי טעויות נותן 100.
    // הניקוד ניתן רק כששאלה נענית נכון במלואה, ולכן ניסיון כושל לא גורע פעמיים.
    private void AddStageScore()
    {
        if (totalQuestions <= 0) return;

        float pointsPerQuestion = 100f / totalQuestions;

        int totalClicks = stageCorrect + stageMistakes;
        if (totalClicks <= 0) return;

        float accuracy = (float)stageCorrect / totalClicks;

        scoreSoFar = scoreSoFar + pointsPerQuestion * accuracy;

        // הגנה מפני חריגה מ-100 בגלל שגיאות עיגול
        if (scoreSoFar > 100f) scoreSoFar = 100f;
    }

    // סיום משחק- מעבר לסצנת הסיום
    private void EndGame(string result, string message)
    {
        nextStageTimer = 0;
        timerRunning = false;

        DataPass.result = result;
        DataPass.score = Mathf.RoundToInt(scoreSoFar);
        DataPass.totalTime = gameTime;
        DataPass.mistakes = totalMistakes;

        // כמה פסילות נשארו. בלי השורה הזאת מסכי הסיום מציגים
        // את הערך של המשחק הקודם, או 0
        DataPass.livesLeft = livesLeft;

        DataPass.questionsAnswered = questionsAnswered;
        DataPass.questionsTotal = totalQuestions;

        ShowMessage(message);

        if (endScene != "")
        {
            endSceneTimer = endSceneDelay;
        }
    }

    // הודעה זמנית על המסך בלי לסיים את המשחק
    private void ShowMessage(string message)
    {
        gameOver = true;
        canAnswer = false;
        startMessageTimer = 0;

        if (messagePanel != null) messagePanel.SetActive(true);
        if (messageText != null) messageText.text = FixText(message);
    }

    private void PlayCorrectSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCorrect();
            return;
        }

        PlaySound(correctSound);
    }

    private void PlayWrongSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWrong();
            return;
        }

        PlaySound(wrongSound);
    }

    private void PlayStageCompleteSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayStageComplete();
            return;
        }

        PlaySound(stageCompleteSound);
    }

    // מנגן סאונד רק אם יש קובץ סאונד
    private void PlaySound(AudioClip sound)
    {
        if (feedbackAudio == null) return;
        if (sound == null) return;

        feedbackAudio.PlayOneShot(sound);
    }

    //כפתור ההשהייה מפעיל את הפונקציה הזאת
    public void PauseGame()
    {
        // הגנה: לחיצה כפולה או לחיצה בזמן משוב סיום לא תעשה כלום
        if (pauseRequested == true) return;
        if (currentStage == null) return;
        if (endSceneTimer > 0) return;

        pauseRequested = true;

        // הניסיון הנוכחי לא נספר ולא משפיע על הציון,
        // ולכן השאלה חוזרת למאגר בלי סימון שגיאה
        if (gameOver == false)
        {
            if (questionPool != null && questionPool.Contains(currentStage) == false)
            {
                questionPool.Add(currentStage);
            }
        }

        DataPass.livesLeft = livesLeft;
        DataPass.keepLives = true;
        DataPass.returningFromPause = true;
        DataPass.savedGameTime = gameTime;
        DataPass.savedScore = scoreSoFar;
        DataPass.savedMistakes = totalMistakes;
        DataPass.savedQuestionsAnswered = questionsAnswered;

        SceneManager.LoadScene(pauseScene);
    }

    // הכפתור ״שאלה הבאה״ מפעיל את הפונקציה הזאת
    public void Next()
    {
        StartStage();
    }

    // מתחיל את כל המשחק מההתחלה
    public void RestartGame()
    {
        DataPass.NewGame();
        GetGame();
        StartStage();
    }

    private void UpdateHearts()
    {
        if (hearts == null) return;

        for (int i = 0; i < hearts.Count; i++)
        {
            if (hearts[i] == null) continue;

            hearts[i].SetActive(i < livesLeft);
        }
    }

    // מספר השאלות שהמד כבר נבנה עבורן, כדי לא לבנות אותו בכל קריאה
    private int progressBuiltFor = 0;

    // המד מייצג את שאלות המשחק: חרוז לכל שאלה, וכל שאלה שנענתה
    // הופכת חרוז אחד מאפור לירוק
    private void UpdateProgressBar()
    {
        if (gameStatus == null) return;

        if (totalQuestions <= 0) return;

        if (progressBuiltFor != totalQuestions)
        {
            gameStatus.Build(totalQuestions);
            progressBuiltFor = totalQuestions;
        }

        gameStatus.SetProgress(questionsAnswered);
    }

    // מציב טקסט שעלול להישבר ליותר משורה אחת (נושא השאלה ותגיות הקצה)
    private void SetWrappedText(TMP_Text target, string text, int maxLength)
    {
        if (target == null) return;

        target.isRightToLeftText = false;

        if (fixHebrewOrder == false)
        {
            target.text = text;
            return;
        }

        target.text = HebrewText.FixLines(text, maxLength);
    }

    private string FixText(string text)
    {
        if (fixHebrewOrder == false) return text;

        return HebrewText.Fix(text);
    }

    // טקסט של אבן תשובה. שוברים לשורות בעצמנו ורק אז הופכים כל שורה,
    // אחרת המשפט מופיע על האבן בסדר מילים אחר מזה שבמחולל
    private string FixRockText(string text)
    {
        if (fixHebrewOrder == false) return text;

        return HebrewText.FixLines(text, rockMaxChars);
    }
}
