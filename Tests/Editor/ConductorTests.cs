using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    public class ConductorTests
    {
        sealed class FakeSpeaker : IDialogSpeaker
        {
            readonly DialogConductor conductor;
            public readonly List<string> Said = new List<string>();
            public DialogLine Current;
            public bool Enabled = true;
            public float X;
            public float Earshot = 10f;

            public FakeSpeaker(DialogConductor conductor, string who, float x = 0f)
            {
                this.conductor = conductor;
                Character = conductor.Engine.GetOrCreateCharacter(who);
                X = x;
                conductor.Register(this);
            }

            public DialogCharacter Character { get; }
            public bool IsAvailable => Enabled && Current == null;
            public bool CanHear(IDialogSpeaker source) => Math.Abs(((FakeSpeaker)source).X - X) <= Earshot;

            public void BeginLine(DialogLine line)
            {
                Current = line;
                Said.Add(line.Rule.Name);
            }

            public void StopLine()
            {
                var l = Current;
                Current = null;
                conductor.EndLine(l, completed: false);
            }

            public void Complete()
            {
                var l = Current;
                Assert.IsNotNull(l, $"{Character} is not speaking");
                Current = null;
                conductor.EndLine(l, completed: true);
            }
        }

        ManualClock clock;
        DialogEngine engine;
        DialogConductor conductor;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock();
            engine = new DialogEngine(clock, new SystemRandomSource(5));
            conductor = new DialogConductor(engine);
        }

        void Load(params Rule[] rules) => engine.Matcher.Add(new RuleSet("test", rules));

        void Tick(double seconds = 0)
        {
            clock.Advance(seconds);
            conductor.Update();
        }

        [Test]
        public void Conversation_RunsAcrossCharacters_OneLineAtATime()
        {
            // Talk slides 67-70: Nick's swamp line hands off to Rochelle, who hands back to Nick.
            Load(
                new RuleBuilder("nick1").Concept("TalkIdle").Who("Nick").Then("Rochelle", "Swamp2").Build(),
                new RuleBuilder("rochelle2").Concept("Swamp2").Who("Rochelle").Then("Nick", "Swamp3").Build(),
                new RuleBuilder("nick3").Concept("Swamp3").Who("Nick").Build());
            var nick = new FakeSpeaker(conductor, "Nick");
            var rochelle = new FakeSpeaker(conductor, "Rochelle");

            Assert.IsNotNull(conductor.TrySpeak(nick, "TalkIdle"));
            Tick();
            Assert.IsEmpty(rochelle.Said, "the reply waits for the first line to finish");

            nick.Complete();
            Assert.IsEmpty(rochelle.Said, "followups go out on Update");
            Tick();
            CollectionAssert.AreEqual(new[] { "rochelle2" }, rochelle.Said);

            rochelle.Complete();
            Tick();
            CollectionAssert.AreEqual(new[] { "nick1", "nick3" }, nick.Said);
        }

        [Test]
        public void FollowupDelay_IsRespected()
        {
            Load(
                new RuleBuilder("a").Concept("Start").Who("Nick").ThenSelf("Next", delay: 2).Build(),
                new RuleBuilder("b").Concept("Next").Who("Nick").Build());
            var nick = new FakeSpeaker(conductor, "Nick");
            conductor.TrySpeak(nick, "Start");
            nick.Complete();

            Tick(1.9);
            Assert.AreEqual(1, nick.Said.Count);
            Tick(0.1);
            CollectionAssert.AreEqual(new[] { "a", "b" }, nick.Said);
        }

        [Test]
        public void StoppedLine_DropsItsFollowups()
        {
            Load(
                new RuleBuilder("long").Concept("Talk").Who("Nick").Then("Ellis", "Reply").Build(),
                new RuleBuilder("ouch").Concept("Hurt").Who("Nick").Build(),
                new RuleBuilder("reply").Concept("Reply").Who("Ellis").Build());
            var nick = new FakeSpeaker(conductor, "Nick");
            var ellis = new FakeSpeaker(conductor, "Ellis");

            conductor.TrySpeak(nick, "Talk");
            Assert.IsNull(conductor.TrySpeak(nick, "Hurt"), "busy speakers aren't interrupted by default");
            Assert.IsNotNull(conductor.TrySpeak(nick, "Hurt", interrupt: true));
            CollectionAssert.AreEqual(new[] { "long", "ouch" }, nick.Said);

            nick.Complete();
            Tick(10);
            Assert.IsEmpty(ellis.Said, "the interrupted line's followup never fires");
            Assert.AreEqual(0, conductor.PendingFollowupCount);
        }

        [Test]
        public void Interrupt_WithNoMatch_KeepsCurrentLine()
        {
            Load(new RuleBuilder("long").Concept("Talk").Who("Nick").Build());
            var nick = new FakeSpeaker(conductor, "Nick");
            var line = conductor.TrySpeak(nick, "Talk");
            Assert.IsNull(conductor.TrySpeak(nick, "Unknown", interrupt: true));
            Assert.AreSame(line, nick.Current);
        }

        [Test]
        public void BusyTarget_SkipsTheFollowup()
        {
            Load(
                new RuleBuilder("ask").Concept("Talk").Who("Nick").Then("Ellis", "Reply").Build(),
                new RuleBuilder("ellisBusy").Concept("Other").Who("Ellis").Build(),
                new RuleBuilder("reply").Concept("Reply").Who("Ellis").Build());
            var nick = new FakeSpeaker(conductor, "Nick");
            var ellis = new FakeSpeaker(conductor, "Ellis");

            conductor.TrySpeak(nick, "Talk");
            conductor.TrySpeak(ellis, "Other");
            nick.Complete();
            Tick();
            CollectionAssert.AreEqual(new[] { "ellisBusy" }, ellis.Said);
        }

        [Test]
        public void ThenAny_BestListenerInEarshotReplies()
        {
            Load(
                new RuleBuilder("hurt").Concept("Hurt").Who("Nick").ThenAny("BuddyHurt").Build(),
                new RuleBuilder("ellisGeneric").Concept("BuddyHurt").Who("Ellis").Build(),
                new RuleBuilder("coachSpecific").Concept("BuddyHurt").Who("Coach").Where(Criterion.Eq("from", "Nick")).Build(),
                new RuleBuilder("nickTalksToHimself").Concept("BuddyHurt").Who("Nick").ScoreBonus(10).Build());
            var nick = new FakeSpeaker(conductor, "Nick", x: 0);
            var ellis = new FakeSpeaker(conductor, "Ellis", x: 5);
            var coach = new FakeSpeaker(conductor, "Coach", x: 50);

            conductor.TrySpeak(nick, "Hurt");
            nick.Complete();
            Tick();
            Assert.IsEmpty(coach.Said, "Coach is out of earshot");
            CollectionAssert.AreEqual(new[] { "ellisGeneric" }, ellis.Said);
            CollectionAssert.AreEqual(new[] { "hurt" }, nick.Said, "the source isn't its own listener");

            ellis.Complete();
            coach.X = 3;
            conductor.TrySpeak(nick, "Hurt");
            nick.Complete();
            Tick();
            CollectionAssert.AreEqual(new[] { "coachSpecific" }, coach.Said, "Coach's line is more specific");
            Assert.AreEqual(1, ellis.Said.Count);
        }

        [Test]
        public void ThenAll_EveryListenerWithALineReplies()
        {
            Load(
                new RuleBuilder("rally").Concept("Rally").Who("Coach").ThenAll("Cheer").Build(),
                new RuleBuilder("ellisCheer").Concept("Cheer").Who("Ellis").Build(),
                new RuleBuilder("nickCheer").Concept("Cheer").Who("Nick").Build());
            var coach = new FakeSpeaker(conductor, "Coach");
            var ellis = new FakeSpeaker(conductor, "Ellis", 1);
            var nick = new FakeSpeaker(conductor, "Nick", 2);
            var rochelle = new FakeSpeaker(conductor, "Rochelle", 3);

            conductor.TrySpeak(coach, "Rally");
            coach.Complete();
            Tick();
            Assert.AreEqual(1, ellis.Said.Count);
            Assert.AreEqual(1, nick.Said.Count);
            Assert.IsEmpty(rochelle.Said);
        }

        [Test]
        public void NamedFollowups_CanRequireEarshot()
        {
            Load(
                new RuleBuilder("call").Concept("Talk").Who("Nick").Then("Ellis", "Reply").Build(),
                new RuleBuilder("reply").Concept("Reply").Who("Ellis").Build());
            var nick = new FakeSpeaker(conductor, "Nick", 0);
            var ellis = new FakeSpeaker(conductor, "Ellis", 100);

            conductor.NamedFollowupsRequireEarshot = true;
            conductor.TrySpeak(nick, "Talk");
            nick.Complete();
            Tick();
            Assert.IsEmpty(ellis.Said);

            conductor.NamedFollowupsRequireEarshot = false;
            conductor.TrySpeak(nick, "Talk");
            nick.Complete();
            Tick();
            Assert.AreEqual(1, ellis.Said.Count);
        }

        [Test]
        public void UnavailableSpeaker_IsNotAsked()
        {
            Load(new RuleBuilder("x").Concept("Talk").Who("Nick").Build());
            var nick = new FakeSpeaker(conductor, "Nick") { Enabled = false };
            Assert.IsNull(conductor.TrySpeak(nick, "Talk"));
        }

        [Test]
        public void Unregister_CancelsItsPendingFollowups()
        {
            Load(
                new RuleBuilder("a").Concept("Talk").Who("Nick").Then("Ellis", "Reply", delay: 1).Build(),
                new RuleBuilder("b").Concept("Reply").Who("Ellis").Build());
            var nick = new FakeSpeaker(conductor, "Nick");
            var ellis = new FakeSpeaker(conductor, "Ellis");
            conductor.TrySpeak(nick, "Talk");
            nick.Complete();
            conductor.Unregister(nick);
            Tick(2);
            Assert.IsEmpty(ellis.Said);
        }

        [Test]
        public void Events_StartBeforeEnd_AndNoMatchIsReported()
        {
            Load(new RuleBuilder("a").Concept("Talk").Who("Nick").Build());
            var nick = new FakeSpeaker(conductor, "Nick");
            var log = new List<string>();
            conductor.LineStarted += l => log.Add("start " + l.Rule.Name);
            conductor.LineEnded += (l, done) => log.Add("end " + l.Rule.Name + " " + done);
            conductor.NoMatch += (s, c) => log.Add("nomatch " + c);

            conductor.TrySpeak(nick, "Talk");
            nick.Complete();
            conductor.TrySpeak(nick, "Missing");
            CollectionAssert.AreEqual(new[] { "start a", "end a True", "nomatch Missing" }, log);
        }

        [Test]
        public void StaleFinish_IsIgnored()
        {
            Load(
                new RuleBuilder("a").Concept("Talk").Who("Nick").Then("Nick", "More").Build(),
                new RuleBuilder("b").Concept("More").Who("Nick").Build());
            var nick = new FakeSpeaker(conductor, "Nick");
            var first = conductor.TrySpeak(nick, "Talk");
            nick.Complete();
            conductor.EndLine(first, completed: true); // duplicate report
            Tick();
            Assert.AreEqual(2, nick.Said.Count, "the followup was scheduled once");
        }

        [Test]
        public void FactValue_ParsesAuthoredText()
        {
            Assert.AreEqual(3.5f, FactValue.Parse("3.5"));
            Assert.AreEqual(1f, FactValue.Parse("TRUE"));
            Assert.AreEqual(0f, FactValue.Parse("false"));
            Assert.AreEqual(0f, FactValue.Parse("  "));
            Assert.AreEqual(Symbol.Of("swamp").AsValue, FactValue.Parse(" Swamp "));

            var between = FactValue.CreateCriterion(Symbol.Of("hp"), CriterionOp.Between, 60, 30);
            Assert.IsTrue(between.Test(true, 45));
            Assert.IsFalse(between.Test(true, 61));
            Assert.IsTrue(FactValue.CreateCriterion(Symbol.Of("hp"), CriterionOp.ZeroOrMissing, 0).Test(false, 0));
            foreach (CriterionOp op in Enum.GetValues(typeof(CriterionOp)))
                Assert.DoesNotThrow(() => FactValue.CreateCriterion(Symbol.Of("hp"), op, 1, 2));
        }
    }
}
