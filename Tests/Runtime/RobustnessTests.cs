using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace DynamicDialog.Tests
{
    /// <summary>
    /// Play Mode without a domain reload (static state is reset when Play Mode starts), and garbage per
    /// lookup through the real components: the director's built-in world facts, Static Facts and Nearby Speaker Facts.
    /// </summary>
    public class RobustnessTests
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

        DialogRuleDatabase MakeDatabase(params RuleDefinition[] rules)
        {
            var db = Track(ScriptableObject.CreateInstance<DialogRuleDatabase>());
            db.name = "RobustnessRules";
            db.rules = new List<RuleDefinition>(rules);
            db.Invalidate();
            return db;
        }

        static RuleDefinition Rule(string name, string who, string concept, float duration = 0.05f)
        {
            var r = new RuleDefinition { name = name, who = who, concept = concept };
            r.response.lines.Add(new DialogLineDefinition { text = name, duration = duration });
            return r;
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

        DialogDirector MakeDirector(DialogRuleDatabase db)
        {
            var director = Track(new GameObject("Director")).AddComponent<DialogDirector>();
            director.AddDatabase(db);
            return director;
        }

        // ---- Play Mode without a domain reload --------------------------------------------------

        [Test]
        public void StaticReset_RunsAtSubsystemRegistration()
        {
            var method = typeof(DialogStaticState).GetMethod(nameof(DialogStaticState.ResetForPlayMode),
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attribute, "ResetForPlayMode needs [RuntimeInitializeOnLoadMethod]");
            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType);
        }

        [UnityTest]
        public IEnumerator StaticReset_ForgetsThePreviousSession()
        {
            var db = MakeDatabase(Rule("hello", "Mei", "Talk"));
            var director = MakeDirector(db);
            var tag = Track(new GameObject("Barrel")).AddComponent<DialogTag>();
            var mei = MakeSpeaker("Mei", Vector3.zero);
            yield return null;

            int staleLineHandler = 0, staleEventHandler = 0, staleAny = 0;
            Action<DialogSpeaker, DialogLine, DialogLineDefinition> onLine = (s, l, v) => staleLineHandler++;
            DialogSpeaker.AnyLineStarted += onLine;
            DialogEvents.On("Stale", (s, e) => staleEventHandler++);
            DialogEvents.Any += (s, e, l) => staleAny++;
            var presenter = new CountingPresenter();
            DialogPresenters.Register(presenter);
            Assert.AreSame(director, DialogDirector.Instance);
            Assert.That(DialogTag.All, Has.Member(tag));

            try
            {
                // What the end of one Play Mode session and the start of the next look like without a domain reload.
                Object.DestroyImmediate(mei.gameObject);
                Object.DestroyImmediate(director.gameObject);
                int session = DialogStaticState.PlaySession;
                DialogStaticState.ResetForPlayMode();

                Assert.AreEqual(session + 1, DialogStaticState.PlaySession);
                Assert.IsNull(DialogDirector.Instance, "no director left over");
                Assert.IsFalse(DialogEvents.HasHandlers("Stale"));
                Assert.IsFalse(DialogPresenters.IsRegistered(presenter));
                Assert.IsEmpty(DialogTag.All, "tags re-register when they are enabled");

                var fresh = MakeDirector(db);
                var mei2 = MakeSpeaker("Mei", Vector3.zero);
                yield return null;
                Assert.AreSame(fresh, DialogDirector.Instance);
                Assert.IsNotNull(mei2.Speak("Talk"));
                DialogEvents.Raise(mei2, new DialogEvent("Stale"));
                Assert.AreEqual(0, staleLineHandler, "AnyLineStarted subscribers were dropped");
                Assert.AreEqual(0, staleEventHandler + staleAny, "DialogEvents handlers were dropped");
                Assert.AreEqual(0, presenter.Started, "presenters were dropped");
            }
            finally
            {
                DialogSpeaker.AnyLineStarted -= onLine;
                DialogPresenters.Unregister(presenter);
            }
        }

        [UnityTest]
        public IEnumerator StaticReset_OnceAndCooldownStateDoesNotCarryOver()
        {
            var once = Rule("onlyOnce", "Mei", "Talk");
            once.once = true;
            var db = MakeDatabase(once);
            var director = MakeDirector(db);
            var mei = MakeSpeaker("Mei", Vector3.zero);
            yield return null;

            Assert.AreEqual("onlyOnce", mei.Speak("Talk")?.Rule.Name);
            mei.StopLine();
            Assert.IsNull(mei.Speak("Talk"), "once rule is used up in this session");

            // The asset stays in memory between sessions when the domain isn't reloaded.
            Object.DestroyImmediate(mei.gameObject);
            Object.DestroyImmediate(director.gameObject);
            DialogStaticState.ResetForPlayMode();

            MakeDirector(db);
            var mei2 = MakeSpeaker("Mei", Vector3.zero);
            yield return null;
            Assert.AreEqual("onlyOnce", mei2.Speak("Talk")?.Rule.Name, "a new session starts with fresh rule state");
        }

        [Test]
        public void RuleSet_IsCachedWithinASession()
        {
            var db = MakeDatabase(Rule("a", "Mei", "Talk"));
            Assert.AreSame(db.RuleSet, db.RuleSet);
        }

        sealed class CountingPresenter : IDialogPresenter
        {
            public int Started;
            public void OnLineStarted(DialogPresentedLine line) => Started++;
            public void OnLineEnded(DialogPresentedLine line) { }
            public void OnLineInterrupted(DialogPresentedLine line) { }
        }

        // ---- Garbage per lookup -------------------------------------------------------------------

        static readonly Symbol Talk = Symbol.Of("Talk");
        static readonly Symbol Nothing = Symbol.Of("NothingAnswersThis");

        IEnumerator SetUpScene(Action<DialogDirector, DialogSpeaker> done)
        {
            var rules = new List<RuleDefinition>();
            foreach (var who in new[] { "Mei", "Jun", "Ada" })
                for (int i = 0; i < 20; i++)
                {
                    var r = Rule($"{who}{i}", who, "Talk");
                    r.criteria.Add(new CriterionDefinition { key = "health", op = CriterionOp.GreaterOrEqual, value = (i * 5).ToString() });
                    rules.Add(r);
                }
            var director = MakeDirector(MakeDatabase(rules.ToArray()));
            var mei = MakeSpeaker("Mei", Vector3.zero);
            MakeSpeaker("Jun", Vector3.right);
            MakeSpeaker("Ada", Vector3.left);
            yield return null;
            director.Engine.Recorder = null; // as in a release build
            done(director, mei);
        }

        [UnityTest]
        public IEnumerator SpeakWithNoMatch_ThroughComponents_AllocatesNothing()
        {
            DialogDirector director = null;
            DialogSpeaker mei = null;
            yield return SetUpScene((d, s) => { director = d; mei = s; });
            for (int i = 0; i < 20; i++) mei.Speak(Nothing); // warm up
            Assert.That(() => { mei.Speak(Nothing); }, Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator AskWithMatch_ThroughComponents_AllocatesNothing()
        {
            DialogDirector director = null;
            DialogSpeaker mei = null;
            yield return SetUpScene((d, s) => { director = d; mei = s; });
            var character = mei.Character;
            for (int i = 0; i < 20; i++) director.Engine.Ask(character, Talk);
            Assert.IsTrue(director.Engine.Ask(character, Talk).Matched);
            Assert.That(() => { director.Engine.Ask(character, Talk); }, Is.Not.AllocatingGCMemory());
        }
    }
}
