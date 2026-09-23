using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ============================================================
// ייצוא המשחק ל-Web, לפי מצגת ההנחיות "ייצוא פרויקט".
//
// הבנייה נוחתת בתיקייה בשם Game בתוך wwwroot של המחולל:
//     <המחולל>/Portelem/Client/wwwroot/Game
//
// המחולל מטמיע אותה ב-iframe ומעביר את קוד המשחק בכתובת:
//     Game/index.html?code=1001
//
// ההגדרות כאן הן בדיוק אלה שבמצגת: דחיסת Brotli ו-Data Caching כבוי,
// בתוספת Decompression Fallback - כי השרת מוגש ב-http ולא ב-https.
//
// שתי דרכים להריץ:
//   1. מהתפריט   ForestGame > Export game to Web   (Ctrl+Shift+W)
//   2. משורת הפקודה, דרך build-all.sh
// ============================================================
public class WebGLExport : EditorWindow
{
    // הנתיב נשמר בין הפעלות של יוניטי
    private const string PathKey = "ForestGame.WebGLExport.OutputPath";

    // ברירת מחדל: תיקיית Game בתוך wwwroot של המחולל
    private const string DefaultTail = "Portelem/Client/wwwroot/Game";

    private string outputPath = "";
    private bool cleanBefore = true;
    private bool developmentBuild = false;
    private Vector2 scroll;

    [MenuItem("ForestGame/Export game to Web %#w", false, 1)]
    public static void Open()
    {
        WebGLExport window = GetWindow<WebGLExport>(true, "ייצוא ל-Web", true);
        window.minSize = new Vector2(560, 430);
        window.Show();
    }

    private void OnEnable()
    {
        outputPath = EditorPrefs.GetString(PathKey, GuessGeneratorPath());
    }

    // מנסה לנחש איפה תיקיית המחולל יושבת, יחסית לפרויקט היוניטי
    private static string GuessGeneratorPath()
    {
        try
        {
            // Assets/.. = תיקיית הפרויקט
            DirectoryInfo project = new DirectoryInfo(Application.dataPath).Parent;
            DirectoryInfo probe = project;

            // מחפשים תיקייה בשם Portelem עד ארבע רמות מעל הפרויקט
            for (int i = 0; i < 4 && probe != null; i++)
            {
                foreach (DirectoryInfo sibling in probe.GetDirectories())
                {
                    string candidate = Path.Combine(sibling.FullName, DefaultTail);
                    string parent = Directory.GetParent(candidate)?.FullName;

                    if (parent != null && Directory.Exists(parent)) return candidate;
                }

                probe = probe.Parent;
            }

            return Path.Combine(project.FullName, "WebGLBuild");
        }
        catch (Exception)
        {
            return "";
        }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("ייצוא המשחק ל-Web", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "הבנייה נוחתת בתיקיית Game שבתוך wwwroot של המחולל.\n" +
            "המחולל מטמיע אותה ב-iframe ומעביר את הקוד בכתובת: Game/index.html?code=1001",
            MessageType.Info);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("תיקיית היעד", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        outputPath = EditorGUILayout.TextField(outputPath);

        if (GUILayout.Button("בחירה...", GUILayout.Width(90)))
        {
            string picked = EditorUtility.SaveFolderPanel("בחרי את תיקיית היעד", outputPath, "Game");
            if (string.IsNullOrEmpty(picked) == false) outputPath = picked;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        cleanBefore = EditorGUILayout.Toggle("לנקות את התיקייה לפני", cleanBefore);
        developmentBuild = EditorGUILayout.Toggle("Development Build", developmentBuild);

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("הגדרות שיוחלו", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  דחיסה", "Brotli + Decompression Fallback");
        EditorGUILayout.LabelField("  Data Caching", "כבוי");
        EditorGUILayout.LabelField("  חריגות", "Explicitly Thrown Only");
        EditorGUILayout.LabelField("  גודל הבמה", "1280 x 720");

        EditorGUILayout.Space(10);

        string[] scenes = EnabledScenes();
        EditorGUILayout.LabelField("סצנות בבנייה", scenes.Length.ToString());

        if (scenes.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "אין אף סצנה מסומנת ב-Build Settings. פתחי File > Build Settings והוסיפי אותן.",
                MessageType.Error);
        }

        EditorGUILayout.Space(12);

        GUI.enabled = scenes.Length > 0 && string.IsNullOrEmpty(outputPath) == false;

        if (GUILayout.Button("בנייה", GUILayout.Height(34)))
        {
            EditorPrefs.SetString(PathKey, outputPath);
            Build();
        }

        GUI.enabled = true;

        EditorGUILayout.Space(6);

        if (Directory.Exists(Path.GetDirectoryName(outputPath) ?? "") == false)
        {
            EditorGUILayout.HelpBox(
                "התיקייה שמעל היעד לא קיימת. ודאי שהנתיב מצביע לתוך המחולל.\n" +
                "הנתיב הצפוי: <המחולל>/Portelem/Client/wwwroot/Game",
                MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();
    }

    // ============================================================
    // תבנית ה-Web של יוניטי ממקמת בלוק קבוע של 1280x720 ועוד סרגל
    // בגובה 38 במרכז החלון. בתוך iframe נמוך יותר מ-758 פיקסלים
    // הבלוק פשוט נחתך - למעלה ולמטה - ולכן הסרגל נעלם וראש התמונה
    // מתקצר. כאן מוסיפים כללי CSS שגורמים לקנבס להתכווץ יחסית
    // במקום להיחתך, בלי לגעת בתבנית של יוניטי עצמה.
    // ============================================================
    private const string ResponsiveMarker = "/* ForestGame responsive */";

    private static void MakeTemplateResponsive(string outputPath)
    {
        string cssPath = Path.Combine(outputPath, "TemplateData", "style.css");

        if (File.Exists(cssPath) == false)
        {
            Debug.LogWarning("[WebGLExport] לא מצאתי את TemplateData/style.css - התבנית לא הותאמה");
            return;
        }

        try
        {
            string css = File.ReadAllText(cssPath);

            // אם כבר הוספנו, לא מוסיפים שוב
            if (css.Contains(ResponsiveMarker) == true) return;

            css += "\n\n" + ResponsiveMarker + "\n" +
                "html, body { width: 100%; height: 100%; overflow: hidden; }\n" +
                "#unity-container.unity-desktop {\n" +
                "  position: static; transform: none; left: auto; top: auto;\n" +
                "  width: 100%; height: 100%;\n" +
                "  display: flex; flex-direction: column; align-items: center;\n" +
                "}\n" +
                "#unity-canvas {\n" +
                "  width: 100%; height: auto; aspect-ratio: 16 / 9;\n" +
                "  max-width: 100%; max-height: 100%;\n" +
                "  min-height: 0; margin: auto 0;\n" +
                "}\n" +
                "#unity-footer { width: 100%; flex: 0 0 38px; }\n";

            File.WriteAllText(cssPath, css);

            Debug.Log("[WebGLExport] התבנית הותאמה למסגרת משתנה");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WebGLExport] לא הצלחתי להתאים את התבנית: " + e.Message);
        }
    }

    private static string[] EnabledScenes()
    {
        System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();

        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.enabled == true) list.Add(s.path);
        }

        return list.ToArray();
    }

    // ============================================================
    // נקודת כניסה לשורת הפקודה (batchmode) - בשביל build-all.sh
    //
    //   Unity -quit -batchmode -projectPath <פרויקט> \
    //         -executeMethod WebGLExport.BuildFromCommandLine \
    //         -outputPath <יעד> -logFile <לוג>
    // ============================================================
    public static void BuildFromCommandLine()
    {
        string path = "";
        bool dev = false;

        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-outputPath" && i + 1 < args.Length) path = args[i + 1];
            if (args[i] == "-developmentBuild") dev = true;
        }

        if (string.IsNullOrEmpty(path) == true) path = GuessGeneratorPath();

        Debug.Log("[WebGLExport] output path: " + path);

        bool ok = RunBuild(path, true, dev);

        if (Application.isBatchMode == true) EditorApplication.Exit(ok == true ? 0 : 1);
    }

    private void Build()
    {
        bool ok = RunBuild(outputPath, cleanBefore, developmentBuild);

        if (ok == true) EditorUtility.RevealInFinder(outputPath);
    }

    // הבנייה עצמה. סטטית, כדי שגם החלון וגם שורת הפקודה יריצו בדיוק אותו קוד.
    private static bool RunBuild(string outputPath, bool cleanBefore, bool developmentBuild)
    {
        bool silent = Application.isBatchMode;

        string[] scenes = EnabledScenes();

        if (scenes.Length == 0)
        {
            Debug.LogError("[WebGLExport] אין אף סצנה מסומנת ב-Build Settings.");
            return false;
        }

        if (string.IsNullOrEmpty(outputPath) == true)
        {
            Debug.LogError("[WebGLExport] לא הוגדר נתיב יעד.");
            return false;
        }

        // ---------- ההגדרות לפי מצגת ההנחיות ----------
        // Brotli, ו-Data Caching מכובה
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.dataCaching = false;

        //דפדפנים מקבלים Content-Encoding: br רק בחיבור מאובטח -
        //HTTPS או localhost. השרת שלנו מוגש ב-http על פורט 5000,
        //ולכן כרום דוחה את הקבצים הדחוסים ב-ERR_CONTENT_DECODING_FAILED.
        //Decompression Fallback מצרף לבנייה מפענח ברוטלי משלה:
        //הקבצים נשמרים כ-.unityweb, השרת מגיש אותם בלי להצהיר על דחיסה,
        //והפענוח קורה בדפדפן. הגודל נשאר קטן והעבודה על http תקינה
        PlayerSettings.WebGL.decompressionFallback = true;

        //UnityWebRequest חוסם מברירת מחדל כל פנייה ב-http.
        //המשחק פונה ל-API של המחולל בכתובת יחסית, והמחולל מוגש
        //ב-http על פורט 5000, ולכן הבקשה נחסמת ב-
        //InvalidOperationException: Insecure connection not allowed.
        //ברגע שהשרת יעבור ל-https אפשר להחזיר את זה ל-NotAllowed
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

        // חריגות מלאות מנפחות את הבנייה. מספיק מה שנזרק במפורש
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

        PlayerSettings.WebGL.template = "APPLICATION:Default";

        // הבמה עוצבה ל-1280x720
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = true;

        if (cleanBefore == true && Directory.Exists(outputPath) == true)
        {
            try
            {
                Directory.Delete(outputPath, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("לא הצלחתי לנקות את התיקייה: " + e.Message);
            }
        }

        Directory.CreateDirectory(outputPath);

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = scenes;
        options.locationPathName = outputPath;
        options.target = BuildTarget.WebGL;
        options.targetGroup = BuildTargetGroup.WebGL;
        options.options = developmentBuild ? BuildOptions.Development : BuildOptions.None;

        Debug.Log("[WebGLExport] מתחיל בנייה ל-Web אל: " + outputPath);

        BuildReport report;

        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        catch (Exception e)
        {
            Debug.LogError("[WebGLExport] הבנייה נכשלה: " + e.Message);

            if (silent == false)
            {
                EditorUtility.DisplayDialog("הבנייה נכשלה",
                    "יוניטי לא הצליחה לבנות ל-WebGL.\n\n" +
                    "בדקי שמודול ה-WebGL מותקן: Unity Hub > Installs > גלגל השיניים > Add Modules > WebGL Build Support\n\n" +
                    e.Message, "סגירה");
            }

            return false;
        }

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[WebGLExport] הבנייה הסתיימה עם שגיאות: " + report.summary.result);

            if (silent == false)
            {
                EditorUtility.DisplayDialog("הבנייה נכשלה",
                    "הבנייה הסתיימה עם שגיאות. הפרטים ב-Console.", "סגירה");
            }

            return false;
        }

        MakeTemplateResponsive(outputPath);

        double mb = report.summary.totalSize / 1048576.0;

        Debug.Log("[WebGLExport] הבנייה הסתיימה בהצלחה. גודל: " + mb.ToString("0.0") + " MB, נתיב: " + outputPath);

        if (silent == false)
        {
            EditorUtility.DisplayDialog("הבנייה הסתיימה",
                "המשחק נבנה בהצלחה (" + mb.ToString("0.0") + " MB)\n\n" +
                outputPath + "\n\n" +
                "עכשיו רענני את המחולל ובדקי מקומית.", "מצוין");
        }

        return true;
    }
}
