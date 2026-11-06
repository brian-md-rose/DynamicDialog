using System.IO;

namespace DynamicDialog
{
    /// <summary>
    /// The localization key for a line, the same everywhere (importers, exports, the runtime):
    /// the explicit key if there is one, else the audio file's name (voice files are usually named
    /// after their line), else "&lt;rule&gt;_&lt;n&gt;" with n counting from 1.
    /// </summary>
    public static class LineKeys
    {
        /// <summary>The key for line <paramref name="lineIndex"/> (0-based) of a rule.</summary>
        /// <param name="ruleName">The rule's name.</param>
        /// <param name="lineIndex">The variation's index, counting from 0.</param>
        /// <param name="explicitKey">The authored key, if any.</param>
        /// <param name="audioName">The audio clip's name or path, if any.</param>
        public static string For(string ruleName, int lineIndex, string explicitKey, string audioName)
        {
            if (!string.IsNullOrWhiteSpace(explicitKey)) return explicitKey.Trim();
            if (!string.IsNullOrWhiteSpace(audioName)) return Path.GetFileNameWithoutExtension(audioName.Trim().Replace('\\', '/'));
            return $"{ruleName}_{lineIndex + 1}";
        }

        /// <summary>The key for line <paramref name="lineIndex"/> (0-based) of an authored rule.</summary>
        public static string For(RuleSource rule, int lineIndex) =>
            For(rule.Name, lineIndex, rule.Lines[lineIndex].Key, rule.Lines[lineIndex].Audio);
    }
}
