#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class AddUserNotificationsFramework
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;

        var projPath = PBXProject.GetPBXProjectPath(path);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

#if UNITY_2019_3_OR_NEWER
        string mainTarget = proj.GetUnityMainTargetGuid();       // "Unity-iPhone"
        string unityFramework = proj.GetUnityFrameworkTargetGuid(); // "UnityFramework"
#else
        string mainTarget = proj.TargetGuidByName("Unity-iPhone");
        string unityFramework = mainTarget;
#endif

        // Подлинковать фреймворк (false = required, можно поставить true для weak link, если Deployment Target < iOS 10)
        proj.AddFrameworkToProject(unityFramework, "UserNotifications.framework", false);
        proj.AddFrameworkToProject(mainTarget, "UserNotifications.framework", false);

        // (Не обязательно) Включить capability Push Notifications, если используете пуши
        // var capManager = new ProjectCapabilityManager(projPath, "Unity-iPhone.entitlements", "Unity-iPhone");
        // capManager.AddPushNotifications(false);
        // capManager.WriteToFile();

        proj.WriteToFile(projPath);
    }
}
#endif