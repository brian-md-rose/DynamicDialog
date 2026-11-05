using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicDialog
{
    public enum RuleVerdict
    {
        /// <summary>The rule won.</summary>
        Won,

        /// <summary>The rule matched, but another rule tied on score and was picked at random.</summary>
        LostTie,

        /// <summary>The rule matched, but a more specific rule scored higher.</summary>
        Outscored,

        /// <summary>One of its conditions wasn't met.</summary>
        FailedCondition,

        /// <summary>It already played (once) or is cooling down.</summary>
        Unavailable,

        /// <summary>Its rule set isn't loaded.</summary>
        NotLoaded,
    }

    /// <summary>Why a rule did or didn't play for a query, in words a writer can act on.</summary>
    public readonly struct RuleExplanation
    {
        public readonly Rule Rule;
        public readonly RuleVerdict Verdict;

        /// <summary>Index into the rule's criteria of the failed condition, or -1.</summary>
        public readonly int FailedCriterion;

        public readonly string Text;

        public RuleExplanation(Rule rule, RuleVerdict verdict, int failedCriterion, string text)
        {
            Rule = rule;
            Verdict = verdict;
            FailedCriterion = failedCriterion;
            Text = text;
        }

        public bool Matched => Verdict == RuleVerdict.Won || Verdict == RuleVerdict.LostTie || Verdict == RuleVerdict.Outscored;

        public override string ToString() => $"{Rule.Name}: {Text}";
    }

    /// <summary>Answers "why didn't my line play?" against a recorded or hand-made query.</summary>
    public static class RuleDiagnostics
    {
        /// <param name="winner">The rule that actually won, if any.</param>
        /// <param name="loaded">Whether the rule's set was active for this lookup.</param>
        /// <param name="wasUnavailable">Whether once/cooldown state blocked it at the time. Null means use its current state.</param>
        public static RuleExplanation Explain(Rule rule, Query query, double now, Rule winner, bool loaded = true, bool? wasUnavailable = null)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            int failed = rule.FirstFailedCriterion(query);
            if (failed >= 0)
                return new RuleExplanation(rule, RuleVerdict.FailedCondition, failed, DescribeFailure(rule.Criteria[failed], query));

            if (!loaded)
                return new RuleExplanation(rule, RuleVerdict.NotLoaded, -1,
                    "Its conditions all pass, but its rule set wasn't loaded (check the director's databases and any Dialog Region).");

            bool unavailable = wasUnavailable ?? !rule.IsAvailable(now);
            if (unavailable)
            {
                string why = rule.State.Disabled
                    ? "It is marked 'once' and has already played."
                    : $"It is on cooldown ({rule.Cooldown:0.##}s after playing; last played at {rule.State.LastFiredTime:0.##}s).";
                return new RuleExplanation(rule, RuleVerdict.Unavailable, -1, "Its conditions all pass, but it can't play right now. " + why);
            }

            if (winner == rule)
                return new RuleExplanation(rule, RuleVerdict.Won, -1, $"Won with score {rule.Score}.");

            if (winner == null)
                return new RuleExplanation(rule, RuleVerdict.NotLoaded, -1,
                    "Its conditions all pass, but nothing played. Its rule set probably wasn't loaded at the time.");

            if (winner.Score > rule.Score)
                return new RuleExplanation(rule, RuleVerdict.Outscored, -1,
                    $"Matched with score {rule.Score}, but '{winner.Name}' is more specific (score {winner.Score}). " +
                    "Add conditions or a bonus to this rule if it should win here.");

            return new RuleExplanation(rule, RuleVerdict.LostTie, -1,
                $"Matched with score {rule.Score} and tied with '{winner.Name}'; that one was picked at random this time.");
        }

        /// <summary>Explains a rule against a recorded lookup, using availability as it was recorded when known.</summary>
        public static RuleExplanation Explain(Rule rule, LookupRecord record, bool loaded = true)
        {
            bool? unavailable = null;
            foreach (var t in record.Tested)
                if (t.Rule == rule)
                {
                    unavailable = t.Unavailable;
                    break;
                }
            return Explain(rule, record.Query, record.Time, record.Winner, loaded, unavailable);
        }

        /// <summary>
        /// Evaluates every rule in <paramref name="sets"/> whose concept fits the query (or that has no concept),
        /// best first: the winner, other matches by score, then failures.
        /// Picks a winner the way the matcher does, without changing any rule's state.
        /// </summary>
        public static List<RuleExplanation> EvaluateAll(IEnumerable<RuleSet> sets, Query query, double now, IRandomSource random = null)
        {
            var setList = sets.ToList();
            var matcher = new RuleMatcher(random ?? new SystemRandomSource(0));
            foreach (var s in setList) matcher.Add(s);
            var winner = matcher.FindBest(query, now);

            bool hasConcept = query.TryGet(WellKnownFacts.Concept, out float concept);
            var results = new List<RuleExplanation>();
            foreach (var set in setList)
                foreach (var rule in set.Rules)
                {
                    if (hasConcept && !ConceptFits(rule, concept)) continue;
                    results.Add(Explain(rule, query, now, winner));
                }

            return results
                .OrderBy(e => Rank(e.Verdict))
                .ThenByDescending(e => e.Rule.Score)
                .ThenBy(e => e.Rule.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static bool ConceptFits(Rule rule, float concept)
        {
            foreach (var c in rule.Criteria)
                if (c.Key == WellKnownFacts.Concept && c.IsExactMatch(out float v)) return v == concept;
            return true;
        }

        static int Rank(RuleVerdict v)
        {
            switch (v)
            {
                case RuleVerdict.Won: return 0;
                case RuleVerdict.LostTie: return 1;
                case RuleVerdict.Outscored: return 2;
                case RuleVerdict.Unavailable: return 3;
                case RuleVerdict.NotLoaded: return 4;
                default: return 5;
            }
        }

        static string DescribeFailure(Criterion c, Query query)
        {
            if (!query.TryGet(c.Key, out float actual))
                return $"Needs '{c}', but '{c.Key}' isn't set." +
                       (c.Key == WellKnownFacts.Who ? "" : " Set it from code, a fact provider, or a 'remember' in another rule.");

            string shown = FactValue.Format(c.Key, actual, query.IsSymbol(c.Key) || c.ValueIsSymbol);
            if (c.Key == WellKnownFacts.Who) return $"It's for {FactValue.Format(c.Key, c.Min, true)}, but {shown} was speaking.";
            if (c.Key == WellKnownFacts.Concept) return $"It answers '{FactValue.Format(c.Key, c.Min, true)}', but this was '{shown}'.";
            return $"Needs '{c}', but '{c.Key}' was {shown}.";
        }
    }
}
