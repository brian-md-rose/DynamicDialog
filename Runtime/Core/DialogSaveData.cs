using System;
using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>
    /// Everything dialog needs to remember across a save: world memory, each character's memory,
    /// and which rules have played (for once and cooldown). Plain serializable fields, so
    /// Unity's JsonUtility or any other serializer can write it. Names are stored as text, since
    /// symbol IDs differ between runs. Lines in progress and pending followups are not saved.
    /// </summary>
    [Serializable]
    public class DialogSaveData
    {
        /// <summary>Format version of this save data.</summary>
        public int version = 1;
        /// <summary>World memory.</summary>
        public List<FactMemoryEntry> world = new List<FactMemoryEntry>();
        /// <summary>Each character's memory. Characters with no facts are left out.</summary>
        public List<CharacterMemorySave> characters = new List<CharacterMemorySave>();
        /// <summary>Once and cooldown state of every rule that has played.</summary>
        public List<RuleStateSave> rules = new List<RuleStateSave>();
    }

    /// <summary>One character's saved memory.</summary>
    [Serializable]
    public class CharacterMemorySave
    {
        /// <summary>The character's name.</summary>
        public string who;
        /// <summary>Its facts.</summary>
        public List<FactMemoryEntry> facts = new List<FactMemoryEntry>();
    }

    /// <summary>One rule's saved once and cooldown state.</summary>
    [Serializable]
    public class RuleStateSave
    {
        /// <summary>The rule set's name (a database's asset name).</summary>
        public string set;
        /// <summary>The rule's name.</summary>
        public string rule;
        /// <summary>A once rule that has been used.</summary>
        public bool disabled;
        /// <summary>How many times it played.</summary>
        public int timesFired;

        /// <summary>Seconds between the rule last playing and the save, or -1 if it never played.</summary>
        public float secondsSinceFired = -1f;
    }

    /// <summary>Saves and restores a <see cref="DialogEngine"/>'s memory and rule state.</summary>
    public static class DialogSaveExtensions
    {
        /// <summary>Captures memory and rule state. Only rules that have played are saved.</summary>
        public static DialogSaveData SaveState(this DialogEngine engine)
        {
            double now = engine.Clock.Now;
            var data = new DialogSaveData { world = engine.World.Save(now) };
            foreach (var c in engine.Characters)
            {
                var facts = c.Memory.Save(now);
                if (facts.Count > 0) data.characters.Add(new CharacterMemorySave { who = c.Who.Name, facts = facts });
            }
            foreach (var set in engine.Matcher.Sets)
                foreach (var r in set.Rules)
                {
                    if (r.State.TimesFired == 0 && !r.State.Disabled) continue;
                    data.rules.Add(new RuleStateSave
                    {
                        set = set.Name,
                        rule = r.Name,
                        disabled = r.State.Disabled,
                        timesFired = r.State.TimesFired,
                        secondsSinceFired = double.IsNegativeInfinity(r.State.LastFiredTime) ? -1f : (float)Math.Max(0, now - r.State.LastFiredTime),
                    });
                }
            return data;
        }

        /// <summary>
        /// Replaces all memory and rule state with <paramref name="data"/>. Characters not in the save
        /// lose their memory. Returns rule states for rule sets that aren't loaded yet; pass them to
        /// <see cref="ApplyRuleStates"/> when those sets load (the Unity director does this for you).
        /// </summary>
        public static List<RuleStateSave> LoadState(this DialogEngine engine, DialogSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            double now = engine.Clock.Now;

            engine.World.Load(data.world, now);
            foreach (var c in engine.Characters) c.Memory.Clear();
            foreach (var c in data.characters ?? new List<CharacterMemorySave>())
                if (!string.IsNullOrEmpty(c.who)) engine.GetOrCreateCharacter(c.who).Memory.Load(c.facts, now);

            var pending = new List<RuleStateSave>(data.rules ?? new List<RuleStateSave>());
            foreach (var set in engine.Matcher.Sets)
            {
                set.ResetState();
                ApplyRuleStates(set, pending, now);
            }
            return pending;
        }

        /// <summary>
        /// Applies saved states that belong to <paramref name="set"/> and removes them from <paramref name="states"/>.
        /// Rules are matched by set name and rule name (ignoring case). Returns how many were applied.
        /// </summary>
        public static int ApplyRuleStates(RuleSet set, List<RuleStateSave> states, double now)
        {
            if (states == null || states.Count == 0) return 0;
            int applied = 0;
            for (int i = states.Count - 1; i >= 0; i--)
            {
                var s = states[i];
                if (!string.Equals(s.set, set.Name, StringComparison.OrdinalIgnoreCase)) continue;
                var rule = set.FindRule(s.rule);
                if (rule == null) continue;
                rule.State.Disabled = s.disabled;
                rule.State.TimesFired = s.timesFired;
                rule.State.LastFiredTime = s.secondsSinceFired < 0 ? double.NegativeInfinity : now - s.secondsSinceFired;
                states.RemoveAt(i);
                applied++;
            }
            return applied;
        }
    }
}
