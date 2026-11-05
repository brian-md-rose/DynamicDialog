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
        public static readonly Symbol MapFact = Symbol.Of("map");
        public static readonly Symbol SpeakersTalkingFact = Symbol.Of("speakers_talking");

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

        public DialogEngine Engine { get; private set; }
        public DialogConductor Conductor { get; private set; }
        public IRandomSource Random { get; private set; }

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

            if (addBuiltInWorldFacts) Engine.WorldProviders.Add(this);
            foreach (var p in GetComponents<IFactProvider>())
                if (!ReferenceEquals(p, this)) Engine.WorldProviders.Add(p);

            Conductor.LineStarted += OnLineStarted;
            Conductor.NoMatch += OnNoMatch;

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
            if (activeSets.TryGetValue(database, out var set)) Engine.Matcher.Remove(set);
            activeSets.Remove(database);
            database.Changed -= OnDatabaseChanged;
        }

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
        public void ReplaceDatabase(DialogRuleDatabase old, DialogRuleDatabase replacement)
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
            if (logLines) Debug.Log($"[DynamicDialog] Reloaded rules from {database.name} ({fresh.Rules.Count} rules).", database);
        }

        // ---- World facts --------------------------------------------------------------------

        void IFactProvider.AddFacts(Query query)
        {
            query.Set(MapFact, Symbol.Of(SceneManager.GetActiveScene().name));
            int talking = 0;
            foreach (var _ in Conductor.ActiveLines) talking++;
            query.Set(SpeakersTalkingFact, talking);
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

        void OnNoMatch(IDialogSpeaker speaker, Symbol concept)
        {
            if (logNoMatch) Debug.Log($"[DynamicDialog] {speaker.Character}: no rule for {concept}", speaker as UnityEngine.Object);
        }
    }
}
