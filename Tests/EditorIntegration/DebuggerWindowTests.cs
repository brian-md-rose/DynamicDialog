using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DynamicDialog.Editor.Tests
{
    public class DebuggerWindowTests
    {
        const string Folder = "Assets/DynamicDialogDebuggerTemp";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "DynamicDialogDebuggerTemp");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void TestQuery_ParsesFactsText()
        {
            var q = DialogDebuggerWindow.BuildTestQuery("Nick", "Talk", "map = swamp\nhealth: 20\n# comment\nscared\nname = \"Big Al\"\n");
            Assert.AreEqual(Symbol.Of("Nick"), q.Who);
            Assert.AreEqual(Symbol.Of("Talk"), q.Concept);
            Assert.IsTrue(q.IsSymbol(Symbol.Of("map")));
            q.TryGet(Symbol.Of("health"), out float health);
            Assert.AreEqual(20f, health);
            q.TryGet(Symbol.Of("scared"), out float scared);
            Assert.AreEqual(1f, scared, "a bare name means 1");
            Assert.AreEqual(Symbol.Of("Big Al"), q.GetSymbol(Symbol.Of("name")));
            Assert.IsTrue(q.Contains(WellKnownFacts.Random));
            Assert.Throws<FormatException>(() => DialogDebuggerWindow.BuildTestQuery("", "", "= 3"));
        }

        [Test]
        public void TestQuery_FindsTheWinnerAcrossProjectDatabases()
        {
            string path = Folder + "/debug.dlg";
            File.WriteAllText(path,
                "rule General\n  concept Talk\n  say \"hi\"\n" +
                "rule Swamp\n  who Nick\n  concept Talk\n  if map == swamp\n  say \"swamp\"\n" +
                "rule Hurt\n  who Nick\n  concept Talk\n  if health < 30\n  say \"ow\"\n");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var window = ScriptableObject.CreateInstance<DialogDebuggerWindow>();
            try
            {
                typeof(DialogDebuggerWindow).GetField("testWho", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(window, "Nick");
                typeof(DialogDebuggerWindow).GetField("testConcept", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(window, "Talk");
                typeof(DialogDebuggerWindow).GetField("testFacts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(window, "map = swamp\nhealth = 80");
                window.RunTest();

                var ours = window.TestResults.Where(e => new[] { "General", "Swamp", "Hurt" }.Contains(e.Rule.Name)).ToList();
                Assert.AreEqual("Swamp", ours[0].Rule.Name);
                Assert.AreEqual(RuleVerdict.Won, ours[0].Verdict);
                var hurt = ours.Single(e => e.Rule.Name == "Hurt");
                Assert.AreEqual(RuleVerdict.FailedCondition, hurt.Verdict);
                StringAssert.Contains("'health' was 80", hurt.Text);
                Assert.AreEqual(RuleVerdict.Outscored, ours.Single(e => e.Rule.Name == "General").Verdict);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }
    }
}
