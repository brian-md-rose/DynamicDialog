using System;
using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>Adds facts it computes on demand (health, weapon, nearby allies, current map...) to a query.</summary>
    public interface IFactProvider
    {
        /// <summary>Sets this provider's facts on <paramref name="query"/>. Called for every lookup, so keep it cheap.</summary>
        void AddFacts(Query query);
    }

    /// <summary>A character that can be asked for lines: its name, its memory, and its fact providers.</summary>
    public sealed class DialogCharacter
    {
        /// <summary>The character's name, the "who" fact.</summary>
        public readonly Symbol Who;
        /// <summary>What the character remembers. Rules write to it with "remember".</summary>
        public readonly FactMemory Memory;
        /// <summary>Sources of character facts computed on demand (health, inventory, ...).</summary>
        public readonly List<IFactProvider> Providers = new List<IFactProvider>();

        /// <summary>Creates a character. Usually done through <see cref="DialogEngine.GetOrCreateCharacter(Symbol)"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="who"/> is <see cref="Symbol.None"/>.</exception>
        public DialogCharacter(Symbol who)
        {
            if (who.IsNone) throw new ArgumentException("A character needs a name.", nameof(who));
            Who = who;
            Memory = new FactMemory(who);
        }

        /// <summary>The character's name.</summary>
        public override string ToString() => Who.ToString();
    }

    /// <summary>The outcome of asking for a line. <see cref="Rule"/> is null when nothing matched.</summary>
    public readonly struct MatchResult
    {
        /// <summary>The winning rule, or null.</summary>
        public readonly Rule Rule;
        /// <summary>The character who was asked.</summary>
        public readonly DialogCharacter Speaker;

        /// <summary>Creates a result.</summary>
        public MatchResult(Rule rule, DialogCharacter speaker)
        {
            Rule = rule;
            Speaker = speaker;
        }

        /// <summary>True when a rule matched.</summary>
        public bool Matched => Rule != null;

        /// <summary>Old name of <see cref="Matched"/>.</summary>
        [Obsolete("Use Matched.")]
        public bool Success => Matched;
        /// <summary>The winning rule's score, or int.MinValue when nothing matched.</summary>
        public int Score => Rule?.Score ?? int.MinValue;
        /// <summary>The winning rule's payload, or null.</summary>
        public object Response => Rule?.Response;
        /// <summary>The winning rule's followups, or none.</summary>
        public IReadOnlyList<Followup> Followups => Rule != null ? Rule.Followups : Array.Empty<Followup>();

        /// <summary>"Speaker: rule", or "no match".</summary>
        public override string ToString() => Matched ? $"{Speaker}: {Rule}" : "no match";
    }

    /// <summary>
    /// Ties the pieces together without depending on Unity: builds a query from every fact source,
    /// finds the best rule, and on <see cref="Commit"/> writes the rule's facts back to memory.
    /// The Unity layer adds audio, timing, earshot and followup scheduling on top.
    /// </summary>
    /// <remarks>
    /// Sources are added from most general to most local, and a later source overrides an
    /// earlier one with the same key:
    /// world memory, world providers, speaker memory, speaker providers, event facts.
    /// The concept, who and rand facts are always set last.
    /// </remarks>
    public sealed class DialogEngine
    {
        readonly Dictionary<Symbol, DialogCharacter> characters = new Dictionary<Symbol, DialogCharacter>();
        readonly IRandomSource random;
        readonly Query scratch = new Query(128);
        readonly Query stage = new Query(64);

        /// <summary>Game time, for expiry, cooldowns and lookups.</summary>
        public readonly IClock Clock;
        /// <summary>The active rule sets. Add and remove sets here.</summary>
        public readonly RuleMatcher Matcher;
        /// <summary>World memory, shared by every character.</summary>
        public readonly FactMemory World = new FactMemory();

        /// <summary>
        /// Raised after any fact in world memory or a character's memory changes, is removed, or expires,
        /// whether a rule or code changed it. <see cref="FactChange.Character"/> says whose memory it was.
        /// </summary>
        /// <remarks>Expiry is reported by <see cref="PurgeExpiredFacts"/>, which the Dialog Director calls every frame.</remarks>
        public event Action<FactChange> FactChanged;

        /// <summary>
        /// Raised after many facts were replaced at once: once per <see cref="DialogSaveExtensions.LoadState(DialogEngine, DialogSaveData)"/>,
        /// and whenever world or a character's memory is cleared or loaded. Re-read the facts you show.
        /// </summary>
        public event Action FactsReset;

        int resetBatch;
        /// <summary>Sources of world facts computed on demand, added to every query.</summary>
        public readonly List<IFactProvider> WorldProviders = new List<IFactProvider>();

        /// <summary>When set, the most recent lookup is recorded here. Leave null in shipping builds.</summary>
        public MatchTrace Trace { get; set; }

        /// <summary>When set, every lookup is kept for the debugger, with where each fact came from.</summary>
        public DialogRecorder Recorder { get; set; }

        /// <summary>Creates an engine with no rules and no characters.</summary>
        /// <param name="clock">Game time.</param>
        /// <param name="random">Tie-breaking and the "rand" fact. Null uses a <see cref="SystemRandomSource"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
        public DialogEngine(IClock clock, IRandomSource random = null)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.random = random ?? new SystemRandomSource();
            Matcher = new RuleMatcher(this.random);
            Watch(World);
        }

        void Watch(FactMemory memory)
        {
            memory.Changed += OnFactChanged;
            memory.Reset += OnMemoryReset;
        }

        void Unwatch(FactMemory memory)
        {
            memory.Changed -= OnFactChanged;
            memory.Reset -= OnMemoryReset;
        }

        void OnFactChanged(FactChange change) => FactChanged?.Invoke(change);

        void OnMemoryReset(FactMemory memory)
        {
            if (resetBatch == 0) FactsReset?.Invoke();
        }

        /// <summary>Groups memory resets so <see cref="FactsReset"/> is raised once, when the outermost batch ends.</summary>
        internal void BeginFactsReset() => resetBatch++;

        internal void EndFactsReset()
        {
            if (resetBatch == 0 || --resetBatch > 0) return;
            FactsReset?.Invoke();
        }

        /// <summary>
        /// Removes expired facts from world and character memory, raising <see cref="FactChanged"/> for each.
        /// Cheap when nothing is due. The Dialog Director calls it every frame; call it yourself if you use the engine without one.
        /// </summary>
        public void PurgeExpiredFacts()
        {
            double now = Clock.Now;
            World.PurgeExpired(now);
            foreach (var c in characters.Values) c.Memory.PurgeExpired(now);
        }

        /// <summary>Every character created so far.</summary>
        public IEnumerable<DialogCharacter> Characters => characters.Values;

        /// <summary>The character with this name, created (with empty memory) if it doesn't exist yet.</summary>
        public DialogCharacter GetOrCreateCharacter(Symbol who)
        {
            if (!characters.TryGetValue(who, out var c))
            {
                characters[who] = c = new DialogCharacter(who);
                Watch(c.Memory);
            }
            return c;
        }

        /// <summary>The character with this name, created (with empty memory) if it doesn't exist yet.</summary>
        public DialogCharacter GetOrCreateCharacter(string who) => GetOrCreateCharacter(Symbol.Of(who));

        /// <summary>Finds a character without creating it.</summary>
        public bool TryGetCharacter(Symbol who, out DialogCharacter character) => characters.TryGetValue(who, out character);

        /// <summary>Forgets a character and its memory. Returns false if it didn't exist.</summary>
        public bool RemoveCharacter(Symbol who)
        {
            if (!characters.TryGetValue(who, out var c)) return false;
            Unwatch(c.Memory);
            return characters.Remove(who);
        }

        /// <summary>Fills <paramref name="query"/> with every fact relevant to <paramref name="speaker"/> saying <paramref name="concept"/>.</summary>
        public void BuildQuery(Query query, DialogCharacter speaker, Symbol concept, Action<Query> eventFacts = null) =>
            BuildQuery(query, speaker, concept, eventFacts, null);

        /// <summary>Like <see cref="BuildQuery(Query, DialogCharacter, Symbol, Action{Query})"/>, optionally recording where each fact came from.</summary>
        /// <param name="query">Cleared, then filled with facts.</param>
        /// <param name="speaker">The character being asked.</param>
        /// <param name="concept">The concept being asked for.</param>
        /// <param name="eventFacts">Sets facts about this event. May be null.</param>
        /// <param name="sources">If not null, filled with where each fact came from (slower; for debugging).</param>
        public void BuildQuery(Query query, DialogCharacter speaker, Symbol concept, Action<Query> eventFacts, Dictionary<Symbol, string> sources)
        {
            double now = Clock.Now;
            query.Clear();
            if (sources == null)
            {
                World.WriteTo(query, now);
                foreach (var p in WorldProviders) p.AddFacts(query);
                speaker.Memory.WriteTo(query, now);
                foreach (var p in speaker.Providers) p.AddFacts(query);
                eventFacts?.Invoke(query);
            }
            else
            {
                sources.Clear();
                stage.Clear();
                World.WriteTo(stage, now);
                Merge(query, "world memory", sources);
                foreach (var p in WorldProviders)
                {
                    p.AddFacts(stage);
                    Merge(query, "world: " + p.GetType().Name, sources);
                }
                speaker.Memory.WriteTo(stage, now);
                Merge(query, speaker.Who.Name + " memory", sources);
                foreach (var p in speaker.Providers)
                {
                    p.AddFacts(stage);
                    Merge(query, p.GetType().Name, sources);
                }
                eventFacts?.Invoke(stage);
                Merge(query, "event", sources);
                sources[WellKnownFacts.Concept] = "engine";
                sources[WellKnownFacts.Who] = "engine";
                sources[WellKnownFacts.Random] = "engine";
            }
            query.Set(WellKnownFacts.Concept, concept);
            query.Set(WellKnownFacts.Who, speaker.Who);
            query.Set(WellKnownFacts.Random, random.NextFloat() * 100f);
        }

        void Merge(Query query, string label, Dictionary<Symbol, string> sources)
        {
            for (int i = 0; i < stage.Count; i++)
            {
                var key = stage.KeyAt(i);
                query.Set(key, stage.ValueAt(i), stage.IsSymbolAt(i));
                sources[key] = label;
            }
            stage.Clear();
        }

        /// <summary>Asks one character for its best line. Nothing is committed.</summary>
        /// <param name="speaker">The character to ask.</param>
        /// <param name="concept">What happened, e.g. "OnSee".</param>
        /// <param name="eventFacts">Sets facts about this event, e.g. <c>q => q.Set("object", "barrel")</c>. May be null.</param>
        public MatchResult Ask(DialogCharacter speaker, Symbol concept, Action<Query> eventFacts = null)
        {
            if (Recorder == null)
            {
                BuildQuery(scratch, speaker, concept, eventFacts, null);
                return new MatchResult(Matcher.FindBest(scratch, Clock.Now, Trace), speaker);
            }

            var sources = new Dictionary<Symbol, string>();
            BuildQuery(scratch, speaker, concept, eventFacts, sources);
            var trace = Trace ?? new MatchTrace();
            var rule = Matcher.FindBest(scratch, Clock.Now, trace);
            Recorder.Add(Clock.Now, speaker.Who, concept, scratch, sources, trace, rule);
            return new MatchResult(rule, speaker);
        }

        /// <summary>Asks one character for its best line. Nothing is committed.</summary>
        public MatchResult Ask(DialogCharacter speaker, string concept, Action<Query> eventFacts = null) =>
            Ask(speaker, Symbol.Of(concept), eventFacts);

        /// <summary>
        /// Asks every candidate and returns the single highest-scoring reply ("then any",
        /// talk slides 73-76). Ties between characters are broken at random.
        /// </summary>
        public MatchResult AskBest(IEnumerable<DialogCharacter> candidates, Symbol concept, Action<Query> eventFacts = null)
        {
            MatchResult best = default;
            int tieCount = 0;
            foreach (var c in candidates)
            {
                var r = Ask(c, concept, eventFacts);
                if (!r.Matched) continue;
                if (!best.Matched || r.Score > best.Score)
                {
                    best = r;
                    tieCount = 1;
                }
                else if (r.Score == best.Score)
                {
                    // Reservoir sampling: each tied character is equally likely.
                    tieCount++;
                    if (random.NextFloat() * tieCount < 1f) best = r;
                }
            }
            return best;
        }

        /// <summary>Asks every candidate and returns every match ("then all").</summary>
        public List<MatchResult> AskEach(IEnumerable<DialogCharacter> candidates, Symbol concept, Action<Query> eventFacts = null)
        {
            var results = new List<MatchResult>();
            foreach (var c in candidates)
            {
                var r = Ask(c, concept, eventFacts);
                if (r.Matched) results.Add(r);
            }
            return results;
        }

        /// <summary>
        /// Call when the line actually starts playing: applies the rule's fact writes and records
        /// once/cooldown state. Followups are left to the caller, which dispatches them when the line ends.
        /// </summary>
        public void Commit(in MatchResult result)
        {
            if (!result.Matched) return;
            Recorder?.MarkStarted(result.Rule, result.Speaker.Who);
            double now = Clock.Now;
            foreach (var w in result.Rule.Writes)
            {
                switch (w.Scope)
                {
                    case FactScope.Speaker: result.Speaker.Memory.Apply(w, now); break;
                    case FactScope.World: World.Apply(w, now); break;
                    case FactScope.Character: GetOrCreateCharacter(w.Character).Memory.Apply(w, now); break;
                }
            }
            result.Rule.MarkFired(now);
        }
    }
}
