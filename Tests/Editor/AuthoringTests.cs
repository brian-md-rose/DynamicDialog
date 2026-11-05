using System.Linq;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    public class DialogScriptParserTests
    {
        const string Sample = @"
# The swamp conversation from talk slides 67-70
@partition concept who
@who Nick

rule NickSwampIntro
  concept TalkIdle
  if map == swamp and survivors >= 3   // trailing comment
  if said_intro missing
  if health in 30..60, zombies > 2 default 0
  select shuffle
  say ""Man, this swamp."" audio nick_swamp_01 duration 3s anim Shrug
  say ""It's a \""cesspool\"", is what it is.""
  remember said_intro = 1
  remember speaking = 1 for 4s
  remember world gossip += 2
  remember Ellis heard_it -= 1
  forget world area
  then Rochelle SwampReply after 1.5s
  then any Cheer
  once
  cooldown 500ms
  weight 2
  bonus 1

@who
rule Anyone
  concept Hurt
  say ""Ouch""
";

        [Test]
        public void ParsesEveryStatement()
        {
            var src = DialogScriptParser.Parse(Sample, "swamp.dlg");
            Assert.IsFalse(src.HasErrors, string.Join("\n", src.Messages));
            CollectionAssert.AreEqual(new[] { "concept", "who" }, src.PartitionKeys);
            Assert.AreEqual(2, src.Rules.Count);

            var r = src.Rules[0];
            Assert.AreEqual("NickSwampIntro", r.Name);
            Assert.AreEqual("Nick", r.Who, "@who default applies");
            Assert.AreEqual("TalkIdle", r.Concept);
            Assert.AreEqual(6, r.SourceLine);

            Assert.AreEqual(5, r.Criteria.Count);
            Assert.AreEqual(CriterionOp.Equals, r.Criteria[0].Op);
            Assert.AreEqual("swamp", r.Criteria[0].Value);
            Assert.AreEqual(CriterionOp.GreaterOrEqual, r.Criteria[1].Op);
            Assert.AreEqual(CriterionOp.ZeroOrMissing, r.Criteria[2].Op);
            Assert.AreEqual(CriterionOp.Between, r.Criteria[3].Op);
            Assert.AreEqual(("30", "60"), (r.Criteria[3].Value, r.Criteria[3].Value2));
            Assert.IsTrue(r.Criteria[4].HasDefault);
            Assert.AreEqual(0f, r.Criteria[4].Default);

            Assert.AreEqual(ResponseSelection.Shuffle, r.Selection);
            Assert.AreEqual(2, r.Lines.Count);
            Assert.AreEqual("Man, this swamp.", r.Lines[0].Text);
            Assert.AreEqual("nick_swamp_01", r.Lines[0].Audio);
            Assert.AreEqual(3f, r.Lines[0].Duration);
            Assert.AreEqual("Shrug", r.Lines[0].Anim);
            Assert.AreEqual("It's a \"cesspool\", is what it is.", r.Lines[1].Text);

            Assert.AreEqual(5, r.Writes.Count);
            Assert.AreEqual((FactScope.Speaker, "said_intro", FactOp.Set, "1", 0f), Tuple(r.Writes[0]));
            Assert.AreEqual(4f, r.Writes[1].Duration);
            Assert.AreEqual((FactScope.World, "gossip", FactOp.Add, "2", 0f), Tuple(r.Writes[2]));
            Assert.AreEqual((FactScope.Character, "heard_it", FactOp.Add, "-1", 0f), Tuple(r.Writes[3]));
            Assert.AreEqual("Ellis", r.Writes[3].Character);
            Assert.AreEqual((FactScope.World, "area", FactOp.Remove), (r.Writes[4].Scope, r.Writes[4].Key, r.Writes[4].Op));

            Assert.AreEqual(2, r.Followups.Count);
            Assert.AreEqual(FollowupTarget.Character, r.Followups[0].Target);
            Assert.AreEqual("Rochelle", r.Followups[0].Character);
            Assert.AreEqual(1.5f, r.Followups[0].Delay);
            Assert.AreEqual(FollowupTarget.Any, r.Followups[1].Target);

            Assert.IsTrue(r.Once);
            Assert.AreEqual(0.5f, r.Cooldown, 1e-6);
            Assert.AreEqual(2f, r.Weight);
            Assert.AreEqual(1, r.ScoreBonus);

            Assert.IsNull(src.Rules[1].Who, "'@who' alone clears the default");
        }

        static (FactScope, string, FactOp, string, float) Tuple(FactWriteSource w) => (w.Scope, w.Key, w.Op, w.Value, w.Duration);

        [Test]
        public void ParsedRule_CompilesAndMatches()
        {
            var r = DialogScriptParser.Parse(Sample).Rules[0].ToRule();
            Assert.AreEqual(2 + 5 + 1, r.Score, "who + concept + 5 criteria + bonus 1");

            var q = new Query();
            q.Set("who", "Nick");
            q.Set("concept", "TalkIdle");
            q.Set("map", "swamp");
            q.Set("survivors", 3);
            q.Set("health", 45);
            Assert.IsFalse(r.Matches(q), "zombies is missing, defaults to 0, and 0 > 2 fails");
            q.Set("zombies", 3);
            Assert.IsTrue(r.Matches(q), "said_intro is missing, which 'missing' allows");
        }

        [TestCase("say \"Hi\"", "must be inside a rule")]
        [TestCase("rule A\n  dance", "Unknown statement 'dance'")]
        [TestCase("rule A\n  if health", "needs a comparison")]
        [TestCase("rule A\n  if health ~ 3", "Unknown comparison")]
        [TestCase("rule A\n  if health in 3", "low..high")]
        [TestCase("rule A\n  say Hello there", "double quotes")]
        [TestCase("rule A\n  say \"unclosed", "closing quote")]
        [TestCase("rule A\n  remember x", "needs '= value'")]
        [TestCase("rule A\n  then Ellis", "needs a concept")]
        [TestCase("rule A\n  cooldown soon", "Expected a number")]
        [TestCase("rule A\n  select loudly", "Unknown selection")]
        [TestCase("@speaker Nick", "Unknown directive")]
        [TestCase("rule", "needs a name")]
        public void ReportsErrorsWithLineNumbers(string text, string expected)
        {
            var src = DialogScriptParser.Parse(text, "bad.dlg");
            var error = src.Messages.FirstOrDefault(m => m.Severity == MessageSeverity.Error);
            Assert.IsNotNull(error.Text, "expected an error");
            StringAssert.Contains(expected, error.Text);
            Assert.AreEqual(text.Count(c => c == '\n') + 1, error.Line);
            Assert.AreEqual("bad.dlg", error.File);
        }

        [Test]
        public void KeepsGoingAfterAnError()
        {
            var src = DialogScriptParser.Parse("rule A\n  concept X\n  bogus\n  say \"still here\"\nrule B\n  concept Y");
            Assert.AreEqual(1, src.Messages.Count(m => m.Severity == MessageSeverity.Error));
            Assert.AreEqual(2, src.Rules.Count);
            Assert.AreEqual(1, src.Rules[0].Lines.Count);
        }

        [Test]
        public void WarnsAboutDuplicatesAndMissingConcepts()
        {
            var src = DialogScriptParser.Parse("rule A\n  concept X\nrule a\n");
            Assert.That(src.Messages.Select(m => m.Text), Has.Some.Contains("more than once"));
            Assert.That(src.Messages.Select(m => m.Text), Has.Some.Contains("no concept"));
        }

        [Test]
        public void OperatorsNeedNoSpaces_AndQuotedValuesWork()
        {
            var src = DialogScriptParser.Parse("rule A\n  concept X\n  if health<30;hp!=-5\n  if name == \"Big Al\"\n  remember count+=1");
            Assert.IsFalse(src.HasErrors, string.Join("\n", src.Messages));
            var r = src.Rules[0];
            Assert.AreEqual(("health", CriterionOp.Less, "30"), (r.Criteria[0].Key, r.Criteria[0].Op, r.Criteria[0].Value));
            Assert.AreEqual(("hp", CriterionOp.NotEquals, "-5"), (r.Criteria[1].Key, r.Criteria[1].Op, r.Criteria[1].Value));
            Assert.AreEqual("Big Al", r.Criteria[2].Value);
            Assert.AreEqual((FactOp.Add, "1"), (r.Writes[0].Op, r.Writes[0].Value));
        }

        [Test]
        public void WhoAny_ClearsTheDefaultForOneRule()
        {
            var src = DialogScriptParser.Parse("@who Nick\nrule A\n  concept X\n  who any\nrule B\n  concept X");
            Assert.IsNull(src.Rules[0].Who);
            Assert.AreEqual("Nick", src.Rules[1].Who);
        }
    }

    public class DialogScriptWriterTests
    {
        [Test]
        public void RoundTrips()
        {
            var original = DialogScriptParser.Parse(@"
@partition concept who
rule ""Odd name""
  who ""Big Al""
  concept TalkIdle
  if map == swamp
  if health in 30..60 default 45
  if said missing
  if zombies != 0
  select sequential
  say ""Line with \""quotes\"" and # hash"" audio al_01 duration 2s anim Wave
  say audio al_02
  remember said = 1 for 4s
  remember world count -= 2
  remember Ellis told = yes
  forget area
  then self More after 1s
  then all Cheer
  once
  cooldown 10s
  weight 0.5
  bonus -1
");
            Assert.IsFalse(original.HasErrors, string.Join("\n", original.Messages));
            string text = DialogScriptWriter.ToDlg(original.Rules, original.PartitionKeys);
            var again = DialogScriptParser.Parse(text);
            Assert.IsFalse(again.HasErrors, string.Join("\n", again.Messages) + "\n" + text);
            Assert.AreEqual(text, DialogScriptWriter.ToDlg(again.Rules, again.PartitionKeys), "writing is stable");
            CollectionAssert.AreEqual(original.PartitionKeys, again.PartitionKeys);

            var a = original.Rules[0];
            var b = again.Rules[0];
            Assert.AreEqual((a.Name, a.Who, a.Concept, a.Selection, a.Once, a.Cooldown, a.Weight, a.ScoreBonus),
                            (b.Name, b.Who, b.Concept, b.Selection, b.Once, b.Cooldown, b.Weight, b.ScoreBonus));
            Assert.AreEqual(a.Criteria.Count, b.Criteria.Count);
            Assert.AreEqual(a.Lines[0].Text, b.Lines[0].Text);
            Assert.AreEqual(a.Lines[1].Audio, b.Lines[1].Audio);
            Assert.AreEqual(a.Writes.Count, b.Writes.Count);
            Assert.AreEqual("-2", b.Writes[1].Value);
            Assert.AreEqual(a.Followups.Count, b.Followups.Count);
        }

        [Test]
        public void RecordingScript_GroupsByCharacter()
        {
            var src = DialogScriptParser.Parse("rule B\n who Zoey\n concept X\n say \"z1\" audio z_01\nrule A\n who Bill\n concept X\n say \"b1, with comma\"\n say \"b2\"");
            var rows = SpreadsheetParser.ReadRows(DialogScriptWriter.ToRecordingScript(src.Rules));
            Assert.AreEqual(4, rows.Count);
            CollectionAssert.AreEqual(new[] { "Character", "Rule", "Concept", "Take", "Text", "Audio File" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "Bill", "A", "X", "1", "b1, with comma", "" }, rows[1]);
            Assert.AreEqual("2", rows[2][3]);
            CollectionAssert.AreEqual(new[] { "Zoey", "B", "X", "1", "z1", "z_01" }, rows[3]);
        }
    }

    public class SpreadsheetParserTests
    {
        [Test]
        public void ReadsQuotedCells_BomAndNewlines()
        {
            var rows = SpreadsheetParser.ReadRows("﻿a,b,c\r\n\"x, y\",\"say \"\"hi\"\"\",\"two\nlines\"\n1,,3");
            Assert.AreEqual(3, rows.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "x, y", "say \"hi\"", "two\nlines" }, rows[1]);
            CollectionAssert.AreEqual(new[] { "1", "", "3" }, rows[2]);
        }

        [Test]
        public void DetectsTabs()
        {
            var rows = SpreadsheetParser.ReadRows("rule\twho\nA\tNick, Jr");
            CollectionAssert.AreEqual(new[] { "A", "Nick, Jr" }, rows[1]);
        }

        [Test]
        public void RowsBecomeRulesWithVariations()
        {
            const string csv =
                "Rule,Who,Concept,If,Map,Health,Text,Audio,Remember,Then,Once,Notes,#scratch\n" +
                "NickSwamp,Nick,TalkIdle,survivors >= 3,swamp,30..60,Man this swamp.,nick_01,said = 1; world gossip += 1 for 10s,Rochelle Reply after 1s,x,first take,ignore me\n" +
                ",,,,,,Cesspool.,nick_02,,,,,\n" +
                "NickHurt,Nick,Hurt,,,< 25,Ow!,,,,,,\n" +
                "NickSwamp,Nick,TalkIdle,,,,Third variation,,,,,,\n";
            var src = SpreadsheetParser.Parse(csv, "lines.csv");
            Assert.IsFalse(src.HasErrors, string.Join("\n", src.Messages));
            Assert.AreEqual(2, src.Rules.Count);

            var swamp = src.Rules[0];
            Assert.AreEqual(("Nick", "TalkIdle", true), (swamp.Who, swamp.Concept, swamp.Once));
            Assert.AreEqual(3, swamp.Criteria.Count);
            Assert.AreEqual(("survivors", CriterionOp.GreaterOrEqual), (swamp.Criteria[0].Key, swamp.Criteria[0].Op));
            Assert.AreEqual(("Map", CriterionOp.Equals, "swamp"), (swamp.Criteria[1].Key, swamp.Criteria[1].Op, swamp.Criteria[1].Value));
            Assert.AreEqual(("Health", CriterionOp.Between, "30", "60"), (swamp.Criteria[2].Key, swamp.Criteria[2].Op, swamp.Criteria[2].Value, swamp.Criteria[2].Value2));
            CollectionAssert.AreEqual(new[] { "Man this swamp.", "Cesspool.", "Third variation" }, swamp.Lines.Select(l => l.Text));
            Assert.AreEqual("nick_02", swamp.Lines[1].Audio);
            Assert.AreEqual(2, swamp.Writes.Count);
            Assert.AreEqual(10f, swamp.Writes[1].Duration);
            Assert.AreEqual("Rochelle", swamp.Followups.Single().Character);

            var hurt = src.Rules[1];
            Assert.AreEqual(("Health", CriterionOp.Less, "25"), (hurt.Criteria[0].Key, hurt.Criteria[0].Op, hurt.Criteria[0].Value));

            Assert.That(src.Messages.Select(m => m.Text), Has.Some.Contains("not together"));
        }

        [Test]
        public void CriterionColumns_AcceptAllForms()
        {
            const string csv = "rule,concept,a,b,c,d,e\nR,X,exists,missing,!= nick,>=3,\"\"\"Big Al\"\"\"\n";
            var src = SpreadsheetParser.Parse(csv);
            Assert.IsFalse(src.HasErrors, string.Join("\n", src.Messages));
            var ops = src.Rules[0].Criteria.Select(c => c.Op).ToArray();
            CollectionAssert.AreEqual(new[] { CriterionOp.Exists, CriterionOp.ZeroOrMissing, CriterionOp.NotEquals, CriterionOp.GreaterOrEqual, CriterionOp.Equals }, ops);
            Assert.AreEqual("Big Al", src.Rules[0].Criteria[4].Value);
        }

        [Test]
        public void ReportsBadCellsWithRowNumbers()
        {
            const string csv = "rule,concept,cooldown,then\nA,X,soon,\nB,X,,Ellis\n";
            var src = SpreadsheetParser.Parse(csv, "bad.csv");
            var errors = src.Messages.Where(m => m.Severity == MessageSeverity.Error).ToList();
            Assert.AreEqual(2, errors.Count, string.Join("\n", src.Messages));
            Assert.AreEqual(2, errors[0].Line);
            StringAssert.Contains("cooldown", errors[0].Text);
            Assert.AreEqual(3, errors[1].Line);
        }

        [Test]
        public void WarnsOnConflictingWhoInLaterRows_AndNeedsRuleColumn()
        {
            var src = SpreadsheetParser.Parse("rule,who,concept,text\nA,Nick,X,one\nA,Ellis,X,two\n");
            Assert.That(src.Messages.Where(m => m.Severity == MessageSeverity.Warning).Select(m => m.Text), Has.Some.Contains("first row"));
            Assert.IsTrue(SpreadsheetParser.Parse("who,text\nNick,hi").HasErrors);
            Assert.IsTrue(SpreadsheetParser.Parse("rule,text\n,orphan").HasErrors);
        }
    }

    public class RuleAnalyzerTests
    {
        static System.Collections.Generic.List<AuthoringMessage> Analyze(string dlg, params string[] known) =>
            RuleAnalyzer.Analyze(new[] { new RuleSourceGroup("test.dlg", DialogScriptParser.Parse(dlg).Rules) }, known);

        static string[] Texts(System.Collections.Generic.IEnumerable<AuthoringMessage> m) => m.Select(x => x.ToString()).ToArray();

        [Test]
        public void CleanRules_HaveNoWarnings()
        {
            var m = Analyze(@"
rule A
  who Nick
  concept Talk
  if seen_barrels missing
  say ""hi""
  remember seen_barrels += 1
  then Ellis Reply
rule B
  who Ellis
  concept Reply
  if from == Nick
  say ""yo""
");
            Assert.IsEmpty(m.Where(x => x.Severity != MessageSeverity.Info), string.Join("\n", Texts(m)));
        }

        [Test]
        public void UnansweredFollowups()
        {
            var m = Analyze(@"
rule A
  who Nick
  concept Talk
  say ""hi""
  then Ellis Reply
  then any Nobody
rule B
  who Coach
  concept Reply
  say ""yo""
");
            Assert.That(Texts(m), Has.Some.Contains("'Nobody', but no rule answers it"));
            Assert.That(Texts(m), Has.Some.Contains("to Ellis, but only Coach answer"));
        }

        [Test]
        public void ContradictionsAndTypos()
        {
            var m = Analyze(@"
rule A
  concept Talk
  if health < 10 and health > 50
  say ""never""
rule B
  concept Talk
  if seen_barels == 1
  say ""typo""
rule C
  concept Talk
  if custom_fact == 1
  say ""from code""
  remember seen_barrels = 1
");
            Assert.That(Texts(m), Has.Some.Contains("Can never match").And.Some.Contains("'health'"));
            Assert.That(Texts(m), Has.Some.Contains("Did you mean 'seen_barrels'"));
            var custom = m.Single(x => x.Text.Contains("custom_fact"));
            Assert.AreEqual(MessageSeverity.Info, custom.Severity);
            Assert.IsFalse(Analyze("rule C\n concept T\n if custom_fact == 1\n say \"x\"", "custom_fact").Any(x => x.Text.Contains("custom_fact")));
        }

        [Test]
        public void DuplicatesTwinsAndDoNothingRules()
        {
            var groups = new[]
            {
                new RuleSourceGroup("a.dlg", DialogScriptParser.Parse("rule Same\n concept T\n say \"1\"\nrule Twin\n concept T\n say \"2\"\nrule Empty\n concept T").Rules),
                new RuleSourceGroup("b.dlg", DialogScriptParser.Parse("rule same\n concept U\n say \"3\"").Rules),
            };
            var m = RuleAnalyzer.Analyze(groups);
            Assert.That(Texts(m), Has.Some.Contains("also named 'same'"));
            Assert.That(Texts(m), Has.Some.Contains("same conditions as 'Same'"));
            Assert.That(Texts(m), Has.Some.Contains("Does nothing"));
        }

        [Test]
        public void BuiltInFactsAreKnown()
        {
            var m = Analyze("rule A\n concept OnSee\n if object == barrel and near_Ellis == 1 and rand < 30\n say \"x\"");
            Assert.IsEmpty(m, string.Join("\n", Texts(m)));
        }
    }
}
