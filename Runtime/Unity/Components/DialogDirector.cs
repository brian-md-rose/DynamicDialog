using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DynamicDialog
{
    /// <summary>
    /// Compares Unity objects by reference. Unity's own equality treats every destroyed object as null,
    /// and a reimported asset leaves a destroyed object behind.
    /// </summary>
    internal sealed class ReferenceComparer : IEqualityComparer<DialogRuleDatabase>
    {
        public static readonly ReferenceComparer Instance = new ReferenceComparer();
        public bool Equals(DialogRuleDatabase a, DialogRuleDatabase b) => ReferenceEquals(a, b);
        public int GetHashCode(DialogRuleDatabase o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
    }

    /// <summary>Game time from Unity. Stops while the game is paused with timeScale = 0.</summary>
    public sealed class UnityClock : IClock
    {
        /// <summary><c>Time.timeAsDouble</c>.</summary>
        public double Now => Time.timeAsDouble;
    }

    /// <summary>
    /// The scene's dialog hub. It owns the <see cref="DialogEngine"/> (rules, world memory) and the
    /// <see cref="DialogConductor"/> (who is speaking, followup scheduling), and loads rule databases.
    /// Add one per scene, or one in a bootstrap scene. Fact-provider components on the same
    /// GameObject add world facts to every query.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Dynamic Dialog/Dialog Director")]
    public class DialogDirector : MonoBehaviour, IFactProvider
    {
        /// <summary>Old name of <see cref="WellKnownFacts.Map"/>.</summary>
        [Obsolete("Use WellKnownFacts.Map.")]
        public static readonly Symbol MapFact = WellKnownFacts.Map;
        /// <summary>Old name of <see cref="WellKnownFacts.SpeakersTalking"/>.</summary>
        [Obsolete("Use WellKnownFacts.SpeakersTalking.")]
        public static readonly Symbol SpeakersTalkingFact = WellKnownFacts.SpeakersTalking;

        [Tooltip("Rule databases that are always loaded. Regions can load more with a DialogRegion.")]
        [SerializeField] List<DialogRuleDatabase> databases = new List<DialogRuleDatabase>();

        [Tooltip("0 picks a new seed each run. Any other value makes choices repeatable.")]
        [SerializeField] int randomSeed;

        [Tooltip("Adds 'map' = active scene name and 'speakers_talking' = lines in progress to every query.")]
        [SerializeField] bool addBuiltInWorldFacts = true;

        [Tooltip("If on, a followup sent to a named character needs that character to be in earshot.")]
        [SerializeField] bool namedFollowupsRequireEarshot;

        [Header("Debugging")]
        [SerializeField] bool logLines;
        [SerializeField] bool logNoMatch;
        [Tooltip("How many recent lines to keep for the debug tools.")]
        [SerializeField, Min(0)] int historySize = 64;

        [Tooltip("Record every lookup (query, rules tested, winner) for the Dialog Debugger window and overlay. Editor and development builds only.")]
        [SerializeField] bool recordLookups = true;

        [Tooltip("How many lookups to keep.")]
        [SerializeField, Min(1)] int recordCapacity = 256;

        static DialogDirector instance;

        readonly Dictionary<DialogRuleDatabase, int> loaded = new Dictionary<DialogRuleDatabase, int>(ReferenceComparer.Instance);
        readonly Dictionary<DialogRuleDatabase, RuleSet> activeSets = new Dictionary<DialogRuleDatabase, RuleSet>(ReferenceComparer.Instance);
        readonly Dictionary<DialogRuleDatabase, DialogRuleDatabase> aliases = new Dictionary<DialogRuleDatabase, DialogRuleDatabase>(ReferenceComparer.Instance);
        readonly Queue<DialogLine> history = new Queue<DialogLine>();
        // Rule state for databases that aren't loaded: from a save, or kept when a region unloaded.
        // secondsSinceFired is relative to pendingAsOf; AgePending brings it up to date.
        // Also holds states that match no rule of a loaded database (removed or renamed rules): they are
        // kept and saved again rather than dropped.
        List<RuleStateSave> pendingRuleStates = new List<RuleStateSave>();
        double pendingAsOf;

        /// <summary>
        /// What happened in the most recent <see cref="LoadState"/>: the save's version, rule states applied
        /// to renamed rules, states that match no rule, and states waiting for databases that aren't loaded.
        /// Updated as databases load later (for example by a region). Null until a save is loaded.
        /// </summary>
        public DialogLoadReport LastLoadReport { get; private set; }

        /// <summary>Rules, characters and world memory. Null until the director initializes (in Awake, or on first use of <see cref="Instance"/>).</summary>
        public DialogEngine Engine { get; private set; }
        /// <summary>Who is speaking, and followup scheduling.</summary>
        public DialogConductor Conductor { get; private set; }
        /// <summary>The random source for tie-breaking, "rand" and picking variations. Seeded by the Random Seed setting.</summary>
        public IRandomSource Random { get; private set; }

        /// <summary>Translates subtitles and voice clips. Defaults to an IDialogLocalizer component on this GameObject.</summary>
        public IDialogLocalizer Localizer { get; set; }

        /// <summary>Recorded lookups, or null when recording is off.</summary>
        public DialogRecorder Recorder => Engine?.Recorder;

        /// <summary>Raised when a director starts up, so editor tools can attach to it.</summary>
        public static event Action<DialogDirector> Initialized;

        /// <summary>The most recent lines, oldest first.</summary>
        public IEnumerable<DialogLine> History => history;

        /// <summary>The active director, found on demand if none has registered yet.</summary>
        public static DialogDirector Instance
        {
            get
            {
                if (instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    instance = FindAnyObjectByType<DialogDirector>();
#else
                    instance = FindObjectOfType<DialogDirector>();
#endif
                    if (instance != null) instance.EnsureInitialized();
                }
                return instance;
            }
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("[DynamicDialog] More than one DialogDirector is active. Disabling the extra one.", this);
                enabled = false;
                return;
            }
            instance = this;
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (Engine != null) return;

            Random = randomSeed != 0 ? new SystemRandomSource(randomSeed) : new SystemRandomSource();
            Engine = new DialogEngine(new UnityClock(), Random);
            Conductor = new DialogConductor(Engine) { NamedFollowupsRequireEarshot = namedFollowupsRequireEarshot };
            if (recordLookups && Debug.isDebugBuild) Engine.Recorder = new DialogRecorder(recordCapacity);

            if (Localizer == null) Localizer = GetComponent<IDialogLocalizer>();
            if (addBuiltInWorldFacts) Engine.WorldProviders.Add(this);
            foreach (var p in GetComponents<IFactProvider>())
                if (!ReferenceEquals(p, this)) Engine.WorldProviders.Add(p);

            Conductor.LineStarted += OnLineStarted;
            Conductor.NoMatch += OnNoMatch;
            Conductor.EventRaised += OnEventRaised;

            foreach (var db in databases) AddDatabase(db);
            Initialized?.Invoke(this);
        }

        void OnDestroy()
        {
            foreach (var db in loaded.Keys) db.Changed -= OnDatabaseChanged;
            if (instance == this) instance = null;
        }

        void Update()
        {
            Engine?.PurgeExpiredFacts();
            Conductor?.Update();
        }

        // ---- Rule databases ----------------------------------------------------------------

        /// <summary>Loads a database. Calls are counted, so two regions sharing a database are fine.</summary>
        public void AddDatabase(DialogRuleDatabase database)
        {
            database = Resolve(database);
            if (database == null) return;
            EnsureInitialized();
            if (loaded.TryGetValue(database, out int count))
            {
                loaded[database] = count + 1;
                return;
            }
            loaded[database] = 1;
            var set = database.RuleSet;
            activeSets[database] = set;
            Engine.Matcher.Add(set);
            ApplyPending(set);
            database.Changed += OnDatabaseChanged;
        }

        /// <summary>Unloads a database once every <see cref="AddDatabase"/> call has been matched.</summary>
        public void RemoveDatabase(DialogRuleDatabase database)
        {
            database = Resolve(database);
            if (ReferenceEquals(database, null) || Engine == null || !loaded.TryGetValue(database, out int count)) return;
            if (count > 1)
            {
                loaded[database] = count - 1;
                return;
            }
            loaded.Remove(database);
            if (activeSets.TryGetValue(database, out var set))
            {
                KeepRuleStates(set);
                Engine.Matcher.Remove(set);
            }
            activeSets.Remove(database);
            database.Changed -= OnDatabaseChanged;
        }

        /// <summary>Whether the database is loaded.</summary>
        public bool IsLoaded(DialogRuleDatabase database)
        {
            database = Resolve(database);
            return !ReferenceEquals(database, null) && loaded.ContainsKey(database);
        }

        /// <summary>Every loaded database.</summary>
        public IEnumerable<DialogRuleDatabase> LoadedDatabases => loaded.Keys;

        /// <summary>
        /// Swaps a loaded database for another, keeping its load count. The editor uses this when a
        /// .dlg or spreadsheet is reimported during Play Mode and Unity hands back a new asset object.
        /// Later calls that pass the old object are redirected to the new one.
        /// </summary>
        internal void ReplaceDatabase(DialogRuleDatabase old, DialogRuleDatabase replacement)
        {
            old = Resolve(old);
            if (ReferenceEquals(old, null) || replacement == null || ReferenceEquals(old, replacement)) return;
            if (!loaded.TryGetValue(old, out int count)) return;

            loaded.Remove(old);
            if (activeSets.TryGetValue(old, out var oldSet)) Engine.Matcher.Remove(oldSet);
            activeSets.Remove(old);
            old.Changed -= OnDatabaseChanged;
            aliases[old] = replacement;

            if (loaded.TryGetValue(replacement, out int existing))
            {
                loaded[replacement] = existing + count;
            }
            else
            {
                loaded[replacement] = count;
                var set = replacement.RuleSet;
                activeSets[replacement] = set;
                Engine.Matcher.Add(set);
                ApplyPending(set); // a rule given a 'renamed' line picks up the state kept under its old name
                replacement.Changed += OnDatabaseChanged;
            }
            if (logLines) Debug.Log($"[DynamicDialog] Reloaded rules from {replacement.name} ({activeSets[replacement].Rules.Count} rules).", replacement);
        }

        DialogRuleDatabase Resolve(DialogRuleDatabase database)
        {
            for (int guard = 0; !ReferenceEquals(database, null) && guard < 32 && aliases.TryGetValue(database, out var next); guard++)
                database = next;
            return database;
        }

        /// <summary>Swaps in the edited rules while playing, so writers can iterate without restarting.</summary>
        void OnDatabaseChanged(DialogRuleDatabase database)
        {
            if (!activeSets.TryGetValue(database, out var old)) return;
            Engine.Matcher.Remove(old);
            var fresh = database.RuleSet;
            activeSets[database] = fresh;
            Engine.Matcher.Add(fresh);
            ApplyPending(fresh);
            if (logLines) Debug.Log($"[DynamicDialog] Reloaded rules from {database.name} ({fresh.Rules.Count} rules).", database);
        }

        // ---- Save / load -------------------------------------------------------------------------

        /// <summary>
        /// Captures dialog memory and rule state (once/cooldown) for a save game. Includes the state of
        /// databases that aren't loaded right now, and saved states that matched no rule (so a removed or
        /// renamed rule's state isn't lost; see <see cref="DiscardUnmatchedRuleStates"/>).
        /// </summary>
        public DialogSaveData SaveState()
        {
            EnsureInitialized();
            var data = Engine.SaveState();
            AgePending();
            foreach (var p in pendingRuleStates) data.rules.Add(p.Clone());
            return data;
        }

        /// <summary>
        /// Restores a save: replaces memory and rule state, and stops any lines and followups in progress.
        /// Older saves are upgraded. Rule state for databases loaded later (e.g. by a region) is applied when
        /// they load. Saved states are matched to rules by name, then by former name ("renamed" in .dlg); states
        /// that match no rule are kept, saved again, and reported with a warning and in <see cref="LastLoadReport"/>.
        /// </summary>
        public void LoadState(DialogSaveData data)
        {
            EnsureInitialized();
            Conductor.StopAll();
            Conductor.ClearPendingFollowups();
            pendingRuleStates = Engine.LoadState(data, out var report);
            pendingAsOf = Engine.Clock.Now;
            LastLoadReport = report;
            if (report.HasWarnings) Debug.LogWarning("[DynamicDialog] Loading dialog save: " + report, this);
        }

        /// <summary>
        /// Forgets saved rule states that match no rule in a loaded database (a removed rule, or one renamed
        /// without a 'renamed' line), so later saves no longer carry them. States for databases that aren't
        /// loaded are kept. Returns how many were discarded.
        /// </summary>
        public int DiscardUnmatchedRuleStates()
        {
            if (Engine == null) return 0;
            return pendingRuleStates.RemoveAll(p =>
            {
                foreach (var set in activeSets.Values)
                    if (string.Equals(set.Name, p.set ?? "", StringComparison.OrdinalIgnoreCase) && set.FindRule(p.rule, includeFormerNames: true) == null)
                        return true;
                return false;
            });
        }

        /// <summary>Applies kept states that belong to a set that just joined the matcher, and reports any that match no rule.</summary>
        void ApplyPending(RuleSet set)
        {
            AgePending();
            var report = LastLoadReport;
            int before = report?.UnmatchedRules.Count ?? 0;
            DialogSaveExtensions.ApplyRuleStates(set, pendingRuleStates, Engine.Clock.Now, report);
            if (report != null && report.UnmatchedRules.Count > before)
            {
                var names = new List<string>();
                for (int i = before; i < report.UnmatchedRules.Count; i++) names.Add(report.UnmatchedRules[i].ToString());
                Debug.LogWarning($"[DynamicDialog] Saved state for {string.Join(", ", names)} matches no rule in {set.Name}, so it wasn't applied. " +
                                 "It is kept in later saves. If the rule was renamed, add 'renamed <old name>' to it.", this);
            }
        }

        /// <summary>
        /// Remembers which rules of an unloading set have played, so leaving a region and coming back
        /// (or saving while it's unloaded) doesn't reset its once/cooldown rules. Kept states that match
        /// none of its rules stay kept.
        /// </summary>
        void KeepRuleStates(RuleSet set)
        {
            AgePending();
            double now = Engine.Clock.Now;
            pendingRuleStates.RemoveAll(p => string.Equals(p.set ?? "", set.Name, StringComparison.OrdinalIgnoreCase) && set.FindRule(p.rule, includeFormerNames: true) != null);
            foreach (var r in set.Rules)
            {
                var state = DialogSaveExtensions.CaptureState(set, r, now);
                if (state != null) pendingRuleStates.Add(state);
            }
        }

        /// <summary>Counts the time since the pending states were captured toward their cooldowns.</summary>
        void AgePending()
        {
            double now = Engine.Clock.Now;
            float elapsed = (float)Math.Max(0, now - pendingAsOf);
            if (elapsed > 0f)
                foreach (var p in pendingRuleStates)
                    if (p.secondsSinceFired >= 0f) p.secondsSinceFired += elapsed;
            pendingAsOf = now;
        }

        /// <summary><see cref="SaveState"/> as JSON (Unity's JsonUtility).</summary>
        public string SaveStateJson(bool prettyPrint = false) => JsonUtility.ToJson(SaveState(), prettyPrint);

        /// <summary>Restores a save made with <see cref="SaveStateJson"/>. See <see cref="LoadState"/>.</summary>
        public void LoadStateJson(string json) => LoadState(JsonUtility.FromJson<DialogSaveData>(json));

        // ---- World facts --------------------------------------------------------------------

        void IFactProvider.AddFacts(Query query)
        {
            query.Set(WellKnownFacts.Map, Symbol.Of(SceneManager.GetActiveScene().name));
            int talking = 0;
            foreach (var _ in Conductor.ActiveLines) talking++;
            query.Set(WellKnownFacts.SpeakersTalking, talking);
        }

        // ---- Logging ------------------------------------------------------------------------

        void OnLineStarted(DialogLine line)
        {
            if (historySize > 0)
            {
                history.Enqueue(line);
                while (history.Count > historySize) history.Dequeue();
            }
            if (logLines) Debug.Log($"[DynamicDialog] {line}", line.Speaker as UnityEngine.Object);
        }

        void OnEventRaised(DialogLine line, DialogEvent e)
        {
            if (logLines) Debug.Log($"[DynamicDialog] {line.Speaker.Character}: {e} (from {line.Rule.Name})", line.Speaker as UnityEngine.Object);
            DialogEvents.OnConductorEvent(line, e);
        }

        void OnNoMatch(IDialogSpeaker speaker, Symbol concept)
        {
            if (logNoMatch) Debug.Log($"[DynamicDialog] {speaker.Character}: no rule for {concept}", speaker as UnityEngine.Object);
        }
    }
}
