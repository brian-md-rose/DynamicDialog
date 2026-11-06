using System.Collections.Generic;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    /// <summary>The 1.0 renames: new names work, and the old 0.6 names still forward to them.</summary>
    public class ApiCompatibilityTests
    {
        [Test]
        public void WellKnownFacts_HaveTheirAuthoredNames()
        {
            Assert.AreEqual(Symbol.Of("from"), WellKnownFacts.From);
            Assert.AreEqual(Symbol.Of("map"), WellKnownFacts.Map);
            Assert.AreEqual(Symbol.Of("speakers_talking"), WellKnownFacts.SpeakersTalking);
            Assert.AreEqual(Symbol.Of("object"), WellKnownFacts.Object);
            Assert.AreEqual(Symbol.Of("object_distance"), WellKnownFacts.ObjectDistance);
            Assert.AreEqual(Symbol.Of("area"), WellKnownFacts.Area);
            Assert.AreEqual(Symbol.Of("nearby_speakers"), WellKnownFacts.NearbySpeakers);
            Assert.AreEqual(Symbol.Of("region"), WellKnownFacts.Region);
            Assert.AreEqual("near_", WellKnownFacts.NearPrefix);
        }

        [Test]
        public void Analyzer_KnowsTheRegionFact()
        {
            var src = DialogScriptParser.Parse("rule A\n  concept Talk\n  if region == swamp\n  say \"hi\"\n");
            var messages = RuleAnalyzer.Analyze(new[] { new RuleSourceGroup("test.dlg", src.Rules) });
            foreach (var m in messages) StringAssert.DoesNotContain("region", m.Text);
        }

#pragma warning disable 618 // the obsolete names are what these tests check
        [Test]
        public void ObsoleteFromFact_IsWellKnownFrom()
        {
            Assert.AreEqual(WellKnownFacts.From, DialogConductor.FromFact);
        }

        [Test]
        public void ObsoleteSuccess_IsMatched()
        {
            var engine = new DialogEngine(new ManualClock());
            engine.Matcher.Add(new RuleSet("s", new[] { new RuleBuilder("hi").Concept("Talk").Build() }));
            var nick = engine.GetOrCreateCharacter("ApiNick");
            var hit = engine.Ask(nick, "Talk");
            var miss = engine.Ask(nick, "Nothing");
            Assert.IsTrue(hit.Matched);
            Assert.IsTrue(hit.Success);
            Assert.IsFalse(miss.Matched);
            Assert.IsFalse(miss.Success);
        }

        sealed class Speaker : IDialogSpeaker
        {
            public Speaker(DialogConductor c, string who) { Character = c.Engine.GetOrCreateCharacter(who); c.Register(this); }
            public DialogCharacter Character { get; }
            public DialogLine Current;
            public bool IsAvailable => Current == null;
            public bool CanHear(IDialogSpeaker source) => true;
            public void BeginLine(DialogLine line) => Current = line;
            public void StopLine() => Current = null;
        }

        [Test]
        public void ObsoleteLineFinished_EndsTheLine()
        {
            var engine = new DialogEngine(new ManualClock());
            engine.Matcher.Add(new RuleSet("s", new[] { new RuleBuilder("hi").Concept("Talk").ThenSelf("Again").Build() }));
            var conductor = new DialogConductor(engine);
            var speaker = new Speaker(conductor, "ApiRochelle");
            var ended = new List<bool>();
            conductor.LineEnded += (l, done) => ended.Add(done);

            var line = conductor.TrySpeak(speaker, "Talk");
            Assert.IsNotNull(line);
            conductor.LineFinished(line, true);

            CollectionAssert.AreEqual(new[] { true }, ended);
            Assert.IsFalse(conductor.IsSpeaking(speaker));
            Assert.AreEqual(1, conductor.PendingFollowupCount, "a completed line schedules its followups");
        }
#pragma warning restore 618
    }
}
