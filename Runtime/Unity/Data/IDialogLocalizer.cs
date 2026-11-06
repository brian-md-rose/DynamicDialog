using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Supplies translated subtitles and voice clips (talk slide 18: players hear characters in their
    /// own language). Put a component implementing this on the Dialog Director, or set
    /// <see cref="DialogDirector.Localizer"/> from code. Return null to fall back to the authored text or clip.
    /// The key comes from <see cref="LineKeys"/>: the line's key, else its clip's name, else "&lt;rule&gt;_&lt;n&gt;".
    /// </summary>
    public interface IDialogLocalizer
    {
        /// <summary>The translated subtitle for <paramref name="key"/>, or null to keep <paramref name="line"/>'s authored text.</summary>
        string GetText(string key, DialogLineDefinition line);
        /// <summary>The translated voice clip for <paramref name="key"/>, or null to keep <paramref name="line"/>'s authored clip.</summary>
        AudioClip GetClip(string key, DialogLineDefinition line);
    }
}
