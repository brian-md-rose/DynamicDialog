using System;
using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>
    /// Everything about one lookup: who asked about what, the full query (with where each fact
    /// came from), every rule the matcher tested, and the winner (talk slide 98).
    /// </summary>
    public sealed class LookupRecord
    {
        /// <summary>Unique, increasing ID.</summary>
        public int Id { get; internal set; }

        /// <summary>Game time of the lookup.</summary>
        public double Time { get; internal set; }

        /// <summary>The character who was asked.</summary>
        public Symbol Speaker { get; internal set; }
        /// <summary>The concept asked for.</summary>
        public Symbol Concept { get; internal set; }

        /// <summary>A copy of the query as it was.</summary>
        public Query Query { get; internal set; }

        /// <summary>Where each fact came from: "world memory", "Nick memory", a provider's type name, "event" or "engine".</summary>
        public IReadOnlyDictionary<Symbol, string> FactSources { get; internal set; }

        /// <summary>Rules the matcher tested, in order. Rules in other buckets, or below the winning score, aren't tested.</summary>
        public IReadOnlyList<TraceEntry> Tested { get; internal set; }

        /// <summary>The rule that won, or null.</summary>
        public Rule Winner { get; internal set; }
        /// <summary>How many rules shared the winning score.</summary>
        public int TieCount { get; internal set; }

        /// <summary>True once the winning line actually started playing (it may be asked for and not used, e.g. by "then any").</summary>
        public bool Started { get; internal set; }

        /// <summary>True when a rule won.</summary>
        public bool Matched => Winner != null;

        /// <summary>Where the fact for <paramref name="key"/> came from, or null.</summary>
        public string SourceOf(Symbol key) => FactSources != null && FactSources.TryGetValue(key, out var s) ? s : null;

        /// <summary>A one-line summary: id, time, speaker, concept and result.</summary>
        public override string ToString() =>
            $"#{Id} {Time:0.00}s {Speaker}: {Concept} -> {(Winner != null ? Winner.Name : "no match")}{(Started ? "" : Winner != null ? " (not played)" : "")}";
    }

    /// <summary>
    /// Keeps the most recent lookups for the debugger. Attach one to <see cref="DialogEngine.Recorder"/>.
    /// Recording copies each query, so leave it off in shipping builds unless you need it.
    /// </summary>
    public sealed class DialogRecorder
    {
        readonly List<LookupRecord> records = new List<LookupRecord>();
        int nextId = 1;
        int capacity;

        /// <summary>Creates a recorder that keeps the newest <paramref name="capacity"/> lookups.</summary>
        public DialogRecorder(int capacity = 256)
        {
            this.capacity = Math.Max(1, capacity);
        }

        /// <summary>How many lookups are kept (at least 1). Lowering it drops the oldest.</summary>
        public int Capacity
        {
            get => capacity;
            set
            {
                capacity = Math.Max(1, value);
                Trim();
            }
        }

        /// <summary>Oldest first.</summary>
        public IReadOnlyList<LookupRecord> Records => records;

        /// <summary>Raised after each lookup is recorded.</summary>
        public event Action<LookupRecord> Recorded;

        /// <summary>Raised when a recorded lookup's line starts playing.</summary>
        public event Action<LookupRecord> Started;

        /// <summary>Forgets every recorded lookup.</summary>
        public void Clear() => records.Clear();

        /// <summary>The newest lookup by <paramref name="speaker"/>, or null.</summary>
        public LookupRecord Latest(Symbol speaker)
        {
            for (int i = records.Count - 1; i >= 0; i--)
                if (records[i].Speaker == speaker) return records[i];
            return null;
        }

        internal LookupRecord Add(double time, Symbol speaker, Symbol concept, Query query,
            Dictionary<Symbol, string> sources, MatchTrace trace, Rule winner)
        {
            var record = new LookupRecord
            {
                Id = nextId++,
                Time = time,
                Speaker = speaker,
                Concept = concept,
                Query = query.Clone(),
                FactSources = sources,
                Tested = trace != null ? trace.entries.ToArray() : Array.Empty<TraceEntry>(),
                Winner = winner,
                TieCount = trace?.TieCount ?? (winner != null ? 1 : 0),
            };
            records.Add(record);
            Trim();
            Recorded?.Invoke(record);
            return record;
        }

        /// <summary>Marks the newest unplayed lookup where <paramref name="speaker"/> won with <paramref name="rule"/> as played.</summary>
        internal void MarkStarted(Rule rule, Symbol speaker)
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                var r = records[i];
                if (r.Winner == rule && r.Speaker == speaker && !r.Started)
                {
                    r.Started = true;
                    Started?.Invoke(r);
                    return;
                }
            }
        }

        void Trim()
        {
            int excess = records.Count - capacity;
            if (excess > 0) records.RemoveRange(0, excess);
        }
    }
}
