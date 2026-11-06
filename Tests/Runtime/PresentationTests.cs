using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DynamicDialog.Tests
{
    /// <summary>The presenter interface and its wiring, the typewriter, overlap rules and screen placement.</summary>
    public class PresentationTests
    {
        // ---- Typewriter -----------------------------------------------------------------------

        [Test]
        public void Typewriter_RevealsAtCharactersPerSecond()
        {
            var t = new DialogTypewriter { charactersPerSecond = 40f, finishBy = 0.7f };
            Assert.AreEqual(0, t.VisibleCharacters(100, 0f, 100f));
            Assert.AreEqual(4, t.VisibleCharacters(100, 0.1f, 100f));
            Assert.AreEqual(40, t.VisibleCharacters(100, 1f, 100f));
            Assert.AreEqual(100, t.VisibleCharacters(100, 60f, 100f), "never more than the line");
            Assert.AreEqual(2.5f, t.RevealSeconds(100, 100f), 1e-4f);
        }

        [Test]
        public void Typewriter_SpeedsUpToFinishBeforeTheLineEnds()
        {
            var t = new DialogTypewriter { charactersPerSecond = 45f, finishBy = 0.5f };
            // 90 characters in a 2 s line must be visible by 1 s: 90 per second.
            Assert.AreEqual(90f, t.CharactersPerSecond(90, 2f), 1e-4f);
            Assert.AreEqual(45, t.VisibleCharacters(90, 0.5f, 2f));
            Assert.AreEqual(90, t.VisibleCharacters(90, 1f, 2f));
            Assert.AreEqual(1f, t.RevealSeconds(90, 2f), 1e-4f);

            t.finishBy = 0f;
            Assert.AreEqual(45f, t.CharactersPerSecond(90, 2f), 1e-4f, "finishBy 0 turns the cap off");
            Assert.AreEqual(45f, new DialogTypewriter { charactersPerSecond = 45f }.CharactersPerSecond(90, 0f), 1e-4f, "unknown duration: no cap");
        }

        [Test]
        public void Typewriter_Disabled_ShowsEverythingAtOnce()
        {
            var t = new DialogTypewriter { enabled = false };
            Assert.AreEqual(12, t.VisibleCharacters(12, 0f, 3f));
            Assert.AreEqual(0f, t.RevealSeconds(12, 3f));
            Assert.AreEqual(0, new DialogTypewriter().VisibleCharacters(0, 5f, 3f));
        }

        // ---- Overlap --------------------------------------------------------------------------

        [Test]
        public void Overlap_SelectVisible_StacksOrReplaces()
        {
            var playing = new List<string> { "old", "mid", "new" };
            var result = new List<string>();

            DialogOverlap.SelectVisible(playing, DialogOverlapMode.Stack, 0, result);
            CollectionAssert.AreEqual(new[] { "old", "mid", "new" }, result);

            DialogOverlap.SelectVisible(playing, DialogOverlapMode.Stack, 2, result);
            CollectionAssert.AreEqual(new[] { "mid", "new" }, result, "over the limit, the oldest goes");

            DialogOverlap.SelectVisible(playing, DialogOverlapMode.Replace, 0, result);
            CollectionAssert.AreEqual(new[] { "new" }, result);

            DialogOverlap.SelectVisible(new List<string>(), DialogOverlapMode.Replace, 0, result);
            CollectionAssert.IsEmpty(result);
        }

        [Test]
        public void Overlap_SeparateVertically_MovesLaterRectsAbove()
        {
            var rects = new List<Rect>
            {
                new Rect(0, 0, 100, 40),
                new Rect(50, 10, 100, 40),   // overlaps the first
                new Rect(300, 0, 100, 40),   // clear of both
                new Rect(20, 0, 100, 40),    // overlaps the first, then the moved second
            };
            DialogOverlap.SeparateVertically(rects, 5f);

            Assert.AreEqual(new Rect(0, 0, 100, 40), rects[0], "the oldest stays put");
            Assert.AreEqual(45f, rects[1].y, 1e-4f);
            Assert.AreEqual(50f, rects[1].x, 1e-4f, "only moves vertically");
            Assert.AreEqual(new Rect(300, 0, 100, 40), rects[2], "a clear rect doesn't move");
            Assert.AreEqual(90f, rects[3].y, 1e-4f);
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.IsFalse(rects[i].Overlaps(rects[j]), $"{i} and {j} overlap");
        }

        [Test]
        public void Overlap_SeparateVertically_GoesBelowWhenThereIsNoRoomAbove()
        {
            var rects = new List<Rect> { new Rect(0, 400, 100, 40), new Rect(10, 410, 100, 40) };
            DialogOverlap.SeparateVertically(rects, 5f, maxY: 480f);
            Assert.AreEqual(355f, rects[1].y, 1e-4f, "no room above the top edge, so below");
            Assert.IsFalse(rects[0].Overlaps(rects[1]));

            rects = new List<Rect> { new Rect(0, 100, 100, 40), new Rect(10, 110, 100, 40) };
            DialogOverlap.SeparateVertically(rects, 5f, maxY: 480f);
            Assert.AreEqual(145f, rects[1].y, 1e-4f, "room above: above");
        }

        // ---- Screen placement -----------------------------------------------------------------

        static readonly Vector2 Screen = new Vector2(1000f, 500f);
        static readonly Vector2 Bubble = new Vector2(100f, 50f);

        [Test]
        public void Placement_OnScreen_SitsAboveTheAnchor()
        {
            var p = DialogScreenPlacement.Place(new Vector3(400f, 200f, 5f), Screen, Bubble, 10f, 5f);
            Assert.AreEqual(new Vector2(400f, 235f), p.Center);
            Assert.AreEqual(new Vector2(400f, 200f), p.Target);
            Assert.IsFalse(p.Clamped);
            Assert.IsFalse(p.BehindCamera);
        }

        [Test]
        public void Placement_AtTheEdge_StaysOnScreen()
        {
            var p = DialogScreenPlacement.Place(new Vector3(1200f, 600f, 5f), Screen, Bubble, 10f, 5f);
            Assert.AreEqual(new Vector2(945f, 470f), p.Center, "kept inside, margin included");
            Assert.AreEqual(new Vector2(1200f, 600f), p.Target, "the tail still points at the speaker");
            Assert.IsTrue(p.Clamped);
            Assert.IsFalse(DialogScreenPlacement.IsOnScreen(new Vector3(1200f, 600f, 5f), Screen));
            Assert.IsTrue(DialogScreenPlacement.IsOnScreen(new Vector3(400f, 200f, 5f), Screen));
        }

        [Test]
        public void Placement_BehindTheCamera_GoesToTheEdgeOnTheSpeakersSide()
        {
            // Behind the camera, projections come out mirrored: a speaker behind and below projects above center.
            var p = DialogScreenPlacement.Place(new Vector3(500f, 400f, -5f), Screen, Bubble, 10f, 5f);
            Assert.IsTrue(p.BehindCamera);
            Assert.IsTrue(p.Clamped);
            Assert.AreEqual(new Vector2(500f, 0f), p.Target, "flipped to the bottom edge");
            Assert.AreEqual(new Vector2(500f, 35f), p.Center);
            Assert.IsFalse(DialogScreenPlacement.IsOnScreen(new Vector3(500f, 400f, -5f), Screen));

            var left = DialogScreenPlacement.Place(new Vector3(900f, 250f, -5f), Screen, Bubble, 10f, 5f);
            Assert.AreEqual(new Vector2(0f, 250f), left.Target, "projected right of center while behind: the speaker is to the left");
        }

        [Test]
        public void EdgePoint_FindsWhereTheTailJoins()
        {
            var r = new Rect(0f, 0f, 100f, 50f);
            Assert.AreEqual(new Vector2(50f, 0f), DialogScreenPlacement.EdgePoint(r, new Vector2(50f, -100f)));
            Assert.AreEqual(new Vector2(100f, 25f), DialogScreenPlacement.EdgePoint(r, new Vector2(300f, 25f)));
            Assert.AreEqual(new Vector2(50f, 0f), DialogScreenPlacement.EdgePoint(r, r.center), "degenerate: bottom center");
        }

        // ---- Presenter wiring (Play Mode) -----------------------------------------------------

        class RecordingPresenter : IDialogPresenter
        {
            public readonly List<string> calls = new List<string>();
            public readonly List<DialogPresentedLine> lines = new List<DialogPresentedLine>();
            public void OnLineStarted(DialogPresentedLine line) { calls.Add("start " + line.Text); lines.Add(line); }
            public void OnLineEnded(DialogPresentedLine line) => calls.Add("end " + line.Text);
            public void OnLineInterrupted(DialogPresentedLine line) => calls.Add("interrupt " + line.Text);
        }

        class ThrowingPresenter : IDialogPresenter
        {
            public void OnLineStarted(DialogPresentedLine line) => throw new System.InvalidOperationException("presenter bug");
            public void OnLineEnded(DialogPresentedLine line) { }
            public void OnLineInterrupted(DialogPresentedLine line) { }
        }

        class BehaviourPresenter : DialogPresenterBehaviour
        {
            public int started, ended, interrupted;
            public override void OnLineStarted(DialogPresentedLine line) => started++;
            public override void OnLineEnded(DialogPresentedLine line) => ended++;
        }

        readonly List<Object> created = new List<Object>();
        DialogRuleDatabase database;

        [SetUp]
        public void SetUp()
        {
            DialogPresenters.Reset();
            database = Track(ScriptableObject.CreateInstance<DialogRuleDatabase>());
            var director = Track(new GameObject("Director")).AddComponent<DialogDirector>();
            director.AddDatabase(database);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            DialogPresenters.Reset();
        }

        T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        DialogSpeaker MakeSpeaker(string who)
        {
            var go = Track(new GameObject(who));
            go.SetActive(false);
            var s = go.AddComponent<DialogSpeaker>();
            typeof(DialogSpeaker).GetField("idleTalk", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(s, false);
            go.SetActive(true);
            return s;
        }

        void SetRules(params (string name, string who, string concept, string text, float duration)[] rules)
        {
            database.rules = new List<RuleDefinition>();
            foreach (var (name, who, concept, text, duration) in rules)
            {
                var r = new RuleDefinition { name = name, who = who, concept = concept };
                r.response.lines.Add(new DialogLineDefinition { text = text, duration = duration });
                database.rules.Add(r);
            }
            database.Invalidate();
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds = 3f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest]
        public IEnumerator Presenter_GetsStartAndEnd_WithLocalizedTextAndTiming()
        {
            SetRules(("hello", "Rosa", "Talk", "Hello there", 0.2f));
            var rosa = MakeSpeaker("Rosa");
            var p = new RecordingPresenter();
            DialogPresenters.Register(p);
            yield return null;

            var line = rosa.Speak("Talk");
            Assert.IsNotNull(line);
            CollectionAssert.AreEqual(new[] { "start Hello there" }, p.calls);
            var presented = p.lines[0];
            Assert.AreSame(rosa, presented.Speaker);
            Assert.AreSame(line, presented.Line);
            Assert.AreEqual("Rosa", presented.SpeakerName);
            Assert.AreEqual(0.2f, presented.Duration, 1e-4f);
            Assert.AreEqual(rosa.CurrentStartTime, presented.StartTime);
            Assert.AreEqual(0.2f, rosa.CurrentDuration, 1e-4f);
            Assert.AreEqual(1, DialogPresenters.Playing.Count);

            yield return WaitUntil(() => p.calls.Count == 2);
            CollectionAssert.AreEqual(new[] { "start Hello there", "end Hello there" }, p.calls);
            Assert.AreEqual(0, DialogPresenters.Playing.Count);
            Assert.AreEqual(0f, rosa.CurrentDuration);
        }

        [UnityTest]
        public IEnumerator Presenter_GetsInterrupted_WhenALineIsStoppedEarly()
        {
            SetRules(("story", "Rosa", "Talk", "A long story", 5f), ("ouch", "Rosa", "Hurt", "Ouch", 0.1f));
            var rosa = MakeSpeaker("Rosa");
            var p = new RecordingPresenter();
            DialogPresenters.Register(p);
            yield return null;

            rosa.Speak("Talk");
            rosa.Speak("Hurt", interrupt: true);
            CollectionAssert.AreEqual(new[] { "start A long story", "interrupt A long story", "start Ouch" }, p.calls);

            rosa.StopLine();
            Assert.AreEqual("interrupt Ouch", p.calls[p.calls.Count - 1]);

            rosa.Speak("Talk");
            rosa.gameObject.SetActive(false);
            Assert.AreEqual("interrupt A long story", p.calls[p.calls.Count - 1], "disabling the speaker interrupts");
        }

        [UnityTest]
        public IEnumerator Presenter_RegisteredMidLine_IsToldAboutIt_AndUnregisterStops()
        {
            SetRules(("story", "Rosa", "Talk", "A long story", 0.3f));
            var rosa = MakeSpeaker("Rosa");
            yield return null;
            rosa.Speak("Talk");

            var late = new RecordingPresenter();
            DialogPresenters.Register(late);
            DialogPresenters.Register(late);
            CollectionAssert.AreEqual(new[] { "start A long story" }, late.calls, "told once, even if registered twice");
            Assert.IsTrue(DialogPresenters.IsRegistered(late));

            Assert.IsTrue(DialogPresenters.Unregister(late));
            Assert.IsFalse(DialogPresenters.Unregister(late));
            yield return WaitUntil(() => !rosa.IsSpeaking);
            Assert.AreEqual(1, late.calls.Count, "no calls after unregistering");
        }

        [UnityTest]
        public IEnumerator Presenter_ThatThrows_DoesNotStopTheOthers()
        {
            SetRules(("hello", "Rosa", "Talk", "Hello", 0.1f));
            var rosa = MakeSpeaker("Rosa");
            DialogPresenters.Register(new ThrowingPresenter());
            var p = new RecordingPresenter();
            DialogPresenters.Register(p);
            yield return null;

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("presenter bug"));
            rosa.Speak("Talk");
            CollectionAssert.AreEqual(new[] { "start Hello" }, p.calls);
            Assert.IsTrue(rosa.IsSpeaking, "the line still plays");
        }

        [UnityTest]
        public IEnumerator PresenterBehaviour_RegistersWhileEnabled()
        {
            SetRules(("hello", "Rosa", "Talk", "Hello", 0.1f));
            var rosa = MakeSpeaker("Rosa");
            var presenter = Track(new GameObject("Presenter")).AddComponent<BehaviourPresenter>();
            Assert.IsTrue(DialogPresenters.IsRegistered(presenter));
            yield return null;

            rosa.Speak("Talk");
            Assert.AreEqual(1, presenter.started);
            rosa.StopLine();
            Assert.AreEqual(1, presenter.ended, "interrupted falls back to OnLineEnded by default");

            presenter.enabled = false;
            Assert.IsFalse(DialogPresenters.IsRegistered(presenter));
            rosa.Speak("Talk");
            Assert.AreEqual(1, presenter.started);
        }

        [UnityTest]
        public IEnumerator Presenter_AudioOnlyLine_HasNoText()
        {
            SetRules(("grunt", "Rosa", "Talk", "", 0.1f));
            var rosa = MakeSpeaker("Rosa");
            var p = new RecordingPresenter();
            DialogPresenters.Register(p);
            yield return null;

            rosa.Speak("Talk");
            Assert.AreEqual(1, p.lines.Count, "audio-only lines are passed on");
            Assert.IsFalse(p.lines[0].HasText);
        }
    }
}
