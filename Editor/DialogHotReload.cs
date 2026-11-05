using System;
using System.Linq;
using UnityEditor;

namespace DynamicDialog.Editor
{
    /// <summary>
    /// When a .dlg or spreadsheet is reimported during Play Mode (for example, a writer saves it),
    /// the running director switches to the new rules without restarting. Talk slide 127: the faster
    /// writers can iterate, the better they write.
    /// </summary>
    internal class DialogHotReload : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!EditorApplication.isPlaying) return;
            DialogDirector director = null;

            foreach (var path in imported)
            {
                if (!IsDialogSource(path)) continue;
                var fresh = AssetDatabase.LoadAssetAtPath<DialogRuleDatabase>(path);
                if (fresh == null) continue;

                director = director != null ? director : DialogDirector.Instance;
                if (director == null || director.Engine == null) return;

                foreach (var db in director.LoadedDatabases.ToList())
                {
                    if (ReferenceEquals(db, fresh)) fresh.Invalidate();
                    else if (!ReferenceEquals(db, null) && string.Equals(db.sourcePath, path, StringComparison.OrdinalIgnoreCase))
                        director.ReplaceDatabase(db, fresh);
                }
            }
        }

        internal static bool IsDialogSource(string path) =>
            path.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".dlgcsv", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".dlgtsv", StringComparison.OrdinalIgnoreCase);
    }
}
