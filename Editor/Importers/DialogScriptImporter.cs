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
        /// <summary>Folder to look for audio clips in first. Empty means this file's folder.</summary>
        [Tooltip("Folder to look for audio clips in first. Empty = this file's folder.")]
        public string audioFolder;

        /// <summary>If a clip isn't in the audio folder, search the whole project by file name.</summary>
        [Tooltip("If a clip isn't in the audio folder, search the whole project by file name.")]
        public bool searchWholeProject = true;

        /// <summary>Imports the file as a <see cref="DialogRuleDatabase"/>.</summary>
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var source = DialogScriptParser.Parse(File.ReadAllText(ctx.assetPath), ctx.assetPath);
            DialogImportUtility.Import(ctx, source, audioFolder, searchWholeProject);
        }
    }
}
