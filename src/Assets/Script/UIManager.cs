using UnityEngine;

// הסקריפט הזה אחראי רק על ניהול מסכי ה-UI ברמה הבסיסית:
// הוא מחליף בין מסך התפריט הראשי למסך המשחק, וכשמתחילים משחק
// הוא קורא ל-GameManager כדי לאתחל את המשחק עצמו.
// כלומר: UIManager מטפל ב"מה רואים על המסך", ו-GameManager מטפל ב"מה קורה במשחק".
public class UIManager : MonoBehaviour
{
    // הפניות לפאנלים השונים במשחק:
    // חיברנו אותן דרך ה-Inspector כדי שנוכל להדליק/לכבות מסכים בלי לחפש אותם בקוד.
    [SerializeField] private GameObject mainMenuPanel;  // פאנל התפריט הראשי
    [SerializeField] private GameObject gamePanel;      // פאנל המשחק עצמו
    [SerializeField] private GameManager gameManager;   // הפניה למנהל המשחק (כדי להתחיל משחק חדש)

    // הפונקציה הזאת נקראת מהכפתור "התחל משחק" (דרך OnClick).
    // כאן אנחנו עושות את המעבר מהתפריט לתוך המשחק בפועל.
    public void StartGame()
    {
        // קודם מסתירים את מסך התפריט הראשי כדי שלא יהיה חפיפה עם המשחק
        if (mainMenuPanel != null) 
            mainMenuPanel.SetActive(false);
        
        // מציגים את מסך המשחק (הפאנל של המשחק עצמו)
        if (gamePanel != null) 
            gamePanel.SetActive(true);

        // מבקשים מה-GameManager להתחיל משחק חדש:
        // כלומר לא רק להציג UI, אלא גם לאפס נתונים (חיים/טעויות/שאלה ראשונה וכו').
        if (gameManager != null)
            gameManager.StartNewGame();
    }
}