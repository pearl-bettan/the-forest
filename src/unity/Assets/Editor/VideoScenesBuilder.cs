using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// ============================================================
//  בניית סצנות הסרטונים.
//
//  מייצר שתי סצנות מוכנות עם כל מה שצריך, ומוסיף אותן
//  ל-Build Settings:
//
//      IntroVideo  ->  Intro.mp4  ->  SampleScene
//      WinVideo    ->  Win.mp4    ->  End
//
//  בכל סצנה: מצלמה, Canvas, RawImage בשם VideoSurface שעליו
//  מוקרן הסרטון, כפתור SkipButton עם תמונת הסלע והכיתוב "דלג",
//  ואובייקט VideoScene שנושא את VideoSceneScript.
//
//  כל אלה הם נקודות התחלה: אפשר להזיז, לעצב ולהחליף אותם בעורך
//  כמו כל אובייקט אחר, והסקריפט ימשיך לעבוד.
//
//  הרצה: ForestGame > Create video scenes
//
//  הכלי בונה את הסצנות דרך יוניטי עצמה ולא כותב קובצי סצנה ביד,
//  ולכן התוצאה תמיד תקינה לפתיחה
// ============================================================
public static class VideoScenesBuilder
{
    private const string ScenesFolder = "Assets/Scenes";

    // שם הספרייט של כפתור הדילוג, תחת Assets/Resources
    private const string SkipSpriteName = "EmptyRock";

    private const string SkipLabel = "דלג";

    [MenuItem("ForestGame/Create video scenes", false, 20)]
    public static void CreateAll()
    {
        // לא דורסים עבודה שלא נשמרה
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;

        string openBefore = SceneManager.GetActiveScene().path;

        string intro = BuildScene("IntroVideo", "Intro.mp4", "SampleScene");
        string win = BuildScene("WinVideo", "Win.mp4", "End");

        AddToBuildSettings(new List<string> { intro, win });

        // מחזירים את העורך לסצנה שהייתה פתוחה
        if (string.IsNullOrEmpty(openBefore) == false && File.Exists(openBefore) == true)
        {
            EditorSceneManager.OpenScene(openBefore);
        }

        EditorUtility.DisplayDialog(
            "סצנות הסרטונים נוצרו",
            "נוצרו IntroVideo ו-WinVideo תחת Assets/Scenes, והן נוספו ל-Build Settings.\n\n" +
            "אפשר לפתוח אותן ולמקם מחדש את הסרטון ואת כפתור הדילוג.",
            "מצוין");
    }

    // ============================================================
    //  בניית סצנה אחת
    // ============================================================

    private static string BuildScene(string sceneName, string videoFile, string nextScene)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildCamera();

        Canvas canvas = BuildCanvas();

        RawImage surface = BuildSurface(canvas.transform);

        GameObject skip = BuildSkipButton(canvas.transform);

        BuildController(videoFile, nextScene, surface, skip);

        if (Directory.Exists(ScenesFolder) == false) Directory.CreateDirectory(ScenesFolder);

        string path = ScenesFolder + "/" + sceneName + ".unity";

        EditorSceneManager.SaveScene(scene, path);

        Debug.Log("[VideoScenesBuilder] נוצרה הסצנה " + path);

        return path;
    }

    // מצלמה עם רקע שחור. הסרטון מוקרן על Canvas ולא בעולם,
    // ולכן המצלמה רק נותנת רקע ומאזין קול
    private static void BuildCamera()
    {
        GameObject item = new GameObject("Main Camera");

        Camera camera = item.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;

        item.AddComponent<AudioListener>();
        item.tag = "MainCamera";
    }

    // בד ציור שמתאים את עצמו לגודל החלון
    private static Canvas BuildCanvas()
    {
        GameObject item = new GameObject("Canvas");

        Canvas canvas = item.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = item.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        item.AddComponent<GraphicRaycaster>();

        // בלי מערכת אירועים לחיצות על הכפתור אינן נקלטות
        GameObject events = new GameObject("EventSystem");
        events.AddComponent<EventSystem>();
        events.AddComponent<StandaloneInputModule>();

        return canvas;
    }

    // המשטח שעליו מוקרן הסרטון, נמתח על כל המסך
    private static RawImage BuildSurface(Transform parent)
    {
        GameObject item = new GameObject("VideoSurface", typeof(RectTransform));
        item.transform.SetParent(parent, false);

        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage surface = item.AddComponent<RawImage>();
        surface.color = Color.white;

        return surface;
    }

    // כפתור הדילוג, בפינה השמאלית התחתונה
    private static GameObject BuildSkipButton(Transform parent)
    {
        GameObject item = new GameObject("SkipButton", typeof(RectTransform));
        item.transform.SetParent(parent, false);

        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(40f, 40f);
        rect.sizeDelta = new Vector2(190f, 110f);

        Image face = item.AddComponent<Image>();

        Sprite rock = LoadSkipSprite();

        if (rock != null)
        {
            face.sprite = rock;
            face.color = Color.white;
        }
        else
        {
            Debug.LogWarning("[VideoScenesBuilder] לא נמצאה התמונה " + SkipSpriteName +
                             " תחת Assets/Resources. הכפתור נוצר בלי סלע");
            face.color = new Color(0f, 0f, 0f, 0.55f);
        }

        Button button = item.AddComponent<Button>();
        button.targetGraphic = face;

        BuildSkipLabel(item.transform);

        return item;
    }

    // הכיתוב שעל הסלע
    private static void BuildSkipLabel(Transform parent)
    {
        GameObject item = new GameObject("Label", typeof(RectTransform));
        item.transform.SetParent(parent, false);

        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = item.AddComponent<TextMeshProUGUI>();

        // HebrewText מחזיר את הטקסט בסדר תצוגה נכון, ולכן ההיפוך
        // המובנה של TextMeshPro חייב להישאר מכובה
        label.isRightToLeftText = false;
        label.text = HebrewText.Fix(SkipLabel);
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16;
        label.fontSizeMax = 40;
        label.color = Color.white;

        // הכיתוב אינו חוסם את הלחיצה על הכפתור שמתחתיו
        label.raycastTarget = false;
    }

    // האובייקט שמנהל את הסצנה: הסקריפט, רכיב הניגון, והחיבורים
    private static void BuildController(string videoFile, string nextScene,
                                        RawImage surface, GameObject skip)
    {
        GameObject item = new GameObject("VideoScene");

        VideoPlayer player = item.AddComponent<VideoPlayer>();
        player.playOnAwake = false;

        item.AddComponent<AudioSource>();

        VideoSceneScript script = item.AddComponent<VideoSceneScript>();

        // החיבורים נעשים דרך SerializedObject, כי השדות פרטיים
        SerializedObject so = new SerializedObject(script);

        so.FindProperty("videoFileName").stringValue = videoFile;
        so.FindProperty("nextScene").stringValue = nextScene;
        so.FindProperty("videoSurface").objectReferenceValue = surface;
        so.FindProperty("video").objectReferenceValue = player;
        so.FindProperty("skipButton").objectReferenceValue = skip;

        so.ApplyModifiedPropertiesWithoutUndo();

        // גם חיבור ב-On Click, כדי שהכפתור ייראה מחובר בעורך.
        // הסקריפט מחבר את עצמו שוב בזמן ריצה, ולכן לחיצה אחת
        // לא תיספר פעמיים
        Button button = skip.GetComponent<Button>();

        if (button != null)
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, script.Skip);
        }
    }

    // ============================================================
    //  עזרה
    // ============================================================

    // EmptyRock.png מיובא כ-Sprite במצב Multiple, כלומר הספרייט
    // עצמו הוא תת-נכס. לכן טעינה ישירה לפי שם הקובץ מחזירה null
    private static Sprite LoadSkipSprite()
    {
        Sprite direct = Resources.Load<Sprite>(SkipSpriteName);

        if (direct != null) return direct;

        Sprite[] all = Resources.LoadAll<Sprite>(SkipSpriteName);

        if (all != null && all.Length > 0) return all[0];

        return null;
    }

    // מוסיף את הסצנות ל-Build Settings, בלי לגעת בסדר הקיים.
    // סצנה שאינה ברשימה פשוט לא נכללת בבנייה, וטעינה שלה
    // נכשלת בזמן ריצה
    private static void AddToBuildSettings(List<string> paths)
    {
        List<EditorBuildSettingsScene> list =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        foreach (string path in paths)
        {
            bool exists = false;

            foreach (EditorBuildSettingsScene scene in list)
            {
                if (scene.path == path)
                {
                    exists = true;
                    break;
                }
            }

            if (exists == true) continue;

            list.Add(new EditorBuildSettingsScene(path, true));

            Debug.Log("[VideoScenesBuilder] נוספה ל-Build Settings: " + path);
        }

        EditorBuildSettings.scenes = list.ToArray();
    }
}
