#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// ============================================================
//  הגדרת הפרויקט אחרי ייבוא החבילה.
//
//  קובץ unitypackage נושא רק את תיקיית Assets. הגדרות הפרויקט
//  עצמו - רשימת הסצנות שב-Build Settings, ההרשאה לפנות לשרת
//  ב-HTTP, וצינור הרינדור - יושבות בתיקיית ProjectSettings
//  ואינן נכללות בחבילה לעולם.
//
//  בלעדיהן המשחק אינו רץ: הקוד בודק אם סצנה נכללת בבנייה לפני
//  שהוא טוען אותה, וכשהרשימה ריקה סרטון הפתיחה מדולג ומסכי
//  ההשהיה והסיום אינם נטענים כלל.
//
//  הקובץ הזה משלים את מה שהחבילה לא יכולה לשאת. הוא רץ פעם
//  אחת, אוטומטית, בפתיחה הראשונה של הפרויקט אחרי הייבוא, ואין
//  לו פריט תפריט - אין מה להפעיל ידנית.
//
//  אחרי ההגדרה אפשר למחוק אותו בלי שום השפעה על המשחק.
// ============================================================
[InitializeOnLoad]
public static class ProjectSetup
{
    // סדר הסצנות ב-Build Settings. הראשונה היא זו שנטענת בבנייה
    private static readonly string[] SceneOrder =
    {
        "Home", "SampleScene", "Pause", "End", "IntroVideo", "WinVideo"
    };

    private const string ScenesFolder = "Assets/Scenes/";
    private const string HomeScene = "Assets/Scenes/Home.unity";

    static ProjectSetup()
    {
        // delayCall ולא הרצה מיידית: בזמן הבנאי יוניטי עדיין
        // באמצע טעינת הפרויקט, וכתיבת הגדרות שם אינה נתפסת
        EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        // מפתח לפי נתיב הפרויקט, כדי שההגדרה תרוץ שוב בפרויקט אחר
        string key = "ForestGame.Setup." + Application.dataPath.GetHashCode();

        if (EditorPrefs.GetBool(key, false) == true) return;

        EditorPrefs.SetBool(key, true);

        List<string> changed = new List<string>();

        if (FixBuildScenes() == true) changed.Add("רשימת הסצנות");
        if (AllowInsecureHttp() == true) changed.Add("פנייה לשרת ב-HTTP");
        if (AssignRenderPipeline() == true) changed.Add("צינור הרינדור");

        OpenHomeScene();

        if (changed.Count == 0)
        {
            Debug.Log("[יער הידע] הפרויקט כבר מוגדר. לפתיחה: Assets/Scenes/Home.unity ואז Play");
            return;
        }

        Debug.Log("[יער הידע] הוגדר אוטומטית: " + string.Join(", ", changed) +
                  ".\nלהרצה: פתחו את Assets/Scenes/Home.unity ולחצו Play.");
    }


    // ============================================================
    // רשימת הסצנות.
    //
    // הרשימה נכתבת מחדש בסדר הנכון ולא רק מושלמת, כי הסדר קובע
    // איזו סצנה נטענת ראשונה בבנייה. סצנה שאינה קיימת בפרויקט
    // פשוט מדולגת
    // ============================================================
    private static bool FixBuildScenes()
    {
        List<EditorBuildSettingsScene> wanted = new List<EditorBuildSettingsScene>();

        foreach (string name in SceneOrder)
        {
            string path = ScenesFolder + name + ".unity";

            if (File.Exists(path) == false) continue;

            wanted.Add(new EditorBuildSettingsScene(path, true));
        }

        if (wanted.Count == 0) return false;

        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;

        if (current.Length == wanted.Count)
        {
            bool same = true;

            for (int i = 0; i < current.Length; i++)
            {
                if (current[i].path != wanted[i].path || current[i].enabled == false)
                {
                    same = false;
                    break;
                }
            }

            if (same == true) return false;
        }

        EditorBuildSettings.scenes = wanted.ToArray();
        return true;
    }


    // ============================================================
    // הרשאה לפנות לשרת ב-HTTP.
    //
    // ברירת המחדל של פרויקט חדש חוסמת כתובות שאינן HTTPS, והשרת
    // של המשחק עונה ב-HTTP. בלי זה כל טעינת משחק נכשלת בהודעת
    // "לא הצלחנו להתחבר לשרת"
    // ============================================================
    private static bool AllowInsecureHttp()
    {
        if (PlayerSettings.insecureHttpOption == InsecureHttpOption.AlwaysAllowed) return false;

        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        return true;
    }


    // ============================================================
    // צינור הרינדור.
    //
    // המשחק מצויר ב-URP. אם הפרויקט נפתח בלי צינור מוגדר, הכול
    // מוצג בוורוד. ההשמה נעשית רק כשאין צינור כלל, כדי לא לדרוס
    // הגדרה קיימת
    // ============================================================
    private static bool AssignRenderPipeline()
    {
        if (GraphicsSettings.defaultRenderPipeline != null) return false;

        string[] found = AssetDatabase.FindAssets("t:RenderPipelineAsset");

        if (found.Length == 0) return false;

        string path = AssetDatabase.GUIDToAssetPath(found[0]);
        RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);

        if (pipeline == null) return false;

        GraphicsSettings.defaultRenderPipeline = pipeline;
        return true;
    }


    // ============================================================
    // פתיחת מסך הפתיחה.
    //
    // אחרי ייבוא לפרויקט חדש הסצנה הפתוחה היא סצנת המשחק, ולחיצה
    // על Play שם מתחילה באמצע בלי נתונים. נפתח רק כשאין עבודה
    // שלא נשמרה, כדי לא לאבד שום דבר
    // ============================================================
    private static void OpenHomeScene()
    {
        if (File.Exists(HomeScene) == false) return;
        if (EditorSceneManager.GetActiveScene().isDirty == true) return;
        if (EditorSceneManager.GetActiveScene().path == HomeScene) return;

        EditorSceneManager.OpenScene(HomeScene, OpenSceneMode.Single);
    }
}
#endif
