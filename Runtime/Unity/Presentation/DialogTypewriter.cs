using System;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Typewriter reveal settings: how many characters of a line are visible after a given time.
    /// The built-in speech bubble and subtitle bar use it; custom presenters can too.
    /// </summary>
    /// <remarks>
    /// Text appears at <see cref="charactersPerSecond"/>, but speeds up if needed so the whole line is
    /// visible by <see cref="finishBy"/> of the line's duration. A line is never still typing when it ends.
    /// </remarks>
    [Serializable]
    public class DialogTypewriter
    {
        /// <summary>Reveal text a character at a time. When false, the whole line shows at once.</summary>
        [Tooltip("Reveal text a character at a time. When off, the whole line shows at once.")]
        public bool enabled = true;

        /// <summary>Characters revealed per second.</summary>
        [Tooltip("Characters revealed per second.")]
        [Min(1f)] public float charactersPerSecond = 45f;

        /// <summary>
        /// The fraction of the line's duration by which the whole line must be visible; typing speeds up to make it.
        /// 0 turns this off.
        /// </summary>
        [Tooltip("The fraction of the line's duration by which the whole line must be visible; typing speeds up to make it. 0 turns this off.")]
        [Range(0f, 1f)] public float finishBy = 0.7f;

        /// <summary>How many of <paramref name="totalCharacters"/> are visible <paramref name="elapsed"/> seconds into a line.</summary>
        /// <param name="totalCharacters">Visible characters in the whole line (rich-text tags don't count).</param>
        /// <param name="elapsed">Seconds since the line started.</param>
        /// <param name="lineDuration">How long the line lasts, in seconds; 0 or less if unknown.</param>
        /// <returns>From 0 to <paramref name="totalCharacters"/>.</returns>
        public int VisibleCharacters(int totalCharacters, float elapsed, float lineDuration)
        {
            if (totalCharacters <= 0) return 0;
            if (!enabled) return totalCharacters;
            if (elapsed <= 0f) return 0;
            float rate = CharactersPerSecond(totalCharacters, lineDuration);
            double visible = Math.Floor(elapsed * (double)rate + 1e-4);
            return visible >= totalCharacters ? totalCharacters : (int)visible;
        }

        /// <summary>The reveal speed for a line: <see cref="charactersPerSecond"/>, or faster to meet <see cref="finishBy"/>.</summary>
        /// <param name="totalCharacters">Visible characters in the whole line.</param>
        /// <param name="lineDuration">How long the line lasts, in seconds; 0 or less if unknown.</param>
        public float CharactersPerSecond(int totalCharacters, float lineDuration)
        {
            float rate = Mathf.Max(1f, charactersPerSecond);
            if (finishBy > 0f && lineDuration > 0f && totalCharacters > 0)
                rate = Mathf.Max(rate, totalCharacters / (lineDuration * finishBy));
            return rate;
        }

        /// <summary>Seconds until the whole line is visible. 0 when <see cref="enabled"/> is false.</summary>
        /// <param name="totalCharacters">Visible characters in the whole line.</param>
        /// <param name="lineDuration">How long the line lasts, in seconds; 0 or less if unknown.</param>
        public float RevealSeconds(int totalCharacters, float lineDuration) =>
            !enabled || totalCharacters <= 0 ? 0f : totalCharacters / CharactersPerSecond(totalCharacters, lineDuration);
    }
}
