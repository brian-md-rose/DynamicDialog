using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DynamicDialog;
using DynamicDialog.Tests;

/// Measures the talk's targets (slide 108): about 50 rules per bucket, about 8 criteria per rule,
/// and under a microsecond per lookup, with no garbage per query.
static class Program
{
    static void Main()
    {
        Console.WriteLine($".NET {Environment.Version}, {(Stopwatch.IsHighResolution ? "high-res" : "low-res")} timer\n");

        var sw = Stopwatch.StartNew();
        var rules = BenchmarkData.MakeRules();
        var set = new RuleSet("bench", rules);
        sw.Stop();
        Console.WriteLine($"Built {rules.Count:N0} rules, avg {rules.Average(r => r.Criteria.Length):0.0} criteria, " +
                          $"{set.BucketCount} buckets, largest {set.LargestBucket}, in {sw.ElapsedMilliseconds} ms");

        var rng = new Random(7);
        var queries = Enumerable.Range(0, 1000).Select(_ => BenchmarkData.MakeQuery(rng)).ToArray();

        var matcher = new RuleMatcher(new SystemRandomSource(1));
        matcher.Add(set);
        int matched = queries.Count(q => matcher.FindBest(q, 0) != null);
        Console.WriteLine($"{matched} of {queries.Length} queries match a rule\n");

        Measure("FindBest (partitioned, early-out)", 2_000_000, i => matcher.FindBest(queries[i % queries.Length], 0));

        var flat = new RuleMatcher(new SystemRandomSource(1));
        flat.Add(new RuleSet("flat", rules, Symbol.Of("bench_unused_partition")));
        Measure("FindBest without partitions (one 10k bucket)", 20_000, i => flat.FindBest(queries[i % queries.Length], 0));

        Measure("Rule.FirstFailedCriterion (one rule)", 20_000_000, i => rules[i % rules.Count].FirstFailedCriterion(queries[i % queries.Length]));

        // Whole engine: memory + providers + query building + matching.
        var clock = new ManualClock();
        var engine = new DialogEngine(clock, new SystemRandomSource(2));
        engine.Matcher.Add(set);
        BenchmarkData.FillMemory(engine.World, 10, rng);
        engine.WorldProviders.Add(new BenchmarkData.StaticProvider(10, 5, rng));
        var speakers = Enumerable.Range(0, 10).Select(s =>
        {
            var c = engine.GetOrCreateCharacter(BenchmarkData.Speaker(s));
            BenchmarkData.FillMemory(c.Memory, 15, rng);
            c.Providers.Add(new BenchmarkData.StaticProvider(25, 10, rng));
            return c;
        }).ToArray();
        var concepts = Enumerable.Range(0, 20).Select(BenchmarkData.Concept).ToArray();
        Measure("DialogEngine.Ask (build ~40-fact query + match)", 1_000_000, i => engine.Ask(speakers[i % speakers.Length], concepts[i % concepts.Length]));

        engine.Recorder = new DialogRecorder(256);
        Measure("DialogEngine.Ask with lookup recording on", 200_000, i => engine.Ask(speakers[i % speakers.Length], concepts[i % concepts.Length]));
    }

    static void Measure(string label, int iterations, Action<int> body)
    {
        for (int i = 0; i < Math.Min(iterations, 50_000); i++) body(i); // warm up and JIT
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++) body(i);
        sw.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        double ns = sw.Elapsed.TotalMilliseconds * 1e6 / iterations;
        Console.WriteLine($"{label,-50} {ns,9:0.0} ns/op   {(double)allocated / iterations,8:0.0} B/op");
    }
}
