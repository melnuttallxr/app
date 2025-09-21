#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class IosPostBuild_LinkUserNotifications
{
    private const string FrameworkPath = "System/Library/Frameworks/UserNotifications.framework";
    private const string MinDeployment = "10.0";

    [PostProcessBuild]
    public static void OnPostBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        var pbxPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var proj = new PBXProject();
        proj.ReadFromFile(pbxPath);

        // Получаем GUIDы таргетов для разных версий Unity
#if UNITY_2019_3_OR_NEWER
        string unityMainTarget = proj.GetUnityMainTargetGuid();       // "Unity-iPhone"
        string unityFrameworkTarget = proj.GetUnityFrameworkTargetGuid(); // "UnityFramework"
#else
        string unityMainTarget = proj.TargetGuidByName("Unity-iPhone");
        string unityFrameworkTarget = unityMainTarget;
#endif

        // 1) Добавить UserNotifications.framework в UnityFramework (и в основной таргет — на всякий случай)
        AddFrameworkIfMissing(proj, unityFrameworkTarget, FrameworkPath, weak: false);
        AddFrameworkIfMissing(proj, unityMainTarget, FrameworkPath, weak: false);

        // 2) Обеспечить iOS Deployment Target >= 10.0
        EnsureDeploymentTargetAtLeast(proj, unityFrameworkTarget, MinDeployment);
        EnsureDeploymentTargetAtLeast(proj, unityMainTarget, MinDeployment);

        proj.WriteToFile(pbxPath);
    }

    private static void AddFrameworkIfMissing(PBXProject proj, string targetGuid, string systemFrameworkProjectPath, bool weak)
    {
        // Проверяем, есть ли файл в проекте
        var fileGuid = proj.FindFileGuidByProjectPath(systemFrameworkProjectPath);
        if (string.IsNullOrEmpty(fileGuid))
        {
            // Добавим в группу Frameworks
            fileGuid = proj.AddFile(systemFrameworkProjectPath, systemFrameworkProjectPath, PBXSourceTree.Sdk);
        }

        // Проверим, линкован ли он в нужный таргет
        if (!proj.ContainsFramework(targetGuid, "UserNotifications.framework"))
        {
            proj.AddFrameworkToProject(targetGuid, "UserNotifications.framework", weak);
        }
    }

    private static void EnsureDeploymentTargetAtLeast(PBXProject proj, string targetGuid, string minVersion)
    {
        // Считываем текущее значение из любой конфигурации
        var current = proj.GetBuildPropertyForAnyConfig(targetGuid, "IPHONEOS_DEPLOYMENT_TARGET");
        // Если не задано или меньше требуемого — проставим
        if (string.IsNullOrEmpty(current) || VersionLess(current, minVersion))
        {
            proj.SetBuildProperty(targetGuid, "IPHONEOS_DEPLOYMENT_TARGET", minVersion);
        }
    }

    private static bool VersionLess(string a, string b)
    {
        // очень простой компаратор версий "X.Y"
        System.Version va, vb;
        if (!System.Version.TryParse(Normalize(a), out va)) return true;   // если не распарсили — считаем меньше
        if (!System.Version.TryParse(Normalize(b), out vb)) return false;
        return va < vb;

        string Normalize(string v) => v.Contains(".") ? v : (v + ".0");
    }
}
#endif
