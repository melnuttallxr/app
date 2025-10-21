#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions; // для AddFileToEmbedFrameworks

public static class AddUserNotificationsFramework
{
    [PostProcessBuild(999)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuildProject)
    {
        if (target != BuildTarget.iOS) return;

        var projPath = PBXProject.GetPBXProjectPath(pathToBuildProject);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

#if UNITY_2019_3_OR_NEWER
        string mainTarget = proj.GetUnityMainTargetGuid();       // "Unity-iPhone"
        string unityFramework = proj.GetUnityFrameworkTargetGuid();  // "UnityFramework"
#else
        string mainTarget = proj.TargetGuidByName("Unity-iPhone");
        string unityFramework = mainTarget;
#endif

        // 1) Как у тебя было — UserNotifications
        proj.AddFrameworkToProject(unityFramework, "UserNotifications.framework", false);
        proj.AddFrameworkToProject(mainTarget, "UserNotifications.framework", false);

        // 2) UniWebView: подключаем .xcframework/.framework, Embed & Sign, runpaths, системные фреймворки
        AddUniWebView(proj, unityFramework, pathToBuildProject);

        // 3) Bitcode off
        proj.SetBuildProperty(unityFramework, "ENABLE_BITCODE", "NO");
        proj.SetBuildProperty(mainTarget, "ENABLE_BITCODE", "NO");

        File.WriteAllText(projPath, proj.WriteToString());
    }

    private static void AddUniWebView(PBXProject proj, string frameworkTarget, string buildPath)
    {
        // Скорректируй пути под свою структуру, если нужно
        var candidates = new[]
        {
            "Frameworks/Plugins/iOS/UniWebView/UniWebView.xcframework",
            "Frameworks/Plugins/iOS/UniWebView/UniWebView.framework",
            "Frameworks/UniWebView/Plugins/iOS/UniWebView.xcframework",
            "Frameworks/UniWebView/Plugins/iOS/UniWebView.framework"
        };

        string relPath = null;
        foreach (var c in candidates)
        {
            var abs = Path.Combine(buildPath, c);
            if (Directory.Exists(abs) || File.Exists(abs))
            {
                relPath = c;
                break;
            }
        }
        if (string.IsNullOrEmpty(relPath))
        {
            // UniWebView не найден — тихо выходим
            return;
        }

        // Добавляем файл в проект (если ещё не добавлен)
        var fileGuid = proj.FindFileGuidByProjectPath(relPath);
        if (string.IsNullOrEmpty(fileGuid))
        {
            fileGuid = proj.AddFile(relPath, relPath, PBXSourceTree.Source);
        }

        // Линкуем с UnityFramework (повторный вызов не критичен — Unity API терпит дубликаты)
        proj.AddFileToBuild(frameworkTarget, fileGuid);

        // Для динамических .framework/.xcframework — обязательно Embed & Sign
        PBXProjectExtensions.AddFileToEmbedFrameworks(proj, frameworkTarget, fileGuid);

        // Системные фреймворки, которые использует UniWebView
        proj.AddFrameworkToProject(frameworkTarget, "WebKit.framework", false);
        proj.AddFrameworkToProject(frameworkTarget, "Security.framework", false);

        // Правильные runpath'ы для динамических фреймворков
        proj.UpdateBuildProperty(
            frameworkTarget,
            "LD_RUNPATH_SEARCH_PATHS",
            new[] { "$(inherited)", "@executable_path/Frameworks", "@loader_path/Frameworks" },
            new string[] { } // ничего не удаляем
        );
    }
}
#endif