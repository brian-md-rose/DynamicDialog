using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace DynamicDialog.Editor
{
    /// <summary>
    /// Imports .dlg text files as <see cref="DialogRuleDatabase"/> assets. Errors appear in the Console
    /// with file and line. Saving the file during Play Mode swaps the new rules in immediately.
    /// </summary>
    [ScriptedImporter(1, "dlg")]
    public class DialogScriptImporter : ScriptedImporter
    {
        [Tooltip("Folder to look for audio clips in first. Empty = this file's folder.")]
        public string audioFolder;

        [Tooltip("If a clip isn't in the audio folder, search the whole project by file name.")]
        public bool searchWholeProject = true;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var source = DialogScriptParser.Parse(File.ReadAllText(ctx.assetPath), ctx.assetPath);
            DialogImportUtility.Import(ctx, source, audioFolder, searchWholeProject);
        }
    }
}
