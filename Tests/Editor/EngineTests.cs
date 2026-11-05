using System;
using System.Linq;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    public class EngineTests
    {
        sealed class LambdaProvider : IFactProvider
        {
            readonly Action<Query> add;
            public LambdaProvider(Action<Query> add) { this.add = add; }
            public void AddFacts(Query query) => add(query);
        }

        ManualClock clock;
        DialogEngine engine;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock();
            engine = new DialogEngine(clock, new SystemRandomSource(42));
        }

        void Load(params Rule[] rules) => engine.Matcher.Add(new RuleSet("test", rules));

        /// <summary>Asks, and commits if something matched, as the Unity layer will when a line starts.</summary>
        string Say(DialogCharacter who, string concept, Action<Query> facts = null)
        {
            var r = engine.Ask(who, concept, facts);
            engine.Commit(r);
            return r.Rule?.Name;
        }

        [Test]
        public void QueryPrecedence_LocalOverridesGlobal_AndWhoConceptAlwaysWin()
        {
            var nick = engine.GetOrCreateCharacter("Nick");
            engine.World.Set("x", 1, 0);
            engine.WorldProviders.Add(new LambdaProvider(q => q.Set("y", 1)));
            nick.Memory.Set("x", 2, 0);
            nick.Providers.Add(new LambdaProvider(q => q.Set("y", 2)));

            var q = new Query();
            engine.BuildQuery(q, nick, Symbol.Of("Talk"), e =>
            {
                e.Set("x", 3);
                e.Set("who", "Impostor");
            });

            q.TryGet(Symbol.Of("x"), out float x);
            q.TryGet(Symbol.Of("y"), out float y);
            Assert.AreEqual(3f, x, "event facts beat memory");
            Assert.AreEqual(2f, y, "speaker providers beat world providers");
            Assert.AreEqual(Symbol.Of("Nick"), q.Who);
            Assert.AreEqual(Symbol.Of("Talk"), q.Concept);
            Assert.IsTrue(q.TryGet(WellKnownFacts.Random, out float rand));
            Assert.That(rand, Is.GreaterThanOrEqualTo(0f).And.LessThan(100f));
        }

        [Test]
        public void RunningGag_MemoryChangesTheNextLine()
        {
            // Talk slides 34-36: the second barrel gets a different line.
            Load(
                new RuleBuilder("firstBarrel").Concept("OnSee").Where(Criterion.Eq("object", "barrel"))
                    .Where(Criterion.ZeroOrMissing("seen_barrels")).Increment("seen_barrels").Build(),
                new RuleBuilder("secondBarrel").Concept("OnSee").Where(Criterion.Eq("object", "barrel"))
                    .Where(Criterion.Eq("seen_barrels", 1)).Increment("seen_barrels").Build());

            var bot = engine.GetOrCreateCharacter("RedBot");
            Action<Query> barrel = q => q.Set("object", "barrel");

            Assert.AreEqual("firstBarrel", Say(bot, "OnSee", barrel));
            Assert.AreEqual("secondBarrel", Say(bot, "OnSee", barrel));
            Assert.IsNull(Say(bot, "OnSee", barrel));
        }

        [Test]
        public void Ask_WithoutCommit_ChangesNothing()
        {
            Load(new RuleBuilder("once").Concept("Talk").Once().Remember("talked", 1).Build());
            var nick = engine.GetOrCreateCharacter("Nick");
            engine.Ask(nick, "Talk");
            engine.Ask(nick, "Talk");
            Assert.AreEqual("once", engine.Ask(nick, "Talk").Rule?.Name);
            Assert.IsFalse(nick.Memory.TryGet("talked", 0, out _));
        }

        [Test]
        public void OnceRule_FallsBackToGeneralLine()
        {
            Load(
                new RuleBuilder("intro").Concept("TalkIdle").Who("Nick").Where(Criterion.Eq("map", "swamp")).Once().Build(),
                new RuleBuilder("generic").Concept("TalkIdle").Who("Nick").Build());
            var nick = engine.GetOrCreateCharacter("Nick");
            engine.World.Set("map", Symbol.Of("swamp").AsValue, 0);

            Assert.AreEqual("intro", Say(nick, "TalkIdle"));
            Assert.AreEqual("generic", Say(nick, "TalkIdle"));
            Assert.AreEqual("generic", Say(nick, "TalkIdle"));
        }

        [Test]
        public void Cooldown_BlocksUntilElapsed()
        {
            Load(new RuleBuilder("quip").Concept("Talk").Cooldown(10).Build());
            var nick = engine.GetOrCreateCharacter("Nick");
            Assert.AreEqual("quip", Say(nick, "Talk"));
            clock.Advance(9.9);
            Assert.IsNull(Say(nick, "Talk"));
            clock.Advance(0.1);
            Assert.AreEqual("quip", Say(nick, "Talk"));
        }

        [Test]
        public void ExpiringFact_SpacesOutLines()
        {
            // Talk slide 68: "Nick is speaking for the next few seconds".
            Load(new RuleBuilder("idle").Concept("TalkIdle").Where(Criterion.ZeroOrMissing("speaking"))
                .Remember("speaking", 1, duration: 4).Build());
            var nick = engine.GetOrCreateCharacter("Nick");
            Assert.AreEqual("idle", Say(nick, "TalkIdle"));
            clock.Advance(3);
            Assert.IsNull(Say(nick, "TalkIdle"));
            clock.Advance(1);
            Assert.AreEqual("idle", Say(nick, "TalkIdle"));
        }

        [Test]
        public void Writes_ReachWorldAndNamedCharacters()
        {
            Load(new RuleBuilder("gossip").Concept("Talk")
                .RememberWorld("gossip_started", 1)
                .Write(new FactWrite(FactScope.Character, Symbol.Of("seen_gossip"), FactOp.Set, 1, character: Symbol.Of("Ellis")))
                .Build());
            var nick = engine.GetOrCreateCharacter("Nick");
            Say(nick, "Talk");
            Assert.IsTrue(engine.World.TryGet("gossip_started", 0, out _));
            Assert.IsTrue(engine.TryGetCharacter(Symbol.Of("Ellis"), out var ellis));
            Assert.IsTrue(ellis.Memory.TryGet("seen_gossip", 0, out _));
        }

        [Test]
        public void ThenAny_MostSpecificListenerReplies()
        {
            // Talk slides 73-76: Coach has the most specific "help your buddy" line.
            Load(
                new RuleBuilder("ellisGeneric").Concept("BuddyHurt").Who("Ellis").Build(),
                new RuleBuilder("coachSpecific").Concept("BuddyHurt").Who("Coach")
                    .Where(Criterion.Eq("hurt_buddy", "Nick")).Where(Criterion.Ge("health", 50)).Build());

            var rochelle = engine.GetOrCreateCharacter("Rochelle");
            var ellis = engine.GetOrCreateCharacter("Ellis");
            var coach = engine.GetOrCreateCharacter("Coach");
            coach.Providers.Add(new LambdaProvider(q => q.Set("health", 90)));
            Action<Query> nickHurt = q => q.Set("hurt_buddy", "Nick");

            var best = engine.AskBest(new[] { rochelle, ellis, coach }, Symbol.Of("BuddyHurt"), nickHurt);
            Assert.AreEqual("coachSpecific", best.Rule.Name);
            Assert.AreSame(coach, best.Speaker);

            best = engine.AskBest(new[] { rochelle, ellis }, Symbol.Of("BuddyHurt"), nickHurt);
            Assert.AreSame(ellis, best.Speaker, "without Coach around, Ellis's generic line plays");

            Assert.IsFalse(engine.AskBest(new[] { rochelle }, Symbol.Of("BuddyHurt"), nickHurt).Success);
        }

        [Test]
        public void AskEach_ReturnsEveryMatch()
        {
            Load(
                new RuleBuilder("a").Concept("Cheer").Who("Ellis").Build(),
                new RuleBuilder("b").Concept("Cheer").Who("Coach").Build());
            var all = engine.AskEach(new[] { "Ellis", "Coach", "Rochelle" }.Select(engine.GetOrCreateCharacter), Symbol.Of("Cheer"));
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, all.Select(r => r.Rule.Name));
        }

        [Test]
        public void Followup_IsEvaluatedWhenDispatched_SoDangerEndsTheConversation()
        {
            // Talk slides 77-78: Ellis looks up his reply when Coach finishes, not when Coach starts.
            Load(
                new RuleBuilder("coachBand").Concept("TalkIdle").Who("Coach").Then("Ellis", "BandReply").Build(),
                new RuleBuilder("ellisAgrees").Concept("BandReply").Who("Ellis").Where(Criterion.ZeroOrMissing("in_danger")).Build());

            var coach = engine.GetOrCreateCharacter("Coach");
            var ellis = engine.GetOrCreateCharacter("Ellis");
            bool zombieOnEllis = false;
            ellis.Providers.Add(new LambdaProvider(q => q.Set("in_danger", zombieOnEllis)));

            var line = engine.Ask(coach, "TalkIdle");
            engine.Commit(line);
            var followup = line.Followups.Single();
            Assert.AreEqual(FollowupTarget.Character, followup.Target);
            Assert.AreEqual(Symbol.Of("Ellis"), followup.Character);

            // Calm: the reply plays.
            Assert.IsTrue(engine.Ask(ellis, followup.Concept).Success);

            // A zombie attacks while Coach is still talking: no reply, no interruption code needed.
            zombieOnEllis = true;
            Assert.IsFalse(engine.Ask(ellis, followup.Concept).Success);
        }

        [Test]
        public void RandFact_CanThrottleLines()
        {
            Load(
                new RuleBuilder("rare").Concept("Talk").Where(Criterion.Lt("rand", 30)).Build(),
                new RuleBuilder("common").Concept("Talk").Build());
            var nick = engine.GetOrCreateCharacter("Nick");
            int rare = Enumerable.Range(0, 2000).Count(_ => engine.Ask(nick, "Talk").Rule.Name == "rare");
            Assert.That(rare, Is.InRange(450, 750), "roughly 30% of 2000");
        }

        [Test]
        public void Trace_IsRecordedWhenEnabled()
        {
            Load(new RuleBuilder("needsAmmo").Concept("Talk").Where(Criterion.Lt("ammo", 5)).Build());
            engine.Trace = new MatchTrace();
            var nick = engine.GetOrCreateCharacter("Nick");
            nick.Providers.Add(new LambdaProvider(q => q.Set("ammo", 50)));

            Assert.IsFalse(engine.Ask(nick, "Talk").Success);
            var entry = engine.Trace.Entries.Single();
            Assert.AreEqual("ammo <= " + FloatMath.NextDown(5).ToString("G9", System.Globalization.CultureInfo.InvariantCulture),
                entry.Rule.Criteria[entry.FailedCriterion].ToString());
            Assert.IsTrue(engine.Trace.Query.Contains(Symbol.Of("ammo")));
        }
    }
}
