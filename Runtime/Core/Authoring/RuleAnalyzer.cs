using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicDialog
{
    /// <summary>A named group of rules to check, such as one file or database.</summary>
    public sealed class RuleSourceGroup
    {
        /// <summary>The group's name, used as the file in messages.</summary>
        public readonly string Name;
        /// <summary>The group's rules.</summary>
        public readonly IReadOnlyList<RuleSource> Rules;

        /// <summary>Creates a group.</summary>
        public RuleSourceGroup(string name, IEnumerable<RuleSource> rules)
        {
            Name = name;
            Rules = rules.ToList();
        }
    }

    /// <summary>
    /// Checks rules against each other: followups nobody answers, criteria that can never all be
    /// true, likely typos in fact names, duplicate names, and rules that do nothing.
    /// </summary>
    public static class RuleAnalyzer
    {
        /// <summary>Facts the engine or the bundled components provide.</summary>
        public static readonly IReadOnlyCollection<string> BuiltInFacts = new[]
        {
            "concept", "who", "rand", "from", "map", "speakers_talking", "object", "object_distance", "area", "nearby_speakers", "region",
        };

        /// <summary>Prefixes of facts the bundled components provide, like "near_Nick".</summary>
        public static readonly IReadOnlyCollection<string> BuiltInFactPrefixes = new[] { WellKnownFacts.NearPrefix };

        /// <summary>Checks every group's rules against each other and returns what it finds.</summary>
        /// <param name="groups">The rules to check, one group per file or database.</param>
        /// <param name="knownFacts">Extra fact names your own code provides, so they aren't reported as unknown.</param>
        public static List<AuthoringMessage> Analyze(IEnumerable<RuleSourceGroup> groups, IEnumerable<string> knownFacts = null)
        {
            var messages = new List<AuthoringMessage>();
            var all = groups.SelectMany(g => g.Rules.Select(r => (group: g.Name, rule: r))).ToList();
            var cmp = StringComparer.OrdinalIgnoreCase;

            // Duplicate names.
            foreach (var dup in all.Where(x => !string.IsNullOrWhiteSpace(x.rule.Name)).GroupBy(x => x.rule.Name, cmp).Where(g => g.Count() > 1))
            {
                string where = string.Join(", ", dup.Select(d => d.group).Distinct());
                foreach (var (group, rule) in dup.Skip(1))
                    Add(messages, MessageSeverity.Warning, $"Another rule is also named '{rule.Name}' (in {where}). Names should be unique so saves and debug tools can find rules.", group, rule);
            }

            // Concepts and who answers them.
            bool anyWildcardConcept = all.Any(x => string.IsNullOrWhiteSpace(x.rule.Concept));
            var conceptsBySpeaker = new Dictionary<string, HashSet<string>>(cmp); // concept -> speakers ("" = anyone)
            foreach (var (_, r) in all)
            {
                if (string.IsNullOrWhiteSpace(r.Concept)) continue;
                if (!conceptsBySpeaker.TryGetValue(r.Concept, out var set)) conceptsBySpeaker[r.Concept] = set = new HashSet<string>(cmp);
                set.Add(string.IsNullOrWhiteSpace(r.Who) ? "" : r.Who);
            }
            var speakers = new HashSet<string>(all.Where(x => !string.IsNullOrWhiteSpace(x.rule.Who)).Select(x => x.rule.Who), cmp);

            // Fact keys.
            var known = new HashSet<string>(BuiltInFacts, cmp);
            if (knownFacts != null) known.UnionWith(knownFacts.Where(k => !string.IsNullOrWhiteSpace(k)));
            foreach (var (_, r) in all)
                foreach (var w in r.Writes)
                    if (!string.IsNullOrWhiteSpace(w.Key)) known.Add(w.Key);
            var readCounts = all.SelectMany(x => x.rule.Criteria.Select(c => c.Key)).Where(k => !string.IsNullOrWhiteSpace(k))
                .GroupBy(k => k, cmp).ToDictionary(g => g.Key, g => g.Count(), cmp);

            foreach (var (group, r) in all)
            {
                if (string.IsNullOrWhiteSpace(r.Concept))
                    Add(messages, MessageSeverity.Warning, "Has no concept, so it can answer any event.", group, r);

                if (r.Lines.Count == 0 && r.Writes.Count == 0 && r.Followups.Count == 0)
                    Add(messages, MessageSeverity.Warning, "Does nothing: no lines, no remember, no then.", group, r);

                CheckCompiles(messages, group, r);
                CheckFollowups(messages, group, r, conceptsBySpeaker, anyWildcardConcept);

                foreach (var w in r.Writes)
                    if (w.Scope == FactScope.Character && !string.IsNullOrWhiteSpace(w.Character) && !speakers.Contains(w.Character))
                        Add(messages, MessageSeverity.Info, $"Writes to '{w.Character}', who has no rules of their own. Check the spelling.", group, r);

                foreach (var c in r.Criteria)
                {
                    if (string.IsNullOrWhiteSpace(c.Key) || known.Contains(c.Key) || BuiltInFactPrefixes.Any(p => c.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    string suggestion = Closest(c.Key, known.Concat(readCounts.Keys.Where(k => readCounts[k] > 1)));
                    if (suggestion != null && readCounts[c.Key] == 1)
                        Add(messages, MessageSeverity.Warning, $"Reads '{c.Key}', which nothing else uses. Did you mean '{suggestion}'?", group, r);
                    else
                        Add(messages, MessageSeverity.Info, $"Reads '{c.Key}', which no rule writes and isn't built in. Fine if your code provides it.", group, r);
                }
            }

            // Exact twins tie forever; usually that's a copy-paste slip rather than intended variety.
            foreach (var g in all.GroupBy(x => x.group))
            {
                var seen = new Dictionary<string, RuleSource>();
                foreach (var (_, r) in g)
                {
                    string sig = Signature(r);
                    if (seen.TryGetValue(sig, out var twin))
                        Add(messages, MessageSeverity.Info, $"Has exactly the same conditions as '{twin.Name}', so the two always tie and alternate at random. Merge them into one rule with several 'say' lines if that's the intent.", g.Key, r);
                    else seen[sig] = r;
                }
            }
            return messages;
        }

        static void CheckCompiles(List<AuthoringMessage> messages, string group, RuleSource r)
        {
            Rule rule;
            try
            {
                rule = r.ToRule();
            }
            catch (Exception e)
            {
                Add(messages, MessageSeverity.Error, "Can't be built: " + e.Message, group, r);
                return;
            }

            // Criteria on one key that can't all pass together.
            foreach (var byKey in rule.Criteria.GroupBy(c => c.Key))
            {
                float lo = float.NegativeInfinity, hi = float.PositiveInfinity;
                foreach (var c in byKey)
                {
                    if (c.Invert || c.Missing != MissingFactPolicy.Reject) continue;
                    lo = Math.Max(lo, c.Min);
                    hi = Math.Min(hi, c.Max);
                }
                if (lo > hi)
                    Add(messages, MessageSeverity.Error, $"Can never match: its conditions on '{byKey.Key}' contradict each other ({string.Join(" and ", byKey)}).", group, r);
            }
        }

        static void CheckFollowups(List<AuthoringMessage> messages, string group, RuleSource r,
            Dictionary<string, HashSet<string>> conceptsBySpeaker, bool anyWildcardConcept)
        {
            if (anyWildcardConcept) return; // a concept-less rule could answer anything
            foreach (var f in r.Followups)
            {
                if (string.IsNullOrWhiteSpace(f.Concept)) continue;
                if (!conceptsBySpeaker.TryGetValue(f.Concept, out var answerers))
                {
                    Add(messages, MessageSeverity.Warning, $"Sends '{f.Concept}', but no rule answers it.", group, r);
                    continue;
                }
                string target = f.Target == FollowupTarget.Character ? f.Character
                    : f.Target == FollowupTarget.Self ? r.Who
                    : null;
                if (!string.IsNullOrWhiteSpace(target) && !answerers.Contains(target) && !answerers.Contains(""))
                    Add(messages, MessageSeverity.Warning, $"Sends '{f.Concept}' to {target}, but only {string.Join(", ", answerers.OrderBy(a => a))} answer it.", group, r);
            }
        }

        static string Signature(RuleSource r)
        {
            var parts = r.Criteria.Select(c => $"{c.Key?.ToLowerInvariant()}|{c.Op}|{c.Value?.ToLowerInvariant()}|{c.Value2}|{c.HasDefault}|{c.Default}")
                .OrderBy(s => s, StringComparer.Ordinal);
            return $"{r.Who?.ToLowerInvariant()}#{r.Concept?.ToLowerInvariant()}#{string.Join(";", parts)}";
        }

        static string Closest(string key, IEnumerable<string> candidates)
        {
            string best = null;
            int bestDistance = int.MaxValue;
            foreach (var c in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(c, key, StringComparison.OrdinalIgnoreCase)) continue;
                int d = Distance(key.ToLowerInvariant(), c.ToLowerInvariant());
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = c;
                }
            }
            int limit = key.Length <= 4 ? 1 : 2;
            return bestDistance <= limit ? best : null;
        }

        /// <summary>Levenshtein distance.</summary>
        static int Distance(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, cur) = (cur, prev);
            }
            return prev[b.Length];
        }

        static void Add(List<AuthoringMessage> messages, MessageSeverity severity, string text, string group, RuleSource r) =>
            messages.Add(new AuthoringMessage(severity, text, group, r.SourceLine, r.Name));
    }
}
