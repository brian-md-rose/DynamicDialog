using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicDialog.Editor
{
    /// <summary>The kinds of names the rule editor offers in its drop-downs.</summary>
    public enum DialogStringCategory
    {
        Characters,
        Concepts,
        Facts,
        Values,
        Animations,
        Audio,
    }

    /// <summary>
    /// The names the Dialog Rule Editor offers in its drop-downs (characters, concepts, fact keys,
    /// fact values, animations and audio clips), so writers pick names instead of typing them.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogStringTable", menuName = "Dynamic Dialog/Rule Editor String Table")]
    public class DialogStringTable : ScriptableObject
    {
        public static readonly DialogStringCategory[] AllCategories =
            (DialogStringCategory[])Enum.GetValues(typeof(DialogStringCategory));

        [Tooltip("Speakers: used by 'who', 'then <Character>' and 'remember <Character> ...'.")]
        public List<string> characters = new List<string> { "Player" };

        [Tooltip("Events and followups: used by 'concept' and 'then ... <Concept>'.")]
        public List<string> concepts = new List<string> { "OnTalk", "OnSee", "OnEnterArea", "TalkIdle" };

        [Tooltip("Fact keys: used by 'if <fact> ...', 'remember <fact>' and 'forget <fact>'.")]
        public List<string> facts = new List<string> { "rand", "object", "area" };

        [Tooltip("Fact values that are names rather than numbers, e.g. 'if area == garden'.")]
        public List<string> values = new List<string>();

        [Tooltip("Animation triggers: used by 'say ... anim <Name>'.")]
        public List<string> animations = new List<string>();

        [Tooltip("Audio clip names: used by 'say ... audio <Name>'.")]
        public List<string> audio = new List<string>();

        public List<string> Get(DialogStringCategory category)
        {
            switch (category)
            {
                case DialogStringCategory.Characters: return characters ??= new List<string>();
                case DialogStringCategory.Concepts: return concepts ??= new List<string>();
                case DialogStringCategory.Facts: return facts ??= new List<string>();
                case DialogStringCategory.Values: return values ??= new List<string>();
                case DialogStringCategory.Animations: return animations ??= new List<string>();
                case DialogStringCategory.Audio: return audio ??= new List<string>();
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        public bool Contains(DialogStringCategory category, string name) =>
            !string.IsNullOrEmpty(name) && Get(category).Contains(name, StringComparer.Ordinal);

        /// <summary>Adds a name if it isn't there yet. Returns true if it was added.</summary>
        public bool Add(DialogStringCategory category, string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || Contains(category, name)) return false;
            Get(category).Add(name);
            return true;
        }

        public static string Describe(DialogStringCategory category)
        {
            switch (category)
            {
                case DialogStringCategory.Characters: return "Speakers, used by 'who', 'then <Character>' and 'remember <Character> ...'.";
                case DialogStringCategory.Concepts: return "Events and followups, used by 'concept' and 'then ... <Concept>'.";
                case DialogStringCategory.Facts: return "Fact keys, used by 'if', 'remember', 'forget' and '@partition'.";
                case DialogStringCategory.Values: return "Fact values that are names rather than numbers, e.g. 'garden' in 'if area == garden'.";
                case DialogStringCategory.Animations: return "Animation triggers, used by 'say ... anim <Name>'.";
                case DialogStringCategory.Audio: return "Audio clip names, used by 'say ... audio <Name>'.";
                default: return "";
            }
        }
    }
}
