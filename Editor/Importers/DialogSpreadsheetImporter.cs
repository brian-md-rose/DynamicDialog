using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace DynamicDialog.Editor
{
    /// <summary>
    /// Imports spreadsheets saved as CSV or TSV. Give the file a .dlgcsv or .dlgtsv extension so Unity
    /// knows it holds dialog rather than other data. See <see cref="SpreadsheetParser"/> for the columns.
    /// </summary>
    [ScriptedImporter(1, new[] { "dlgcsv", "dlgtsv" })]
    public class DialogSpreadsheetImporter : ScriptedImporter
    {
        [Tooltip("Folder to look for audio clips in first. Empty = this file's folder.")]
        public string audioFolder;

        [Tooltip("If a clip isn't in the audio folder, search the whole project by file name.")]
        public bool searchWholeProject = true;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            char delimiter = ctx.assetPath.EndsWith(".dlgtsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',';
            var source = SpreadsheetParser.Parse(File.ReadAllText(ctx.assetPath), ctx.assetPath, delimiter);
            DialogImportUtility.Import(ctx, source, audioFolder, searchWholeProject);
        }
    }
}
