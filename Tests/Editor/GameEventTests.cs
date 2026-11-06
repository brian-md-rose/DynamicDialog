using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    /// <summary>"do" statements: parsing, writing, checking, and dispatch by the conductor.</summary>
    public class GameEventTests
    {
        // ---- Parsing and writing ----------------------------------------------------------------

        static RuleSource ParseOne(string body)
        {
            var src = DialogScriptParser.Parse("rule R\n  concept C\n" + body);
            Assert.IsFalse(src.HasErrors, string.Join("\n", src.Messages));
            return src.Rules.Single();
        }

        [Test]
        public void Do_ParsesNameAndArguments()
        {
            var r = ParseOne("  do GiveItem glasses\n  do Shout \"hello there\" 3\n  do Ping\n");
            Assert.AreEqual(3, r.Events.Count);
            Assert.AreEqual("GiveItem", r.Events[0].Name);
            CollectionAssert.AreEqual(new[] { "glasses" }, r.Events[0].Args);
            Assert.AreEqual(DialogEventTiming.LineStarted, r.Events[0].Timing);
            CollectionAssert.AreEqual(new[] { "hello there", "3" }, r.Events[1].Args);
            Assert.AreEqual(0, r.Events[2].Args.Count);
        }

        [Test]
        public void Do_AtEndAndAtStart()
        {
            var r = ParseOne("  do StartWalking at end\n  do Wave at start\n  do Say \"at\" end\n  do Point AT END # caps\n");
            Assert.AreEqual(DialogEventTiming.LineEnded, r.Events[0].Timing);
            Assert.AreEqual(0, r.Events[0].Args.Count);
            Assert.AreEqual(DialogEventTiming.LineStarted, r.Events[1].Timing);
            Assert.AreEqual(0, r.Events[1].Args.Count);
            // A quoted "at" is an argument, not the timing.
            Assert.AreEqual(DialogEventTiming.LineStarted, r.Events[2].Timing);
            CollectionAssert.AreEqual(new[] { "at", "end" }, r.Events[2].Args);
            Assert.AreEqual(DialogEventTiming.LineEnded, r.Events[3].Timing);
        }

        [Test]
        public void Do_ReportsMistakes()
        {
            foreach (var bad in new[] { "  do\n", "  do GiveItem a, b\n", "  do GiveItem at noon\n", "  do == x\n" })
            {
                var src = DialogScriptParser.Parse("rule R\n  concept C\n" + bad);
                Assert.IsTrue(src.HasErrors, $"'{bad.Trim()}' should be an error");
                Assert.AreEqual(1, src.Rules.Count, "the rest of the rule still loads");
            }
        }

        [Test]
        public void Do_WriterRoundTrips()
        {
            const string text = "rule R\n  concept C\n  say \"Hi\"\n  remember x = 1\n  do GiveItem glasses\n  do Odd \"two words\" \"at\" end \"\" \"a#b\"\n  do Leave at end\n  then Player Reply\n";
            var first = DialogScriptParser.Parse(text);
            Assert.IsFalse(first.HasErrors, string.Join("\n", first.Messages));
            string written = DialogScriptWriter.ToDlg(first.Rules);
            StringAssert.Contains("do GiveItem glasses", written);
            StringAssert.Contains("do Leave at end", written);
            var second = DialogScriptParser.Parse(written);
            Assert.IsFalse(second.HasErrors, written);
            var a = first.Rules[0].Events;
            var b = second.Rules[0].Events;
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Name, b[i].Name);
                Assert.AreEqual(a[i].Timing, b[i].Timing);
                CollectionAssert.AreEqual(a[i].Args, b[i].Args, written);
            }
            Assert.AreEqual(written, DialogScriptWriter.ToDlg(second.Rules), "writing is stable");
        }

        [Test]
        public void FilesWithoutDo_ParseAsBefore()
        {
            var src = DialogScriptParser.Parse("rule R\n  concept C\n  say \"Hi\"\n  then Player Reply\n");
            Assert.IsFalse(src.HasErrors);
            Assert.AreEqual(0, src.Rules[0].Events.Count);
            Assert.AreEqual(0, src.Rules[0].ToRule().Events.Length);
            StringAssert.DoesNotContain("do ", DialogScriptWriter.ToDlg(src.Rules));
        }

        [Test]
        public void Do_CompilesIntoTheRule()
        {
            var rule = ParseOne("  do GiveItem glasses 2\n  do Leave at end\n").ToRule();
            Assert.AreEqual(2, rule.Events.Length);
            Assert.IsTrue(rule.Events[0].Is("giveitem"), "names match ignoring case");
            Assert.AreEqual("glasses", rule.Events[0].Arg(0));
            Assert.AreEqual(2, rule.Events[0].IntArg(1));
            Assert.AreEqual(2f, rule.Events[0].FloatArg(1));
            Assert.AreEqual("none", rule.Events[0].Arg(5, "none"));
            Assert.AreEqual(-1, rule.Events[0].IntArg(0, -1), "not a number");
            Assert.AreEqual(DialogEventTiming.LineEnded, rule.Events[1].Timing);
            Assert.AreEqual("do GiveItem glasses 2", rule.Events[0].ToString());
            Assert.AreEqual("do Leave at end", rule.Events[1].ToString());
        }

        [Test]
        public void Spreadsheet_DoColumn()
        {
            var src = SpreadsheetParser.Parse("Rule,Concept,Text,Do\nR,C,Hi,GiveItem glasses; do Leave at end\n,,Hello,\n");
            Assert.IsFalse(src.HasErrors, string.Join("\n", src.Messages));
            var r = src.Rules.Single();
            Assert.AreEqual(2, r.Events.Count);
            Assert.AreEqual("GiveItem", r.Events[0].Name);
            CollectionAssert.AreEqual(new[] { "glasses" }, r.Events[0].Args);
            Assert.AreEqual("Leave", r.Events[1].Name);
            Assert.AreEqual(DialogEventTiming.LineEnded, r.Events[1].Timing);
            Assert.AreEqual(0, r.Criteria.Count, "'Do' is no longer a criterion column");
        }

        [Test]
        public void Analyzer_EventOnlyRuleDoesSomething_AndTyposAreFlagged()
        {
            var src = DialogScriptParser.Parse(
                "rule A\n  concept C\n  do GiveItem x\n" +
                "rule B\n  concept D\n  do GiveItem y\n" +
                "rule E\n  concept F\n  do GiveItme z\n");
            var messages = RuleAnalyzer.Analyze(new[] { new RuleSourceGroup("test", src.Rules) });
            Assert.IsFalse(messages.Any(m => m.Rule == "A" && m.Text.StartsWith("Does nothing")), string.Join("\n", messages));
            Assert.IsTrue(messages.Any(m => m.Rule == "E" && m.Severity == MessageSeverity.Warning && m.Text.Contains("'GiveItem'")),
                string.Join("\n", messages));
            Assert.IsFalse(messages.Any(m => m.Rule == "A" && m.Text.Contains("Did you mean")));
        }

        [Test]
        public void DialogEvent_RejectsEmptyName()
        {
            Assert.Throws<ArgumentException>(() => new DialogEvent(" "));
            Assert.AreEqual(0, new DialogEvent("X").ArgCount);
            Assert.AreEqual(0, default(DialogEvent).Args.Count);
        }

        // ---- Dispatch -----------------------------------------------------------------------------

        sealed class Speaker : IDialogSpeaker
        {
            readonly DialogConductor conductor;
            readonly List<string> log;
            public DialogLine Current;

            public Speaker(DialogConductor conductor, string who, List<string> log)
            {
                this.conductor = conductor;
                this.log = log;
                Character = conductor.Engine.GetOrCreateCharacter(who);
                conductor.Register(this);
            }

            public DialogCharacter Character { get; }
            public bool IsAvailable => Current == null;
            public bool CanHear(IDialogSpeaker source) => true;

            public void BeginLine(DialogLine line)
            {
                Current = line;
                log.Add("begin " + line.Rule.Name);
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
                Current = null;
                conductor.EndLine(l, completed: true);
            }
        }

        ManualClock clock;
        DialogEngine engine;
        DialogConductor conductor;
        List<string> log;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock();
            engine = new DialogEngine(clock, new SystemRandomSource(1));
            conductor = new DialogConductor(engine);
            log = new List<string>();
            conductor.LineStarted += l => log.Add("started " + l.Rule.Name);
            conductor.LineEnded += (l, done) => log.Add((done ? "ended " : "stopped ") + l.Rule.Name);
            conductor.EventRaised += (l, e) => log.Add(e.ToString());
        }

        void AddRules(params Rule[] rules) => engine.Matcher.Add(new RuleSet("test", rules));

        [Test]
        public void StartEventsFireAfterFactsAndBeforeTheLineBegins_EndEventsWhenItEnds()
        {
            AddRules(new RuleBuilder("Give").Who("Mei").Concept("OnTalk")
                .RememberWorld("glasses_returned", 1)
                .Do("GiveItem", "glasses").DoAtEnd("StartWalking").Do("Smile").Build());
            var mei = new Speaker(conductor, "Mei", log);

            bool factSeenByHandler = false;
            conductor.EventRaised += (l, e) =>
            {
                if (e.Is("GiveItem")) factSeenByHandler = engine.World.TryGet("glasses_returned", clock.Now, out _);
            };

            conductor.TrySpeak(mei, "OnTalk");
            CollectionAssert.AreEqual(new[] { "started Give", "do GiveItem glasses", "do Smile", "begin Give" }, log);
            Assert.IsTrue(factSeenByHandler, "the rule's facts are written before its events are raised");

            log.Clear();
            mei.Complete();
            CollectionAssert.AreEqual(new[] { "do StartWalking at end", "ended Give" }, log);
        }

        [Test]
        public void EndEventsAreDroppedWhenTheLineIsInterrupted()
        {
            AddRules(
                new RuleBuilder("Talk").Who("Jun").Concept("OnTalk").Do("Wave").DoAtEnd("Leave").Build(),
                new RuleBuilder("Hurt").Who("Jun").Concept("OnHurt").Build());
            var jun = new Speaker(conductor, "Jun", log);

            conductor.TrySpeak(jun, "OnTalk");
            conductor.TrySpeak(jun, "OnHurt", interrupt: true);
            Assert.That(log, Has.Member("do Wave"));
            Assert.That(log, Has.No.Member("do Leave at end"));
            Assert.That(log, Has.Member("stopped Talk"));
        }

        [Test]
        public void AHandlerCanReplaceTheLineBeforeItBegins()
        {
            AddRules(
                new RuleBuilder("First").Who("Otto").Concept("A").Do("Swap").Build(),
                new RuleBuilder("Second").Who("Otto").Concept("B").Build());
            var otto = new Speaker(conductor, "Otto", log);
            conductor.EventRaised += (l, e) =>
            {
                if (e.Is("Swap")) conductor.TrySpeak(otto, "B", interrupt: true);
            };

            conductor.TrySpeak(otto, "A");
            Assert.AreEqual("Second", otto.Current.Rule.Name);
            Assert.That(log, Has.No.Member("begin First"), "the replaced line never begins");
        }

        [Test]
        public void RulesWithoutEventsRaiseNothing()
        {
            AddRules(new RuleBuilder("Plain").Who("Rosa").Concept("OnTalk").Build());
            var rosa = new Speaker(conductor, "Rosa", log);
            conductor.TrySpeak(rosa, "OnTalk");
            rosa.Complete();
            Assert.IsFalse(log.Any(s => s.StartsWith("do ")));
        }
    }
}
