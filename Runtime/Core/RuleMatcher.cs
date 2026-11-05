using System;
using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>Random numbers for tie-breaking and the "rand" fact. Swap in a seeded one for tests.</summary>
    public interface IRandomSource
    {
        /// <summary>A value in [0, 1).</summary>
        float NextFloat();
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        readonly Random random;
        public SystemRandomSource() { random = new Random(); }
        public SystemRandomSource(int seed) { random = new Random(seed); }
        public float NextFloat() => (float)random.NextDouble();
    }

    /// <summary>One rule the matcher looked at, for the debugging tools (talk slide 98).</summary>
    public readonly struct TraceEntry
    {
        public readonly Rule Rule;
        public readonly RuleSet RuleSet;

        /// <summary>Index into <see cref="DynamicDialog.Rule.Criteria"/> of the first failure, or -1 if all passed.</summary>
        public readonly int FailedCriterion;

        /// <summary>Skipped because of once or cooldown state.</summary>
        public readonly bool Unavailable;

        public TraceEntry(Rule rule, RuleSet set, int failedCriterion, bool unavailable)
        {
            Rule = rule;
            RuleSet = set;
            FailedCriterion = failedCriterion;
            Unavailable = unavailable;
        }

        public bool Passed => !Unavailable && FailedCriterion < 0;

        public override string ToString()
        {
            if (Unavailable) return $"{Rule}: unavailable (once/cooldown)";
            if (FailedCriterion < 0) return $"{Rule}: MATCH";
            return $"{Rule}: failed on {Rule.Criteria[FailedCriterion]}";
        }
    }

    /// <summary>Optional record of one lookup: the query, every rule tested, and the winner.</summary>
    public sealed class MatchTrace
    {
        public readonly List<TraceEntry> Entries = new List<TraceEntry>();
        public Query Query;
        public Rule Winner;
        public int TieCount;

        public void Clear()
        {
            Entries.Clear();
            Query = null;
            Winner = null;
            TieCount = 0;
        }
    }

    /// <summary>
    /// Finds the best-scoring rule for a query across every active <see cref="RuleSet"/>,
    /// such as the global set plus the current region's set. Not thread-safe; use one per thread.
    /// </summary>
    public sealed class RuleMatcher
    {
        readonly List<RuleSet> sets = new List<RuleSet>();
        readonly List<Rule[]> bucketScratch = new List<Rule[]>();
        readonly List<Rule> ties = new List<Rule>();
        readonly DenseFacts dense = new DenseFacts();
        readonly IRandomSource random;

        public RuleMatcher(IRandomSource random = null)
        {
            this.random = random ?? new SystemRandomSource();
        }

        public IReadOnlyList<RuleSet> Sets => sets;

        public void Add(RuleSet set)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (!sets.Contains(set)) sets.Add(set);
        }

        public bool Remove(RuleSet set) => sets.Remove(set);

        /// <summary>
        /// Returns the most specific available rule that matches, or null if none does.
        /// When several rules share the best score, one is picked at random, weighted by
        /// <see cref="Rule.Weight"/>.
        /// </summary>
        public Rule FindBest(Query query, double now, MatchTrace trace = null)
        {
            if (trace != null)
            {
                trace.Clear();
                trace.Query = query.Clone();
            }

            ties.Clear();
            int bestScore = int.MinValue;
            dense.Load(query);

            foreach (var set in sets)
            {
                bucketScratch.Clear();
                set.CollectCandidateBuckets(query, bucketScratch);
                foreach (var bucket in bucketScratch)
                {
                    foreach (var rule in bucket)
                    {
                        // Buckets are sorted by descending score, so nothing below can win.
                        if (rule.Score < bestScore) break;

                        if (!rule.IsAvailable(now))
                        {
                            trace?.Entries.Add(new TraceEntry(rule, set, -1, unavailable: true));
                            continue;
                        }

                        int failed = dense.FirstFailedCriterion(rule);
                        trace?.Entries.Add(new TraceEntry(rule, set, failed, unavailable: false));
                        if (failed >= 0) continue;

                        if (rule.Score > bestScore)
                        {
                            bestScore = rule.Score;
                            ties.Clear();
                        }
                        ties.Add(rule);
                    }
                }
            }

            Rule winner = PickWeighted(ties);
            if (trace != null)
            {
                trace.Winner = winner;
                trace.TieCount = ties.Count;
            }
            ties.Clear();
            return winner;
        }

        Rule PickWeighted(List<Rule> candidates)
        {
            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            float total = 0f;
            foreach (var r in candidates) total += r.Weight;
            if (total <= 0f) return candidates[(int)(random.NextFloat() * candidates.Count) % candidates.Count];

            float pick = random.NextFloat() * total;
            foreach (var r in candidates)
            {
                pick -= r.Weight;
                if (pick < 0f) return r;
            }
            return candidates[candidates.Count - 1];
        }
    }
}
