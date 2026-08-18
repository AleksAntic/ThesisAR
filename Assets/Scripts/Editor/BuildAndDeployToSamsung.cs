using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

public class BuildAndDeployToSamsung
{
    [MenuItem("ThesisAR/Build and Deploy APK to Samsung")]
    public static void BuildApk()
    {
        FixMissingBakedVisualPrefabs.UnpackMissingBakedVisualPrefabs();

        string buildFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds");
        if (!Directory.Exists(buildFolder)) Directory.CreateDirectory(buildFolder);

        string apkPath = Path.Combine(buildFolder, "BergenBelsen_AR.apk");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/emptyy.unity" },
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.AutoRunPlayer
        };

        Debug.Log($"[BuildScript] Starting build to: {apkPath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildScript] ✅ APK Build Succeeded! Size: {report.summary.totalSize / 1024 / 1024} MB. Saved to '{apkPath}'.");
        }
        else
        {
            Debug.LogError($"[BuildScript] ❌ Build Failed with {report.summary.totalErrors} errors!");
        }
    }
}
