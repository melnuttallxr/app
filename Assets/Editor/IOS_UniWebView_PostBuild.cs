#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public static class IOS_UniWebView_PostBuild
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        var projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string mainTarget = proj.GetUnityMainTargetGuid();         // Unity-iPhone
        string unityFramework = proj.GetUnityFrameworkTargetGuid(); // UnityFramework

        // 1) WebKit.framework
        proj.AddFrameworkToProject(unityFramework, "WebKit.framework", false);

        // 2) -ObjC
        var flags = proj.GetBuildPropertyForAnyConfig(unityFramework, "OTHER_LDFLAGS");
        if (string.IsNullOrEmpty(flags) || !flags.Contains("-ObjC"))
        {
            proj.AddBuildProperty(unityFramework, "OTHER_LDFLAGS", "-ObjC");
        }

        // 3) Bitcode OFF
        proj.SetBuildProperty(unityFramework, "ENABLE_BITCODE", "NO");
        proj.SetBuildProperty(mainTarget, "ENABLE_BITCODE", "NO");

        // 4) Подключаем UniWebView.xcframework (стандартный путь после экспорта)
        string relXCFrameworkPath = "Libraries/UniWebView/Plugins/iOS/UniWebView.xcframework";
        string absXCFrameworkPath = Path.Combine(pathToBuiltProject, relXCFrameworkPath);

        if (File.Exists(absXCFrameworkPath) || Directory.Exists(absXCFrameworkPath))
        {
            // Добавим файл и залинкуем в UnityFramework
            string fileGuid = proj.AddFile(relXCFrameworkPath, relXCFrameworkPath, PBXSourceTree.Source);
            proj.AddFileToBuild(unityFramework, fileGuid);

            // Создадим фазу Copy Files → Frameworks (= "Embed Frameworks") и добавим туда xcframework
            string embedPhaseGuid = FindOrCreateEmbedPhase(proj, unityFramework, "Embed Frameworks");
            proj.AddFileToBuildSection(unityFramework, embedPhaseGuid, fileGuid);

            // Swift stdlib на всякий случай
            proj.SetBuildProperty(unityFramework, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            proj.SetBuildProperty(mainTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[PostBuild] UniWebView.xcframework not found at: {relXCFrameworkPath}. " +
                                         "Проверь импорт плагина (Assets/UniWebView/Plugins/iOS).");
        }

        proj.WriteToFile(projPath);

        // 5) (опционально) ATS послабление — если нужно для платёжек/редиректов
        var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;
        if (!root.values.ContainsKey("NSAppTransportSecurity"))
        {
            var ats = root.CreateDict("NSAppTransportSecurity");
            ats.SetBoolean("NSAllowsArbitraryLoads", true);
        }
        plist.WriteToFile(plistPath);
    }

    // Создаёт/возвращает Copy Files Build Phase для фреймворков (эквивалент "Embed Frameworks")
    static string FindOrCreateEmbedPhase(PBXProject proj, string targetGuid, string phaseName)
    {
        // Unity API не даёт перебирать фазы, поэтому просто добавим (дубликаты Xcode пережует).
        // "10" — код поддиректории Frameworks для Copy Files phase.
        return proj.AddCopyFilesBuildPhase(targetGuid, phaseName, "", "10");
    }
}
#endif