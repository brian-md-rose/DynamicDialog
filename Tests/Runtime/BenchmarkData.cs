using System;
using System.Collections.Generic;

namespace DynamicDialog.Tests
{
    /// <summary>
    /// Synthetic rule databases at the scale the talk describes (slide 108: ~50 rules per bucket,
    /// ~8 criteria per rule). Pure C#, shared by the Unity performance tests and the .NET benchmark runner.
    /// </summary>
    public static class BenchmarkData
    {
        public const int FactKeyCount = 40;

        static readonly Symbol[] keys = MakeKeys();

        static Symbol[] MakeKeys()
        {
            var k = new Symbol[FactKeyCount];
            for (int i = 0; i < k.Length; i++) k[i] = Symbol.Of("bench_fact_" + i);
            return k;
        }

        public static Symbol Speaker(int i) => Symbol.Of("BenchSpeaker" + i);
        public static Symbol Concept(int i) => Symbol.Of("BenchConcept" + i);

        /// <summary>
        /// speakers × concepts × rulesPerBucket rules (10 × 20 × 50 = 10,000 by default). Each has who, concept and
        /// 0-12 more criteria (6 on average, so 8 in total) over 40 fact keys with values 0-9.
        /// </summary>
        public static List<Rule> MakeRules(int speakers = 10, int concepts = 20, int rulesPerBucket = 50, int seed = 1)
        {
            var rng = new Random(seed);
            var rules = new List<Rule>(speakers * concepts * rulesPerBucket);
            for (int s = 0; s < speakers; s++)
                for (int c = 0; c < concepts; c++)
                    for (int r = 0; r < rulesPerBucket; r++)
                    {
                        var criteria = new List<Criterion>
                        {
                            Criterion.Eq(WellKnownFacts.Who, Speaker(s)),
                            Criterion.Eq(WellKnownFacts.Concept, Concept(c)),
                        };
                        int extra = rng.Next(0, 13);
                        for (int e = 0; e < extra; e++)
                        {
                            var key = keys[rng.Next(keys.Length)];
                            int a = rng.Next(0, 10), b = rng.Next(0, 10);
                            criteria.Add(rng.NextDouble() < 0.5
                                ? Criterion.Eq(key, a)
                                : Criterion.Between(key, Math.Min(a, b), Math.Max(a, b)));
                        }
                        rules.Add(new Rule($"bench_{s}_{c}_{r}", criteria));
                    }
            return rules;
        }

        /// <summary>A query with every fact key set (0-9), plus who, concept and rand.</summary>
        public static Query MakeQuery(Random rng, int speakers = 10, int concepts = 20)
        {
            var q = new Query(FactKeyCount + 4);
            foreach (var k in keys) q.Set(k, rng.Next(0, 10));
            q.Set(WellKnownFacts.Who, Speaker(rng.Next(speakers)));
            q.Set(WellKnownFacts.Concept, Concept(rng.Next(concepts)));
            q.Set(WellKnownFacts.Random, (float)(rng.NextDouble() * 100));
            return q;
        }

        /// <summary>Fills a memory with <paramref name="count"/> facts, for whole-engine benchmarks.</summary>
        public static void FillMemory(FactMemory memory, int count, Random rng)
        {
            for (int i = 0; i < count; i++) memory.Set(keys[i % keys.Length], rng.Next(0, 10), 0);
        }

        /// <summary>A fact provider that sets a few facts and allocates nothing.</summary>
        public sealed class StaticProvider : IFactProvider
        {
            readonly Symbol[] k;
            readonly float[] v;

            public StaticProvider(int first, int count, Random rng)
            {
                k = new Symbol[count];
                v = new float[count];
                for (int i = 0; i < count; i++)
                {
                    k[i] = keys[(first + i) % keys.Length];
                    v[i] = rng.Next(0, 10);
                }
            }

            public void AddFacts(Query query)
            {
                for (int i = 0; i < k.Length; i++) query.Set(k[i], v[i]);
            }
        }
    }
}
