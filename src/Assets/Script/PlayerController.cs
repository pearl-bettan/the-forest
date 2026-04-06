using System.Collections.Generic;
using UnityEngine;

// הסקריפט הזה אחראי על הדמות של השחקן: תנועה עם מקשים,
// איסוף/נשיאת סלע, הנחת סלע בסלוט מתאים, והגבלות תנועה (לא להיכנס לאגם לפני שמותר).
// המטרה שלנו הייתה שהשחקן יהיה "הידיים" של המשתמש: הוא מתקרב לסלעים, מרים אותם עם רווח,
// ומניח אותם במסלול בסדר הנכון, ובמקביל המשחק נשאר בשליטה של ה-GameManager.
public class PlayerController : MonoBehaviour
{
    [Header("References (Drag in Inspector)")]
    // הפניות שחיברנו דרך האינספקטור כדי שלא נצטרך לחפש אובייקטים בזמן ריצה:
    // gameManager - כדי לשאול אם מותר לשחק, ולדווח כשסלע הונח.
    // carryAnchor - נקודה על השחקן שאליה "נצמיד" את הסלע בזמן שהוא נישא.
    // cam - מצלמה כדי להמיר מיקום לעולם/viewport ולהגביל תנועה בתוך המסך.
    [SerializeField] private GameManager gameManager;  // הפניה למנהל המשחק
    [SerializeField] private Transform carryAnchor;    // נקודת העיגון שבה נישא את הסלע
    [SerializeField] private Camera cam;               // המצלמה - בשביל חישובי המסך

    [Header("Movement")]
    // מהירות תנועה של השחקן (אפשר לשנות באינספקטור לפי תחושה).
    [SerializeField] private float moveSpeed = 4f;     // מהירות התנועה של השחקן

    [Header("Allowed Area (Viewport 0..1)")]
    // הגדרת "אזור מותר" למסך: אנחנו לא רוצות שהשחקן יברח מחוץ למסגרת או יעלה גבוה מדי.
    // השתמשנו ב-Viewport (0..1) כדי שזה יעבוד גם אם הרזולוציה משתנה.
    [SerializeField] private float allowedMinY = 0.02f;    // הגבול התחתון של האזור המותר
    [SerializeField] private float allowedMaxY = 0.55f;    // הגבול העליון של האזור המותר
    [SerializeField] private float screenPadding = 0.02f;  // ריווח מקצוות המסך

    [Header("Lake Boundary (World X)")]
    // הגבול בין אזור התשובות/היבשה לבין האגם (בערך X מסוים בעולם).
    // זה מאפשר לנו ליישם כלל משחק: לא נכנסים לאגם לפני שהנחנו סלעים במסלול.
    [SerializeField] private float lakeBoundaryX = 1.0f;   // הגבול שמפריד בין האגם לאזור התשובות

    // רשימה של כל הסלוטים שהשחקן קרוב אליהם כרגע (נאספת דרך Trigger Enter/Exit).
    // במקום לבדוק כל הזמן את כל הסלוטים בסצנה, אנחנו שומרות רק את אלו שבסביבה.
    private readonly List<Slot> nearbySlots = new List<Slot>();

    // highlightedRock - הסלע שהשחקן עומד לידו כרגע ומודגש (כדי להבין שאפשר להרים).
    // carriedRock - הסלע שנישא כרגע על השחקן.
    // lastValidPosition - המיקום האחרון שהיה "חוקי" כדי שנוכל להחזיר את השחקן אחורה אם הוא נכנס לאגם מוקדם מדי.
    // justPlacedRock - דגל קטן שמונע מצב שבו מיד אחרי הנחה המערכת חושבת שהשחקן נכנס לאגם ומחזירה אותו בטעות.
    private AnswerRock highlightedRock;      // הסלע שמודגש כרגע (שהשחקן קרוב אליו)
    private AnswerRock carriedRock;          // הסלע שהשחקן נושא כרגע
    private Vector3 lastValidPosition;       // המיקום האחרון התקין - בשביל מניעת כניסה לאגם
    private bool justPlacedRock = false;     // דגל שמסמן שהשחקן בדיוק הניח סלע
    
    // מיקום בסיס של השחקן לתחילת שאלה/איפוס (כדי להתחיל תמיד מאותה נקודה).
    private Vector3 basePosition = new Vector3(4.5f,-2.65f,0.0f); //המיקום ההתחלתי של השחקן

    // בתחילת המשחק שומרים את המיקום התקין הראשון כדי שיהיה לנו "איפה לחזור".
    private void Start()
    {
        lastValidPosition = transform.position;
    }

    // Update מנהל את ההתנהגות כל פריים:
    // 1) אם המשחק כרגע לא מאפשר לשחק (canPlay=false) - אנחנו לא נותנות לשחקן לזוז/להרים/להניח.
    // 2) תנועה עם חיצים.
    // 3) אם נושאים סלע - מצמידים אותו לנקודת העיגון.
    // 4) בלחיצה על Space: אם אין סלע ביד -> מנסות להרים. אם יש -> מנסות להניח בסלוט הקרוב.
    private void Update()
    {
        if (gameManager != null && !gameManager.canPlay)
            return;

        HandleMovement();

        // כשהשחקן נושא סלע, אנחנו רוצים שהוא "יישב" על השחקן ולא יישאר מאחור.
        if (carriedRock != null && carryAnchor != null)
            carriedRock.transform.position = carryAnchor.position;

        // כפתור פעולה אחד (Space) שעושה Toggle:
        // אם אין לנו סלע ביד - להרים,
        // אם יש - לנסות להניח, ואם לא הצליח אז להחזיר למקום ההתחלה.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (carriedRock == null)
                TryPickUpHighlightedRock();
            else{
             	// Try place on slot
        		if (!TryPlaceOnNearestSlot())
        		{
            		ReturnRockToStart();
        		}
        	}
    	}
	}

    // פונקציה שנקראת מה-GameManager בתחילת כל שאלה,
    // כדי להחזיר את השחקן לנקודת ההתחלה.
    public void RestePos()
    {
        transform.position = basePosition;
    }

    // טיפול בתנועה:
    // קוראות קלט מחיצים, יוצרות כיוון תנועה, מזיזות לפי מהירות וזמן (deltaTime),
    // ואז מגבילות למסך ולוגיקת "אגם".
    private void HandleMovement()
    {
        float x = 0f;
        float y = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))  x = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) x = 1f;
        if (Input.GetKey(KeyCode.UpArrow))    y = 1f;
        if (Input.GetKey(KeyCode.DownArrow))  y = -1f;

        // normalized כדי שלא תהיה תנועה מהירה יותר באלכסון
        Vector3 dir = new Vector3(x, y, 0f).normalized;
        
        transform.position += dir * moveSpeed * Time.deltaTime;

        // אחרי ההזזה אנחנו מוודאות שהשחקן נשאר בתוך אזור מותר
        ClampToAllowedArea();

        // ואז מפעילות את כלל המשחק לגבי כניסה לאגם
        CheckLakeArea();
    }

    // מגבילה את השחקן למסך לפי Viewport:
    // המרה מ-World ל-Viewport (0..1), חיתוך לתחום שהגדרנו, ואז חזרה ל-World.
    // ככה השחקן לא יוצא מהמסך ולא חוצה את טווח ה-Y המותר.
    private void ClampToAllowedArea()
    {
        if (cam == null) return;

        Vector3 vp = cam.WorldToViewportPoint(transform.position);

        vp.x = Mathf.Clamp(vp.x, 0f + screenPadding, 1f - screenPadding);
        vp.y = Mathf.Clamp(vp.y, allowedMinY, allowedMaxY);

        Vector3 world = cam.ViewportToWorldPoint(vp);
        world.z = transform.position.z;
        transform.position = world;
    }

    // לוגיקת האגם:
    // אנחנו מונעות מהשחקן להיכנס לאזור האגם לפני שהונחו סלעים במסלול.
    // אם השחקן נושא סלע – לא מגבילות אותו כאן (כדי לא לייצר מצבים מוזרים בזמן נשיאה).
    // משתמשות ב-lastValidPosition כדי "להחזיר" את השחקן אחורה אם הוא נכנס לאגם בניגוד לחוק.
    private void CheckLakeArea()
    {
        // אם השחקן נושא סלע – לא עושים חסימה כאן
        if (carriedRock != null)
        {
            return;
        }

        // אחרי הנחה, אנחנו נותנות פריים אחד "חסינות" כדי לא להקפיץ את השחקן אחורה בטעות
        if (justPlacedRock)
        {
            justPlacedRock = false;
            lastValidPosition = transform.position;
            return;
        }

        // בדיקה אם אנחנו בצד של האגם
        bool isInLake = transform.position.x < lakeBoundaryX;

        // בדיקה האם התנועה הייתה דווקא לכיוון אזור התשובות (ימינה),
        // במקרה כזה אנחנו מאפשרות "יציאה" מהאגם ולא חוסמות.
        bool movingTowardsAnswerArea = transform.position.x > lastValidPosition.x;

        // אם אנחנו בכלל לא באגם – המיקום תקין ומעדכנים אותו
        if (!isInLake)
        {
            lastValidPosition = transform.position;
            return;
        }

        // אם אנחנו באגם אבל זזנו לכיוון החוצה – מעדכנים ומאפשרים לצאת
        if (movingTowardsAnswerArea)
        {
            lastValidPosition = transform.position;
            return;
        }

        // כמה סלוטים כבר תפוסים (כלומר כמה סלעים הונחו בפועל במסלול)
        int occupiedSlotsCount = CountOccupiedSlots();

        // אם לא הונח אף סלע עדיין – אסור להיכנס לאגם, מחזירים למיקום האחרון התקין
        if (occupiedSlotsCount == 0)
        {
            Debug.Log("!!! Cannot enter lake - no rocks placed yet!");
            transform.position = lastValidPosition;
            return;
        }

        // אם יש לפחות סלע אחד – מאפשרים להיכנס, רק מעדכנים מיקום תקין חדש
        lastValidPosition = transform.position;
    }

    // פונקציית עזר: שואלת את GameManager כמה סלוטים תפוסים בשאלה הנוכחית.
    private int CountOccupiedSlots()
    {
        if (gameManager == null) return 0;

        return gameManager.GetOccupiedSlotsCount();
    }

    // פונקציית עזר: שואלת את GameManager מה ה-SlotIndex של הסלוט האחרון שתפוס,
    // כדי לקבוע מה הסלוט הבא שמותר להניח עליו (סדר נכון).
    private int GetLastOccupiedSlotIndex()
    {
        if (gameManager == null) return -1;

        return gameManager.GetLastOccupiedSlotIndex();
    }

    // פונקציה שבודקת אם השחקן עומד קרוב לסלוט תפוס (כרגע לא בשימוש פעיל בלוגיקה כאן),
    // אבל הכנו אותה כעזר אפשרי למניעת הנחה/תנועה במצבים מסוימים.
    private bool IsStandingOnOccupiedSlot()
    {
        for (int i = 0; i < nearbySlots.Count; i++)
        {
            Slot slot = nearbySlots[i];
            if (slot == null) continue;

            float distance = Vector3.Distance(transform.position, slot.transform.position);

            if (distance < 0.8f)
            {
                return true;
            }
        }

        return false;
    }

    // ניסיון להרים סלע:
    // עובד רק אם יש סלע מודגש (כלומר השחקן קרוב אליו),
    // ואם הסלע "מותר להרמה" וגם לא נמצא כבר בסלוט.
    // כשמרימים – מעבירים אותו למצב "על השחקן" ומבטלים הדגשה.
    private void TryPickUpHighlightedRock()
    {
        if (highlightedRock == null) return;

        if (!highlightedRock.CanBePickedUp()) return;
        if (highlightedRock.currentSlot != null) return;

        carriedRock = highlightedRock;
        carriedRock.SetStateOnPlayer();

        carriedRock.SetHighlight(false);
        highlightedRock = null;

        if (carryAnchor != null)
            carriedRock.transform.position = carryAnchor.position;
    }

    // ניסיון להניח סלע על הסלוט הקרוב:
    // 1) מוצאים את הסלוט הקרוב ביותר מתוך nearbySlots.
    // 2) מחייבים סדר: מותר להניח רק בסלוט הבא אחרי האחרון שתפוס.
    // 3) אם הסדר נכון – מצמידים סלע לסלוט ומדווחים ל-GameManager כדי שיבדוק נכונות/סיום שאלה.
    private bool TryPlaceOnNearestSlot()
    {
        Slot slot = GetNearestSlot();
        if (slot == null) return false;

        int lastOccupiedSlotIndex = GetLastOccupiedSlotIndex();
        
        // אם אין סלוט תפוס עדיין, הסלוט הראשון שמותר הוא 1.
        // אחרת, חייבים להניח בסלוט הבא בתור.
        int expectedNextSlot = (lastOccupiedSlotIndex == -1) ? 1 : lastOccupiedSlotIndex + 1;

        // אם השחקן מנסה להניח לא בסלוט הבא – לא מאפשרים
        if (slot.SlotIndex != expectedNextSlot)
        {
            Debug.Log("!!! Can only place on next slot in sequence! Expected: " + expectedNextSlot + ", Got: " + slot.SlotIndex);
            return false;
        }

        // מצמידים את הסלע למיקום הסלוט
        carriedRock.transform.position = slot.transform.position;

        // מדווחות ל-GameManager: הוא זה שמחליט אם זה נכון ומעדכן טעויות/ניצחון/המשך
        if (gameManager != null)
            gameManager.OnRockPlaced(carriedRock, slot);

        // משחררות את הסלע מהשחקן
        carriedRock = null;
        
        // דגל שמונע "חסימת אגם" באותו רגע
        justPlacedRock = true;
		return true;
    }
	
    // אם ניסינו להניח ולא הצליח (אין סלוט קרוב/סדר לא נכון),
    // אנחנו מחזירות את הסלע למיקום ההתחלתי שלו ומחזירות אותו למצב "לא הונח".
	private void ReturnRockToStart()
	{
    	if (carriedRock == null)
        	return;

    	carriedRock.transform.position = carriedRock.startPos;
		carriedRock.SetStateNotPlaced();
    	carriedRock = null;
	}

    // מחפשת את הסלוט הקרוב ביותר לשחקן מתוך nearbySlots:
    // מנקות קודם ערכים null (למקרה שסלוט נהרס/נכבה),
    // ואז בודקות מרחק ומחזירות את הקרוב ביותר.
    private Slot GetNearestSlot()
    {
        CleanNulls(nearbySlots);

        Slot best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < nearbySlots.Count; i++)
        {
            Slot s = nearbySlots[i];
            if (s == null) continue;

            // משתמשות ב-sqrMagnitude כדי לחסוך חישוב שורש (יותר יעיל)
            float d = (s.transform.position - transform.position).sqrMagnitude;
            
            if (d < bestDist)
            {
                bestDist = d;
                best = s;
            }
        }

        return best;
    }

    // פונקציה כללית לניקוי איברים שהם null מתוך רשימה:
    // עוברת מהסוף להתחלה כדי שאפשר להסיר בלי לשבור אינדקסים.
    private void CleanNulls<T>(List<T> list) where T : Object
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i] == null) list.RemoveAt(i);
    }

    // Trigger Enter:
    // כשאנחנו נכנסים לטווח של אובייקט:
    // - אם זה AnswerRock: מדגישות אותו כדי שהשחקן יבין שזה הסלע שניתן להרים.
    // - אם זה Slot: מוסיפות אותו לרשימת nearbySlots כדי שנוכל לבחור את הקרוב ביותר בהנחה.
 	private void OnTriggerEnter2D(Collider2D other)
    {
        AnswerRock rock = other.GetComponent<AnswerRock>();

        if (rock != null && rock.CanBePickedUp())
        {
            // אם כבר היה סלע מודגש קודם ואנחנו עוברים לסלע אחר – מבטלות הדגשה קודמת
            if (highlightedRock != null && highlightedRock != rock)
            {
                highlightedRock.SetHighlight(false);
            }
			
            highlightedRock = rock;
            highlightedRock.SetHighlight(true);
            return;
        }

        Slot slot = other.GetComponent<Slot>();
        if (slot != null && !nearbySlots.Contains(slot))
        {
            nearbySlots.Add(slot);
        }
    }

    // Trigger Exit:
    // כשאנחנו יוצאים מטווח:
    // - אם זה הסלע המודגש: מבטלות הדגשה ומנקים רפרנס.
    // - אם זה סלוט: מסירים אותו מהרשימה של הסלוטים הקרובים.
    private void OnTriggerExit2D(Collider2D other)
    {
        AnswerRock rock = other.GetComponent<AnswerRock>();
        if (rock != null && rock == highlightedRock)
        {
            highlightedRock.SetHighlight(false);
            highlightedRock = null;
            return;
        }

        Slot slot = other.GetComponent<Slot>();
        if (slot != null)
        {
            nearbySlots.Remove(slot);
        }
    }
}