using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DynamicDialog.Tests
{
    /// <summary>
    /// Logs garbage per lookup through the real components, for the README's Performance table. It uses the
    /// profiler's "GC Allocated In Frame" counter, because Unity's Mono doesn't implement
    /// GC.GetAllocatedBytesForCurrentThread. Uses only public API, so it can be run against older versions.
    /// </summary>
    public class AllocationMeasureTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        DialogSpeaker MakeSpeaker(string who, Vector3 position)
        {
            var go = Track(new GameObject(who));
            go.transform.position = position;
            go.SetActive(false);
            var s = go.AddComponent<DialogSpeaker>();
            typeof(DialogSpeaker).GetField("idleTalk", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(s, false);
            go.AddComponent<NearbySpeakerFacts>();
            var facts = go.AddComponent<StaticFacts>();
            facts.SetFact("faction", "nurse");
            facts.SetFact("health", "80");
            go.SetActive(true);
            return s;
        }

        const int Lookups = 1000;

        [UnityTest]
        public IEnumerator LogGarbagePerLookup()
        {
            var db = Track(ScriptableObject.CreateInstance<DialogRuleDatabase>());
            db.name = "MeasureRules";
            foreach (var who in new[] { "Mei", "Jun", "Ada" })
                for (int i = 0; i < 20; i++)
                {
                    var r = new RuleDefinition { name = $"{who}{i}", who = who, concept = "Talk" };
                    r.response.lines.Add(new DialogLineDefinition { text = "Hi.", duration = 1f });
                    r.criteria.Add(new CriterionDefinition { key = "health", op = CriterionOp.GreaterOrEqual, value = (i * 5).ToString() });
                    db.rules.Add(r);
                }
            db.Invalidate();
            var director = Track(new GameObject("Director")).AddComponent<DialogDirector>();
            director.AddDatabase(db);
            var mei = MakeSpeaker("Mei", Vector3.zero);
            MakeSpeaker("Jun", Vector3.right);
            MakeSpeaker("Ada", Vector3.left);
            yield return null;

            var engine = director.Engine;
            var character = mei.Character;
            var talk = Symbol.Of("Talk");
            var nothing = Symbol.Of("NothingAnswersThis");
            var recorder = engine.Recorder;
            engine.Recorder = null;

            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                if (!gc.Valid)
                {
                    Debug.Log("[DynamicDialog] Garbage per lookup: the 'GC Allocated In Frame' counter isn't available here.");
                    yield break;
                }
                var results = new long[3];
                Action[] bodies =
                {
                    () => mei.Speak(nothing),
                    () => engine.Ask(character, talk),
                    () => engine.Ask(character, talk),
                };
                for (int b = 0; b < bodies.Length; b++)
                {
                    if (b == 2) engine.Recorder = recorder ?? new DialogRecorder(256);
                    for (int i = 0; i < 50; i++) bodies[b]();
                    // Baseline: the quietest of a few frames without lookups.
                    long baseline = long.MaxValue;
                    for (int f = 0; f < 4; f++)
                    {
                        yield return null;
                        baseline = Math.Min(baseline, gc.LastValue);
                    }
                    for (int i = 0; i < Lookups; i++) bodies[b]();
                    yield return null;
                    results[b] = Math.Max(0, gc.LastValue - baseline) / Lookups;
                }
                Debug.Log($"[DynamicDialog] Garbage per lookup through components (Unity {Application.unityVersion}): " +
                          $"Speak with no match {results[0]} B, Engine.Ask with a match {results[1]} B, " +
                          $"with lookup recording on {results[2]} B.");
            }
        }
    }
}
