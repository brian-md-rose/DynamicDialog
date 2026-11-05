using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    public class SaveLoadTests
    {
        ManualClock clock;
        DialogEngine engine;
        RuleSet global;
        Rule once, cooldown;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock { Now = 100 };
            engine = new DialogEngine(clock, new SystemRandomSource(1));
            once = new RuleBuilder("intro").Concept("Talk").Who("Nick").Once().Build();
            cooldown = new RuleBuilder("quip").Concept("Quip").Who("Nick").Cooldown(30).Build();
            global = new RuleSet("Global", new[] { once, cooldown });
            engine.Matcher.Add(global);
        }

        [Test]
        public void SaveAndLoad_RestoresMemoryAndRuleState()
        {
            var nick = engine.GetOrCreateCharacter("Nick");
            engine.World.Set(Symbol.Of("map"), Symbol.Of("swamp"), clock.Now);
            nick.Memory.Set("seen_barrels", 2, clock.Now);
            nick.Memory.Set("speaking", 1, clock.Now, duration: 30);
            engine.Commit(engine.Ask(nick, "Talk"));
            clock.Advance(5);
            engine.Commit(engine.Ask(nick, "Quip"));
            clock.Advance(10);

            var data = engine.SaveState();
            Assert.AreEqual(2, data.rules.Count);
            var quipState = data.rules.Single(r => r.rule == "quip");
            Assert.AreEqual(10f, quipState.secondsSinceFired, 1e-4);

            // A fresh session: new engine, new rule objects, time starts over.
            var clock2 = new ManualClock { Now = 0 };
            var engine2 = new DialogEngine(clock2, new SystemRandomSource(1));
            var once2 = new RuleBuilder("intro").Concept("Talk").Who("Nick").Once().Build();
            var cool2 = new RuleBuilder("quip").Concept("Quip").Who("Nick").Cooldown(30).Build();
            engine2.Matcher.Add(new RuleSet("global", new[] { once2, cool2 })); // set names ignore case
            var pending = engine2.LoadState(data);

            Assert.IsEmpty(pending);
            Assert.IsTrue(once2.State.Disabled, "the once rule stays used");
            Assert.AreEqual(1, cool2.State.TimesFired);
            Assert.IsFalse(cool2.IsAvailable(clock2.Now), "cooldown carries over");
            Assert.IsTrue(cool2.IsAvailable(clock2.Now + 20.01), "with 20s left");

            var nick2 = engine2.GetOrCreateCharacter("Nick");
            Assert.IsTrue(nick2.Memory.TryGet("seen_barrels", 0, out float seen));
            Assert.AreEqual(2f, seen);
            Assert.IsTrue(nick2.Memory.TryGet("speaking", 14.9, out _), "expiring facts keep their remaining time (15s)");
            Assert.IsFalse(nick2.Memory.TryGet("speaking", 15.1, out _));
            var q = new Query();
            engine2.World.WriteTo(q, 0);
            Assert.AreEqual(Symbol.Of("swamp"), q.GetSymbol(Symbol.Of("map")));
        }

        [Test]
        public void Load_ReplacesExistingState_AndKeepsStatesForSetsNotLoaded()
        {
            var nick = engine.GetOrCreateCharacter("Nick");
            nick.Memory.Set("stale", 1, clock.Now);
            engine.Commit(engine.Ask(nick, "Talk"));

            var data = new DialogSaveData();
            data.rules.Add(new RuleStateSave { set = "SwampRegion", rule = "swampOnce", disabled = true, timesFired = 1, secondsSinceFired = 3 });
            var pending = engine.LoadState(data);

            Assert.IsFalse(nick.Memory.TryGet("stale", clock.Now, out _), "memory not in the save is cleared");
            Assert.IsFalse(once.State.Disabled, "rule state not in the save is reset");
            Assert.AreEqual(1, pending.Count);

            var swampRule = new RuleBuilder("swampOnce").Concept("Talk").Once().Build();
            var swamp = new RuleSet("SwampRegion", new[] { swampRule });
            Assert.AreEqual(1, DialogSaveExtensions.ApplyRuleStates(swamp, pending, clock.Now));
            Assert.IsTrue(swampRule.State.Disabled);
            Assert.IsEmpty(pending);
        }

        [Test]
        public void UnplayedRulesAreNotSaved()
        {
            Assert.IsEmpty(engine.SaveState().rules);
        }
    }

    public class LineKeyTests
    {
        [Test]
        public void KeyPrecedence_ExplicitThenAudioThenRuleIndex()
        {
            Assert.AreEqual("NICK_HI", LineKeys.For("Greet", 0, " NICK_HI ", "vo/nick_hi_01.wav"));
            Assert.AreEqual("nick_hi_01", LineKeys.For("Greet", 0, null, "vo\\nick_hi_01.wav"));
            Assert.AreEqual("Greet_2", LineKeys.For("Greet", 1, "", null));
        }

        [Test]
        public void KeysParseFromBothFormats_AndRoundTrip()
        {
            var dlg = DialogScriptParser.Parse("rule A\n concept X\n say \"Hi\" key GREET_1\n say \"Yo\" audio yo_01\n say \"Hey\"");
            Assert.IsFalse(dlg.HasErrors, string.Join("\n", dlg.Messages));
            Assert.AreEqual("GREET_1", dlg.Rules[0].Lines[0].Key);
            var again = DialogScriptParser.Parse(DialogScriptWriter.ToDlg(dlg.Rules));
            Assert.AreEqual("GREET_1", again.Rules[0].Lines[0].Key);

            var csv = SpreadsheetParser.Parse("Rule,Concept,Text,Key\nA,X,Hi,GREET_1\n");
            Assert.AreEqual("GREET_1", csv.Rules[0].Lines[0].Key);
        }

        [Test]
        public void StringTableExport_HasOneRowPerKey()
        {
            var src = DialogScriptParser.Parse(
                "rule A\n who Nick\n concept X\n say \"Hi, there\" key GREET_1\n say \"Yo\" audio yo_01\n say \"Hey\"\n" +
                "rule B\n concept Y\n say \"Same key\" key GREET_1\n say audio only_audio\n");
            var rows = SpreadsheetParser.ReadRows(DialogScriptWriter.ToStringTableCsv(src.Rules));
            CollectionAssert.AreEqual(new[] { "Key", "Source", "Character", "Rule", "Concept" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "GREET_1", "yo_01", "A_3" }, rows.Skip(1).Select(r => r[0]));
            Assert.AreEqual("Hi, there", rows[1][1]);
            Assert.AreEqual("Nick", rows[1][2]);
        }
    }
}
