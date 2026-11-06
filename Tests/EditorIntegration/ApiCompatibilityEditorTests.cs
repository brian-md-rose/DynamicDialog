using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DynamicDialog.Editor.Tests
{
    /// <summary>The 1.0 renames in the Unity and editor layers: old 0.6 names forward to the new ones.</summary>
    public class ApiCompatibilityEditorTests
    {
#pragma warning disable 618 // the obsolete names are what these tests check
        [Test]
        public void ObsoleteFactStatics_AreWellKnownFacts()
        {
            Assert.AreEqual(WellKnownFacts.Map, DialogDirector.MapFact);
            Assert.AreEqual(WellKnownFacts.SpeakersTalking, DialogDirector.SpeakersTalkingFact);
            Assert.AreEqual(WellKnownFacts.Area, DialogArea.AreaFact);
            Assert.AreEqual(WellKnownFacts.Object, DialogVision.ObjectFact);
            Assert.AreEqual(WellKnownFacts.ObjectDistance, DialogVision.ObjectDistanceFact);
            Assert.AreEqual(WellKnownFacts.NearbySpeakers, NearbySpeakerFacts.NearbySpeakersFact);
            Assert.AreEqual(WellKnownFacts.Region, RegionFacts.RegionFact);
        }

        [Test]
        public void ObsoleteAnyLineFinished_SubscribesToAnyLineEnded()
        {
            var field = typeof(DialogSpeaker).GetField(nameof(DialogSpeaker.AnyLineEnded), BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "AnyLineEnded's backing field");
            Action<DialogSpeaker, DialogLine, bool> handler = (s, l, c) => { };

            DialogSpeaker.AnyLineFinished += handler;
            try
            {
                var subscribed = (Delegate)field.GetValue(null);
                Assert.IsNotNull(subscribed);
                CollectionAssert.Contains(subscribed.GetInvocationList(), handler);
            }
            finally
            {
                DialogSpeaker.AnyLineFinished -= handler;
            }
            var after = (Delegate)field.GetValue(null);
            Assert.IsTrue(after == null || Array.IndexOf(after.GetInvocationList(), handler) < 0);
        }

        [Test]
        public void ObsoleteOnLineFinished_IsOnLineEnded()
        {
            var go = new GameObject("ApiSpeaker");
            try
            {
                var speaker = go.AddComponent<DialogSpeaker>();
                Assert.AreSame(speaker.onLineEnded, speaker.onLineFinished);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OldSerializedSourcePath_StillLoads()
        {
            const string folder = "Assets/DynamicDialogApiTestTemp";
            const string path = folder + "/Old.asset";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "DynamicDialogApiTestTemp");
            try
            {
                // Save a database, then rewrite its file the way 0.6 wrote it (field "sourcePath").
                var db = ScriptableObject.CreateInstance<DialogRuleDatabase>();
                db.sourceAssetPath = "Assets/Old/swamp.dlg";
                AssetDatabase.CreateAsset(db, path);
                AssetDatabase.SaveAssetIfDirty(db);

                string text = File.ReadAllText(path);
                Assume.That(text, Does.Contain("sourceAssetPath:"), "needs text asset serialization");
                File.WriteAllText(path, text.Replace("sourceAssetPath:", "sourcePath:"));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var loaded = AssetDatabase.LoadAssetAtPath<DialogRuleDatabase>(path);
                Assert.AreEqual("Assets/Old/swamp.dlg", loaded.SourcePath);
                Assert.AreEqual(loaded.SourcePath, loaded.sourcePath);
                Assert.IsTrue(loaded.IsImported);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void RuleEditor_OpenAndObsoleteShowWindow_ReturnTheSameWindow()
        {
            // Opening an editor window logs an error without a graphics device (batch mode with -nographics).
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Needs a graphics device to open an editor window.");

            // Leave a window the user already had open alone.
            bool wasOpen = EditorWindow.HasOpenInstances<DialogRuleEditorWindow>();
            var opened = DialogRuleEditorWindow.Open();
            try
            {
                Assert.IsNotNull(opened);
                Assert.AreSame(opened, DialogRuleEditorWindow.ShowWindow());
            }
            finally
            {
                if (!wasOpen) opened.Close();
            }
        }
#pragma warning restore 618
    }
}
