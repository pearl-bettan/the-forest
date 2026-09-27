using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// מנהל המשחק

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

    // סצנת סרטון הניצחון
    [SerializeField] string winVideoScene = "WinVideo";

    //מספר השניות שרואים את המסך האחרון לפני המעבר לסצנת הסיום
    [SerializeField] float endSceneDelay = 1.5f;

    // גובה המצלמה בזמן אנימציית הדילוג
    [SerializeField] float skipTopMargin = 0.3f;
    [SerializeField] float skipMaxZoomOut = 2f;

    [Header("Intro")]
    [SerializeField] CameraScript gameCamera;

    // כמה שניות רואים את האגם בתחילת כל שאלה, לפני שהטיימר מתחיל
    [SerializeField] float introLakeTime = 3;

    //מספר השניות שרואים את המסך לפני שהמצלמה זזה לכיוון האגם
    [SerializeField] float introStartDelay = 1;

    // ההודעה שמסמנת לשחקן שהטיימר התחיל לרוץ

    [Header("Objects")]
    [SerializeField] DwarfScript dwarf;

    // האבנים שנמצאות בסצנה
    [SerializeField] List<RockScript> rocks;

    // מיקומים באגם
    [SerializeField] List<Transform> slots;

    // תצוגת מצב המשחק: הטיימר  השמש, ומד ההתקדמות
    [SerializeField] GameStatus gameStatus;

    //מסך ההגדלה
    [SerializeField] ZoomPanelScript zoomPanel;

    // כפתורי ההגדלה
    [SerializeField] List<GameObject> magnifiers;

    [Header("Free Walk")]
    // אזור הדשא. לחיצה בתוכו שולחת את הגמד לנקודה .
    [SerializeField] Collider2D grassArea;

    [Header("Skipping")]
    // הנקודה שאליה הגמד מדלג בסוף - התגית השמאלית.
    [SerializeField] Transform leftTagPoint;

    [SerializeField] bool spreadSlotsEvenly = true;

    [Header("Texts")]
    [SerializeField] TMP_Text leftTagText;
    [SerializeField] TMP_Text rightTagText;

    [Header("Lives")]
    [SerializeField] List<GameObject> hearts;

    [Header("Sounds")]
    [SerializeField] AudioSource feedbackAudio;
    [SerializeField] AudioClip correctSound;
    [SerializeField] AudioClip wrongSound;
    [SerializeField] AudioClip stageCompleteSound;

    [Header("Hebrew")]
    // מסדר את סדר האותיות בעברית
    [SerializeField] bool fixHebrewOrder = true;

    // כמה תווים נכנסים בשורה אחת על אבן תשובה  
    [SerializeField] int rockMaxChars = 12;

    // כמה תווים נכנסים בשורה אחת לתגיות שמשמאל ומימין לאגם
    [SerializeField] int tagLineLength = 7;

    // האם מותר ללחוץ על אבן כרגע
    public bool canAnswer;

    private StageData currentStage;

    // מאגר השאלות שעוד לא נענו נכון
    private List<StageData> questionPool;

    private int totalQuestions;

    // כמה שאלות כבר נענו נכון
    private int questionsAnswered;

    // מיקום התשובה הנכונה הבאה 
    private int nextSlot;

    private float stageTimer;
    private int livesLeft;
    private bool gameOver;

    // עצירת הטיימר
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

    // הגנה מפני לחיצות חוזרות על כפתור עצירת המשחק
    private bool pauseRequested;

    // הגמד באמצע אנימציית הדילוג בסיום שאלה
    private bool skipping;

    // המיקומים שסודרו בסצנה
    private List<Vector3> manualSlotPositions;

    // ההפעלה של המשחק
    void Start()
    {
        BuildZoomPanel();
        SaveManualSlotPositions();
        CheckSetup();
        GetGame();
        StartStage();
    }

    // בתחילת כל שאלה המצלמה נוסעת לאגם ועומדת שם כמה שניות,
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

        //הודעה במקרה שבו לא נטען משחק
        if (game == null || game.stagesList == null || game.stagesList.Count == 0)
        {
            Debug.LogError("No game data. Enter a game code in the Home scene, " +
                           "or fill Game in the GameManager Inspector");
            return;
        }

        // חזרה מהשהייה
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

        // מספר החיים   
        livesLeft = GameRules.Lives;

        // לא יכולים להציג יותר לבבות ממה שיש בסצנה
        if (hearts != null && hearts.Count > 0 && livesLeft > hearts.Count)
        {
            Debug.LogWarning("There are only " + hearts.Count + " hearts in the scene but the game " +
                             "needs " + GameRules.Lives + ". Add hearts to GameManager > Hearts");
            livesLeft = hearts.Count;
        }

        // חזרה מהשהייה 
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

    // בדיקת תקינות האוביקטים 
    private void CheckSetup()
    {
        string missing = "";

        if (dwarf == null) missing = missing + "Dwarf, ";
        if (rocks == null || rocks.Count == 0) missing = missing + "Rocks, ";
        if (slots == null || slots.Count == 0) missing = missing + "Slots, ";
        if (gameStatus == null) missing = missing + "Game Status, ";
        if (magnifiers == null || magnifiers.Count == 0) missing = missing + "Magnifiers, ";
        if (leftTagText == null) missing = missing + "Left Tag Text, ";
        if (rightTagText == null) missing = missing + "Right Tag Text, ";
        if (hearts == null || hearts.Count == 0) missing = missing + "Hearts, ";

        if (missing != "")
        {
            Debug.LogWarning("Empty fields in GameManager: " + missing);
        }
        // חיפוש המצלמה
        if (gameCamera == null)
        {
            gameCamera = Object.FindFirstObjectByType<CameraScript>();

            if (gameCamera == null)
            {
                Debug.LogWarning("No CameraScript in the scene - the lake intro will not run. " +
                                 "Add Camera Script to the Main Camera and drag it into GameManager > Game Camera");
            }
        }

        // חיבור הגמד למנהל המשחק ולמצלמה
        if (dwarf != null)
        {
            dwarf.gameManager = this;
            if (dwarf.gameCamera == null) dwarf.gameCamera = gameCamera;
        }
    }

    // הלולאה הראשית של המשחק, רצה בכל פריים.
 
    void Update()
    {
        //אם השאלה לא התחילה - אין מה לעדכן
        if (currentStage == null) return;

        // לחיצה על הדשא שולחת את הגמד לנקודה
        HandleGrassClick();

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
                // בניצחון עוברים דרך סצנת הסרטון
               
                if (DataPass.result == "win" &&
                    string.IsNullOrEmpty(winVideoScene) == false &&
                    Application.CanStreamedLevelBeLoaded(winVideoScene) == true)
                {
                    SceneManager.LoadScene(winVideoScene);
                    return;
                }

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

    }

    // לחיצה על הדשא שולחת את הגמד לנקודה 
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

        //נגמר הזמן
        EndGame("timeout");
    }

    // מוריד חיים ומעדכן את מונה השגיאות
    private void CountMistake()
    {
        totalMistakes = totalMistakes + 1;
        livesLeft = livesLeft - 1;
        UpdateHearts();
    }

    // מתחילה שאלה חדשה: איפוס מצב, פיזור האבנים לפי מספר
    // הפריטים, הצגת הנושא ותגיות הקיצון, והפעלת הטיימר.
    
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
            //סיימת את כל השאלות
            EndGame("win");
            return;
        }

        // מגרילים שאלה מתוך המאגר. שאלה שתיענה נכון תצא ממנו,
        // שאלה שתיענה לא נכון תוחזר אליו
        int randomIndex = Random.Range(0, questionPool.Count);
        currentStage = questionPool[randomIndex];
        questionPool.RemoveAt(randomIndex);

        // כותרות
        SetWrappedText(leftTagText, currentStage.leftTag, tagLineLength);
        SetWrappedText(rightTagText, currentStage.rightTag, tagLineLength);

        // טיימר. 0 = ללא הגבלת זמן
        stageTimer = currentStage.stageTime;

        bool noTimeLimit = currentStage.stageTime <= 0;

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

        if (zoomPanel != null) zoomPanel.Hide();

        LayoutSlots();
        ShowSlots();
        SetupRocks();
        UpdateProgressBar();

        if (dwarf != null) dwarf.ResetDwarf();

        canAnswer = false;
        ShowLakeIntro();
    }

    // שמירת המיקומים באגם 
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

    // סידןר המיקומים באגם
    private void LayoutSlots()
    {
        if (slots == null || slots.Count == 0) return;
        if (manualSlotPositions == null || manualSlotPositions.Count == 0) return;

        int answersCount = currentStage.answersList.Count;
        if (answersCount <= 0) return;

        // מספר התשובות המלא
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

    // איחזור סידור המיקומים באגם
    private void RestoreManualSlots()
    {
        for (int i = 0; i < slots.Count && i < manualSlotPositions.Count; i++)
        {
            if (slots[i] == null) continue;

            slots[i].position = manualSlotPositions[i];
        }
    }

    // פונקציית עזר לסידור הציקוצים באגם
    // מחזיר נקודה על המסלול
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

    // הצגת המיקומים באגם לפי מספר התשובות בשאלה הנוכחית
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

                // מחברים לאבן את כפתור ההגדלה 
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

        // ערבוב הרשימה
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

        // התשובה נכונה אם המיקום של האבן תואם למיקום הבא באגם
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

            PlayWrongSound();
        }
    }

    // ============================================================
    // נקראת בכל פעם שהגמד סיים להניח אבן, ומחליטה מה קורה הלאה.
    //
    // שלוש אפשרויות: נגמרו הפסילות, כל האבנים סודרו נכון, או
    // שממשיכים לתור הבא.
    //
    // הבדיקה על skipping מונעת מהשגרה לרוץ שוב בזמן שהגמד
    // באמצע אנימציית הדילוג שמסיימת שאלה מוצלחת
    // ============================================================
    public void TurnFinished()
    {
        if (gameOver == true) return;
        if (skipping == true) return;

        // נגמרו הפסילות
        if (livesLeft <= 0)
        {
            //נגמרו הפסילות
            EndGame("nolives");
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

        // המצלמה עוקבת אופקית בלבד, כדי שהקפיצות לא יטלטלו אותה ומתרחקת כדי שהגמד לא ייחתך בקצה העליון
    
        if (gameCamera != null)
        {
            float highest = path[0].y;

            for (int i = 1; i < path.Count; i++)
            {
                if (path[i].y > highest) highest = path[i].y;
            }

            float needed = highest + dwarf.SkipClearance();

            gameCamera.FollowSidewaysShowing(dwarf.transform, needed,
                                             skipTopMargin, skipMaxZoomOut);
        }

        dwarf.SkipOverRocks(path, AfterSkipping);
    }

    // הנקודה שבה הדילוג נגמר: התגית השמאלית אם חוברה
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
            EndGame("win");
            return;
        }

        //סיום שאלה: אי אפשר לענות עד שהשאלה הבאה מתחילה
        StopPlay();

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
    private void EndGame(string result)
    {
        nextStageTimer = 0;
        timerRunning = false;

        DataPass.result = result;
        DataPass.score = Mathf.RoundToInt(scoreSoFar);
        DataPass.totalTime = gameTime;
        DataPass.mistakes = totalMistakes;

        // כמה פסילות נשארו
        DataPass.livesLeft = livesLeft;

        DataPass.questionsAnswered = questionsAnswered;
        DataPass.questionsTotal = totalQuestions;

        StopPlay();

        if (endScene != "")
        {
            endSceneTimer = endSceneDelay;
        }
    }

    // עוצרת את המשחק: אי אפשר לענות יותר, והשאלה נחשבת סגורה
    // נקראת בסיום שאלה ובסיום משחק. בלעדיה אפשר ללחוץ על אבנים בזמן שהמשחק כבר עבר שלב
    private void StopPlay()
    {
        gameOver = true;
        canAnswer = false;
    }

    // שלוש שגרות הצליל מסתמכות על מנהל הסאונד, שקיים לאורך כל המשחק
    private void PlayCorrectSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCorrect();
            return;
        }

        PlaySound(correctSound);
    }

    // צליל תשובה שגויה
    private void PlayWrongSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWrong();
            return;
        }

        PlaySound(wrongSound);
    }

    // צליל סיום שלב מוצלח
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

        // השאלה חוזרת למאגר ותישאל שוב
        // מה כן נשמר ומה לא: הלבבות שאבדו נשמרים
        // וכך גם הזמן, הניקוד וסופר הטעויות
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

    // מעדכנת את תצוגת הפסילות: מדליקה לב לכל פסילה שנותרה
    // ומכבה את השאר
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

    // המד מייצג את שאלות המשחק: אייקון מעבר שלב לכל שאלה, וכל שאלה שנענתה
    // הופכת אייקון מעבר שלב אחד מאפור לירוק
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

    // מתקן את טקסט האבן לפי סדר האותיות בעברית
    private string FixRockText(string text)
    {
        if (fixHebrewOrder == false) return text;

        return HebrewText.FixLines(text, rockMaxChars);
    }
}