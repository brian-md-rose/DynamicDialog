using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>One recorded variation of a line.</summary>
    [Serializable]
    public class DialogLineDefinition
    {
        [Tooltip("Subtitle text. Also used to estimate the line's length when there is no audio.")]
        [TextArea(1, 4)]
        public string text;

        public AudioClip clip;

        [Tooltip("Seconds the line lasts. 0 = the clip's length, or an estimate from the text.")]
        [Min(0f)]
        public float duration;

        [Tooltip("Optional Animator trigger to set on the speaker when the line starts.")]
        public string animatorTrigger;
    }

    /// <summary>What a rule does when it wins: a set of variations of one line.</summary>
    [Serializable]
    public class DialogResponse
    {
        public ResponseSelection selection = ResponseSelection.RandomNoRepeat;
        public List<DialogLineDefinition> lines = new List<DialogLineDefinition>();

        [NonSerialized] int lastIndex = -1;
        [NonSerialized] List<int> bag;

        /// <summary>Picks the next variation, or null if there are none.</summary>
        public DialogLineDefinition Pick(IRandomSource random)
        {
            int n = lines?.Count ?? 0;
            if (n == 0) return null;
            if (n == 1) return lines[lastIndex = 0];

            int index;
            switch (selection)
            {
                case ResponseSelection.Sequential:
                    index = (lastIndex + 1) % n;
                    break;

                case ResponseSelection.RandomNoRepeat:
                    index = RandomIndex(random, n - 1);
                    if (lastIndex >= 0 && index >= lastIndex) index++; // skip the last one
                    break;

                case ResponseSelection.Shuffle:
                    if (bag == null || bag.Count == 0) RefillBag(random, n);
                    index = bag[bag.Count - 1];
                    bag.RemoveAt(bag.Count - 1);
                    break;

                default:
                    index = RandomIndex(random, n);
                    break;
            }
            lastIndex = index;
            return lines[index];
        }

        /// <summary>Forgets selection history, e.g. when a database is reloaded.</summary>
        public void ResetSelection()
        {
            lastIndex = -1;
            bag = null;
        }

        void RefillBag(IRandomSource random, int n)
        {
            bag = bag ?? new List<int>(n);
            bag.Clear();
            for (int i = 0; i < n; i++) bag.Add(i);
            for (int i = n - 1; i > 0; i--)
            {
                int j = RandomIndex(random, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
            // Don't start a new cycle with the variation that ended the last one.
            if (n > 1 && bag[n - 1] == lastIndex) (bag[n - 1], bag[0]) = (bag[0], bag[n - 1]);
        }

        static int RandomIndex(IRandomSource random, int count) =>
            Math.Min(count - 1, (int)(random.NextFloat() * count));
    }
}
