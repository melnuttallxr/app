#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;

public static class FixUniWebViewLink
{
    [PostProcessBuild(999)]
    public static void OnPostprocessBuild(BuildTarget t, string path)
    {
        if (t != BuildTarget.iOS) return;

        var projPath = PBXProject.GetPBXProjectPath(path);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

#if UNITY_2019_3_OR_NEWER
        var appTarget = proj.GetUnityMainTargetGuid();      // Unity-iPhone
        var ufTarget = proj.GetUnityFrameworkTargetGuid(); // UnityFramework
#else
        var appTarget = proj.TargetGuidByName("Unity-iPhone");
        var ufTarget  = appTarget;
#endif

        // Найди свой фактический путь, добавь при необходимости
        var rel = "Frameworks/Plugins/iOS/UniWebView/UniWebView.framework";
        if (!Directory.Exists(Path.Combine(path, rel)))
            rel = "Frameworks/Plugins/iOS/UniWebView/UniWebView.xcframework";
        if (!Directory.Exists(Path.Combine(path, rel))) return;

        var guid = proj.FindFileGuidByProjectPath(rel)
                   ?? proj.AddFile(rel, rel, PBXSourceTree.Source);

        // Линкуем в UnityFramework
        proj.AddFileToBuild(ufTarget, guid);

        // Эмбедим в Unity-iPhone
        PBXProjectExtensions.AddFileToEmbedFrameworks(proj, appTarget, guid);

        // Системные фреймворки
        proj.AddFrameworkToProject(ufTarget, "WebKit.framework", false);
        proj.AddFrameworkToProject(ufTarget, "Security.framework", false);

        // Runpaths и поиск фреймворков
        var run = new[] { "$(inherited)", "@executable_path/Frameworks", "@loader_path/Frameworks" };
        proj.UpdateBuildProperty(ufTarget, "LD_RUNPATH_SEARCH_PATHS", run, new string[] { });
        proj.UpdateBuildProperty(appTarget, "LD_RUNPATH_SEARCH_PATHS", run, new string[] { });

        var searchDir = "$(PROJECT_DIR)/" + Path.GetDirectoryName(rel);
        proj.AddBuildProperty(ufTarget, "FRAMEWORK_SEARCH_PATHS", searchDir);
        proj.AddBuildProperty(ufTarget, "FRAMEWORK_SEARCH_PATHS", "$(inherited)");

        // На случай статического .framework — заставить подтянуть все объектники
        if (rel.EndsWith(".framework"))
        {
            var bin = Path.Combine("$(PROJECT_DIR)", rel, "UniWebView");
            proj.AddBuildProperty(ufTarget, "OTHER_LDFLAGS", "-ObjC");
            proj.AddBuildProperty(ufTarget, "OTHER_LDFLAGS", "-force_load");
            proj.AddBuildProperty(ufTarget, "OTHER_LDFLAGS", bin);
        }

        // Биткод лучше выключить
        proj.SetBuildProperty(ufTarget, "ENABLE_BITCODE", "NO");
        proj.SetBuildProperty(appTarget, "ENABLE_BITCODE", "NO");

        // Если внутри есть Swift
        proj.SetBuildProperty(appTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");

        File.WriteAllText(projPath, proj.WriteToString());
    }
}
#endif