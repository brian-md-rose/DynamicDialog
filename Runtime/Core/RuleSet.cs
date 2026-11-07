using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicDialog
{
    /// <summary>
    /// A compiled group of rules, such as the global rules or one region's rules (talk slide 110).
    /// Rules are bucketed by their exact-match values for the partition keys, by default
    /// (concept, who). Each bucket is sorted by descending score, so a lookup only tests a few
    /// rules and can stop once scores fall below the best match (slides 106-113).
    /// A rule with no exact criterion on a partition key goes into that key's wildcard bucket.
    /// </summary>
    public sealed class RuleSet
    {
        /// <summary>At most 4 keys, which means 16 bucket probes per lookup.</summary>
        public const int MaxPartitionKeys = 4;

        /// <summary>The set's name. Save data matches rule state to sets by it (a database's asset name).</summary>
        public readonly string Name;
        /// <summary>Every rule, in authored order.</summary>
        public readonly IReadOnlyList<Rule> Rules;
        /// <summary>The fact keys rules are bucketed by.</summary>
        public readonly IReadOnlyList<Symbol> PartitionKeys;

        readonly Dictionary<ulong, Rule[]> buckets;

        /// <summary>Compiles rules into buckets.</summary>
        /// <param name="name">The set's name.</param>
        /// <param name="rules">The rules.</param>
        /// <param name="partitionKeys">Keys to bucket by, at most <see cref="MaxPartitionKeys"/>. None means (concept, who).</param>
        /// <exception cref="ArgumentException">Too many keys, or a key repeats.</exception>
        public RuleSet(string name, IEnumerable<Rule> rules, params Symbol[] partitionKeys)
        {
            Name = name ?? "";
            var ruleList = (rules ?? Enumerable.Empty<Rule>()).ToList();
            Rules = ruleList;

            if (partitionKeys == null || partitionKeys.Length == 0)
                partitionKeys = new[] { WellKnownFacts.Concept, WellKnownFacts.Who };
            if (partitionKeys.Length > MaxPartitionKeys)
                throw new ArgumentException($"At most {MaxPartitionKeys} partition keys are supported.");
            if (partitionKeys.Distinct().Count() != partitionKeys.Length)
                throw new ArgumentException("Partition keys must be distinct.");
            PartitionKeys = partitionKeys;

            var building = new Dictionary<ulong, List<Rule>>();
            var exact = new float[partitionKeys.Length];
            foreach (var rule in ruleList)
            {
                int wildcardMask = 0;
                for (int k = 0; k < partitionKeys.Length; k++)
                {
                    if (!TryGetExactValue(rule, partitionKeys[k], out exact[k]))
                        wildcardMask |= 1 << k;
                }
                ulong hash = BucketHash(wildcardMask, exact);
                if (!building.TryGetValue(hash, out var list)) building[hash] = list = new List<Rule>();
                list.Add(rule);
            }

            // Stable sort: equal scores keep their authored order.
            buckets = building.ToDictionary(kv => kv.Key, kv => kv.Value.OrderByDescending(r => r.Score).ToArray());
        }

        /// <summary>How many buckets the rules were split into.</summary>
        public int BucketCount => buckets.Count;
        /// <summary>The number of rules in the largest bucket: roughly the most rules one lookup can test.</summary>
        public int LargestBucket => buckets.Count == 0 ? 0 : buckets.Values.Max(b => b.Length);

        /// <summary>The rule with this name (ignoring case), or null.</summary>
        public Rule FindRule(string ruleName) =>
            Rules.FirstOrDefault(r => string.Equals(r.Name, ruleName, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The rule with this name (ignoring case). If no rule has it and <paramref name="includeFormerNames"/> is set,
        /// the first rule that lists it as a former name (see <see cref="Rule.FormerNames"/>). Null if neither.
        /// </summary>
        public Rule FindRule(string ruleName, bool includeFormerNames)
        {
            var rule = FindRule(ruleName);
            if (rule != null || !includeFormerNames) return rule;
            foreach (var r in Rules)
                if (r.WasNamed(ruleName)) return r;
            return null;
        }

        /// <summary>Resets every rule's once and cooldown state.</summary>
        public void ResetState()
        {
            foreach (var r in Rules) r.State.Reset();
        }

        /// <summary>
        /// Adds to <paramref name="into"/> each bucket that could hold rules matching
        /// <paramref name="query"/>. Every bucket is sorted by descending score.
        /// </summary>
        internal void CollectCandidateBuckets(Query query, List<Rule[]> into)
        {
            int k = PartitionKeys.Count;
            Span<float> values = stackalloc float[MaxPartitionKeys];
            int missingMask = 0;
            for (int i = 0; i < k; i++)
            {
                if (!query.TryGet(PartitionKeys[i], out values[i])) missingMask |= 1 << i;
            }

            int combos = 1 << k;
            for (int wildcardMask = 0; wildcardMask < combos; wildcardMask++)
            {
                // A bucket that needs an exact value for a key the query lacks can't match.
                if ((missingMask & ~wildcardMask) != 0) continue;
                if (buckets.TryGetValue(BucketHash(wildcardMask, values.Slice(0, k)), out var bucket))
                    into.Add(bucket);
            }
        }

        static bool TryGetExactValue(Rule rule, Symbol key, out float value)
        {
            foreach (var c in rule.Criteria)
            {
                if (c.Key == key && c.IsExactMatch(out value)) return true;
            }
            value = 0f;
            return false;
        }

        /// <summary>FNV-1a over the wildcard mask and the exact values. A collision only merges two buckets.</summary>
        static ulong BucketHash(int wildcardMask, ReadOnlySpan<float> values)
        {
            const ulong prime = 1099511628211UL;
            ulong h = 14695981039346656037UL;
            h = (h ^ (uint)wildcardMask) * prime;
            for (int i = 0; i < values.Length; i++)
            {
                if ((wildcardMask & (1 << i)) != 0) continue;
                h = (h ^ (uint)BitConverter.SingleToInt32Bits(values[i] == 0f ? 0f : values[i])) * prime;
            }
            return h;
        }
    }
}
