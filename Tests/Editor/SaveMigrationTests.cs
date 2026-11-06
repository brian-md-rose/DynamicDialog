using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    /// <summary>Save versions, upgrading old saves, and saved rule state for renamed and removed rules.</summary>
    public class SaveMigrationTests
    {
        ManualClock clock;
        DialogEngine engine;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock { Now = 50 };
            engine = new DialogEngine(clock, new SystemRandomSource(1));
        }

        /// <summary>What a 0.6 game saved: version 1, rule states keyed by the names rules had then.</summary>
        static DialogSaveData Save06()
        {
            var data = new DialogSaveData { version = 1 };
            data.world.Add(new FactMemoryEntry { Key = "quest_glasses", Value = 1 });
            data.characters.Add(new CharacterMemorySave
            {
                who = "Nurse",
                facts = new List<FactMemoryEntry> { new FactMemoryEntry { Key = "mood", SymbolValue = "cheerful" } },
            });
            data.rules.Add(new RuleStateSave { set = "Clinic", rule = "NurseThanks", disabled = true, timesFired = 1, secondsSinceFired = 12 });
            data.rules.Add(new RuleStateSave { set = "Clinic", rule = "NurseIdle", disabled = false, timesFired = 3, secondsSinceFired = 4 });
            data.rules.Add(new RuleStateSave { set = "Clinic", rule = "NurseCutScene", disabled = true, timesFired = 1, secondsSinceFired = 30 });
            data.rules.Add(new RuleStateSave { set = "Garden", rule = "PigeonsOnce", disabled = true, timesFired = 1, secondsSinceFired = 2 });
            data.rules.Add(new RuleStateSave { set = "Garden", rule = "OldGardenRule", disabled = true, timesFired = 1, secondsSinceFired = 2 });
            return data;
        }

        [Test]
        public void NewSaves_HaveTheCurrentVersion()
        {
            Assert.AreEqual(DialogSaveData.CurrentVersion, new DialogSaveData().version);
            Assert.AreEqual(DialogSaveData.CurrentVersion, engine.SaveState().version);
            Assert.AreEqual(2, DialogSaveData.CurrentVersion);
        }

        [Test]
        public void Version06Save_LoadsAndIsUpgraded()
        {
            var once = new RuleBuilder("NurseThanks").Concept("Talk").Who("Nurse").Once().Build();
            var idle = new RuleBuilder("NurseIdle").Concept("Idle").Who("Nurse").Cooldown(10).Build();
            engine.Matcher.Add(new RuleSet("Clinic", new[] { once, idle }));
            engine.Matcher.Add(new RuleSet("Unrelated", new Rule[0]));

            var data = Save06();
            data.rules.RemoveAll(r => r.rule == "NurseCutScene"); // just the rules that still exist here
            var pending = engine.LoadState(data, out var report);

            Assert.AreEqual(1, report.SavedVersion);
            Assert.IsTrue(report.Upgraded);
            Assert.IsFalse(report.FromNewerVersion);
            Assert.AreEqual(DialogSaveData.CurrentVersion, data.version, "upgraded in place");
            Assert.IsFalse(report.HasWarnings);

            Assert.IsTrue(once.State.Disabled);
            Assert.AreEqual(3, idle.State.TimesFired);
            Assert.IsFalse(idle.IsAvailable(clock.Now), "6s of cooldown left");
            Assert.IsTrue(idle.IsAvailable(clock.Now + 6.01));

            var nurse = engine.GetOrCreateCharacter("Nurse");
            Assert.IsTrue(nurse.Memory.TryGet("mood", clock.Now, out float mood));
            Assert.AreEqual(Symbol.Of("cheerful").AsValue, mood);
            var q = new Query();
            engine.World.WriteTo(q, clock.Now);
            Assert.IsTrue(q.TryGet(Symbol.Of("quest_glasses"), out float quest));
            Assert.AreEqual(1f, quest);

            Assert.AreEqual(2, pending.Count, "the Garden set isn't loaded: its states wait");
            Assert.AreEqual(2, report.WaitingRules.Count);
            Assert.IsTrue(pending.All(p => p.set == "Garden"));
        }

        [Test]
        public void SaveWithoutVersion_OrHalfFilled_LoadsWithoutThrowing()
        {
            var rule = new RuleBuilder("intro").Concept("Talk").Once().Build();
            engine.Matcher.Add(new RuleSet("Global", new[] { rule }));
            var data = new DialogSaveData { version = 0, world = null, characters = null, rules = null };
            Assert.DoesNotThrow(() => engine.LoadState(data, out _));

            data = new DialogSaveData { version = 0 };
            data.characters.Add(null);
            data.characters.Add(new CharacterMemorySave { who = "Nick", facts = null });
            data.rules.Add(null);
            data.rules.Add(new RuleStateSave { set = null, rule = "" });
            data.rules.Add(new RuleStateSave { set = "Global", rule = "intro", disabled = true, timesFired = -4, secondsSinceFired = float.NaN });
            var pending = engine.LoadState(data, out var report);

            Assert.AreEqual(0, report.SavedVersion);
            Assert.IsTrue(report.Upgraded);
            Assert.IsEmpty(pending);
            Assert.IsTrue(rule.State.Disabled);
            Assert.AreEqual(0, rule.State.TimesFired, "a negative count is corrected");
            Assert.IsTrue(double.IsNegativeInfinity(rule.State.LastFiredTime), "an invalid time counts as never played");
        }

        [Test]
        public void SaveFromNewerVersion_LoadsWhatItCan_AndWarns()
        {
            var rule = new RuleBuilder("intro").Concept("Talk").Once().Build();
            engine.Matcher.Add(new RuleSet("Global", new[] { rule }));
            var data = new DialogSaveData { version = 99 };
            data.rules.Add(new RuleStateSave { set = "Global", rule = "intro", disabled = true, timesFired = 1, secondsSinceFired = 1 });

            Assert.DoesNotThrow(() => engine.LoadState(data, out var report));
            engine.LoadState(data, out var r);
            Assert.IsTrue(r.FromNewerVersion);
            Assert.IsFalse(r.Upgraded);
            Assert.IsTrue(r.HasWarnings);
            Assert.AreEqual(99, data.version, "a newer version isn't downgraded");
            Assert.IsTrue(rule.State.Disabled);
            StringAssert.Contains("newer", r.ToString());
        }

        [Test]
        public void RenamedRule_GetsItsSavedState_ThroughItsFormerName()
        {
            var thanks = new RuleBuilder("NurseThanksForGlasses").Concept("Talk").Who("Nurse").Once().Renamed("NurseThanks").Build();
            var idle = new RuleBuilder("NurseIdleChat").Concept("Idle").Who("Nurse").Cooldown(10).Renamed("NurseIdleOld", "nurseidle").Build();
            var set = new RuleSet("Clinic", new[] { thanks, idle });
            engine.Matcher.Add(set);

            var data = Save06();
            data.rules.RemoveAll(r => r.rule == "NurseCutScene");
            engine.LoadState(data, out var report);

            Assert.IsTrue(thanks.State.Disabled, "the once rule stays used after its rename");
            Assert.AreEqual(3, idle.State.TimesFired, "former names ignore case");
            Assert.IsFalse(idle.IsAvailable(clock.Now));
            Assert.AreEqual(2, report.RenamedRules.Count);
            var rename = report.RenamedRules.Single(r => r.FormerName == "NurseThanks");
            Assert.AreEqual("Clinic", rename.Set);
            Assert.AreEqual("NurseThanksForGlasses", rename.Name);
            Assert.IsEmpty(report.UnmatchedRules);
            Assert.IsFalse(report.HasWarnings, "a handled rename isn't a problem");

            var resaved = engine.SaveState();
            Assert.IsTrue(resaved.rules.Any(r => r.rule == "NurseThanksForGlasses" && r.disabled), "saved again under the new name");
            Assert.IsFalse(resaved.rules.Any(r => r.rule == "NurseThanks"));
            Assert.AreSame(thanks, set.FindRule("nursethanks", includeFormerNames: true));
            Assert.IsNull(set.FindRule("NurseThanks"));
        }

        [Test]
        public void RemovedRule_IsReported_AndKept_NotThrown()
        {
            var thanks = new RuleBuilder("NurseThanks").Concept("Talk").Who("Nurse").Once().Build();
            engine.Matcher.Add(new RuleSet("Clinic", new[] { thanks })); // NurseIdle and NurseCutScene were removed

            List<RuleStateSave> pending = null;
            DialogLoadReport report = null;
            Assert.DoesNotThrow(() => pending = engine.LoadState(Save06(), out report));

            Assert.IsTrue(thanks.State.Disabled);
            CollectionAssert.AreEquivalent(new[] { "NurseIdle", "NurseCutScene" }, report.UnmatchedRules.Select(r => r.rule));
            Assert.IsTrue(report.HasWarnings);
            StringAssert.Contains("Clinic/NurseCutScene", report.ToString());
            StringAssert.Contains("renamed", report.ToString(), "the message says how to fix a rename");
            Assert.AreEqual(2, report.WaitingRules.Count, "Garden's two states, not the unmatched ones");

            // Unmatched states come back in the pending list so the caller can keep them in later saves.
            CollectionAssert.AreEquivalent(new[] { "NurseIdle", "NurseCutScene", "PigeonsOnce", "OldGardenRule" }, pending.Select(r => r.rule));
        }

        [Test]
        public void StateKeptForARemovedRule_AppliesAgainWhenTheRuleIsRenamedBack()
        {
            var save = Save06();
            engine.Matcher.Add(new RuleSet("Clinic", new[] { new RuleBuilder("Other").Concept("Talk").Build() }));
            var kept = engine.LoadState(save, out _);

            // A later build gives the rule a 'renamed' line; the kept state finds it.
            var thanks = new RuleBuilder("NurseThanks2").Concept("Talk").Once().Renamed("NurseThanks").Build();
            var later = new RuleSet("Clinic", new[] { thanks });
            var report = new DialogLoadReport();
            Assert.AreEqual(1, DialogSaveExtensions.ApplyRuleStates(later, kept, clock.Now, report));
            Assert.IsTrue(thanks.State.Disabled);
            Assert.AreEqual(1, report.RenamedRules.Count);
        }

        [Test]
        public void CurrentName_WinsOverFormerName_AndBothAreCombined()
        {
            // Old name in an earlier save, new name played since: both states reach one rule.
            var rule = new RuleBuilder("Greet").Concept("Talk").Cooldown(60).Renamed("Hello").Build();
            var other = new RuleBuilder("Hello").Concept("Bye").Build(); // another rule still has the old name
            var set = new RuleSet("Global", new[] { rule });
            var states = new List<RuleStateSave>
            {
                new RuleStateSave { set = "Global", rule = "Hello", disabled = true, timesFired = 2, secondsSinceFired = 100 },
                new RuleStateSave { set = "Global", rule = "Greet", disabled = false, timesFired = 1, secondsSinceFired = 5 },
            };
            Assert.AreEqual(2, DialogSaveExtensions.ApplyRuleStates(set, states, 1000));
            Assert.IsTrue(rule.State.Disabled, "used if either was used");
            Assert.AreEqual(3, rule.State.TimesFired, "plays add up");
            Assert.AreEqual(995, rule.State.LastFiredTime, 1e-6, "the most recent play counts for the cooldown");

            // With a rule that still has the old name, that rule gets the state instead.
            var set2 = new RuleSet("Global", new[] { new RuleBuilder("Greet").Concept("Talk").Renamed("Hello").Build(), other });
            states = new List<RuleStateSave> { new RuleStateSave { set = "Global", rule = "Hello", disabled = true, timesFired = 1 } };
            DialogSaveExtensions.ApplyRuleStates(set2, states, 0);
            Assert.IsTrue(other.State.Disabled);
            Assert.IsFalse(set2.FindRule("Greet").State.Disabled);
        }

        [Test]
        public void RegionNotLoaded_KeepsRenamedAndRemovedState_UntilItLoads()
        {
            engine.Matcher.Add(new RuleSet("Clinic", new[]
            {
                new RuleBuilder("NurseThanks").Concept("Talk").Once().Build(),
                new RuleBuilder("NurseIdle").Concept("Idle").Build(),
                new RuleBuilder("NurseCutScene").Concept("Cut").Once().Build(),
            }));
            var pending = engine.LoadState(Save06(), out var report);
            Assert.AreEqual(2, report.WaitingRules.Count);
            Assert.IsEmpty(report.UnmatchedRules, "nothing is unmatched until Garden loads");

            // Saving now still includes the waiting states (the Unity director appends them).
            var resave = engine.SaveState();
            resave.rules.AddRange(pending.Select(p => p.Clone()));
            Assert.AreEqual(5, resave.rules.Count);

            // Garden loads: PigeonsOnce was renamed, OldGardenRule was removed.
            var pigeons = new RuleBuilder("PigeonsScatter").Concept("See").Once().Renamed("PigeonsOnce").Build();
            var garden = new RuleSet("Garden", new[] { pigeons });
            DialogSaveExtensions.ApplyRuleStates(garden, pending, clock.Now, report);

            Assert.IsTrue(pigeons.State.Disabled);
            Assert.AreEqual("PigeonsScatter", report.RenamedRules.Single().Name);
            Assert.AreEqual("OldGardenRule", report.UnmatchedRules.Single().rule);
            Assert.IsEmpty(report.WaitingRules);
            Assert.AreEqual("OldGardenRule", pending.Single().rule, "kept for later saves");

            // Loading the set again doesn't report the same state twice.
            DialogSaveExtensions.ApplyRuleStates(new RuleSet("Garden", new Rule[0]), pending, clock.Now, report);
            Assert.AreEqual(1, report.UnmatchedRules.Count);
        }

        [Test]
        public void LoadState_DoesNotChangeTheCallersRuleStates()
        {
            engine.Matcher.Add(new RuleSet("Clinic", new Rule[0]));
            var data = Save06();
            var pending = engine.LoadState(data);
            pending[0].secondsSinceFired = 999;
            Assert.IsFalse(data.rules.Any(r => r.secondsSinceFired == 999));
        }

        // ---- Authoring: "renamed" ---------------------------------------------------------------

        [Test]
        public void Dlg_RenamedStatement_ParsesWritesAndBuilds()
        {
            var src = DialogScriptParser.Parse("rule NurseThanks2\n  concept Talk\n  renamed NurseThanks \"Nurse Thanks Old\"\n  renamed Older\n  once\n");
            Assert.IsFalse(src.HasErrors, string.Join("\n", src.Messages));
            var r = src.Rules.Single();
            CollectionAssert.AreEqual(new[] { "NurseThanks", "Nurse Thanks Old", "Older" }, r.FormerNames);

            string text = DialogScriptWriter.ToDlg(src.Rules);
            StringAssert.Contains("renamed NurseThanks \"Nurse Thanks Old\" Older", text);
            var again = DialogScriptParser.Parse(text).Rules.Single();
            CollectionAssert.AreEqual(r.FormerNames, again.FormerNames);

            var rule = r.ToRule();
            CollectionAssert.AreEqual(new[] { "NurseThanks", "Nurse Thanks Old", "Older" }, rule.FormerNames);
            Assert.IsTrue(rule.WasNamed("older"));
            Assert.IsFalse(rule.WasNamed("NurseThanks2"));
        }

        [Test]
        public void Dlg_RenamedWithoutAName_IsAnError_AndOldFilesAreUnchanged()
        {
            var src = DialogScriptParser.Parse("rule A\n  concept Talk\n  renamed\n");
            Assert.IsTrue(src.HasErrors);
            Assert.AreEqual(1, src.Rules.Count, "the rest of the rule still loads");

            var plain = DialogScriptParser.Parse("rule A\n  concept Talk\n  once\n");
            Assert.IsEmpty(plain.Rules[0].FormerNames);
            Assert.IsFalse(DialogScriptWriter.ToDlg(plain.Rules).Contains("renamed"), "nothing is written for rules without former names");
            Assert.IsEmpty(plain.Rules[0].ToRule().FormerNames);
        }

        [Test]
        public void Spreadsheet_RenamedColumn()
        {
            var src = SpreadsheetParser.Parse("Rule,Concept,Renamed,Text\nThanks2,Talk,\"Thanks, ThanksOld\",Hi\n,,,Hello again\nPlain,Talk,,Hey\n");
            Assert.IsFalse(src.HasErrors, string.Join("\n", src.Messages));
            CollectionAssert.AreEqual(new[] { "Thanks", "ThanksOld" }, src.Rules[0].FormerNames);
            Assert.AreEqual(2, src.Rules[0].Lines.Count);
            Assert.IsEmpty(src.Rules[1].FormerNames);
            Assert.IsEmpty(src.Rules[0].Criteria, "'Renamed' is not read as a criterion");

            var alt = SpreadsheetParser.Parse("Rule,Concept,Renamed From,Text\nA,Talk,renamed B C,Hi\n");
            CollectionAssert.AreEqual(new[] { "B", "C" }, alt.Rules[0].FormerNames);
        }

        [Test]
        public void Analyzer_FlagsFormerNameClashes()
        {
            var rules = DialogScriptParser.Parse(
                "rule A\n  concept Talk\n  say \"a\"\n  renamed Old\n" +
                "rule B\n  concept Talk2\n  say \"b\"\n  renamed Old\n" +
                "rule C\n  concept Talk3\n  say \"c\"\n  renamed A\n" +
                "rule D\n  concept Talk4\n  say \"d\"\n  renamed D\n").Rules;
            var messages = RuleAnalyzer.Analyze(new[] { new RuleSourceGroup("Clinic", rules) });

            Assert.IsTrue(messages.Any(m => m.Rule == "B" && m.Severity == MessageSeverity.Warning && m.Text.Contains("so does 'A'")),
                string.Join("\n", messages));
            Assert.IsTrue(messages.Any(m => m.Rule == "C" && m.Severity == MessageSeverity.Warning && m.Text.Contains("still called 'A'")));
            Assert.IsTrue(messages.Any(m => m.Rule == "D" && m.Severity == MessageSeverity.Info && m.Text.Contains("its own name")));
            Assert.IsFalse(messages.Any(m => m.Rule == "A" && m.Text.Contains("former name")));

            // Different files are different rule sets, so the same former name there is fine.
            var other = DialogScriptParser.Parse("rule E\n  concept Talk\n  say \"e\"\n  renamed Old\n").Rules;
            var split = RuleAnalyzer.Analyze(new[] { new RuleSourceGroup("Clinic", rules.Take(1)), new RuleSourceGroup("Garden", other) });
            Assert.IsFalse(split.Any(m => m.Text.Contains("former name")));
        }
    }
}
