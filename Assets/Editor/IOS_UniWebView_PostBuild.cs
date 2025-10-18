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

        string mainTarget = proj.GetUnityMainTargetGuid();        // "Unity-iPhone"
        string unityFramework = proj.GetUnityFrameworkTargetGuid();// "UnityFramework"

        // 1) Добавим системный WebKit.framework
        proj.AddFrameworkToProject(unityFramework, "WebKit.framework", false);

        // 2) Добавим -ObjC (на всякий случай)
        var flags = proj.GetBuildPropertyForAnyConfig(unityFramework, "OTHER_LDFLAGS");
        if (string.IsNullOrEmpty(flags) || !flags.Contains("-ObjC"))
        {
            proj.AddBuildProperty(unityFramework, "OTHER_LDFLAGS", "-ObjC");
        }

        // 3) Отключим Bitcode (если где-то включился)
        proj.SetBuildProperty(unityFramework, "ENABLE_BITCODE", "NO");
        proj.SetBuildProperty(mainTarget, "ENABLE_BITCODE", "NO");

        // 4) Подключим UniWebView.xcframework как Embedded (если Unity не сделал)
        // Путь к xcframework в экспортированном Xcode-проекте:
        // Обычно Unity копирует содержимое Assets/UniWebView/Plugins/iOS в:
        //   <xcode>/Libraries/UniWebView/Plugins/iOS/…
        string relXCFrameworkPath = "Libraries/UniWebView/Plugins/iOS/UniWebView.xcframework";
        string absXCFrameworkPath = Path.Combine(pathToBuiltProject, relXCFrameworkPath);

        if (File.Exists(absXCFrameworkPath) || Directory.Exists(absXCFrameworkPath))
        {
            string fileGuid = proj.AddFile(relXCFrameworkPath, relXCFrameworkPath, PBXSourceTree.Source);
            // Линкуем в UnityFramework
            proj.AddFileToBuild(unityFramework, fileGuid);
            // И добавим в Embed Frameworks, чтобы он попал в пакет
            PBXProjectExtensions.AddFileToEmbedFrameworks(proj, unityFramework, fileGuid);

            // Иногда Xcode ругается на signing — поможем:
            proj.SetBuildProperty(unityFramework, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            proj.SetBuildProperty(mainTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[PostBuild] UniWebView.xcframework not found at: {relXCFrameworkPath}. " +
                                         "Проверь Import Settings плагина и путь.");
        }

        proj.WriteToFile(projPath);

        // 5) Подправим Info.plist (не обязательно, но полезно для WebKit)
        var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // Пример: разрешить произвольные загрузки (если нужно платежной/SSO-цепочке)
        // <key>NSAppTransportSecurity</key><dict><key>NSAllowsArbitraryLoads</key><true/></dict>
        var root = plist.root;
        if (!root.values.ContainsKey("NSAppTransportSecurity"))
        {
            var ats = root.CreateDict("NSAppTransportSecurity");
            ats.SetBoolean("NSAllowsArbitraryLoads", true);
        }

        plist.WriteToFile(plistPath);
    }
}
#endif