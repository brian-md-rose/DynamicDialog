using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace DynamicDialog.Tests
{
    /// <summary>
    /// The talk's targets under Unity's own runtime: a 10,000-rule database in buckets of ~50,
    /// no garbage per lookup, and lookups fast enough to run every frame.
    /// </summary>
    public class PerformanceTests
    {
        static RuleSet set;
        static Query[] queries;

        [OneTimeSetUp]
        public void Build()
        {
            if (set != null) return;
            set = new RuleSet("bench", BenchmarkData.MakeRules());
            var rng = new Random(7);
            queries = Enumerable.Range(0, 256).Select(_ => BenchmarkData.MakeQuery(rng)).ToArray();
        }

        [Test]
        public void TenThousandRules_PartitionIntoBucketsOfFifty()
        {
            Assert.AreEqual(10_000, set.Rules.Count);
            Assert.AreEqual(200, set.BucketCount);
            Assert.AreEqual(50, set.LargestBucket);
        }

        [Test]
        public void FindBest_AllocatesNothing()
        {
            var matcher = new RuleMatcher(new SystemRandomSource(1));
            matcher.Add(set);
            foreach (var q in queries) matcher.FindBest(q, 0); // warm up: sizes internal buffers
            var query = queries[3];
            Assert.That(() => { matcher.FindBest(query, 0); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void EngineAsk_AllocatesNothing()
        {
            var engine = MakeEngine(out var speaker);
            var concept = BenchmarkData.Concept(4);
            for (int i = 0; i < 100; i++) engine.Ask(speaker, concept);
            Assert.That(() => { engine.Ask(speaker, concept); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void FindBest_IsFast()
        {
            var matcher = new RuleMatcher(new SystemRandomSource(1));
            matcher.Add(set);
            const int iterations = 50_000;
            for (int i = 0; i < 5_000; i++) matcher.FindBest(queries[i % queries.Length], 0);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) matcher.FindBest(queries[i % queries.Length], 0);
            sw.Stop();
            double microseconds = sw.Elapsed.TotalMilliseconds * 1000.0 / iterations;
            UnityEngine.Debug.Log($"[DynamicDialog] FindBest over 10,000 rules: {microseconds:0.00} µs per lookup (Mono, editor).");

            // Generous bound so slow CI machines don't fail: 100 lookups a frame would still cost under 2 ms.
            Assert.Less(microseconds, 20.0);
        }

        static DialogEngine MakeEngine(out DialogCharacter speaker)
        {
            var rng = new Random(2);
            var engine = new DialogEngine(new ManualClock(), new SystemRandomSource(2));
            engine.Matcher.Add(set);
            BenchmarkData.FillMemory(engine.World, 10, rng);
            engine.WorldProviders.Add(new BenchmarkData.StaticProvider(10, 5, rng));
            speaker = engine.GetOrCreateCharacter(BenchmarkData.Speaker(1));
            BenchmarkData.FillMemory(speaker.Memory, 15, rng);
            speaker.Providers.Add(new BenchmarkData.StaticProvider(25, 10, rng));
            return engine;
        }
    }
}
