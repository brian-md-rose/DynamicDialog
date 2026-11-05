using System;
using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>Adds facts it computes on demand (health, weapon, nearby allies, current map...) to a query.</summary>
    public interface IFactProvider
    {
        void AddFacts(Query query);
    }

    /// <summary>A character that can be asked for lines: its name, its memory, and its fact providers.</summary>
    public sealed class DialogCharacter
    {
        public readonly Symbol Who;
        public readonly FactMemory Memory = new FactMemory();
        public readonly List<IFactProvider> Providers = new List<IFactProvider>();

        public DialogCharacter(Symbol who)
        {
            if (who.IsNone) throw new ArgumentException("A character needs a name.", nameof(who));
            Who = who;
        }

        public override string ToString() => Who.ToString();
    }

    /// <summary>The outcome of asking for a line. <see cref="Rule"/> is null when nothing matched.</summary>
    public readonly struct MatchResult
    {
        public readonly Rule Rule;
        public readonly DialogCharacter Speaker;

        public MatchResult(Rule rule, DialogCharacter speaker)
        {
            Rule = rule;
            Speaker = speaker;
        }

        public bool Success => Rule != null;
        public int Score => Rule?.Score ?? int.MinValue;
        public object Response => Rule?.Response;
        public IReadOnlyList<Followup> Followups => Rule != null ? Rule.Followups : Array.Empty<Followup>();

        public override string ToString() => Success ? $"{Speaker}: {Rule}" : "no match";
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

        public readonly IClock Clock;
        public readonly RuleMatcher Matcher;
        public readonly FactMemory World = new FactMemory();
        public readonly List<IFactProvider> WorldProviders = new List<IFactProvider>();

        /// <summary>When set, the most recent lookup is recorded here. Leave null in shipping builds.</summary>
        public MatchTrace Trace;

        /// <summary>When set, every lookup is kept for the debugger, with where each fact came from.</summary>
        public DialogRecorder Recorder;

        public DialogEngine(IClock clock, IRandomSource random = null)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.random = random ?? new SystemRandomSource();
            Matcher = new RuleMatcher(this.random);
        }

        public IEnumerable<DialogCharacter> Characters => characters.Values;

        public DialogCharacter GetOrCreateCharacter(Symbol who)
        {
            if (!characters.TryGetValue(who, out var c)) characters[who] = c = new DialogCharacter(who);
            return c;
        }

        public DialogCharacter GetOrCreateCharacter(string who) => GetOrCreateCharacter(Symbol.Of(who));

        public bool TryGetCharacter(Symbol who, out DialogCharacter character) => characters.TryGetValue(who, out character);

        public bool RemoveCharacter(Symbol who) => characters.Remove(who);

        /// <summary>Fills <paramref name="query"/> with every fact relevant to <paramref name="speaker"/> saying <paramref name="concept"/>.</summary>
        public void BuildQuery(Query query, DialogCharacter speaker, Symbol concept, Action<Query> eventFacts = null) =>
            BuildQuery(query, speaker, concept, eventFacts, null);

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
                if (!r.Success) continue;
                if (!best.Success || r.Score > best.Score)
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
                if (r.Success) results.Add(r);
            }
            return results;
        }

        /// <summary>
        /// Call when the line actually starts playing: applies the rule's fact writes and records
        /// once/cooldown state. Followups are left to the caller, which dispatches them when the line ends.
        /// </summary>
        public void Commit(in MatchResult result)
        {
            if (!result.Success) return;
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
