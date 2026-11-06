using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicDialog
{
    /// <summary>
    /// Everything dialog needs to remember across a save: world memory, each character's memory,
    /// and which rules have played (for once and cooldown). Plain serializable fields, so
    /// Unity's JsonUtility or any other serializer can write it. Names are stored as text, since
    /// symbol IDs differ between runs. Lines in progress and pending followups are not saved.
    /// </summary>
    /// <remarks>
    /// Saves carry a <see cref="version"/>. Loading upgrades older saves (see <see cref="Upgrade"/>),
    /// and a save from a newer version of the package loads as far as it can instead of failing.
    /// </remarks>
    [Serializable]
    public class DialogSaveData
    {
        /// <summary>
        /// The format version this package writes. History: 1 is the 0.6 format; 2 has the same fields,
        /// and adds that rule states may be stored under a rule's former name and that states matching
        /// no rule are kept rather than dropped.
        /// </summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// Format version of this save data. 0 means it was written without one and is read like version 1
        /// (the 0.6 format). <see cref="DialogSaveExtensions.SaveState"/> always writes <see cref="CurrentVersion"/>.
        /// </summary>
        public int version = CurrentVersion;
        /// <summary>World memory.</summary>
        public List<FactMemoryEntry> world = new List<FactMemoryEntry>();
        /// <summary>Each character's memory. Characters with no facts are left out.</summary>
        public List<CharacterMemorySave> characters = new List<CharacterMemorySave>();
        /// <summary>
        /// Once and cooldown state of every rule that has played, including states kept for rules that
        /// no longer exist (see <see cref="DialogLoadReport.UnmatchedRules"/>).
        /// </summary>
        public List<RuleStateSave> rules = new List<RuleStateSave>();

        /// <summary>
        /// Brings this data up to <see cref="CurrentVersion"/> in place and tidies anything a serializer
        /// may have left half-filled: missing lists become empty, empty entries are removed, and negative
        /// or invalid counts and times are corrected. Never throws. Loading calls it for you.
        /// </summary>
        /// <returns>The version the data had before. Data from a newer version keeps its version number.</returns>
        public int Upgrade()
        {
            int from = version;

            // Version 0 (no version written) and 1 (0.6) need no field changes: version 2 reads the same
            // fields. Future format changes go here, one step per version.

            if (world == null) world = new List<FactMemoryEntry>();
            if (characters == null) characters = new List<CharacterMemorySave>();
            characters.RemoveAll(c => c == null);
            foreach (var c in characters)
                if (c.facts == null) c.facts = new List<FactMemoryEntry>();
            if (rules == null) rules = new List<RuleStateSave>();
            rules.RemoveAll(r => r == null || string.IsNullOrWhiteSpace(r.rule));
            foreach (var r in rules)
            {
                if (r.set == null) r.set = "";
                if (r.timesFired < 0) r.timesFired = 0;
                if (float.IsNaN(r.secondsSinceFired) || r.secondsSinceFired < 0f) r.secondsSinceFired = -1f;
            }

            if (version < CurrentVersion) version = CurrentVersion;
            return from;
        }
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
        /// <summary>The rule's name when the state was saved. A rule that lists it as a former name also matches.</summary>
        public string rule;
        /// <summary>A once rule that has been used.</summary>
        public bool disabled;
        /// <summary>How many times it played.</summary>
        public int timesFired;

        /// <summary>Seconds between the rule last playing and the save, or -1 if it never played.</summary>
        public float secondsSinceFired = -1f;

        /// <summary>A copy of this state.</summary>
        public RuleStateSave Clone() => new RuleStateSave
        {
            set = set, rule = rule, disabled = disabled, timesFired = timesFired, secondsSinceFired = secondsSinceFired,
        };

        /// <summary>"set/rule", for messages.</summary>
        public override string ToString() => string.IsNullOrEmpty(set) ? rule : set + "/" + rule;
    }

    /// <summary>A saved rule state that was applied to a rule under its new name.</summary>
    public readonly struct RuleRename
    {
        /// <summary>The rule set's name.</summary>
        public readonly string Set;
        /// <summary>The name in the save, one of the rule's <see cref="Rule.FormerNames"/>.</summary>
        public readonly string FormerName;
        /// <summary>The rule's name now.</summary>
        public readonly string Name;

        /// <summary>Creates a rename record.</summary>
        public RuleRename(string set, string formerName, string name)
        {
            Set = set;
            FormerName = formerName;
            Name = name;
        }

        /// <summary>"set/former -&gt; name".</summary>
        public override string ToString() => $"{Set}/{FormerName} -> {Name}";
    }

    /// <summary>
    /// What happened when a save was loaded: whether it was upgraded, which rule states were applied
    /// to renamed rules, which match no rule, and which wait for a rule set that isn't loaded.
    /// Nothing in a save is dropped: unmatched and waiting states are kept and written into later saves.
    /// </summary>
    public sealed class DialogLoadReport
    {
        readonly HashSet<string> unmatchedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The version the save was written with (0 if it had none).</summary>
        public int SavedVersion { get; internal set; }

        /// <summary>The save was written by an older version and upgraded (see <see cref="DialogSaveData.Upgrade"/>).</summary>
        public bool Upgraded => SavedVersion < DialogSaveData.CurrentVersion;

        /// <summary>
        /// The save was written by a newer version of the package. It is loaded as far as this version
        /// understands it; anything newer is ignored.
        /// </summary>
        public bool FromNewerVersion => SavedVersion > DialogSaveData.CurrentVersion;

        /// <summary>States applied to a rule through one of its former names ("renamed" in .dlg).</summary>
        public readonly List<RuleRename> RenamedRules = new List<RuleRename>();

        /// <summary>
        /// States for a loaded rule set that match no rule in it, by name or former name: the rule was removed,
        /// or renamed without listing its old name. They are kept and saved again, so they reapply if the rule
        /// comes back. Copies, as they were in the save.
        /// </summary>
        public readonly List<RuleStateSave> UnmatchedRules = new List<RuleStateSave>();

        /// <summary>
        /// States for rule sets that aren't loaded (for example a region's database). They are applied when the
        /// set loads. Copies, as they were in the save.
        /// </summary>
        public readonly List<RuleStateSave> WaitingRules = new List<RuleStateSave>();

        /// <summary>Something the game may want to know about: a save from a newer version, or unmatched rule states.</summary>
        public bool HasWarnings => FromNewerVersion || UnmatchedRules.Count > 0;

        internal bool AddUnmatched(RuleStateSave state)
        {
            if (!unmatchedKeys.Add(Key(state))) return false;
            UnmatchedRules.Add(state.Clone());
            return true;
        }

        internal void RemoveWaiting(RuleStateSave state)
        {
            string key = Key(state);
            WaitingRules.RemoveAll(w => string.Equals(Key(w), key, StringComparison.OrdinalIgnoreCase));
        }

        internal static string Key(RuleStateSave s) => (s.set ?? "") + "\n" + (s.rule ?? "");

        /// <summary>A one-paragraph summary, suitable for a log.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (FromNewerVersion)
                sb.Append($"The save is format version {SavedVersion}, newer than this package's {DialogSaveData.CurrentVersion}; loaded what this version understands. ");
            else if (Upgraded)
                sb.Append($"Upgraded a format version {SavedVersion} save to {DialogSaveData.CurrentVersion}. ");
            if (RenamedRules.Count > 0)
                sb.Append($"{RenamedRules.Count} rule state(s) applied under a former name: {string.Join(", ", RenamedRules)}. ");
            if (UnmatchedRules.Count > 0)
                sb.Append($"{UnmatchedRules.Count} saved rule state(s) match no rule, so they were not applied (they are kept in later saves; add 'renamed <old name>' to the rule if it was renamed): {string.Join(", ", UnmatchedRules)}. ");
            if (WaitingRules.Count > 0)
                sb.Append($"{WaitingRules.Count} rule state(s) wait for databases that aren't loaded. ");
            return sb.Length == 0 ? "Loaded with nothing to report." : sb.ToString().TrimEnd();
        }
    }

    /// <summary>Saves and restores a <see cref="DialogEngine"/>'s memory and rule state.</summary>
    public static class DialogSaveExtensions
    {
        /// <summary>Captures memory and rule state. Only rules that have played are saved.</summary>
        public static DialogSaveData SaveState(this DialogEngine engine)
        {
            double now = engine.Clock.Now;
            var data = new DialogSaveData { version = DialogSaveData.CurrentVersion, world = engine.World.Save(now) };
            foreach (var c in engine.Characters)
            {
                var facts = c.Memory.Save(now);
                if (facts.Count > 0) data.characters.Add(new CharacterMemorySave { who = c.Who.Name, facts = facts });
            }
            foreach (var set in engine.Matcher.Sets)
                foreach (var r in set.Rules)
                {
                    var state = CaptureState(set, r, now);
                    if (state != null) data.rules.Add(state);
                }
            return data;
        }

        /// <summary>
        /// The saved form of one rule's state, or null if the rule has never played (nothing to save).
        /// </summary>
        public static RuleStateSave CaptureState(RuleSet set, Rule rule, double now)
        {
            if (rule.State.TimesFired == 0 && !rule.State.Disabled) return null;
            return new RuleStateSave
            {
                set = set.Name,
                rule = rule.Name,
                disabled = rule.State.Disabled,
                timesFired = rule.State.TimesFired,
                secondsSinceFired = double.IsNegativeInfinity(rule.State.LastFiredTime) ? -1f : (float)Math.Max(0, now - rule.State.LastFiredTime),
            };
        }

        /// <summary>
        /// Replaces all memory and rule state with <paramref name="data"/>. Characters not in the save
        /// lose their memory. Returns rule states not applied: those for rule sets that aren't loaded yet
        /// (pass them to <see cref="ApplyRuleStates(RuleSet, List{RuleStateSave}, double)"/> when those sets load;
        /// the Unity director does this for you), and those that match no rule. Keep both and add them to
        /// later saves so nothing is lost. Older saves are upgraded in place first (see <see cref="DialogSaveData.Upgrade"/>).
        /// </summary>
        public static List<RuleStateSave> LoadState(this DialogEngine engine, DialogSaveData data) =>
            LoadState(engine, data, out _);

        /// <summary>
        /// <see cref="LoadState(DialogEngine, DialogSaveData)"/>, also reporting what happened: the save's version,
        /// states applied to renamed rules, states that match no rule, and states waiting for sets that aren't loaded.
        /// </summary>
        public static List<RuleStateSave> LoadState(this DialogEngine engine, DialogSaveData data, out DialogLoadReport report)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            report = new DialogLoadReport { SavedVersion = data.version };
            data.Upgrade();
            double now = engine.Clock.Now;

            engine.BeginFactsReset();
            try
            {
                engine.World.Load(data.world, now);
                foreach (var c in engine.Characters) c.Memory.Clear();
                foreach (var c in data.characters)
                    if (!string.IsNullOrEmpty(c.who)) engine.GetOrCreateCharacter(c.who).Memory.Load(c.facts, now);
            }
            finally
            {
                engine.EndFactsReset();
            }

            // Copies, so aging them later doesn't change the caller's data.
            var pending = new List<RuleStateSave>(data.rules.Count);
            foreach (var r in data.rules) pending.Add(r.Clone());
            foreach (var r in pending) report.WaitingRules.Add(r.Clone());

            foreach (var set in engine.Matcher.Sets)
            {
                set.ResetState();
                ApplyRuleStates(set, pending, now, report);
            }
            return pending;
        }

        /// <summary>
        /// Applies saved states that belong to <paramref name="set"/> and removes them from <paramref name="states"/>.
        /// Rules are matched by set name and rule name (ignoring case), then by the rules' former names
        /// (<see cref="Rule.FormerNames"/>). States that match no rule stay in <paramref name="states"/>.
        /// Returns how many were applied.
        /// </summary>
        public static int ApplyRuleStates(RuleSet set, List<RuleStateSave> states, double now) =>
            ApplyRuleStates(set, states, now, null);

        /// <summary>
        /// <see cref="ApplyRuleStates(RuleSet, List{RuleStateSave}, double)"/>, recording renamed and unmatched rules
        /// in <paramref name="report"/> (which may be null). If two states reach the same rule (one under its current
        /// name, one under a former name) they are combined: used if either was used, plays added together, and the
        /// most recent play kept for the cooldown.
        /// </summary>
        public static int ApplyRuleStates(RuleSet set, List<RuleStateSave> states, double now, DialogLoadReport report)
        {
            if (set == null || states == null || states.Count == 0) return 0;
            int applied = 0;
            HashSet<Rule> touched = null;

            // Current names first, so a rule's own state wins over one found through a former name.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < states.Count;)
                {
                    var s = states[i];
                    if (s == null)
                    {
                        states.RemoveAt(i);
                        continue;
                    }
                    if (!string.Equals(s.set ?? "", set.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        i++;
                        continue;
                    }
                    var rule = pass == 0 ? set.FindRule(s.rule) : set.FindRule(s.rule, includeFormerNames: true);
                    if (rule == null)
                    {
                        i++;
                        continue;
                    }

                    if (touched == null) touched = new HashSet<Rule>();
                    double lastFired = s.secondsSinceFired < 0 ? double.NegativeInfinity : now - s.secondsSinceFired;
                    if (touched.Add(rule))
                    {
                        rule.State.Disabled = s.disabled;
                        rule.State.TimesFired = Math.Max(0, s.timesFired);
                        rule.State.LastFiredTime = lastFired;
                    }
                    else
                    {
                        rule.State.Disabled |= s.disabled;
                        rule.State.TimesFired += Math.Max(0, s.timesFired);
                        rule.State.LastFiredTime = Math.Max(rule.State.LastFiredTime, lastFired);
                    }

                    if (report != null)
                    {
                        if (pass == 1) report.RenamedRules.Add(new RuleRename(set.Name, s.rule, rule.Name));
                        report.RemoveWaiting(s);
                    }
                    states.RemoveAt(i);
                    applied++;
                }
            }

            if (report != null)
                foreach (var s in states)
                    if (s != null && string.Equals(s.set ?? "", set.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        report.RemoveWaiting(s);
                        report.AddUnmatched(s);
                    }
            return applied;
        }
    }
}
