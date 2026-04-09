using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;


// הסקריפט הזה הוא ה"מנהל" של המשחק: הוא מחליט איזה מסך מוצג, איזו שאלה נטענת,
// כמה זמן נשאר, כמה טעויות וחיים יש לשחקן, ומתי עוברים שאלה/מנצחים/מפסידים.
// הרעיון שלנו היה שכל הלוגיקה המרכזית תהיה במקום אחד כדי שיהיה קל לשלוט בזרימה של המשחק.
public class GameManager : MonoBehaviour
{
    #if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CloseWindow();

        [DllImport("__Internal")]
        private static extern void RedirectHome();
    #endif
    // מחלקה קטנה שמטרתה לייצג חיים עם 2 מצבים (דלוק/כבוי).
    // בפועל אצלנו החיים מוצגים דרך רשימת lifeMenu, אבל השארנו את המבנה הזה כתשתית מסודרת.
    [System.Serializable] public class Life
	{
		[SerializeField] Sprite On;
		[SerializeField] Sprite Off;
	}

    // AnswerData מייצגת תשובה אחת למשחק:
    // יכולה להיות טקסט, יכולה להיות תמונה, או שילוב.
    // שמרנו פה גם משתנה פנימי slotNumberPlace (כרגע פרטי ולא בשימוש גלוי בקוד),
    // כדי לשמור אפשרות עתידית לשייך תשובה למיקום/סלוט מסוים בלי לחשוף את זה החוצה.
    [System.Serializable] public class AnswerData
    {  
        public string answerTextContent;
        public Sprite answerImageContent;
        private int slotNumberPlace;
    }

    // GameData מייצגת "שאלה" במשחק:
    // שם/נושא, תגיות לקצוות (ימין/שמאל), זמן מוקצב לשאלה, ורשימת תשובות.
    // כך אנחנו יכולות להוסיף/לשנות שאלות דרך האינספקטור בלי לשנות קוד.
    [System.Serializable] 
    public class GameData
    {
        [Header("Game Name")]
        public string gameName;
        
        [Header("Edge Tags")]
        public string edgeTagLeft = "";            // טקסט שמופיע בקצה השמאלי של המסלול
        public string edgeTagRight = "";           // טקסט שמופיע בקצה הימני של המסלול
        
        [Header("Timer")]
        public int questionTime;                   // כמה שניות יש לשאלה הזו
        [Header("Q&A")] 
        public List<AnswerData> answersList;        // כל התשובות של השאלה (סלעים)
    }

    // כל המסכים במשחק: פתיחה/משחק/השהייה/ניצחון/הפסד.
    // אנחנו מכבות/מדליקות אותם כדי לשלוט במצבים השונים של המשחק.
    [Header("Screens")]
    [SerializeField] private GameObject startScreen;
    [SerializeField] private GameObject gameScreen;
    [SerializeField] private GameObject pauseScreen;
	[SerializeField] private GameObject winScreen;
	[SerializeField] private GameObject loseScreen;

    // כל רכיבי ה-UI שמציגים מידע במהלך המשחק:
    // נושא השאלה, זמן, טעויות, התקדמות, תצוגת חיים ותצוגת טיימר ויזואלית, וטקסט סיכום.
	[Header("UI")]
    [SerializeField] private TextMeshPro subjectText; 
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text mistakesText;
    [SerializeField] private TMP_Text progressText;
 	[SerializeField] private List<SpriteRenderer> lifeMenu;   // אייקונים של חיים (מכבים לפי ירידת חיים)
	[SerializeField] private List<SpriteRenderer> timerMenu;  // אייקונים/מצבים ויזואליים לטיימר (רגיל/אזהרה/דחוף)
	[SerializeField] private TMP_Text sumText;                // מסך סיכום בניצחון
	

    // טקסטים של תגיות הקצוות שמתחלפים בכל שאלה לפי הנתונים.
    [Header("Edge Tags")]
    [SerializeField] private TMP_Text edgeTagLeftText;
    [SerializeField] private TMP_Text edgeTagRightText;

    // פריפאב לסלעים דקורטיביים: כשיש פחות תשובות ממספר הסלוטים,
    // אנחנו ממלאות את הסלוטים העודפים בסלעי נוי כדי שהמסלול יראה מלא.
    [Header("Prefabs")]
    [SerializeField] private GameObject decorRockPrefab;

    // רשימת הסלעים שהם התשובות (האובייקטים שאפשר לגרור/למקם).
    // אנחנו מציגות/מסתירות אותם לפי מספר התשובות בשאלה הנוכחית.
    [SerializeField] private List<AnswerRock> answerRocks;
	
    // הסלוטים במסלול: לכל סלוט יש SlotIndex, ולסלע נכון יש RockValue תואם.
    // כך אנחנו בודקות האם הסלע הונח במקום הנכון.
    [Header("Slots")]
    [SerializeField] private List<Slot> slots;

    // משוב על טעויות: שכבה אדומה שמופיעה לשנייה, וסאונד לטעות/נכון/רקע (אם קיים).
    [Header("Feedback")]
    [SerializeField] private GameObject redOverlay;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip wrongClip;
    [SerializeField] private AudioClip rightClip;
    
    // כפתורים שמאפשרים להשתיק/להפעיל סאונד תוך כדי משחק (שני מצבים של כפתור).
    [Header("Sounds Buttons")]
    [SerializeField] private GameObject playSoundBTN;
    [SerializeField] private GameObject muteSoundBTN;

    // השחקן: בכל שאלה אנחנו מאפסות אותו למיקום ההתחלתי כדי להתחיל מסודר.
    [Header("Player")]
    [SerializeField] private PlayerController player;

    // כל השאלות של המשחק נמצאות כאן ברשימה.
    // אנחנו גם מערבבות אותן כדי שהסדר יהיה שונה בכל משחק.
    [Header("Game Data")]
    [SerializeField] private List<GameData> allGames;

	// startingLives = כמה חיים יש בתחילת משחק.
	// spawnedDecorRocks = רשימה של הסלעים הדקורטיביים שיצרנו, כדי שנוכל למחוק אותם בין שאלות.
	private int startingLives = 3;
	private List<GameObject> spawnedDecorRocks = new List<GameObject>();

    // canPlay = "האם מותר לשחק עכשיו?"
    // אנחנו משתמשות בזה כדי לחסום אינטראקציות בזמן סוף שאלה/טעינה/מסכים.
    public bool canPlay { get; private set; }

    // currentGameIndex = באיזו שאלה אנחנו כרגע.
    // answersCount = כמה תשובות יש בשאלה הנוכחית (כדי להפעיל רק חלק מהסלעים).
    private int currentGameIndex;
    private int answersCount;

    // nextSlotIndex = לאיזה סלוט אנחנו רוצים להדגיש Outline הבא (כדי להדריך את השחקן).
    private int nextSlotIndex ;

    // mistakes = ספירת טעויות כללית.
    // lives = כמה חיים נשארו.
    // timerRoutine = שומרת את הקורוטינה של הטיימר כדי לעצור/להתחיל מחדש.
    // winner = האם השחקן סיים את כל השאלות.
    // playSound = מצב סאונד (אנחנו הפכנו את השמות כדי להתאים לכפתורי UI: כשהוא true אנחנו משמיעות בפועל).
    private int mistakes;
    private int lives;
    private Coroutine timerRoutine;
    private bool winner;
    private bool playSound = true;

    // פונקציה שמכבה את כל המסכים כדי שלא יהיו שני מסכים פתוחים יחד,
    // וגם מנקה סלעי דקור שנוצרו דינאמית כדי שלא יישארו בין מצבים/מסכים.
    private void hideAllScreens()
    {
	    gameScreen.SetActive(false);
	    pauseScreen.SetActive(false);
	    startScreen.SetActive(false);
	    winScreen.SetActive(false);
        loseScreen.SetActive(false);

        for (int i = 0; i < spawnedDecorRocks.Count; i++)
	    {
		    if (spawnedDecorRocks[i] != null)
			    Destroy(spawnedDecorRocks[i]);
	    }
	    spawnedDecorRocks.Clear();
	    ResetAllSlots();
    }

    // בהתחלה אנחנו תמיד מציגות את מסך הפתיחה בלבד.
    // לפני זה מכבות הכל כדי להתחיל "נקי".
    private void Start()
    {
	    hideAllScreens();
	    startScreen.SetActive(true);

    }

    // מתחילה משחק חדש:
    // מאפסות טעויות וחיים, מאפשרות משחק, מתחילות מהשאלה הראשונה,
    // מציגות את שם הנושא, מאפסות תצוגת חיים, וטוענות את השאלה.
    public void StartNewGame()
    {
	    gameScreen.SetActive(true);
        mistakes = 0;
        lives = startingLives;
        canPlay = true;
        currentGameIndex = 0;
        winner = false;

        // מציג את נושא השאלה/המשחק על המסך
        subjectText.text = allGames[currentGameIndex].gameName;

		resetLifeMenu();
		LoadQuestion(currentGameIndex);
    }

    // מאפסת את תצוגת החיים בתחילת משחק (כל האייקונים דלוקים).
    void resetLifeMenu()
    {
	    for (int i = 0; i < lifeMenu.Count; i++ )
		    lifeMenu[i].enabled = true;
    }

    // לחיצה על "Play Sound" מבחינת UI:
    // כאן אנחנו מעדכנות משתנה ומחליפות כפתורים כדי לשקף את מצב הסאונד.
    public void OnPlaySound()
    {
	    playSound = false;
	    playSoundBTN.SetActive(false);
	    muteSoundBTN.SetActive(true);
    }

    // לחיצה על "Mute Sound" מבחינת UI:
    // מחזירות מצב סאונד ומחליפות חזרה את הכפתורים.
    public void OnMuteSound()
    {
	    playSound = true;
	    muteSoundBTN.SetActive(false);
	    playSoundBTN.SetActive(true);

    }

    // כפתור Pause:
    // מכבות את כל המסכים ומציגות רק את מסך ההשהייה.
    public void OnPauseGameClick()
    {
	    Debug.Log("OnPauseGameClick");
        canPlay = false;
	    hideAllScreens();
	    pauseScreen.SetActive(true);
    }

    // כפתור התחלת משחק חדש מהתפריט:
    // מנקות מסכים, מערבבות שאלות כדי לקבל סדר חדש, ואז מתחילות משחק.
	public void OnStartNewGameClick()
	{
		Debug.Log("OnStartNewGameClick");
		hideAllScreens();
		ShuffleGames();
		StartNewGame();
	}
	
    // חזרה מהשהייה למשחק:
    // מציגות מסך משחק, מערבבות שאלות (כדי ליצור רענון),
    // מאפסות אינדקס שאלה, מציגות נושא, ואז טוענות שאלה.
	public void OnBacktoGameClicked()
	{
   		Debug.Log("OnBacktoGameClicked!");
	    hideAllScreens();
	    gameScreen.SetActive(true);
	    ShuffleGames();
	    canPlay = true;
	    currentGameIndex = 0;

	    // מציג את הנושא
	    subjectText.text = allGames[currentGameIndex].gameName;

	    LoadQuestion(currentGameIndex);
	}

    // Restart מחזיר למסך פתיחה (לא מאתחל את כל הנתונים דרך StartNewGame פה,
    // אלא פשוט חוזר למסך ההתחלתי).
	public void OnRestartClicked()
	{
		Debug.Log("OnRestartClicked!");
		hideAllScreens();
		startScreen.SetActive(true);
	}

    // יציאה מהמשחק:
    // אם אנחנו בתוך Unity Editor, עוצר Play Mode,
    // ואם זו גרסה בנויה (Build) אז יוצאים מהאפליקציה.
	public void OnExitClicked()
	{
		Debug.Log("OnExitClicked!");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        // Try close (works only if opened via script)
        CloseWindow();

        // Fallback: redirect
        RedirectHome();
#else
        Application.Quit();
#endif

    }

    // מערבבת את רשימת השאלות (Fisher-Yates בסגנון פשוט עם Random.Range),
    // כדי שבכל הרצה השאלות יופיעו בסדר שונה.
    private void ShuffleGames()
	{
    	for (int i = 0; i < allGames.Count; i++)
    	{
        	int randomIndex = Random.Range(i, allGames.Count);

        	GameData temp = allGames[i];
        	allGames[i] = allGames[randomIndex];
        	allGames[randomIndex] = temp;
    	}
	}

    // טוענת שאלה לפי אינדקס:
    // אם עברנו את כמות השאלות – מסמנות ניצחון ומסיימות משחק.
    // אחרת: מאפסות שחקן, קוראות נתונים, מעדכנות UI, מפעילות סלעים ומתחילות טיימר.
    private void LoadQuestion(int gameIndex)
    {
        if (gameIndex >= allGames.Count)
        {
            //TODO: Pause timer before showing win screen, and show total time on win screen
            canPlay = false;
            Debug.Log("סיימת את כל השאלות!");
            winner = true;
            EndGame();
            return;
        }
        
        // מאפסות את השחקן לתחילת מסלול כדי להתחיל כל שאלה מאותה נקודה
        player.RestePos();
        player.ResetInteractionState();

        // שומרות רפרנס לשאלה הנוכחית מתוך הרשימה
        GameData currentGame = allGames[gameIndex];
        
        // כמה תשובות יש בשאלה הזו – זה קובע כמה סלעים יהיו פעילים
        answersCount = currentGame.answersList.Count;

        // מעדכנות טקסטים של תגיות הקצה (אם חוברו)
        if (edgeTagLeftText != null)
            edgeTagLeftText.text = currentGame.edgeTagLeft;

        if (edgeTagRightText != null)
            edgeTagRightText.text = currentGame.edgeTagRight;

        // מטעינות את הטקסט/תמונה לכל סלע תשובה,
        // מפעילות רק את הסלעים שצריך,
        // וממלאות סלוטים עודפים בסלעי דקור.
        SetupRocksContent(currentGame);
        SetupAnswerRocks();
        SetupDecorSlots();

        // מאפסות תצוגה ויזואלית של "מצב טיימר" בתחילת שאלה
		timerMenu[0].enabled = true;
        timerMenu[1].enabled = false;
		timerMenu[2].enabled = false;

        UpdateUI();
        StartTimer();
    }
   
    // ערבוב מיקומי הסלעים:
    // לוקחות את מיקומי הבסיס של כל סלע, מערבבות אותם,
    // ואז משבצות מחדש כדי שהסלעים לא יופיעו תמיד באותו סדר על המסך.
	private void ShuffleRockPositions()
	{
    	List<Vector3> positions = new List<Vector3>();

    	for (int i = 0; i < answerRocks.Count; i++)
    	{
        	positions.Add(answerRocks[i].basePos);
    	}

    	for (int i = 0; i < positions.Count; i++)
    	{
        	int randomIndex = Random.Range(i, positions.Count);

        	Vector3 temp = positions[i];
        	positions[i] = positions[randomIndex];
        	positions[randomIndex] = temp;
    	}

    	for (int i = 0; i < answerRocks.Count; i++)
    	{
        	answerRocks[i].transform.position = positions[i];

            // מעדכנות לסלע את "מיקום ההתחלה החדש" כדי שאם הוא חוזר אחורה,
            // הוא יחזור למקום הנכון לאחר הערבוב.
			answerRocks[i].UpdateStartPosition();
			
    	}
	}

    // מטעינה תוכן לכל סלע תשובה מתוך הנתונים של השאלה:
    // קודם מאפסות את הסלע (שלא יישאר משהו מהשאלה הקודמת),
    // ואז שולפות טקסט/תמונה לפי אינדקס.
    // RockValue נקבע כ-(i+1) כדי להתאים לסלוטים שמוגדרים עם SlotIndex.
    private void SetupRocksContent(GameData currentGame)
    {
        for (int i = 0; i < answersCount && i < answerRocks.Count; i++)
        {
            // מאפסות תוכן קודם (טקסט/תמונה/מצבים) כדי לא "לסחוב" מידע משאלה קודמת
            answerRocks[i].ResetContent();  
            
            string text = (i < currentGame.answersList.Count) ? currentGame.answersList[i].answerTextContent : "";
            Sprite sprite = (i < currentGame.answersList.Count && currentGame.answersList[i].answerImageContent != null ) 
                ? currentGame.answersList[i].answerImageContent
                : null;
            
            // RockValue הוא i+1 (1-8) – זה בעצם "התשובה הנכונה" לפי הסדר
            answerRocks[i].SetContent(text, sprite,(i+1));
        }

        // אחרי שהתוכן נטען אנחנו מערבבות את מיקום הסלעים כדי שלא יהיה סדר קבוע
		ShuffleRockPositions();
    }

    // מפעילה/מכבה את סלעי התשובות לפי answersCount:
    // רק הסלעים הדרושים בשאלה יהיו פעילים, והשאר יוסתרו.
    // כשסלע לא פעיל אנחנו גם מנקות לו currentSlot כדי שלא יישמר מצב ישן.
    private void SetupAnswerRocks()
    {
        for (int i = 0; i < answerRocks.Count; i++)
        {
            if (answerRocks[i] == null) continue;
            
            bool active = (i < answersCount);
            
            answerRocks[i].gameObject.SetActive(active);

            if (!active)
                answerRocks[i].currentSlot = null;
        }
    }

    // ממלאת את הסלוטים העודפים בסלעי דקור:
    // קודם מוחקות דקור קודם אם קיים, ואז יוצרות (Instantiate) דקור בכל סלוט שאין לו תשובה.
    // בנוסף: מאתחלות את nextSlotIndex ומפעילות Outline על הסלוט הראשון כדי להכווין את השחקן.
    private void SetupDecorSlots()
	{
    	if (slots == null) return;

    	for (int i = 0; i < spawnedDecorRocks.Count; i++)
    	{
        	if (spawnedDecorRocks[i] != null)
           		Destroy(spawnedDecorRocks[i]);
    	}
    	spawnedDecorRocks.Clear();

    	for (int i = answersCount; i < slots.Count; i++)
    	{
        	if (slots[i] == null) continue;

            // יוצרים סלע דקור במקום הסלוט (כדי שהמסלול יראה מלא)
        	GameObject decor = Instantiate( decorRockPrefab,slots[i].transform.position,Quaternion.identity);
        	spawnedDecorRocks.Add(decor);
    	}

        // מסמנים שהסלוט הבא למילוי הוא הראשון
		nextSlotIndex = 0;

        // מפעילים Outline על הסלוט הראשון כדי לסמן לשחקן איפה כדאי להניח עכשיו
    	if (nextSlotIndex < slots.Count && slots[nextSlotIndex] != null)
        	slots[nextSlotIndex].ActivateOutline();
	}

    // מפעילה טיימר מחדש:
    // אם כבר רץ טיימר קודם – עוצרות אותו כדי שלא ירוצו שני טיימרים במקביל.
    private void StartTimer()
    {
        if (timerRoutine != null)
            StopCoroutine(timerRoutine);

        timerRoutine = StartCoroutine(TimerCoroutine());
    }

    // עדכון תצוגת החיים:
    // לפי מספר החיים שנשארו, אנחנו מכבות אייקון מתאים מתוך lifeMenu.
    // זה נותן חיווי ויזואלי שהשחקן "נפסל" חלקית.
	private void UpdateLifeMenu()
	{
		int index = 0;
		if (lives == startingLives) return;
		if (lives == 2) index  = 0;
		if (lives == 1) index  = 2;
		if (lives == 0) index  = 4;

		lifeMenu[index].enabled = false;
	}

    // קורוטינה של הטיימר:
    // לוקחת זמן מהשאלה הנוכחית ומורידה אותו כל פריים (Time.deltaTime).
    // במהלך הספירה אנחנו גם מחליפות מצב של timerMenu כדי להראות שהזמן אוזל.
    // אם הזמן נגמר – אנחנו מוסיפות טעות, מורידות חיים, נותנות סאונד/אדום ואז מסיימות.
    private IEnumerator TimerCoroutine()
    {
        float t = allGames[currentGameIndex].questionTime;

        if (timerText != null)
            timerText.text = Mathf.CeilToInt(t).ToString();

        while (t > 0f && canPlay)
        {
            t -= Time.deltaTime;

            if (timerText != null)
                timerText.text = Mathf.CeilToInt(t).ToString();

            // "אזהרה" כשמתקרבים לסוף הזמן
			if( t < 10 && t > 5)
			{
 				timerMenu[0].enabled = false;
                timerMenu[1].enabled = true;
				timerMenu[2].enabled = false;
			}

            // "דחוף" ממש כשנשאר מעט זמן
			if( t < 5 )
			{
 				timerMenu[0].enabled = false;
                timerMenu[1].enabled = false;
				timerMenu[2].enabled = true;
			}
            yield return null;
        }

        // אם המשחק הוקפא/הסתיים באמצע – לא ממשיכות ל"עונש" של סיום זמן
        if (!canPlay) yield break;

        // time up
         EndGame();

        /* TODO: check if needed
        // זמן נגמר: חוסמות משחק כדי שלא ימשיכו לשים סלעים
        canPlay = false;
      
        // מסמנות את זה כטעות ומורידות חיים
        mistakes++;
        lives--;
		UpdateLifeMenu();
        
        UpdateUI();
        // משוב סאונד + משוב אדום
        if (playSound && sfxSource && wrongClip)
            sfxSource.PlayOneShot(wrongClip);

        if (redOverlay)
            StartCoroutine(FlashRed());
        
        // כרגע בכל מקרה מסיימות את המשחק כאשר זמן נגמר (גם אם נשארו חיים),
        // כי זה מוגדר אצלנו כחוק משחק.
        if (lives <= 0)
            EndGame();
        else 
			EndGame();
        */
        
    }

    // הדגשת הסלוט הבא:
    // מורידות Outline מהסלוט הקודם, מעלות אינדקס,
    // ואם עברנו את הסוף – חוזרות להתחלה, ואז מפעילות Outline על הסלוט החדש.
    private void HighlightNextSpot()
    {
        slots[nextSlotIndex].DeactivateOutline();
        nextSlotIndex++;
        if (nextSlotIndex  >= slots.Count)
        {
			nextSlotIndex = 0;
        }
	    slots[nextSlotIndex].ActivateOutline();

    }

    // נקראת כאשר סלע הונח בסלוט:
    // פה אנחנו בודקות אם ההנחה נכונה (RockValue מול SlotIndex).
    // אם נכון – שומרים את הקשר בין הסלוט לסלע, משמיעים סאונד, ובודקים אם השאלה נפתרה.
    // אם לא נכון – מוסיפים טעות, מורידים חיים, מפעילים משוב, ומחזירים סלע להתנהגות "שגויה".
    public void OnRockPlaced(AnswerRock rock, Slot slot)
    {
        if (!canPlay || rock == null || slot == null)
            return;

        bool correct = (rock.RockValue == slot.SlotIndex);

        if (correct)
        {
            // שומרות שהסלוט תפוס ע"י הסלע הזה, וגם הסלע יודע באיזה סלוט הוא נמצא
            slot.currentRock = rock;
            rock.currentSlot = slot;
            
            if(playSound && sfxSource && rightClip)
	            sfxSource.PlayOneShot(rightClip);
           	
            // אם כל הסלוטים הרלוונטיים מלאים נכון – השאלה הסתיימה
            if (IsQuestionSolved())
            {
                canPlay = false;

                // עוברים לשאלה הבאה
                currentGameIndex++;
                
                if (currentGameIndex >= allGames.Count)
                {
                    // אם אין יותר שאלות – ניצחון
                    nextSlotIndex=0;
                    Debug.Log("כל הכבוד! סיימת את כל השאלות!");
                    EndGame();
                }
                else
                {
                    // נותנות רגע קטן של "נשימה" לפני טעינת השאלה הבאה
                    StartCoroutine(LoadNextQuestionAfterDelay(1f));
                }
            }
			else
			{
                // אם עדיין לא סיימנו, מסמנות לשחקן איפה לשים את הסלע הבא
				HighlightNextSpot();
        	}
		}
        else
        {
            // סלע הונח במקום לא נכון:
            // מוסיפות טעות, מורידות חיים, מעדכנות UI ומשובים.
            mistakes++;
            lives--;
			UpdateLifeMenu();
			
            UpdateUI();
    
            if (playSound && sfxSource && wrongClip)
                sfxSource.PlayOneShot(wrongClip);

            if (redOverlay)
                StartCoroutine(FlashRed());

            // משוב ספציפי לסלע עצמו (למשל חזרה אחורה/רטט/צביעה) – נמצא בסקריפט של AnswerRock
            rock.PlayWrongFeedback();

            // אם נגמרו חיים – סיום משחק
            if (lives <= 0)
                EndGame();
        } 
    }

    // טוענת שאלה חדשה אחרי השהייה קצרה:
    // קודם מחכות, מאפסות סלוטים, מאפשרות משחק מחדש, ואז טוענות את השאלה הבאה.
    private IEnumerator LoadNextQuestionAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // איפוס הסלוטים כדי שלא יישארו "תפוסים" מהשאלה הקודמת
        ResetAllSlots();
        
 		canPlay = true;

        // טעינת שאלה חדשה לפי currentGameIndex שכבר הוגדל
        LoadQuestion(currentGameIndex);
    }

    // משוב אדום קצר על המסך:
    // מפעילות אובייקט אדום לשנייה ואז מכבות אותו.
    private IEnumerator FlashRed()
    {
        redOverlay.SetActive(true);
        yield return new WaitForSeconds(1f);
        redOverlay.SetActive(false);
    }
    
    // בודקת אם השאלה נפתרה:
    // עוברת רק על מספר הסלוטים הרלוונטיים (answersCount),
    // ובודקת: יש סלוט? יש בו סלע? והאם ערך הסלע מתאים לאינדקס הסלוט.
	private bool IsQuestionSolved()
    {
        for (int i = 0; i < answersCount; i++)
        {
            if (slots[i] == null) return false;
            if (slots[i].currentRock == null) return false;
            if (slots[i].currentRock.RockValue != slots[i].SlotIndex) return false;
        }

        return true;
    }
    
    // מאפס את כל הסלוטים בין שאלות:
    // מכבה Outline שהיה דלוק, מחזירה nextSlotIndex ל-0, ומנקה currentRock מכל סלוט.
	private void ResetAllSlots()
    {
        if (slots == null) return;	
		if (nextSlotIndex < slots.Count)
        	slots[nextSlotIndex].DeactivateOutline();
        nextSlotIndex = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].currentRock = null;
        }

    }
    
    // מעדכנת את ה-UI הבסיסי:
    // טקסט טעויות + טקסט התקדמות (מספר שאלה נוכחית מתוך סך הכל).
	private void UpdateUI()
    {
		if (mistakesText)
            mistakesText.text =  mistakes+ " :תויועט " ;

        if (progressText)
            progressText.text = (currentGameIndex + 1) + "/" + allGames.Count;
 		
    }

    // סיום משחק:
    // עוצרות את המשחק, עוצרות טיימר אם רץ, מנקות מסכים,
    // ומציגות או מסך ניצחון (עם סיכום) או מסך הפסד.
    private void EndGame()
    {
        canPlay = false;

        if (timerRoutine != null)
            StopCoroutine(timerRoutine);

        hideAllScreens();

        if (winner)
        {
            // מסכמות למשתמש: זמן כולל ומספר טעויות
            sumText.text = "זמן כולל:" + timerText.text + "\r\n" + " מספר טעויות:" + mistakes;
            winScreen.SetActive(true);
        }
        else
            loseScreen.SetActive(true);

    }

    // פונקציה עזר שמחזירה כמה סלוטים כבר תפוסים בשאלה הנוכחית:
    // עוברת רק על הסלוטים הרלוונטיים (answersCount) וסופרת כמה מהם מכילים currentRock.
    public int GetOccupiedSlotsCount()
    {
        if (slots == null) return 0;

        int count = 0;

        for (int i = 0; i < answersCount; i++)
        {
            if (i < slots.Count && slots[i] != null && slots[i].currentRock != null)
            {
                count++;
            }
        }

        return count;
    }

    // פונקציה עזר שמחזירה את ה-SlotIndex של הסלוט האחרון שתפוס בשאלה הנוכחית:
    // אם אין סלוט תפוס – מחזירה -1.
    public int GetLastOccupiedSlotIndex()
    {
        if (slots == null) return -1;

        int lastIndex = -1;

        for (int i = 0; i < answersCount && i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].currentRock != null)
            {
                lastIndex = slots[i].SlotIndex;
            }
        }

        return lastIndex;
    }
}
