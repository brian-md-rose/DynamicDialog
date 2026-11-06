using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DynamicDialog.Editor.Tests
{
    /// <summary>Imports real files into a temporary folder under Assets.</summary>
    public class ImporterTests
    {
        const string Folder = "Assets/DynamicDialogTestTemp";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "DynamicDialogTestTemp");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        static DialogRuleDatabase ImportFile(string fileName, string content)
        {
            string path = Folder + "/" + fileName;
            File.WriteAllText(path, content);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<DialogRuleDatabase>(path);
        }

        /// <summary>Writes a tiny silent 16-bit mono WAV so the audio lookup has something real to find.</summary>
        static void WriteWav(string path, int samples = 800, int rate = 8000)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int dataBytes = samples * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                for (int i = 0; i < samples; i++) w.Write((short)0);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void DlgFile_ImportsAsRuleDatabase_WithAudio()
        {
            Directory.CreateDirectory(Folder + "/vo");
            AssetDatabase.Refresh();
            WriteWav(Folder + "/vo/nick_swamp_01.wav");

            var db = ImportFile("swamp.dlg", @"
@partition concept who
rule NickSwamp
  who Nick
  concept TalkIdle
  if map == swamp
  select shuffle
  say ""Man, this swamp."" audio nick_swamp_01
  say ""By path."" audio vo/nick_swamp_01.wav
  remember said = 1
  then Rochelle Reply
");
            Assert.IsNotNull(db, "the .dlg imported as a DialogRuleDatabase");
            Assert.AreEqual(Folder + "/swamp.dlg", db.SourcePath);
            CollectionAssert.AreEqual(new[] { "concept", "who" }, db.partitionKeys);

            var rule = db.rules.Single();
            Assert.AreEqual(("NickSwamp", "Nick", "TalkIdle", 3), (rule.name, rule.who, rule.concept, rule.sourceLine));
            Assert.AreEqual(ResponseSelection.Shuffle, rule.response.selection);
            Assert.IsNotNull(rule.response.lines[0].clip, "found by name");
            Assert.IsNotNull(rule.response.lines[1].clip, "found by path");
            Assert.AreEqual("nick_swamp_01", rule.response.lines[0].clip.name);
            Assert.AreEqual(3, db.RuleSet.Rules[0].Score);
        }

        [Test]
        public void MissingAudio_IsAWarning_AndTheLineKeepsItsText()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Audio 'nope_01' not found"));
            var db = ImportFile("missing.dlg", "rule A\n  concept X\n  say \"Text only\" audio nope_01\n");
            Assert.AreEqual("Text only", db.rules[0].response.lines[0].text);
            Assert.IsNull(db.rules[0].response.lines[0].clip);
        }

        [Test]
        public void ParseErrors_AreReportedWithFileAndLine()
        {
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(Folder + "/broken.dlg") + @"\(3\).*Unknown statement 'dance'"));
            var db = ImportFile("broken.dlg", "rule A\n  concept X\n  dance\n  say \"ok\"\n");
            Assert.IsNotNull(db, "the rest of the file still imports");
            Assert.AreEqual(1, db.rules[0].response.lines.Count);
        }

        [Test]
        public void Spreadsheet_Imports()
        {
            var db = ImportFile("lines.dlgcsv",
                "Rule,Who,Concept,Map,Text\n" +
                "NickSwamp,Nick,TalkIdle,swamp,Man this swamp.\n" +
                ",,,,Cesspool.\n");
            Assert.IsNotNull(db);
            var rule = db.rules.Single();
            Assert.AreEqual(2, rule.response.lines.Count);
            Assert.AreEqual("Map", rule.criteria.Single().key);

            var tsv = ImportFile("lines.dlgtsv", "Rule\tConcept\tText\nA\tX\tHello, with a comma\n");
            Assert.AreEqual("Hello, with a comma", tsv.rules[0].response.lines[0].text);
        }

        [Test]
        public void Reimport_PicksUpEdits()
        {
            var db = ImportFile("edit.dlg", "rule Old\n  concept X\n  say \"one\"\n");
            Assert.AreEqual("Old", db.rules[0].name);
            var again = ImportFile("edit.dlg", "rule New\n  concept X\n  say \"two\"\n");
            Assert.AreEqual("New", again.rules[0].name);
            Assert.AreEqual("New", again.RuleSet.Rules[0].Name, "the compiled rules are fresh, not cached");
        }

        [Test]
        public void ExportedDlg_ReimportsToTheSameRules()
        {
            var original = ImportFile("orig.dlg",
                "rule A\n  who Nick\n  concept X\n  if hp in 1..5 default 3\n  say \"hi\" duration 2s\n  remember world n += 1 for 3s\n  then any Y after 1s\n  once\n" +
                "rule B\n  concept Y\n  say \"yo\"\n");
            string text = DialogScriptWriter.ToDlg(original.ToSources(), original.partitionKeys);
            var copy = ImportFile("copy.dlg", text);
            Assert.AreEqual(DialogScriptWriter.ToDlg(original.ToSources()), DialogScriptWriter.ToDlg(copy.ToSources()));
        }

        [Test]
        public void InspectorMadeDatabase_ExportsToDlg()
        {
            var db = ScriptableObject.CreateInstance<DialogRuleDatabase>();
            var rule = new RuleDefinition { name = "Made in inspector", who = "Nick", concept = "Talk" };
            rule.criteria.Add(new CriterionDefinition { key = "health", op = CriterionOp.Less, value = "30" });
            rule.response.lines.Add(new DialogLineDefinition { text = "Ow." });
            db.rules.Add(rule);

            var parsed = DialogScriptParser.Parse(DialogScriptWriter.ToDlg(db.ToSources()));
            Assert.IsFalse(parsed.HasErrors, string.Join("\n", parsed.Messages));
            Assert.AreEqual("Made in inspector", parsed.Rules[0].Name);
            Assert.AreEqual(CriterionOp.Less, parsed.Rules[0].Criteria[0].Op);
            Object.DestroyImmediate(db);
        }

        [Test]
        public void Templates_ImportCleanly_AndPassTheValidator()
        {
            foreach (var (file, content) in new[] { ("t.dlg", DialogCreateMenu.ScriptTemplate), ("t.dlgcsv", DialogCreateMenu.SpreadsheetTemplate) })
            {
                var db = ImportFile(file, content);
                Assert.IsNotNull(db, file);
                Assert.AreEqual(3, db.rules.Count, file);
                var problems = DialogRuleDatabaseEditor.Check(db).Where(m => m.Severity != MessageSeverity.Info).ToList();
                Assert.IsEmpty(problems, file + ":\n" + string.Join("\n", problems));
            }
        }

        [Test]
        public void ValidatorWindow_FindsImportedDatabases()
        {
            ImportFile("a.dlg", "rule A\n  concept X\n  say \"hi\"\n  then Ellis Nobody\n");
            var window = ScriptableObject.CreateInstance<DialogValidatorWindow>();
            window.Run();
            var rows = (System.Collections.IList)typeof(DialogValidatorWindow)
                .GetField("rows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(window);
            var texts = rows.Cast<object>().Select(r => r.GetType().GetField("Message").GetValue(r).ToString()).ToList();
            Assert.That(texts, Has.Some.Contains("'Nobody', but no rule answers it").And.Some.Contains("a.dlg"));
            Object.DestroyImmediate(window);
        }

        [UnityTest]
        public IEnumerator SavingDuringPlayMode_HotReloadsRules()
        {
            // Locals set before EnterPlayMode don't survive its domain reload, so everything starts after it.
            yield return new EnterPlayMode();
            const string path = Folder + "/hot.dlg";

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "DynamicDialogTestTemp");
            var db = ImportFile("hot.dlg", "rule Before\n  who Nick\n  concept Talk\n  say \"one\" duration 0.01\n");
            Assert.IsTrue(File.Exists(path), "file written");
            var director = new GameObject("Director").AddComponent<DialogDirector>();
            director.AddDatabase(db);
            var speaker = new GameObject("Nick").AddComponent<DialogSpeaker>();
            yield return null;

            Assert.IsNotNull(db, "database loads after entering Play Mode");
            Assert.AreEqual(1, db.rules.Count, "rules survive entering Play Mode");
            Assert.IsTrue(director.IsLoaded(db));
            Assert.AreSame(director, DialogDirector.Instance, "the speaker sees this director");
            Assert.IsNotNull(speaker.Character, "speaker registered");
            var trace = new MatchTrace();
            director.Engine.Trace = trace;
            var first = speaker.Speak("Talk");
            Assert.IsNotNull(first, $"no match. Query: {trace.Query}. Rules tested: {string.Join("; ", trace.Entries)}. " +
                                    $"Sets: {string.Join(",", director.Engine.Matcher.Sets.Select(s => s.Name + ":" + s.Rules.Count))}");
            director.Engine.Trace = null;
            Assert.AreEqual("Before", first.Rule.Name);
            // Let the line end. Edit mode tests may only yield null on older test frameworks (1.1.x), so no WaitForSeconds.
            float lineEnds = Time.time + 0.1f;
            for (int frame = 0; Time.time < lineEnds && frame < 1000; frame++) yield return null;

            File.WriteAllText(path, "rule After\n  who Nick\n  concept Talk\n  say \"two\"\n");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            yield return null;

            Assert.AreEqual("After", speaker.Speak("Talk")?.Rule.Name, "the director picked up the saved file");
            Assert.IsTrue(director.IsLoaded(db), "callers holding the old asset still find it loaded");

            Object.Destroy(director.gameObject);
            Object.Destroy(speaker.gameObject);
            yield return new ExitPlayMode();
        }
    }
}
