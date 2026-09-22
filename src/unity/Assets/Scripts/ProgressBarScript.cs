using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  מד ההתקדמות של המשחק.
//
//  המד הוא שרשרת חרוזים על גבי הגבעול: חרוז אחד לכל שאלה.
//  שלושת האובייקטים שבסצנה משמשים כעוגנים:
//
//      StartProgressBar   - החרוז הראשון, בקצה אחד
//      EndProgressBar     - החרוז האחרון, בקצה השני
//      CenterProgressBar  - תבנית לחרוזים שבאמצע
//
//  החרוזים שבאמצע נוצרים כשכפולים של תבנית המרכז ומפוזרים
//  במרווחים שווים על הקו שבין שני הקצוות. כך המיקום, הסיבוב
//  והגודל נקבעים בעורך ולא בקוד.
//
//  כל שאלה שנענתה הופכת חרוז אחד מאפור לירוק, מהתחלה לסוף.
//
//  שימוש מהקוד:
//      progressBar.Build(totalQuestions);
//      progressBar.SetProgress(questionsAnswered);
// ============================================================
public class ProgressBarScript : MonoBehaviour
{
    [Header("Sprites")]

    // חרוז הקצה הראשון
    [SerializeField] Sprite startGray;
    [SerializeField] Sprite startGreen;

    // החרוזים שבאמצע
    [SerializeField] Sprite middleGray;
    [SerializeField] Sprite middleGreen;

    // חרוז הקצה האחרון
    [SerializeField] Sprite endGray;
    [SerializeField] Sprite endGreen;

    [Header("Anchors")]

    // שלושת האובייקטים שמסמנים את המד בסצנה. אם לא חוברו כאן,
    // הם נמצאים לפי השם בין הילדים של האובייקט הזה
    [SerializeField] SpriteRenderer startView;
    [SerializeField] SpriteRenderer centerView;
    [SerializeField] SpriteRenderer endView;


    // החרוזים לפי סדר ההתקדמות: ראשון, אמצעיים, אחרון
    private readonly List<SpriteRenderer> beads = new List<SpriteRenderer>();

    // השכפולים שנוצרו בזמן ריצה, כדי לנקות אותם בבנייה מחדש
    private readonly List<GameObject> clones = new List<GameObject>();

    private int total = 0;
    private int filled = 0;


    void Awake()
    {
        FindAnchors();
    }


    // ============================================================
    //  בונה את המד מחדש לפי מספר השאלות במשחק
    // ============================================================
    public void Build(int questionCount)
    {
        FindAnchors();
        ClearClones();

        beads.Clear();
        total = questionCount;

        if (total <= 0)
        {
            ShowAnchors(false);
            return;
        }

        // חרוז ראשון
        beads.Add(startView);
        if (startView != null) startView.gameObject.SetActive(true);

        // חרוזי האמצע: הראשון שבהם הוא תבנית המרכז עצמה,
        // והשאר שכפולים שלה
        int middleCount = total - 2;

        if (middleCount > 0 && centerView != null)
        {
            for (int i = 0; i < middleCount; i++)
            {
                SpriteRenderer bead = centerView;

                if (i > 0)
                {
                    GameObject copy = Instantiate(centerView.gameObject,
                                                  centerView.transform.parent);
                    copy.name = "Bead_" + (i + 2);
                    clones.Add(copy);

                    bead = copy.GetComponent<SpriteRenderer>();
                }

                bead.gameObject.SetActive(true);
                beads.Add(bead);
            }
        }
        else if (centerView != null)
        {
            // פחות משלוש שאלות: אין חרוזי אמצע
            centerView.gameObject.SetActive(false);
        }

        // חרוז אחרון. כששאלה אחת בלבד אין קצה שני
        if (total >= 2)
        {
            beads.Add(endView);
            if (endView != null) endView.gameObject.SetActive(true);
        }
        else if (endView != null)
        {
            endView.gameObject.SetActive(false);
        }

        Spread();
        SetProgress(filled);
    }


    // ============================================================
    //  מסמן כמה שאלות כבר נענו. אלה שנענו מקבלות חרוז ירוק
    // ============================================================
    public void SetProgress(int answered)
    {
        if (answered < 0) answered = 0;
        if (answered > total) answered = total;

        filled = answered;

        for (int i = 0; i < beads.Count; i++)
        {
            if (beads[i] == null) continue;

            beads[i].sprite = SpriteFor(i, i < filled);
        }
    }


    // מפזר את חרוזי האמצע במרווחים שווים בין שני הקצוות.
    // הקצוות עצמם נשארים בדיוק במקום שנקבע להם בעורך
    private void Spread()
    {
        if (startView == null || endView == null) return;
        if (beads.Count < 3) return;

        Vector3 from = startView.transform.localPosition;
        Vector3 to = endView.transform.localPosition;

        for (int i = 1; i < beads.Count - 1; i++)
        {
            if (beads[i] == null) continue;

            float t = (float)i / (beads.Count - 1);
            beads[i].transform.localPosition = Vector3.Lerp(from, to, t);
        }
    }


    // החרוז הראשון והאחרון מקבלים את ספרייטי הקצה
    private Sprite SpriteFor(int index, bool green)
    {
        if (index == 0)
        {
            return green == true ? startGreen : startGray;
        }

        if (index == total - 1)
        {
            return green == true ? endGreen : endGray;
        }

        return green == true ? middleGreen : middleGray;
    }


    // אם העוגנים לא חוברו ב-Inspector, מאתרים אותם לפי השם
    private void FindAnchors()
    {
        if (startView == null) startView = FindChild("StartProgressBar");
        if (centerView == null) centerView = FindChild("CenterProgressBar");
        if (endView == null) endView = FindChild("EndProgressBar");
    }


    private SpriteRenderer FindChild(string childName)
    {
        Transform found = transform.Find(childName);

        if (found == null) return null;

        return found.GetComponent<SpriteRenderer>();
    }


    private void ShowAnchors(bool on)
    {
        if (startView != null) startView.gameObject.SetActive(on);
        if (centerView != null) centerView.gameObject.SetActive(on);
        if (endView != null) endView.gameObject.SetActive(on);
    }


    private void ClearClones()
    {
        for (int i = 0; i < clones.Count; i++)
        {
            if (clones[i] == null) continue;

            Destroy(clones[i]);
        }

        clones.Clear();
    }
}
