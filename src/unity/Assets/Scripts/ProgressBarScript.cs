using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  מד ההתקדמות של המשחק.
//
//  המד הוא שרשרת חרוזים: חרוז אחד לכל שאלה במשחק. החרוז הראשון
//  והאחרון הם חרוזי הקצה המעוצבים (Start ו-End), ובאמצע חרוזים
//  רגילים שמפוזרים במרווחים שווים.
//
//  כל שאלה שנענתה הופכת חרוז אחד מאפור לירוק, לפי הסדר.
//
//  שימוש מהקוד:
//      progressBar.Build(totalQuestions);
//      progressBar.SetProgress(questionsAnswered);
//
//  הגדרה בעורך: אובייקט ריק בסצנה עם הסקריפט הזה, ושישה
//  ספרייטים גרורים לשדות למטה. המרווח והגודל נקבעים בהתאם
//  למקום שיש למד במסך
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

    [Header("Layout")]

    // המרחק בין מרכזי החרוזים, ביחידות עולם
    [SerializeField] float spacing = 2.6f;

    // גודל כל חרוז
    [SerializeField] float beadScale = 1f;

    // כיוון ההתקדמות. ברירת המחדל משמאל לימין
    [SerializeField] bool rightToLeft = false;

    [Header("Sorting")]
    [SerializeField] string sortingLayerName = "Default";
    [SerializeField] int sortingOrder = 10;


    // החרוזים שנוצרו, לפי סדר ההתקדמות
    private readonly List<SpriteRenderer> beads = new List<SpriteRenderer>();

    // מספר השאלות שהמד נבנה עבורן
    private int total = 0;

    // כמה חרוזים כבר ירוקים
    private int filled = 0;


    // ============================================================
    //  בונה את המד מחדש לפי מספר השאלות במשחק
    // ============================================================
    public void Build(int questionCount)
    {
        Clear();

        total = questionCount;

        if (total <= 0) return;

        for (int i = 0; i < total; i++)
        {
            GameObject bead = new GameObject("Bead_" + (i + 1));

            bead.transform.SetParent(transform, false);
            bead.transform.localPosition = PositionOf(i);
            bead.transform.localScale = new Vector3(beadScale, beadScale, 1);

            SpriteRenderer view = bead.AddComponent<SpriteRenderer>();
            view.sortingLayerName = sortingLayerName;
            view.sortingOrder = sortingOrder;

            beads.Add(view);
        }

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


    // מרכז השרשרת יושב על נקודת האובייקט שמחזיק אותה
    private Vector3 PositionOf(int index)
    {
        float offset = (index - (total - 1) * 0.5f) * spacing;

        if (rightToLeft == true) offset = -offset;

        return new Vector3(offset, 0, 0);
    }


    // החרוז הראשון והאחרון הם חרוזי הקצה. כששאלה אחת בלבד,
    // החרוז היחיד מקבל את ספרייט ההתחלה
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


    private void Clear()
    {
        for (int i = 0; i < beads.Count; i++)
        {
            if (beads[i] == null) continue;

            Destroy(beads[i].gameObject);
        }

        beads.Clear();
        total = 0;
    }
}
