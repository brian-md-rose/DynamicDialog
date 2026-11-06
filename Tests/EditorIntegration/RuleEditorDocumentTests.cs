using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DynamicDialog.Editor.Tests
{
    /// <summary>
    /// The rule editor loads a .dlg file into a <see cref="DlgDocument"/> and writes it back out on save.
    /// Saving must never change what the rules mean, whatever the file's layout or comments.
    /// </summary>
    public class RuleEditorDocumentTests
    {
        const string Kitchen = @"# Header comment
// another header line

@partition who concept

# Comment above the first rule
rule Hello
  who Rosa
  concept OnTalk
  if met_rosa missing
  if mood > 2 default 0
  if area == garden
  if hour in 9..17
  select shuffle
  say ""Hi there."" anim Wave
  say ""Hello # not a comment."" audio rosa_hello duration 2.5 key rosa.hello
  remember world met_rosa = 1
  remember mood += 1 for 30s
  remember Jun greeted = yes
  forget old_fact
  then Player PlayerIntroduces after 0.3s
  then any OnGreeted
  once
  cooldown 10
  weight 2
  bonus 1

@who Jun
@concept OnSee
rule JunSees  # comment inside a rule
  if object == crutch
  say ""My crutch!""

# trailing comment
";

        [Test]
        public void SavingKeepsEveryRuleTheSame()
        {
            AssertRoundTrip("kitchen.dlg", Kitchen);
        }

        [Test]
        public void SavingKeepsHeaderAndRuleComments()
        {
            var doc = DlgDocument.Load("kitchen.dlg", Kitchen, out _);
            try
            {
                StringAssert.Contains("Header comment", doc.header);
                Assert.That(doc.rules[0].comment, Is.Empty.Or.Contains("first rule"));
                StringAssert.Contains("trailing comment", doc.trailer);
                StringAssert.Contains("# Header comment", doc.ToText());
            }
            finally
            {
                Object.DestroyImmediate(doc);
            }
        }

        [Test]
        public void SavingTwiceGivesTheSameText()
        {
            var first = DlgDocument.Load("kitchen.dlg", Kitchen, out _);
            DlgDocument second = null;
            try
            {
                string once = first.ToText();
                second = DlgDocument.Load("kitchen.dlg", once, out _);
                Assert.AreEqual(once, second.ToText());
            }
            finally
            {
                Object.DestroyImmediate(first);
                if (second != null) Object.DestroyImmediate(second);
            }
        }

        /// <summary>Every .dlg file in the project and the package's samples, so real files are covered too.</summary>
        static IEnumerable<string> DlgFiles()
        {
            var files = AssetDatabase.GetAllAssetPaths().Where(p => p.EndsWith(".dlg")).Select(Path.GetFullPath).ToList();
            var samples = Path.GetFullPath("Packages/com.dynamicdialog/Samples~");
            if (Directory.Exists(samples)) files.AddRange(Directory.GetFiles(samples, "*.dlg", SearchOption.AllDirectories));
            return files.Distinct();
        }

        [Test]
        public void SavingKeepsRealFilesTheSame()
        {
            var files = DlgFiles().ToList();
            Assert.That(files, Is.Not.Empty, "Expected at least the Robot Banter sample.");
            foreach (var file in files) AssertRoundTrip(file, File.ReadAllText(file));
        }

        static void AssertRoundTrip(string name, string text)
        {
            var original = DialogScriptParser.Parse(text, name);
            Assert.IsFalse(original.HasErrors, $"{name} should parse: {string.Join("\n", original.Messages)}");

            var doc = DlgDocument.Load(name, text, out _);
            Assert.IsNotNull(doc, $"{name} should load into the rule editor");
            try
            {
                var saved = DialogScriptParser.Parse(doc.ToText(), name);
                Assert.IsFalse(saved.HasErrors, $"{name} should still parse after saving: {string.Join("\n", saved.Messages)}");
                // The writer gives a canonical form, so equal output means equal rules.
                Assert.AreEqual(
                    DialogScriptWriter.ToDlg(original.Rules, original.PartitionKeys),
                    DialogScriptWriter.ToDlg(saved.Rules, saved.PartitionKeys),
                    $"Saving {name} from the rule editor changed its rules.");
            }
            finally
            {
                Object.DestroyImmediate(doc);
            }
        }
    }
}
