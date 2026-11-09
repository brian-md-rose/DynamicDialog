using System.IO;
using UnityEditor;
using UnityEngine;

namespace DynamicDialog.Editor
{
    /// <summary>
    /// Imports TextMeshPro's Essential Resources (default font, settings and shaders) when a Dynamic Dialog sample is
    /// imported into a project that doesn't have them yet. The samples' speech bubbles and subtitle bar use
    /// TextMeshPro text, which shows nothing (and Unity asks to import these) without them.
    /// </summary>
    sealed class DialogTmpEssentials : AssetPostprocessor
    {
        /// <summary>Where Package Manager imports this package's samples: Assets/Samples/Dynamic Dialog/&lt;version&gt;/&lt;sample&gt;.</summary>
        internal const string SamplesFolder = "Assets/Samples/Dynamic Dialog/";

        // Survives the domain reload that importing a sample's scripts causes.
        const string PendingKey = "DynamicDialog.ImportTmpEssentials";

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (SessionState.GetBool(PendingKey, false)) return;
            foreach (var path in imported)
            {
                if (!IsSampleAsset(path)) continue;
                SessionState.SetBool(PendingKey, true);
                Schedule();
                return;
            }
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (SessionState.GetBool(PendingKey, false)) Schedule();
        }

        static void Schedule()
        {
            EditorApplication.update -= WaitForIdle;
            EditorApplication.update += WaitForIdle;
        }

        internal static bool IsSampleAsset(string path) =>
            path != null && path.Replace('\\', '/').StartsWith(SamplesFolder, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>True when the project has a TMP Settings asset, which Essential Resources provides.</summary>
        internal static bool HasEssentials() => AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;

        // Imports can't start during another import, a compile or Play Mode, so wait for the editor to be idle.
        static void WaitForIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= WaitForIdle;
            ImportIfMissing();
        }

        static void ImportIfMissing()
        {
            SessionState.EraseBool(PendingKey);
            if (HasEssentials()) return;
            string package = FindEssentialsPackage();
            if (package == null)
            {
                Debug.LogWarning("Dynamic Dialog: this sample's speech bubbles use TextMeshPro. Import its Essential Resources " +
                                 "with Window > TextMeshPro > Import TMP Essential Resources.");
                return;
            }
            Debug.Log("Dynamic Dialog: importing TextMeshPro Essential Resources, which the samples' speech bubbles and subtitles use.");
            AssetDatabase.ImportPackage(package, false);
        }

        /// <summary>
        /// The Essential Resources package ships inside uGUI 2.x (Unity 6) or the TextMeshPro package (earlier versions).
        /// </summary>
        internal static string FindEssentialsPackage()
        {
            foreach (var name in new[] { "com.unity.ugui", "com.unity.textmeshpro" })
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + name);
                if (info == null || string.IsNullOrEmpty(info.resolvedPath)) continue;
                string path = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (File.Exists(path)) return path;
            }
            return null;
        }
    }
}
