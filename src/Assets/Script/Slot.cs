using UnityEngine;

// הסקריפט הזה מייצג סלוט אחד במסלול שעליו מניחים את הסלעים.
// התפקיד שלו פשוט וברור: לדעת איזה מספר הוא בסדר (1-8),
// לשמור איזה סלע מונח עליו כרגע,
// ולהציג/להסתיר מסגרת הדגשה כדי לכוון את השחקן לסלוט הבא שצריך למלא.
// כלומר – הוא לא מנהל את המשחק, אלא רק מייצג "נקודת יעד" אחת במסלול.
public class Slot : MonoBehaviour
{
    // כאן אנחנו שומרות איזה סלע מונח כרגע על הסלוט הזה.
    // המשתנה ציבורי כדי ש-GameManager ו-PlayerController יוכלו לעדכן ולבדוק אותו בזמן משחק.
    public AnswerRock currentRock;
    
    // המספר הסידורי של הסלוט במסלול (למשל 1,2,3...).
    // זה מה שמשווים מול RockValue של הסלע כדי לבדוק אם הוא הונח במקום הנכון.
    [SerializeField] private int slotIndex;
    
    // Property לקריאה בלבד: מאפשר לשאר הסקריפטים לדעת מה המספר של הסלוט,
    // אבל לא לשנות אותו בטעות בזמן ריצה.
    public int SlotIndex => slotIndex;

    // הפניה לרכיב שמצייר את מסגרת ההדגשה (למשל קו כחול).
    // דרכו אנחנו מסמנות לשחקן איזה סלוט הוא הבא בתור.
    [SerializeField] private SpriteRenderer outlineRenderer;
    
    // מפעילה את המסגרת – כלומר מסמנת לשחקן שזה הסלוט הבא שצריך למלא.
    public void ActivateOutline()
    {
        // בודקות שהרכיב מחובר לפני שמדליקות אותו, כדי למנוע שגיאה.
        if(outlineRenderer != null)
        {
            outlineRenderer.enabled = true;  // מדליקות את המסגרת
        }
    }

    // מכבה את המסגרת – למשל אחרי שהסלוט מולא ועוברים לסלוט הבא.
    public void DeactivateOutline()
    {
        // שוב בודקות שהרכיב קיים לפני שמכבות.
        if(outlineRenderer != null)
        {
            outlineRenderer.enabled = false;  // מכבות את המסגרת
        }
    }
}