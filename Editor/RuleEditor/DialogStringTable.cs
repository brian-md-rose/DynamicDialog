using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DynamicDialog.Editor
{
    /// <summary>The kinds of names the rule editor offers in its drop-downs.</summary>
    public enum DialogStringCategory
    {
        /// <summary>Character names.</summary>
        Characters,
        /// <summary>Concept names.</summary>
        Concepts,
        /// <summary>Fact keys.</summary>
        Facts,
        /// <summary>Fact values that are names.</summary>
        Values,
        /// <summary>Animator triggers.</summary>
        Animations,
        /// <summary>Audio clip names.</summary>
        Audio,
        /// <summary>Game event names, used by "do".</summary>
        Events,
    }

    /// <summary>
    /// The names the Dialog Rule Editor offers in its drop-downs (characters, concepts, fact keys,
    /// fact values, animations and audio clips), so writers pick names instead of typing them.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogStringTable", menuName = "Dynamic Dialog/Rule Editor String Table")]
    public class DialogStringTable : ScriptableObject
    {
        internal static readonly DialogStringCategory[] AllCategories =
            (DialogStringCategory[])Enum.GetValues(typeof(DialogStringCategory));

        /// <summary>Character names, used by "who", "then &lt;Character&gt;" and "remember &lt;Character&gt; ...".</summary>
        [Tooltip("Speakers: used by 'who', 'then <Character>' and 'remember <Character> ...'.")]
        public List<string> characters = new List<string> { "Player" };

        /// <summary>Concept names, used by "concept" and "then ... &lt;Concept&gt;".</summary>
        [Tooltip("Events and followups: used by 'concept' and 'then ... <Concept>'.")]
        public List<string> concepts = new List<string> { "OnTalk", "OnSee", "OnEnterArea", "TalkIdle" };

        /// <summary>Fact keys, used by "if", "remember", "forget" and "@partition".</summary>
        [Tooltip("Fact keys: used by 'if <fact> ...', 'remember <fact>' and 'forget <fact>'.")]
        public List<string> facts = new List<string> { "rand", "object", "area" };

        /// <summary>Fact values that are names rather than numbers, e.g. "garden" in "if area == garden".</summary>
        [Tooltip("Fact values that are names rather than numbers, e.g. 'if area == garden'.")]
        public List<string> values = new List<string>();

        /// <summary>Animator trigger names, used by "say ... anim &lt;Name&gt;".</summary>
        [Tooltip("Animation triggers: used by 'say ... anim <Name>'.")]
        public List<string> animations = new List<string>();

        /// <summary>Audio clip names, used by "say ... audio &lt;Name&gt;".</summary>
        [Tooltip("Audio clip names: used by 'say ... audio <Name>'.")]
        public List<string> audio = new List<string>();

        /// <summary>Game event names, used by "do &lt;Event&gt; ...".</summary>
        [Tooltip("Game events: used by 'do <Event> ...'. Your code listens for them with DialogEvents.On or a Dialog Event Listener.")]
        public List<string> events = new List<string>();

        /// <summary>The list of names for a category.</summary>
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
                case DialogStringCategory.Events: return events ??= new List<string>();
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }

        /// <summary>Whether the category holds this exact name (case-sensitive).</summary>
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

        internal static string Describe(DialogStringCategory category)
        {
            switch (category)
            {
                case DialogStringCategory.Characters: return "Speakers, used by 'who', 'then <Character>' and 'remember <Character> ...'.";
                case DialogStringCategory.Concepts: return "Events and followups, used by 'concept' and 'then ... <Concept>'.";
                case DialogStringCategory.Facts: return "Fact keys, used by 'if', 'remember', 'forget' and '@partition'.";
                case DialogStringCategory.Values: return "Fact values that are names rather than numbers, e.g. 'garden' in 'if area == garden'.";
                case DialogStringCategory.Animations: return "Animation triggers, used by 'say ... anim <Name>'.";
                case DialogStringCategory.Audio: return "Audio clip names, used by 'say ... audio <Name>'.";
                case DialogStringCategory.Events: return "Game events, used by 'do <Event> ...'. Code listens with DialogEvents.On or a Dialog Event Listener.";
                default: return "";
            }
        }
    }
}
