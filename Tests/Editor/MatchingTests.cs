using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    public class MatchingTests
    {
        static Query NickHurtQuery()
        {
            var q = new Query();
            q.Set("concept", "OnHurt");
            q.Set("who", "Nick");
            q.Set("health", 20);
            q.Set("map", "swamp");
            q.Set("nearAlly", 1);
            q.Set("weapon", "shotgun");
            return q;
        }

        static RuleBuilder NickHurt(string name) => new RuleBuilder(name).Concept("OnHurt").Who("Nick");

        /// <summary>The worked example from talk slides 63-64.</summary>
        static List<Rule> SlideRules(bool includeMostSpecific)
        {
            var rules = new List<Rule>
            {
                NickHurt("R1_general").Build(),                                                      // 2
                new RuleBuilder("R2_wrongConcept").Concept("OnReload").Who("Nick").Build(),          // fails
                NickHurt("R3_wrongHealth").Where(Criterion.Gt("health", 50)).Build(),               // fails
                NickHurt("R4_lowHealth").Where(Criterion.Lt("health", 30)).Build(),                 // 3
                NickHurt("R5_swamp").Where(Criterion.Eq("map", "swamp")).Build(),                   // 3
                NickHurt("R6_shotgun").Where(Criterion.Eq("weapon", "shotgun")).Build(),            // 3
            };
            if (includeMostSpecific)
                rules.Add(NickHurt("R7_lowHealthNearAlly")
                    .Where(Criterion.Lt("health", 30)).Where(Criterion.Eq("nearAlly", 1)).Build()); // 4
            return rules;
        }

        [Test]
        public void MostSpecificRuleWins()
        {
            var matcher = new RuleMatcher(new SystemRandomSource(1));
            matcher.Add(new RuleSet("test", SlideRules(includeMostSpecific: true)));
            var winner = matcher.FindBest(NickHurtQuery(), now: 0);
            Assert.AreEqual("R7_lowHealthNearAlly", winner.Name);
            Assert.AreEqual(4, winner.Score);
        }

        [Test]
        public void TiesArePickedAtRandom()
        {
            var matcher = new RuleMatcher(new SystemRandomSource(7));
            matcher.Add(new RuleSet("test", SlideRules(includeMostSpecific: false)));
            var q = NickHurtQuery();
            var seen = new HashSet<string>();
            for (int i = 0; i < 300; i++) seen.Add(matcher.FindBest(q, 0).Name);
            CollectionAssert.AreEquivalent(new[] { "R4_lowHealth", "R5_swamp", "R6_shotgun" }, seen);
        }

        [Test]
        public void ZeroWeightRuleNeverWinsATie()
        {
            var rules = new[]
            {
                NickHurt("heavy").Where(Criterion.Exists("map")).Build(),
                NickHurt("never").Where(Criterion.Exists("weapon")).Weight(0).Build(),
            };
            var matcher = new RuleMatcher(new SystemRandomSource(3));
            matcher.Add(new RuleSet("test", rules));
            for (int i = 0; i < 100; i++) Assert.AreEqual("heavy", matcher.FindBest(NickHurtQuery(), 0).Name);
        }

        [Test]
        public void CriterionOnMissingFact_RejectsRule()
        {
            var matcher = new RuleMatcher();
            matcher.Add(new RuleSet("test", new[]
            {
                NickHurt("general").Build(),
                NickHurt("needsZombies").Where(Criterion.Ge("zombies", 0)).Build(),
            }));
            Assert.AreEqual("general", matcher.FindBest(NickHurtQuery(), 0).Name);
        }

        [Test]
        public void NoMatch_ReturnsNull()
        {
            var matcher = new RuleMatcher();
            matcher.Add(new RuleSet("test", new[] { new RuleBuilder("x").Concept("Nope").Build() }));
            Assert.IsNull(matcher.FindBest(NickHurtQuery(), 0));
        }

        [Test]
        public void WildcardSpeakerRules_AreFound()
        {
            var matcher = new RuleMatcher();
            matcher.Add(new RuleSet("test", new[]
            {
                new RuleBuilder("anyoneHurt").Concept("OnHurt").Build(),
                new RuleBuilder("anyoneHurtBadly").Concept("OnHurt").Where(Criterion.Lt("health", 25)).Build(),
                new RuleBuilder("ellisHurt").Concept("OnHurt").Who("Ellis").Build(),
            }));
            Assert.AreEqual("anyoneHurtBadly", matcher.FindBest(NickHurtQuery(), 0).Name);
        }

        [Test]
        public void MultipleRuleSets_AreSearchedTogether()
        {
            var global = new RuleSet("global", new[] { NickHurt("global").Build() });
            var swamp = new RuleSet("swamp", new[] { NickHurt("swampOnly").Where(Criterion.Eq("map", "swamp")).Build() });
            var matcher = new RuleMatcher();
            matcher.Add(global);
            Assert.AreEqual("global", matcher.FindBest(NickHurtQuery(), 0).Name);
            matcher.Add(swamp);
            Assert.AreEqual("swampOnly", matcher.FindBest(NickHurtQuery(), 0).Name);
            matcher.Remove(swamp);
            Assert.AreEqual("global", matcher.FindBest(NickHurtQuery(), 0).Name);
        }

        [Test]
        public void ScoreBonus_OverridesCriterionCount()
        {
            var matcher = new RuleMatcher();
            matcher.Add(new RuleSet("test", new[]
            {
                NickHurt("specific").Where(Criterion.Eq("map", "swamp")).Build(),
                NickHurt("boosted").ScoreBonus(5).Build(),
            }));
            Assert.AreEqual("boosted", matcher.FindBest(NickHurtQuery(), 0).Name);
        }

        [Test]
        public void Trace_ExplainsFailures()
        {
            var matcher = new RuleMatcher(new SystemRandomSource(1));
            matcher.Add(new RuleSet("test", SlideRules(includeMostSpecific: false)));
            var trace = new MatchTrace();
            var winner = matcher.FindBest(NickHurtQuery(), 0, trace);

            Assert.AreSame(winner, trace.Winner);
            Assert.AreEqual(3, trace.TieCount);
            var r3 = trace.Entries.Single(e => e.Rule.Name == "R3_wrongHealth");
            Assert.IsFalse(r3.Passed);
            Assert.AreEqual(Symbol.Of("health"), r3.Rule.Criteria[r3.FailedCriterion].Key);
            Assert.IsFalse(trace.Entries.Any(e => e.Rule.Name == "R2_wrongConcept"),
                "rules in other partitions are never tested");
            Assert.IsFalse(trace.Entries.Any(e => e.Rule.Name == "R1_general"),
                "lower-scoring rules are skipped once a match is found");
        }

        [Test]
        public void SymbolsCreatedAfterTheFirstLookup_StillMatch()
        {
            var matcher = new RuleMatcher();
            var early = new RuleBuilder("early").Concept("Grow").Build();
            matcher.Add(new RuleSet("a", new[] { early }));
            var q = new Query();
            q.Set("concept", "Grow");
            Assert.AreSame(early, matcher.FindBest(q, 0));

            // Many new symbols, so the matcher's fact table has to grow.
            for (int i = 0; i < 2000; i++) Symbol.Of("MatchingTests_growth_" + i);
            var lateKey = Symbol.Of("MatchingTests_late_key");
            var late = new RuleBuilder("late").Concept("Grow").Where(Criterion.Eq(lateKey, 1)).Build();
            var neverSet = new RuleBuilder("needsUnsetKey").Concept("Grow").Where(Criterion.Exists(Symbol.Of("MatchingTests_never_set"))).ScoreBonus(5).Build();
            matcher.Add(new RuleSet("b", new[] { late, neverSet }));

            q.Set(lateKey, 1);
            Assert.AreSame(late, matcher.FindBest(q, 0));
            q.Remove(lateKey);
            Assert.AreSame(early, matcher.FindBest(q, 0), "facts from the previous lookup don't leak into the next");
        }

        [Test]
        public void RuleSet_PartitionsByConceptAndWho()
        {
            var set = new RuleSet("test", SlideRules(includeMostSpecific: true));
            // (OnHurt, Nick) and (OnReload, Nick)
            Assert.AreEqual(2, set.BucketCount);
            Assert.AreEqual(6, set.LargestBucket);
        }

        /// <summary>
        /// Partitioned, score-ordered, early-out matching must give the same best score and the
        /// same set of tied winners as testing every rule.
        /// </summary>
        [Test]
        public void PartitionedMatching_AgreesWithBruteForce([Values(1, 2, 3, 4, 5)] int seed)
        {
            var rng = new Random(seed);
            string[] concepts = { "Fuzz_A", "Fuzz_B", "Fuzz_C" };
            string[] whos = { "Fuzz_Nick", "Fuzz_Ellis", "Fuzz_Coach" };
            string[] keys = { "fz_hp", "fz_zombies", "fz_map", "fz_count" };

            var rules = new List<Rule>();
            for (int i = 0; i < 400; i++)
            {
                var b = new RuleBuilder("fuzz" + i);
                if (rng.NextDouble() < 0.9) b.Concept(concepts[rng.Next(concepts.Length)]);
                if (rng.NextDouble() < 0.7) b.Who(whos[rng.Next(whos.Length)]);
                int extra = rng.Next(0, 4);
                for (int k = 0; k < extra; k++)
                {
                    var key = keys[rng.Next(keys.Length)];
                    float a = rng.Next(0, 10), c = rng.Next(0, 10);
                    var crit = Criterion.Between(key, Math.Min(a, c), Math.Max(a, c));
                    if (rng.NextDouble() < 0.2) crit = Criterion.NotEq(key, a);
                    if (rng.NextDouble() < 0.2) crit = crit.WhenMissing(0);
                    b.Where(crit);
                }
                rules.Add(b.Build());
            }

            var matcher = new RuleMatcher(new SystemRandomSource(seed));
            matcher.Add(new RuleSet("fuzz", rules));

            for (int trial = 0; trial < 300; trial++)
            {
                var q = new Query();
                if (rng.NextDouble() < 0.95) q.Set("concept", concepts[rng.Next(concepts.Length)]);
                if (rng.NextDouble() < 0.95) q.Set("who", whos[rng.Next(whos.Length)]);
                foreach (var key in keys)
                    if (rng.NextDouble() < 0.7) q.Set(key, rng.Next(0, 10));

                var matching = rules.Where(r => r.Matches(q)).ToList();
                var winner = matcher.FindBest(q, 0);
                if (matching.Count == 0)
                {
                    Assert.IsNull(winner, q.ToString());
                    continue;
                }
                int best = matching.Max(r => r.Score);
                Assert.IsNotNull(winner, q.ToString());
                Assert.AreEqual(best, winner.Score, q.ToString());
                Assert.IsTrue(winner.Matches(q));
            }
        }
    }
}
