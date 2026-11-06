using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DynamicDialog.Presentation.Tests
{
    /// <summary>Play Mode tests for the shipped speech bubble and subtitle bar prefabs.</summary>
    public class PresentationComponentTests
    {
        const string Folder = "Packages/com.dynamicdialog/Runtime/Presentation/Prefabs/";

        readonly List<Object> created = new List<Object>();
        DialogRuleDatabase database;
        Camera cam;

        [SetUp]
        public void SetUp()
        {
            DialogPresenters.Reset();
            database = Track(ScriptableObject.CreateInstance<DialogRuleDatabase>());
            Track(new GameObject("Director")).AddComponent<DialogDirector>().AddDatabase(database);
            cam = Track(new GameObject("Camera")).AddComponent<Camera>();
            cam.transform.position = Vector3.zero;
            cam.transform.rotation = Quaternion.identity;
            cam.tag = "MainCamera";
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            DialogPresenters.Reset();
        }

        T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        static T LoadPrefab<T>(string name) where T : Component
        {
#if UNITY_EDITOR
            var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            Assert.IsNotNull(go, "missing prefab " + name);
            return go.GetComponent<T>();
#else
            Assert.Ignore("Needs the editor to load the package prefabs.");
            return null;
#endif
        }

        DialogSpeechBubbles MakeBubbles()
        {
            var b = Track(Object.Instantiate(LoadPrefab<DialogSpeechBubbles>("Dialog Speech Bubbles")));
            b.viewCamera = cam;
            b.heightOffset = 0f;
            return b;
        }

        DialogSubtitleBar MakeBar() => Track(Object.Instantiate(LoadPrefab<DialogSubtitleBar>("Dialog Subtitle Bar")));

        DialogSpeaker MakeSpeaker(string who, Vector3 position)
        {
            var go = Track(new GameObject(who));
            go.transform.position = position;
            go.SetActive(false);
            var s = go.AddComponent<DialogSpeaker>();
            typeof(DialogSpeaker).GetField("idleTalk", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(s, false);
            go.SetActive(true);
            return s;
        }

        void SetRules(params (string who, string concept, string text, float duration)[] rules)
        {
            database.rules = new List<RuleDefinition>();
            foreach (var (who, concept, text, duration) in rules)
            {
                var r = new RuleDefinition { name = who + concept, who = who, concept = concept };
                r.response.lines.Add(new DialogLineDefinition { text = text, duration = duration });
                database.rules.Add(r);
            }
            database.Invalidate();
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds = 3f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        static Rect ScreenRect(DialogSpeechBubble b)
        {
            var corners = new Vector3[4];
            b.RectTransform.GetWorldCorners(corners); // overlay canvas: world corners are screen pixels
            return new Rect(corners[0], corners[2] - corners[0]);
        }

        // ---- Speech bubbles -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Bubble_ShowsLineWithName_FadesInAndOut()
        {
            SetRules(("Rosa", "Talk", "Morning! Lovely day for it.", 0.6f));
            var bubbles = MakeBubbles();
            var rosa = MakeSpeaker("Rosa", new Vector3(0f, 0f, 6f));
            yield return null;

            rosa.Speak("Talk");
            var bubble = bubbles.BubbleFor(rosa);
            Assert.IsNotNull(bubble, "a bubble per speaking speaker");
            Assert.AreEqual("Morning! Lovely day for it.", bubble.body.text);
            Assert.AreEqual("Rosa", bubble.nameLabel.text);
            Assert.Greater(bubble.Size.x, 0f);

            yield return WaitUntil(() => bubble.Alpha >= 1f);
            Assert.AreEqual(1f, bubble.Alpha, "faded in");
            Assert.IsTrue(new Rect(0, 0, Screen.width, Screen.height).Overlaps(ScreenRect(bubble)), "on screen");

            yield return WaitUntil(() => bubbles.BubbleFor(rosa) == null);
            Assert.IsNull(bubbles.BubbleFor(rosa), "released after the line ends and the bubble fades");
            Assert.IsFalse(bubble.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Bubble_TypewriterRevealsOverTime()
        {
            SetRules(("Rosa", "Talk", "This line is long enough to watch it type out.", 2f));
            var bubbles = MakeBubbles();
            bubbles.typewriter.charactersPerSecond = 10f;
            bubbles.typewriter.finishBy = 0.5f; // 46 characters by 1 s
            var rosa = MakeSpeaker("Rosa", new Vector3(0f, 0f, 6f));
            yield return null;

            rosa.Speak("Talk");
            var bubble = bubbles.BubbleFor(rosa);
            Assert.Greater(bubble.TotalCharacters, 0, "TextMeshPro laid out the text (needs TMP Essential Resources)");
            yield return null;
            Assert.Less(bubble.VisibleCharacters, bubble.TotalCharacters, "still typing");
            yield return WaitUntil(() => Time.timeAsDouble - rosa.CurrentStartTime > 1.05);
            yield return null;
            Assert.AreEqual(bubble.TotalCharacters, bubble.VisibleCharacters, "all visible by finishBy");
        }

        [UnityTest]
        public IEnumerator Bubbles_OverlappingSpeakers_StackWithoutOverlap()
        {
            SetRules(("Rosa", "Talk", "First line, said at the same spot.", 3f),
                     ("Ike", "Talk", "Second line, right on top of it.", 3f));
            var bubbles = MakeBubbles();
            var rosa = MakeSpeaker("Rosa", new Vector3(0f, 0f, 6f));
            var ike = MakeSpeaker("Ike", new Vector3(0.05f, 0f, 6f));
            yield return null;

            rosa.Speak("Talk");
            yield return Frames(2);
            ike.Speak("Talk");
            var a = bubbles.BubbleFor(rosa);
            var b = bubbles.BubbleFor(ike);
            yield return WaitUntil(() => a.Alpha >= 1f && b.Alpha >= 1f);
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(1f, a.Alpha);
            Assert.AreEqual(1f, b.Alpha);
            Assert.IsFalse(ScreenRect(a).Overlaps(ScreenRect(b)), "bubbles were moved apart");
            Assert.Greater(ScreenRect(b).y, ScreenRect(a).y, "the newer bubble goes above");
        }

        [UnityTest]
        public IEnumerator Bubbles_ReplaceMode_ShowsOnlyTheNewest_ThenTheOlderAgain()
        {
            SetRules(("Rosa", "Talk", "I'm talking for a while here.", 3f), ("Ike", "Talk", "Short.", 0.4f));
            var bubbles = MakeBubbles();
            bubbles.overlap = DialogOverlapMode.Replace;
            var rosa = MakeSpeaker("Rosa", new Vector3(-1f, 0f, 6f));
            var ike = MakeSpeaker("Ike", new Vector3(1f, 0f, 6f));
            yield return null;

            rosa.Speak("Talk");
            yield return Frames(2);
            ike.Speak("Talk");
            yield return WaitUntil(() => bubbles.BubbleFor(rosa).Alpha <= 0f);
            Assert.AreEqual(0f, bubbles.BubbleFor(rosa).Alpha, "the older bubble is hidden");
            Assert.IsTrue(bubbles.BubbleFor(rosa).IsShowing, "but its line is still playing");
            Assert.Greater(bubbles.BubbleFor(ike).Alpha, 0f);

            yield return WaitUntil(() => !ike.IsSpeaking);
            yield return WaitUntil(() => bubbles.BubbleFor(rosa).Alpha >= 1f);
            Assert.AreEqual(1f, bubbles.BubbleFor(rosa).Alpha, "comes back when the newer line ends");
        }

        [UnityTest]
        public IEnumerator Bubble_SpeakerOffScreenOrBehind_StaysOnScreen_OrHides()
        {
            SetRules(("Rosa", "Talk", "Over here! Behind you!", 3f));
            var bubbles = MakeBubbles();
            var rosa = MakeSpeaker("Rosa", new Vector3(0f, 0f, -6f)); // behind the camera
            yield return null;

            rosa.Speak("Talk");
            var bubble = bubbles.BubbleFor(rosa);
            yield return WaitUntil(() => bubble.Alpha >= 1f);
            yield return new WaitForEndOfFrame();
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var r = ScreenRect(bubble);
            Assert.AreEqual(1f, bubble.Alpha, "still shown");
            Assert.IsTrue(screen.Contains(r.min) && screen.Contains(r.max), $"inside the screen: {r} in {screen}");

            rosa.transform.position = new Vector3(60f, 0f, 6f); // far off to the right
            yield return Frames(1);
            yield return new WaitForEndOfFrame();
            r = ScreenRect(bubble);
            Assert.IsTrue(screen.Contains(r.min) && screen.Contains(r.max), $"clamped to the edge: {r}");
            Assert.Greater(r.center.x, screen.center.x, "on the speaker's side");

            bubbles.offscreen = DialogOffscreenMode.Hide;
            yield return WaitUntil(() => bubble.Alpha <= 0f);
            Assert.AreEqual(0f, bubble.Alpha, "Hide mode hides it");
            rosa.transform.position = new Vector3(0f, 0f, 6f);
            yield return WaitUntil(() => bubble.Alpha >= 1f);
            Assert.AreEqual(1f, bubble.Alpha, "and shows it again on screen");
        }

        [UnityTest]
        public IEnumerator Bubble_Interrupted_FadesFast()
        {
            SetRules(("Rosa", "Talk", "Something long...", 5f));
            var bubbles = MakeBubbles();
            var rosa = MakeSpeaker("Rosa", new Vector3(0f, 0f, 6f));
            yield return null;
            rosa.Speak("Talk");
            var bubble = bubbles.BubbleFor(rosa);
            yield return WaitUntil(() => bubble.Alpha >= 1f);

            // Measure one frame's fade (after LateUpdate), so an editor hitch can't break the test.
            yield return new WaitForEndOfFrame();
            rosa.StopLine();
            float before = bubble.Alpha;
            yield return new WaitForEndOfFrame();
            float expected = Mathf.Max(0f, before - Time.deltaTime / bubble.interruptedFadeSeconds);
            Assert.AreEqual(expected, bubble.Alpha, 1e-3f, "uses the faster interrupted fade");
            Assert.Less(bubble.interruptedFadeSeconds, bubble.fadeOutSeconds);
            yield return WaitUntil(() => bubbles.BubbleFor(rosa) == null);
            Assert.IsNull(bubbles.BubbleFor(rosa));
        }

        // ---- Subtitle bar ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SubtitleBar_StacksRows_WithNames_UpToMaxRows()
        {
            SetRules(("Rosa", "Talk", "One.", 3f), ("Ike", "Talk", "Two.", 3f), ("Mei", "Talk", "Three.", 3f));
            var bar = MakeBar();
            bar.maxRows = 2;
            var rosa = MakeSpeaker("Rosa", Vector3.zero);
            var ike = MakeSpeaker("Ike", Vector3.zero);
            var mei = MakeSpeaker("Mei", Vector3.zero);
            yield return null;

            rosa.Speak("Talk");
            ike.Speak("Talk");
            yield return WaitUntil(() => bar.Rows.Count == 2 && bar.Rows[1].Alpha >= 1f);
            Assert.AreEqual(2, bar.Rows.Count);
            StringAssert.Contains("Rosa", bar.Rows[0].Text.text);
            StringAssert.EndsWith("One.", bar.Rows[0].Text.text);
            StringAssert.Contains("Ike", bar.Rows[1].Text.text);

            mei.Speak("Talk");
            yield return WaitUntil(() => bar.Rows[0].Alpha <= 0f);
            Assert.AreEqual(0f, bar.Rows[0].Alpha, "over maxRows, the oldest row hides");
            Assert.IsTrue(bar.Rows[0].IsPlaying);
            Assert.AreEqual(1f, bar.Rows[2].Alpha, 0.2f);
        }

        [UnityTest]
        public IEnumerator SubtitleBar_Replace_ShowsOnlyNewest_AndNamesAreOptional()
        {
            SetRules(("Rosa", "Talk", "One.", 3f), ("Ike", "Talk", "Two.", 3f));
            var bar = MakeBar();
            bar.overlap = DialogOverlapMode.Replace;
            bar.showSpeakerName = false;
            var rosa = MakeSpeaker("Rosa", Vector3.zero);
            var ike = MakeSpeaker("Ike", Vector3.zero);
            yield return null;

            rosa.Speak("Talk");
            ike.Speak("Talk");
            yield return WaitUntil(() => bar.Rows[0].Alpha <= 0f);
            Assert.AreEqual(0f, bar.Rows[0].Alpha);
            Assert.AreEqual("Two.", bar.Rows[1].Text.text, "no name prefix");

            ike.StopLine();
            yield return WaitUntil(() => bar.Rows.Count == 1 && bar.Rows[0].Alpha >= 1f);
            Assert.AreEqual(1, bar.Rows.Count);
            Assert.AreEqual(1f, bar.Rows[0].Alpha, "the older line comes back");
        }
    }
}
