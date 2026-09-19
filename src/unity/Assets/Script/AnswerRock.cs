using UnityEngine;
using TMPro;
using System.Collections;
using static RTLFixer;

// הסקריפט הזה הוא הסקריפט של "סלע תשובה" במשחק.
// הוא אחראי על: לשמור את הערך של הסלע (מה המיקום הנכון שלו בסדר),
// להציג טקסט/תמונה על הסלע, להדגיש אותו כשהשחקן קרוב,
// לנהל מצבים (לא הונח / על השחקן / הונח),
// וגם לתת משוב כשמניחים סלע במקום לא נכון (שקיעה + שקיפות וחזרה להתחלה).
public class AnswerRock : MonoBehaviour
{
    // Enum שמגדיר בצורה ברורה באיזה מצב הסלע נמצא כרגע.
    // עשינו את זה כדי שהקוד יהיה קריא ושנוכל לדעת בכל רגע מה מותר לעשות עם הסלע.
    public enum RockState
    {
        NotPlaced,  // הסלע עדיין לא הונח במסלול, ולכן אפשר להרים אותו
        OnPlayer,   // הסלע נמצא כרגע אצל השחקן (נישא)
        Placed      // הסלע כבר הונח על סלוט במסלול
    }

    // נתוני בסיס של הסלע: הערך שלו והאובייקט שמציג "היילייט" (זוהר/מסגרת).
    [Header("Rock Data")]
    [SerializeField] private int rockValue = 0;           // הערך המספרי של הסלע (1-8) - משמש לבדיקה מול SlotIndex
    [SerializeField] private GameObject highlightObject;  // אובייקט ההדגשה (הזוהר הצהוב)

    // רכיבי התצוגה שעל הסלע: טקסט ותמונה.
    // הסלע יכול להיות טקסט בלבד, תמונה בלבד, או גם וגם.
    [Header("Content Display")]
    [SerializeField] private TMP_Text rockText;           // הטקסט שמודפס על הסלע
    [SerializeField] private SpriteRenderer rockSprite;   // התמונה שמופיעה על הסלע

    // הגדרות לגודל גופן: אנחנו מכבות AutoSize אבל משאירות טווחים כדי לשלוט על גודל הטקסט.
    [Header("Auto Size Settings")]
    [SerializeField] private float fontSizeMin = 10f;     // גודל גופן מינימלי
    [SerializeField] private float fontSizeMax = 50f;     // גודל גופן מקסימלי

    // הגדרות משוב שגיאה: כמה לשקוע, כמה זמן, ועד כמה להיות שקוף.
    [Header("Wrong Feedback")]
    [SerializeField] private float sinkDistance = 0.6f;   // כמה הסלע "שוקע" למטה בזמן טעות
    [SerializeField] private float sinkTime = 1f;         // כמה זמן המשוב נמשך
    [SerializeField] private float fadeAlpha = 0.6f;      // לאיזה שקיפות מגיעים בסוף האנימציה

    // Property לקריאה בלבד של הערך של הסלע.
    // זה מאפשר לשאר הקוד לבדוק RockValue בלי לאפשר שינוי מבחוץ בטעות.
    public int RockValue => rockValue;

    // מצב נוכחי של הסלע - מתחיל כ-NotPlaced.
    public RockState State { get; private set; } = RockState.NotPlaced;

    // הסלוט שהסלע מונח עליו כרגע (אם מונח).
    // [HideInInspector] כדי שלא נערוך את זה ידנית באינספקטור, זה משתנה בזמן משחק.
    [HideInInspector] public Slot currentSlot;

    // startPos = המיקום שאליו הסלע חוזר אם יש טעות או אם מחזירים אותו.
    // basePos = המיקום המקורי של הסלע לפני ערבוב/הזזות.
    public Vector3 startPos;   // המיקום ההתחלתי הנוכחי (אחרי ערבוב/עדכונים)
    public Vector3 basePos;    // המיקום הבסיסי המקורי
    
    // sr = ה-SpriteRenderer של הגוף של הסלע כדי שנוכל לשנות שקיפות בזמן משוב טעות.
    // busy = דגל שמונע התחלה של כמה אנימציות במקביל (כדי שלא יהיו קפיצות/באגים).
    private SpriteRenderer sr;  // רכיב ה-SpriteRenderer
    private bool busy;          // האם הסלע עסוק באנימציה

    // Awake רץ פעם אחת כשהאובייקט נטען:
    // כאן אנחנו שומרות מיקומים התחלתיים, מביאות את רכיב ה-SpriteRenderer,
    // ומוודאות שהסלע מתחיל בלי הדגשה.
    private void Awake()
    {
        // שומרים את המיקומים המקוריים
        basePos = transform.position;
        startPos = transform.position;
        
        // מקבלים את רכיב ה-SpriteRenderer כדי שנוכל לשנות לו צבע/שקיפות במשוב
        sr = GetComponent<SpriteRenderer>();
        
        // מכבים הדגשה בהתחלה כדי שלא יהיה זוהר לפני שהשחקן מתקרב
        SetHighlight(false);
    }

    // פונקציה שמעדכנת את startPos למיקום הנוכחי של הסלע.
    // משתמשים בזה אחרי ערבוב מיקומים כדי שה"חזרה להתחלה" תחזיר למקום הנכון החדש.
    public void UpdateStartPosition()
    {
        startPos = transform.position;
    }

    // איפוס מלא של הסלע בין שאלות:
    // מנקות טקסט ותמונה, מחזירות למיקום הבסיסי, מכבות הדגשה,
    // מאפסות מצב/סלוט, ומוודאות שהסלע פעיל.
    public void ResetContent()
    {
        // מוחקים תצוגה קודמת כדי שלא יישאר משהו משאלה קודמת
        rockText.text = "";
        rockSprite.sprite = null;
        
        // מחזירים את הסלע למיקום הבסיסי שלו ומגדירים שזה גם startPos החדש
        transform.position = basePos;
        UpdateStartPosition();
        
        // מכבים הדגשה
        SetHighlight(false);
        
        // מאפסים קשר לסלוט ומחזירים מצב התחלה
        currentSlot = null;
        State = RockState.NotPlaced;
        
        // מוודאות שהאובייקט מופעל
        gameObject.SetActive(true);
    }

    // הגדרת תוכן הסלע עבור שאלה מסוימת:
    // מקבל טקסט, תמונה וערך חדש (RockValue) ומעדכן את התצוגה בהתאם.
    public void SetContent(string text, Sprite sprite, int newRockValue)
    {
        // הערך הוא מה שנבדק מול SlotIndex כדי לדעת אם ההנחה נכונה
        rockValue = newRockValue;
        
        // מטפלים בטקסט אם קיים רכיב טקסט
        if (rockText != null)
        {
            rockText.text = text;
            
            // תיקון עברית/כיווניות כדי שהטקסט יוצג נכון
            RTLFixer.FixRtl(rockText, text);
            
            // אם אין טקסט - מסתירים את האובייקט של הטקסט כדי שלא יראו "ריק"
            rockText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            
            // אנחנו מבטלות AutoSizing ומגדירות את טווחי הגודל שהחלטנו עליהם
            rockText.enableAutoSizing = false;
            rockText.fontSizeMin = fontSizeMin;
            rockText.fontSizeMax = fontSizeMax;
        }
        
        // אם יש תמונה - משייכים אותה ל-SpriteRenderer של הסלע
        if (sprite != null)
        {
            rockSprite.sprite = sprite;
        }
        
        // אחרי שינוי תוכן/מיקום, מעדכנים גם startPos כדי ש"חזרה" תהיה נכונה
        UpdateStartPosition();
    }

    // בדיקה האם מותר להרים את הסלע:
    // רק אם הוא עדיין לא הונח (NotPlaced) וגם לא עסוק באנימציה (busy=false).
    public bool CanBePickedUp()
    {
        return State == RockState.NotPlaced && !busy;
    }

    // הדלקה/כיבוי של ההדגשה:
    // משמש כששחקן מתקרב לסלע כדי לתת רמז ויזואלי שניתן להרים אותו.
    public void SetHighlight(bool on)
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(on);  // מדליקים/מכבים את הזוהר הצהוב
        }
    }

    // כשהשחקן מרים את הסלע - אנחנו מעבירות אותו למצב OnPlayer ומכבות הדגשה.
    public void SetStateOnPlayer()
    {
        State = RockState.OnPlayer;
        SetHighlight(false);
    }

    // כשהסלע הונח על סלוט - מעבירות למצב Placed ומכבות הדגשה.
    public void SetStatePlaced()
    {
        State = RockState.Placed;
        SetHighlight(false);
    }

    // מצב חזרה ל-NotPlaced:
    // משתמשים בזה כשהסלע חוזר להתחלה/או אחרי משוב שגיאה.
    public void SetStateNotPlaced()
    {
        State = RockState.NotPlaced;
        SetHighlight(false);
    }

    // הפעלת משוב שגיאה:
    // אם כרגע אין אנימציה אחרת (busy=false) אנחנו מפעילות Coroutine שתעשה שקיעה+שקיפות וחזרה.
    public void PlayWrongFeedback()
    {
        if (!busy)
            StartCoroutine(WrongRoutine());
    }

    // קורוטינה של משוב טעות:
    // 1) מסמנת busy כדי לנעול אינטראקציה
    // 2) מזיזה את הסלע כלפי מטה בהדרגה
    // 3) מורידה שקיפות בהדרגה
    // 4) מחזירה את הסלע ל-startPos ומאפסה שקיפות
    // 5) מאפסת מצב/סלוט כדי שהסלע יהיה שוב זמין
    private IEnumerator WrongRoutine()
    {
        busy = true;
        SetHighlight(false);

        Vector3 currentPos = transform.position;
        Vector3 downPos = currentPos + Vector3.down * sinkDistance;

        float t = 0f;

        while (t < sinkTime)
        {
            t += Time.deltaTime;
            float k = t / sinkTime;

            // תנועה חלקה כלפי מטה
            transform.position = Vector3.Lerp(currentPos, downPos, k);

            // שינוי שקיפות בצורה חלקה
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, fadeAlpha, k);
                sr.color = c;
            }

            yield return null;
        }

        // בסוף המשוב מחזירים את הסלע למיקום ההתחלתי שלו
        transform.position = startPos;

        // מחזירים שקיפות מלאה
        if (sr != null)
        {
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }

        // מנקים קשר לסלוט ומחזירים מצב להתחלה כדי שאפשר יהיה לנסות שוב
        currentSlot = null;
        State = RockState.NotPlaced;

        // משחררים נעילה
        busy = false;
    }

    // ResetToStart הוא איפוס "קשיח" לסלע:
    // מחזיר מיקום, מאפס מצב, מכבה הדגשה, מחזיר שקיפות מלאה ומוודא שהאובייקט פעיל.
    // זה שימושי כשאנחנו רוצות להחזיר את הסלע למצב התחלה בלי תלות במה קרה לפני.
    public void ResetToStart()
    {
        transform.position = startPos;
        currentSlot = null;
        State = RockState.NotPlaced;
        busy = false;

        SetHighlight(false);

        if (sr != null)
        {
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }

        gameObject.SetActive(true);
    }
}