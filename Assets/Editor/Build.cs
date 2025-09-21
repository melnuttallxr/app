using System.Linq;
using System;
using UnityEditor;
using UnityEngine;

public static class BuildScript
{
    [MenuItem("Build/Build Android")]
    public static void BuildAndroid()
    {
        PlayerSettings.Android.useCustomKeystore = true;
        EditorUserBuildSettings.buildAppBundle = true;

        // Set bundle version. NEW_BUILD_NUMBER environment variable is set in the codemagic.yaml 
        var versionIsSet = int.TryParse(Environment.GetEnvironmentVariable("NEW_BUILD_NUMBER"), out int version);
        if (versionIsSet)
        {
            Debug.Log($"Bundle version code set to {version}");
            PlayerSettings.Android.bundleVersionCode = version;
        }
        else
        {
            Debug.Log("Bundle version not provided");
        }

        // Set keystore name
        string keystoreName = Environment.GetEnvironmentVariable("CM_KEYSTORE_PATH");
        if (!String.IsNullOrEmpty(keystoreName))
        {
            Debug.Log($"Setting path to keystore: {keystoreName}");
            PlayerSettings.Android.keystoreName = keystoreName;
        }
        else
        {
            Debug.Log("Keystore name not provided");
        }

        // Set keystore password
        string keystorePass = Environment.GetEnvironmentVariable("CM_KEYSTORE_PASSWORD");
        if (!String.IsNullOrEmpty(keystorePass))
        {
            Debug.Log("Setting keystore password");
            PlayerSettings.Android.keystorePass = keystorePass;
        }
        else
        {
            Debug.Log("Keystore password not provided");
        }

        // Set keystore alias name
        string keyaliasName = Environment.GetEnvironmentVariable("CM_KEY_ALIAS");
        if (!String.IsNullOrEmpty(keyaliasName))
        {
            Debug.Log("Setting keystore alias");
            PlayerSettings.Android.keyaliasName = keyaliasName;
        }
        else
        {
            Debug.Log("Keystore alias not provided");
        }

        // Set keystore password
        string keyaliasPass = Environment.GetEnvironmentVariable("CM_KEY_PASSWORD");
        if (!String.IsNullOrEmpty(keyaliasPass))
        {
            Debug.Log("Setting keystore alias password");
            PlayerSettings.Android.keyaliasPass = keyaliasPass;
        }
        else
        {
            Debug.Log("Keystore alias password not provided");
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.locationPathName = "android/android.aab";
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;
        buildPlayerOptions.scenes = GetScenes();

        Debug.Log("Building Android");
        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Built Android");
    }

    [MenuItem("Build/Build iOS")]
    public static void BuildIos()
    {
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.locationPathName = "ios";
        buildPlayerOptions.target = BuildTarget.iOS;
        buildPlayerOptions.options = BuildOptions.None;
        buildPlayerOptions.scenes = GetScenes();

        Debug.Log("Building iOS");
        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Built iOS");
    }

    [MenuItem("Build/Build Windows")]
    public static void BuildWindows()
    {
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.locationPathName = "win/" + Application.productName + ".exe";
        buildPlayerOptions.target = BuildTarget.StandaloneWindows;
        buildPlayerOptions.options = BuildOptions.None;
        buildPlayerOptions.scenes = GetScenes();

        Debug.Log("Building Windows");
        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Built Windows");
    }
    [MenuItem("Build/Build Mac")]
    public static void BuildMac()
    {
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.locationPathName = "mac/" + Application.productName + ".app";
        buildPlayerOptions.target = BuildTarget.StandaloneOSX;
        buildPlayerOptions.options = BuildOptions.None;
        buildPlayerOptions.scenes = GetScenes();

        Debug.Log("Building StandaloneOSX");
        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Built StandaloneOSX");
    }

    private static string[] GetScenes()
    {
        return (from scene in EditorBuildSettings.scenes where scene.enabled select scene.path).ToArray();
    }
}

//#if UNITY_IOS
//using UnityEditor.iOS.Xcode;
//using UnityEditor.Callbacks;

///// <summary>
///// После экспорта iOS-проекта:
///// - добавляем UserNotifications.framework в UnityFramework и Unity-iPhone (Required)
///// - выставляем IPHONEOS_DEPLOYMENT_TARGET = 13.0 (на всякий случай, чтобы Xcode-проект точно был ≥ iOS 13)
///// </summary>
//public static class IosPostBuild_UserNotifications
//{
//    private const string MinIos = "13.0";
//    private const string FrameworkName = "UserNotifications.framework";

//    [PostProcessBuild]
//    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
//    {
//        if (target != BuildTarget.iOS) return;

//        var projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
//        var proj = new PBXProject();
//        proj.ReadFromFile(projPath);

//        // Unity 2019.3+ (Unity 6 в т.ч.)
//        string mainTargetGuid = proj.GetUnityMainTargetGuid();        // "Unity-iPhone"
//        string frameworkTargetGuid = proj.GetUnityFrameworkTargetGuid(); // "UnityFramework"

//        // 1) Линкуем UserNotifications.framework (Required)
//        proj.AddFrameworkToProject(frameworkTargetGuid, FrameworkName, /*weak:*/ false);
//        proj.AddFrameworkToProject(mainTargetGuid, FrameworkName, /*weak:*/ false);

//        // 2) Гарантируем iOS Deployment Target = 13.0 для обоих таргетов
//        proj.SetBuildProperty(frameworkTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", MinIos);
//        proj.SetBuildProperty(mainTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", MinIos);

//        proj.WriteToFile(projPath);

//        Debug.Log($"[iOS PostBuild] Linked {FrameworkName} and set IPHONEOS_DEPLOYMENT_TARGET={MinIos} for targets UnityFramework & Unity-iPhone.");
//    }
//}
//#endif