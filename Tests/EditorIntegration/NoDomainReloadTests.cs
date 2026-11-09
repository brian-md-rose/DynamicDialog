using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DynamicDialog.Editor.Tests
{
    /// <summary>
    /// Enters Play Mode twice with Enter Play Mode Options set to skip the domain reload, and checks the
    /// second session doesn't see the first one's director, subscribers or once/cooldown state.
    /// </summary>
    public class NoDomainReloadTests
    {
        bool oldEnabled;
        EnterPlayModeOptions oldOptions;

        [SetUp]
        public void SetUp()
        {
            oldEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            oldOptions = EditorSettings.enterPlayModeOptions;
        }

        [TearDown]
        public void TearDown()
        {
            EditorSettings.enterPlayModeOptionsEnabled = oldEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
        }

        static DialogRuleDatabase MakeDatabase()
        {
            var db = ScriptableObject.CreateInstance<DialogRuleDatabase>();
            db.name = "NoReloadRules";
            // Survives entering and leaving Play Mode, as a project asset would.
            db.hideFlags = HideFlags.DontSave;
            var rule = new RuleDefinition { name = "greetOnce", who = "Mei", concept = "Talk", once = true };
            rule.response.lines.Add(new DialogLineDefinition { text = "Hello.", duration = 0.01f });
            db.rules = new List<RuleDefinition> { rule };
            return db;
        }

        static DialogSpeaker MakeSpeaker()
        {
            var go = new GameObject("Mei");
            return go.AddComponent<DialogSpeaker>();
        }

        [UnityTest]
        public IEnumerator SecondPlayModeSession_StartsFresh_WithoutDomainReload()
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            var db = MakeDatabase();
            int staleLines = 0, staleEvents = 0;
            try
            {
                // Edit Mode: a static subscriber that should not reach Play Mode.
                DialogEvents.On("Stale", (s, e) => staleEvents++);

                yield return new EnterPlayMode();
                Assert.IsFalse(DialogEvents.HasHandlers("Stale"), "DialogEvents cleared when Play Mode starts");
                int firstSession = DialogStaticState.PlaySession;

                var director = new GameObject("Director").AddComponent<DialogDirector>();
                director.AddDatabase(db);
                var mei = MakeSpeaker();
                yield return null;
                Assert.AreEqual("greetOnce", mei.Speak("Talk")?.Rule.Name);
                mei.StopLine();
                Assert.IsNull(mei.Speak("Talk"), "once rule used up");
                DialogSpeaker.AnyLineStarted += (s, l, v) => staleLines++;
                DialogEvents.On("Stale", (s, e) => staleEvents++);
                yield return new ExitPlayMode();

                yield return new EnterPlayMode();
                Assert.Greater(DialogStaticState.PlaySession, firstSession, "the reset ran again (no domain reload)");
                Assert.IsNull(DialogDirector.Instance, "the previous session's director is gone");
                Assert.IsFalse(DialogEvents.HasHandlers("Stale"));

                director = new GameObject("Director").AddComponent<DialogDirector>();
                director.AddDatabase(db);
                mei = MakeSpeaker();
                yield return null;
                Assert.AreEqual("greetOnce", mei.Speak("Talk")?.Rule.Name, "once state starts fresh");
                DialogEvents.Raise(mei, new DialogEvent("Stale"));
                Assert.AreEqual(0, staleLines, "AnyLineStarted subscribers from the last session were dropped");
                Assert.AreEqual(0, staleEvents, "DialogEvents handlers from earlier sessions were dropped");
                yield return new ExitPlayMode();
            }
            finally
            {
                Object.DestroyImmediate(db);
            }
        }
    }
}
