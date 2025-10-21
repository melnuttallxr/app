#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;

public static class AddUserNotificationsFramework
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuildProject)
    {
        if (target != BuildTarget.iOS) return;

        var projPath = PBXProject.GetPBXProjectPath(pathToBuildProject);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

#if UNITY_2019_3_OR_NEWER
        string appTarget = proj.GetUnityMainTargetGuid();      // Unity-iPhone
        string unityFramework = proj.GetUnityFrameworkTargetGuid(); // UnityFramework
#else
        string appTarget = proj.TargetGuidByName("Unity-iPhone");
        string unityFramework = appTarget;
#endif

        // 0) как было — UserNotifications
        proj.AddFrameworkToProject(unityFramework, "UserNotifications.framework", false);
        proj.AddFrameworkToProject(appTarget, "UserNotifications.framework", false);

        // 1) UniWebView: link + embed + search paths + (в случае статического .framework) -force_load
        LinkAndEmbedUniWebView(proj, pathToBuildProject, unityFramework, appTarget);

        // 2) Общие флаги
        proj.SetBuildProperty(unityFramework, "ENABLE_BITCODE", "NO");
        proj.SetBuildProperty(appTarget, "ENABLE_BITCODE", "NO");

        // На всякий: Obj-C категории
        proj.AddBuildProperty(unityFramework, "OTHER_LDFLAGS", "-ObjC");

        File.WriteAllText(projPath, proj.WriteToString());
    }

    private static void LinkAndEmbedUniWebView(PBXProject proj, string buildPath, string frameworkTarget, string appTarget)
    {
        // Популярные места; дополни своим, если у тебя другой путь
        var candidates = new[]
        {
            "Frameworks/Plugins/iOS/UniWebView/UniWebView.xcframework",
            "Frameworks/Plugins/iOS/UniWebView/UniWebView.framework",
            "Frameworks/UniWebView/Plugins/iOS/UniWebView.xcframework",
            "Frameworks/UniWebView/Plugins/iOS/UniWebView.framework",
            "Libraries/Plugins/iOS/UniWebView/UniWebView.xcframework",
            "Libraries/Plugins/iOS/UniWebView/UniWebView.framework"
        };

        string relPath = null;
        foreach (var c in candidates)
        {
            var abs = Path.Combine(buildPath, c);
            if (Directory.Exists(abs) || File.Exists(abs)) { relPath = c; break; }
        }
        if (string.IsNullOrEmpty(relPath))
        {
            UnityEngine.Debug.LogWarning("[PostBuild] UniWebView not found in expected paths. Skipping.");
            return;
        }

        UnityEngine.Debug.Log("[PostBuild] UniWebView at: " + relPath);

        // Добавляем файл в проект
        var fileGuid = proj.FindFileGuidByProjectPath(relPath);
        if (string.IsNullOrEmpty(fileGuid))
            fileGuid = proj.AddFile(relPath, relPath, PBXSourceTree.Source);

        // 1) Линкуем UniWebView в UnityFramework (основной таргет C# нативщины)
        proj.AddFileToBuild(frameworkTarget, fileGuid);

        // 2) Встраиваем (Embed & Sign) в appTarget, чтобы попал в .app/Frameworks
        PBXProjectExtensions.AddFileToEmbedFrameworks(proj, appTarget, fileGuid);

        // 3) Системные фреймворки
        proj.AddFrameworkToProject(frameworkTarget, "WebKit.framework", false);
        proj.AddFrameworkToProject(frameworkTarget, "Security.framework", false);

        // 4) RUNPATHS
        var runpaths = new[] { "$(inherited)", "@executable_path/Frameworks", "@loader_path/Frameworks" };
        proj.UpdateBuildProperty(frameworkTarget, "LD_RUNPATH_SEARCH_PATHS", runpaths, new string[] { });
        proj.UpdateBuildProperty(appTarget, "LD_RUNPATH_SEARCH_PATHS", runpaths, new string[] { });

        // 5) FRAMEWORK_SEARCH_PATHS чтобы UnityFramework точно нашёл UniWebView
        var searchDir = "$(PROJECT_DIR)/" + Path.GetDirectoryName(relPath);
        proj.AddBuildProperty(frameworkTarget, "FRAMEWORK_SEARCH_PATHS", searchDir);
        proj.AddBuildProperty(frameworkTarget, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");

        // 6) Если это .framework (а не .xcframework), часто он статический — добавим -force_load
        if (relPath.EndsWith(".framework"))
        {
            // Путь к бинарю внутри .framework
            var binPath = Path.Combine("$(PROJECT_DIR)", relPath, "UniWebView");
            proj.AddBuildProperty(frameworkTarget, "OTHER_LDFLAGS", "-force_load");
            proj.AddBuildProperty(frameworkTarget, "OTHER_LDFLAGS", binPath);
        }

        // 7) Если во фреймворке есть Swift — подстрахуем appTarget
        proj.SetBuildProperty(appTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
    }
}
#endif