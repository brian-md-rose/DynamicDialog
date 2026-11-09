// Player build smoke test for CI: builds the First Bark sample, which CI~/make-host-project.sh --first-bark copies into
// the host project's Assets/FirstBark. Run with -executeMethod DynamicDialogCiBuild.BuildFirstBark (GameCI's
// unity-builder passes it as buildMethod). Arguments, all optional:
//   -buildTarget <target>             defaults to the editor's active target
//   -customBuildPath <path>           the player's file or folder (GameCI sets this); default Builds/FirstBark/FirstBark
//   -scriptingBackend <IL2CPP|Mono>   default IL2CPP, which the package has no other build check for
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class DynamicDialogCiBuild
{
    const string Scene = "Assets/FirstBark/FirstBark.unity";

    public static void BuildFirstBark()
    {
        try
        {
            Build();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static void Build()
    {
        if (!File.Exists(Scene)) throw new Exception(Scene + " is missing; generate the project with make-host-project.sh --first-bark.");
        CheckTmpEssentials();

        var target = EditorUserBuildSettings.activeBuildTarget;
        string targetArg = Arg("-buildTarget");
        if (targetArg != null && !Enum.TryParse(targetArg, true, out target)) throw new Exception("Unknown -buildTarget " + targetArg);
        var group = BuildPipeline.GetBuildTargetGroup(target);

        var backend = string.Equals(Arg("-scriptingBackend") ?? "IL2CPP", "Mono", StringComparison.OrdinalIgnoreCase)
            ? ScriptingImplementation.Mono2x
            : ScriptingImplementation.IL2CPP;
#if UNITY_2021_2_OR_NEWER
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(group), backend);
#else
        PlayerSettings.SetScriptingBackend(group, backend);
#endif

        string path = Arg("-customBuildPath") ?? "Builds/FirstBark/FirstBark";
        if (target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64)
            if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) path += ".exe";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

        Debug.Log($"DynamicDialogCiBuild: building {Scene} for {target} ({backend}) to {path}");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            target = target,
            targetGroup = group,
            locationPathName = path,
            options = BuildOptions.StrictMode,
        });
        var summary = report.summary;
        Debug.Log($"DynamicDialogCiBuild: {summary.result}, {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalSize} bytes");
        if (summary.result != BuildResult.Succeeded)
        {
            foreach (var message in report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                Debug.LogError(message.content);
            EditorApplication.Exit(1);
        }
    }

    // First Bark's speech bubbles use TextMeshPro. make-host-project.sh unpacks its Essential Resources before Unity starts
    // (importing them from here would finish asynchronously, after the build, on Unity 6).
    static void CheckTmpEssentials()
    {
        if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            throw new Exception("TMP Essential Resources are missing; generate the project with make-host-project.sh (full profile).");
    }

    static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
