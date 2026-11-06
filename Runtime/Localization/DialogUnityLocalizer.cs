using UnityEngine;
using UnityEngine.Localization.Settings;

namespace DynamicDialog
{
    /// <summary>
    /// Looks up subtitles and voice clips in Unity Localization tables, using each line's key
    /// (see <see cref="LineKeys"/>). Add it to the Dialog Director. Lines with no entry in the current
    /// language fall back to their authored text and clip. Only compiled when the Localization package is installed.
    /// To fill the string table, use the rule database's "Export String Table CSV" and import it.
    /// </summary>
    [AddComponentMenu("Dynamic Dialog/Unity Localization Localizer")]
    public class DialogUnityLocalizer : MonoBehaviour, IDialogLocalizer
    {
        [Tooltip("String table collection holding subtitles. Empty = keep authored text.")]
        [SerializeField] string stringTable = "Dialog";

        [Tooltip("Asset table collection holding localized voice clips. Empty = keep authored clips.")]
        [SerializeField] string audioTable = "";

        /// <summary>The string table collection holding subtitles. Empty keeps the authored text.</summary>
        public string StringTable { get => stringTable; set => stringTable = value; }
        /// <summary>The asset table collection holding voice clips. Empty keeps the authored clips.</summary>
        public string AudioTable { get => audioTable; set => audioTable = value; }

        /// <inheritdoc/>
        public string GetText(string key, DialogLineDefinition line)
        {
            if (string.IsNullOrEmpty(stringTable) || string.IsNullOrEmpty(key) || !LocalizationSettings.HasSettings) return null;
            var table = LocalizationSettings.StringDatabase.GetTable(stringTable);
            var entry = table != null ? table.GetEntry(key) : null;
            if (entry == null || string.IsNullOrEmpty(entry.Value)) return null;
            return entry.GetLocalizedString();
        }

        /// <inheritdoc/>
        public AudioClip GetClip(string key, DialogLineDefinition line)
        {
            if (string.IsNullOrEmpty(audioTable) || string.IsNullOrEmpty(key) || !LocalizationSettings.HasSettings) return null;
            var table = LocalizationSettings.AssetDatabase.GetTable(audioTable);
            var entry = table != null ? table.GetEntry(key) : null;
            if (entry == null || entry.IsEmpty) return null;
            return LocalizationSettings.AssetDatabase.GetLocalizedAsset<AudioClip>(audioTable, key);
        }
    }
}
