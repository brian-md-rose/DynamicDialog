using System.Linq;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    public class DebuggingTests
    {
        sealed class HealthProvider : IFactProvider
        {
            public float Health = 80;
            public void AddFacts(Query q) => q.Set("health", Health);
        }

        ManualClock clock;
        DialogEngine engine;
        DialogCharacter nick;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock { Now = 10 };
            engine = new DialogEngine(clock, new SystemRandomSource(3)) { Recorder = new DialogRecorder(4) };
            nick = engine.GetOrCreateCharacter("Nick");
        }

        RuleSet Load(params Rule[] rules)
        {
            var set = new RuleSet("test", rules);
            engine.Matcher.Add(set);
            return set;
        }

        [Test]
        public void Recorder_KeepsQueriesWithFactSources()
        {
            Load(new RuleBuilder("hurt").Concept("Hurt").Who("Nick").Where(Criterion.Lt("health", 50)).Build());
            nick.Providers.Add(new HealthProvider { Health = 20 });
            engine.World.Set(Symbol.Of("map"), Symbol.Of("swamp"), 0);
            nick.Memory.Set("seen", 1, 0);

            var result = engine.Ask(nick, "Hurt", q => q.Set("damage", 30));
            var rec = engine.Recorder.Records.Single();

            Assert.AreSame(result.Rule, rec.Winner);
            Assert.AreEqual(Symbol.Of("Nick"), rec.Speaker);
            Assert.AreEqual(Symbol.Of("Hurt"), rec.Concept);
            Assert.AreEqual(10, rec.Time);
            Assert.IsFalse(rec.Started, "asking isn't playing");
            Assert.AreEqual("world memory", rec.SourceOf(Symbol.Of("map")));
            Assert.AreEqual("Nick memory", rec.SourceOf(Symbol.Of("seen")));
            Assert.AreEqual("HealthProvider", rec.SourceOf(Symbol.Of("health")));
            Assert.AreEqual("event", rec.SourceOf(Symbol.Of("damage")));
            Assert.AreEqual("engine", rec.SourceOf(WellKnownFacts.Who));
            Assert.AreEqual(1, rec.Tested.Count);

            engine.Commit(result);
            Assert.IsTrue(rec.Started);
        }

        [Test]
        public void RecordedQuery_IsACopy_AndTheRecorderIsBounded()
        {
            Load(new RuleBuilder("a").Concept("Talk").Build());
            var provider = new HealthProvider();
            nick.Providers.Add(provider);
            for (int i = 0; i < 6; i++)
            {
                provider.Health = i;
                engine.Ask(nick, "Talk");
            }
            Assert.AreEqual(4, engine.Recorder.Records.Count);
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6 }, engine.Recorder.Records.Select(r => r.Id));
            engine.Recorder.Records[0].Query.TryGet(Symbol.Of("health"), out float h);
            Assert.AreEqual(2f, h);
            Assert.AreSame(engine.Recorder.Records.Last(), engine.Recorder.Latest(Symbol.Of("Nick")));
        }

        [Test]
        public void SameQueryWithAndWithoutRecording_PicksTheSameRules()
        {
            Load(
                new RuleBuilder("general").Concept("Talk").Build(),
                new RuleBuilder("specific").Concept("Talk").Where(Criterion.Ge("health", 50)).Build());
            nick.Providers.Add(new HealthProvider());
            Assert.AreEqual("specific", engine.Ask(nick, "Talk").Rule.Name);
            engine.Recorder = null;
            Assert.AreEqual("specific", engine.Ask(nick, "Talk").Rule.Name);
        }

        [Test]
        public void Explain_FailedCondition_SaysWhatWasThere()
        {
            var lowHealth = new RuleBuilder("lowHealth").Concept("Talk").Who("Nick").Where(Criterion.Lt("health", 30)).Build();
            var swamp = new RuleBuilder("swamp").Concept("Talk").Who("Nick").Where(Criterion.Eq("map", "swamp")).Build();
            var ellis = new RuleBuilder("ellisOnly").Concept("Talk").Who("Ellis").Build();
            var otherConcept = new RuleBuilder("reload").Concept("Reload").Who("Nick").Build();
            Load(lowHealth, swamp, ellis, otherConcept, new RuleBuilder("general").Concept("Talk").Build());
            nick.Providers.Add(new HealthProvider { Health = 80 });

            engine.Ask(nick, "Talk");
            var rec = engine.Recorder.Records.Single();

            StringAssert.Contains("'health' was 80", RuleDiagnostics.Explain(lowHealth, rec).Text);
            StringAssert.Contains("'map' isn't set", RuleDiagnostics.Explain(swamp, rec).Text);
            StringAssert.Contains("It's for Ellis, but Nick was speaking", RuleDiagnostics.Explain(ellis, rec).Text);
            StringAssert.Contains("answers 'Reload', but this was 'Talk'", RuleDiagnostics.Explain(otherConcept, rec).Text);
            Assert.AreEqual(RuleVerdict.Won, RuleDiagnostics.Explain(rec.Winner, rec).Verdict);
        }

        [Test]
        public void Explain_OutscoredTiedUnavailableAndNotLoaded()
        {
            var general = new RuleBuilder("general").Concept("Talk").Build();
            var twin = new RuleBuilder("twin").Concept("Talk").Build();
            var specific = new RuleBuilder("specific").Concept("Talk").Where(Criterion.Exists("health")).Build();
            var once = new RuleBuilder("onceOnly").Concept("Talk").Where(Criterion.Exists("health")).Where(Criterion.Exists("rand")).Once().Build();
            var elsewhere = new RuleBuilder("elsewhere").Concept("Talk").Build();
            Load(general, twin, specific, once);
            nick.Providers.Add(new HealthProvider());

            engine.Commit(engine.Ask(nick, "Talk"));   // onceOnly wins and disables itself
            var result = engine.Ask(nick, "Talk");
            var rec = engine.Recorder.Records.Last();
            Assert.AreSame(specific, result.Rule);

            Assert.AreEqual(RuleVerdict.Outscored, RuleDiagnostics.Explain(general, rec).Verdict);
            StringAssert.Contains("more specific", RuleDiagnostics.Explain(general, rec).Text);
            var onceExplained = RuleDiagnostics.Explain(once, rec);
            Assert.AreEqual(RuleVerdict.Unavailable, onceExplained.Verdict);
            StringAssert.Contains("'once'", onceExplained.Text);
            Assert.AreEqual(RuleVerdict.NotLoaded, RuleDiagnostics.Explain(elsewhere, rec, loaded: false).Verdict);

            var q = new Query();
            q.Set("concept", "Talk");
            q.Set("who", "Nick");
            Assert.AreEqual(RuleVerdict.LostTie, RuleDiagnostics.Explain(twin, q, 0, winner: general).Verdict);
        }

        [Test]
        public void EvaluateAll_RanksWinnerFirst_AndSkipsOtherConcepts()
        {
            var set = new RuleSet("test", new[]
            {
                new RuleBuilder("general").Concept("Talk").Build(),
                new RuleBuilder("specific").Concept("Talk").Where(Criterion.Eq("map", "swamp")).Build(),
                new RuleBuilder("wrongMap").Concept("Talk").Where(Criterion.Eq("map", "mall")).Build(),
                new RuleBuilder("reload").Concept("Reload").Build(),
                new RuleBuilder("anyConcept").Where(Criterion.Exists("map")).Build(),
            });
            var q = new Query();
            q.Set("concept", "Talk");
            q.Set("map", "swamp");

            var results = RuleDiagnostics.EvaluateAll(new[] { set }, q, 0);
            CollectionAssert.AreEqual(new[] { "specific", "anyConcept", "general", "wrongMap" }, results.Select(r => r.Rule.Name));
            Assert.AreEqual(RuleVerdict.Won, results[0].Verdict);
            Assert.AreEqual(RuleVerdict.FailedCondition, results[3].Verdict);
            StringAssert.Contains("'map' was swamp", results[3].Text);
        }

        [Test]
        public void SymbolValues_DisplayByName()
        {
            var q = new Query();
            q.Set("map", "swamp");
            q.Set("mood", "angry");
            q.Set("health", 3);
            var mood = Symbol.Of("mood");
            Assert.IsTrue(q.IsSymbol(mood));
            StringAssert.Contains("mood=angry", q.ToString());
            StringAssert.Contains("health=3", q.ToString());

            Assert.AreEqual("mood == angry", Criterion.Eq("mood", "angry").ToString());
            Assert.AreEqual("health == 3", Criterion.Eq("health", 3).ToString());
            var parsed = DialogScriptParser.Parse("rule A\n concept X\n if mood == angry\n remember mood = calm").Rules[0].ToRule();
            Assert.AreEqual("mood == angry", parsed.Criteria.Single(c => c.Key == mood).ToString());
            Assert.IsTrue(parsed.Writes[0].ValueIsSymbol);
        }

        [Test]
        public void SymbolFacts_SurviveMemoryAndSaveByName()
        {
            var memory = new FactMemory();
            memory.Apply(new FactWrite(FactScope.Speaker, Symbol.Of("mood"), FactOp.Set, Symbol.Of("angry").AsValue, valueIsSymbol: true), 0);
            var q = new Query();
            memory.WriteTo(q, 0);
            Assert.IsTrue(q.IsSymbol(Symbol.Of("mood")));

            var saved = memory.Save(0);
            Assert.AreEqual("angry", saved.Single().SymbolValue, "symbols are saved by name, since IDs change between runs");
            var loaded = new FactMemory();
            loaded.Load(saved, 0);
            Assert.IsTrue(loaded.TryGet("mood", 0, out float v));
            Assert.AreEqual(Symbol.Of("angry").AsValue, v);
        }
    }
}
