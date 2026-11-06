using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DynamicDialog.Tests
{
    /// <summary>Play Mode tests for the Unity components. Time runs 4x faster so idle and vision polling are quick.</summary>
    public class ComponentTests
    {
        readonly List<Object> created = new List<Object>();
        readonly List<string> started = new List<string>();
        DialogDirector director;
        DialogRuleDatabase database;
        float oldTimeScale;

        [SetUp]
        public void SetUp()
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 4f;
            started.Clear();
            DialogSpeaker.AnyLineStarted += OnAnyLineStarted;

            database = Track(ScriptableObject.CreateInstance<DialogRuleDatabase>());
            database.name = "TestRules";
            director = Track(new GameObject("Director")).AddComponent<DialogDirector>();
            director.AddDatabase(database);
        }

        [TearDown]
        public void TearDown()
        {
            DialogSpeaker.AnyLineStarted -= OnAnyLineStarted;
            Time.timeScale = oldTimeScale;
            foreach (var o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        void OnAnyLineStarted(DialogSpeaker speaker, DialogLine line, DialogLineDefinition variation) =>
            started.Add($"{speaker.Who}:{line.Rule.Name}");

        T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        DialogSpeaker MakeSpeaker(string who, Vector3 position, bool idle = false)
        {
            var go = Track(new GameObject(who));
            go.transform.position = position;
            go.SetActive(false);
            var s = go.AddComponent<DialogSpeaker>();
            SetPrivate(s, "idleTalk", idle);
            SetPrivate(s, "idleInterval", 1f);
            SetPrivate(s, "idleJitter", 0f);
            go.SetActive(true);
            return s;
        }

        static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"no field {field}");
            f.SetValue(target, value);
        }

        static RuleDefinition Rule(string name, string who, string concept, float duration = 0.2f)
        {
            var r = new RuleDefinition { name = name, who = who, concept = concept };
            r.response.lines.Add(new DialogLineDefinition { text = name, duration = duration });
            return r;
        }

        static FollowupDefinition Then(FollowupTarget target, string concept, string character = null) =>
            new FollowupDefinition { target = target, concept = concept, character = character };

        void SetRules(params RuleDefinition[] rules)
        {
            database.rules = new List<RuleDefinition>(rules);
            database.Invalidate();
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds = 3f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        // ---- Tests ----------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Conversation_PlaysLineThenReply()
        {
            var first = Rule("nickAsks", "Nick", "Talk");
            first.then.Add(Then(FollowupTarget.Character, "Reply", "Rochelle"));
            SetRules(first, Rule("rochelleAnswers", "Rochelle", "Reply"));
            var nick = MakeSpeaker("Nick", Vector3.zero);
            MakeSpeaker("Rochelle", Vector3.right);
            yield return null;

            Assert.IsNotNull(nick.Speak("Talk"));
            Assert.IsTrue(nick.IsSpeaking);
            yield return WaitUntil(() => started.Count == 2);
            CollectionAssert.AreEqual(new[] { "Nick:nickAsks", "Rochelle:rochelleAnswers" }, started);
        }

        [UnityTest]
        public IEnumerator DoEvents_ReachDialogEventsAndListeners_AtStartAndEnd()
        {
            var give = Rule("meiGetsGlasses", "Mei", "OnTalk", duration: 0.3f);
            give.events.Add(new DialogEventDefinition { name = "GiveItem", args = new List<string> { "glasses" } });
            give.events.Add(new DialogEventDefinition { name = "Leave", timing = DialogEventTiming.LineEnded });
            SetRules(give, Rule("junTalks", "Jun", "OnTalk"));
            var mei = MakeSpeaker("Mei", Vector3.zero);
            var jun = MakeSpeaker("Jun", Vector3.right * 50f);
            yield return null;

            var seen = new List<string>();
            var subscription = DialogEvents.On("giveitem", (speaker, e) => seen.Add($"{speaker.Who}:{e.Name}:{e.Arg(0)}"));
            DialogEvents.On("Leave", (speaker, e) => seen.Add($"{speaker.Who}:{e.Name}"));

            var listenerGo = Track(new GameObject("Listener"));
            var listener = listenerGo.AddComponent<DialogEventListener>();
            var fromMei = new DialogEventListener.Entry { eventName = "GiveItem", onlyFrom = mei };
            var fromJun = new DialogEventListener.Entry { eventName = "GiveItem", onlyFrom = jun };
            string argument = null;
            int junCount = 0;
            fromMei.onEventWithArgument.AddListener(a => argument = a);
            fromJun.onEvent.AddListener(() => junCount++);
            listener.events.Add(fromMei);
            listener.events.Add(fromJun);

            try
            {
                Assert.IsNotNull(mei.Speak("OnTalk"));
                CollectionAssert.AreEqual(new[] { "Mei:GiveItem:glasses" }, seen, "start events arrive as the line starts");
                Assert.AreEqual("glasses", argument);
                Assert.AreEqual(0, junCount, "onlyFrom filters by speaker");

                yield return WaitUntil(() => !mei.IsSpeaking);
                CollectionAssert.AreEqual(new[] { "Mei:GiveItem:glasses", "Mei:Leave" }, seen, "end events arrive when the line ends");

                subscription.Dispose();
                yield return new WaitForSeconds(0.1f);
                mei.Speak("OnTalk");
                Assert.AreEqual(1, seen.Count(s => s.StartsWith("Mei:GiveItem")), "a disposed subscription stops listening");
            }
            finally
            {
                DialogEvents.Clear();
            }
        }

        [UnityTest]
        public IEnumerator FactListener_ReactsToRuleWritesAndExpiry()
        {
            var r = Rule("junGetsCrutch", "Jun", "OnTalk");
            r.remember.Add(new FactWriteDefinition { scope = FactScope.World, key = "crutch_returned", value = "1" });
            r.remember.Add(new FactWriteDefinition { scope = FactScope.Speaker, key = "grateful", value = "1", duration = 0.5f });
            SetRules(r);
            var jun = MakeSpeaker("Jun", Vector3.zero);

            var worldGo = Track(new GameObject("WorldWatcher"));
            worldGo.SetActive(false);
            var world = worldGo.AddComponent<DialogFactListener>();
            world.Fact = "crutch_returned";
            var values = new List<float>();
            world.onValueChanged.AddListener(values.Add);

            jun.gameObject.SetActive(false);
            var mine = jun.gameObject.AddComponent<DialogFactListener>();
            mine.Scope = FactScope.Speaker;
            mine.Fact = "grateful";
            int set = 0, removed = 0;
            mine.onSet.AddListener(() => set++);
            mine.onRemoved.AddListener(() => removed++);
            jun.gameObject.SetActive(true);
            worldGo.SetActive(true);
            yield return null;

            CollectionAssert.AreEqual(new[] { 0f }, values, "the current value is sent on enable");
            Assert.IsNotNull(jun.Speak("OnTalk"));
            CollectionAssert.AreEqual(new[] { 0f, 1f }, values);
            Assert.AreEqual(1, set);
            Assert.IsTrue(mine.TryGetValue(out float v) && v == 1f);

            yield return WaitUntil(() => removed > 0);
            Assert.AreEqual(1, removed, "expiry is reported by the director's per-frame check");

            director.LoadStateJson(director.SaveStateJson());
            Assert.AreEqual(1f, values[values.Count - 1], "loading a save re-sends the current value");
        }

        [UnityTest]
        public IEnumerator Interrupt_StopsLineAndDropsFollowup()
        {
            var first = Rule("longStory", "Nick", "Talk", duration: 0.5f);
            first.then.Add(Then(FollowupTarget.Character, "Reply", "Ellis"));
            SetRules(first, Rule("ouch", "Nick", "Hurt"), Rule("ellisReply", "Ellis", "Reply"));
            var nick = MakeSpeaker("Nick", Vector3.zero);
            MakeSpeaker("Ellis", Vector3.right);
            yield return null;

            nick.Speak("Talk");
            Assert.IsNull(nick.Speak("Hurt"), "no interrupt unless asked");
            Assert.IsNotNull(nick.Speak("Hurt", interrupt: true));
            yield return new WaitForSeconds(1.5f);
            CollectionAssert.AreEqual(new[] { "Nick:longStory", "Nick:ouch" }, started);
        }

        [UnityTest]
        public IEnumerator ThenAny_UsesEarshot()
        {
            var hurt = Rule("hurt", "Nick", "Hurt");
            hurt.then.Add(Then(FollowupTarget.Any, "BuddyHurt"));
            var coach = Rule("coachHelps", "Coach", "BuddyHurt");
            coach.criteria.Add(new CriterionDefinition { key = "from", value = "Nick" });
            SetRules(hurt, Rule("ellisHelps", "Ellis", "BuddyHurt"), coach);

            var nick = MakeSpeaker("Nick", Vector3.zero);
            MakeSpeaker("Ellis", new Vector3(5, 0, 0));
            var coachSpeaker = MakeSpeaker("Coach", new Vector3(100, 0, 0));
            yield return null;

            nick.Speak("Hurt");
            yield return WaitUntil(() => started.Count == 2);
            CollectionAssert.AreEqual(new[] { "Nick:hurt", "Ellis:ellisHelps" }, started, "Coach is out of earshot");

            yield return new WaitForSeconds(0.5f);
            started.Clear();
            coachSpeaker.transform.position = new Vector3(0, 0, 3);
            nick.Speak("Hurt");
            yield return WaitUntil(() => started.Count == 2);
            CollectionAssert.AreEqual(new[] { "Nick:hurt", "Coach:coachHelps" }, started, "Coach's line is more specific");
        }

        [UnityTest]
        public IEnumerator IdleChatter_Fires()
        {
            SetRules(Rule("idleLine", "Nick", "TalkIdle"));
            MakeSpeaker("Nick", Vector3.zero, idle: true);
            yield return WaitUntil(() => started.Count > 0);
            CollectionAssert.Contains(started, "Nick:idleLine");
        }

        [UnityTest]
        public IEnumerator Vision_RemarksOnTaggedObject_AndRemembers()
        {
            var first = Rule("firstBarrel", null, "OnSee");
            first.criteria.Add(new CriterionDefinition { key = "object", value = "barrel" });
            first.criteria.Add(new CriterionDefinition { key = "seen_barrels", op = CriterionOp.ZeroOrMissing });
            first.remember.Add(new FactWriteDefinition { key = "seen_barrels", op = FactOp.Add, value = "1" });
            SetRules(first);

            var bot = MakeSpeaker("RedBot", Vector3.zero);
            var vision = bot.gameObject.AddComponent<DialogVision>();
            SetPrivate(vision, "pollInterval", 0.2f);

            var barrel = Track(new GameObject("barrel"));
            barrel.transform.position = new Vector3(0, 0, 3);
            barrel.AddComponent<DialogTag>();

            var behind = Track(new GameObject("soda_can"));
            behind.transform.position = new Vector3(0, 0, -3);
            behind.AddComponent<DialogTag>();

            yield return WaitUntil(() => started.Count > 0);
            CollectionAssert.AreEqual(new[] { "RedBot:firstBarrel" }, started);
            Assert.IsTrue(bot.Memory.TryGet("seen_barrels", Time.timeAsDouble, out float seen));
            Assert.AreEqual(1f, seen);
        }

        [UnityTest]
        public IEnumerator Region_LoadsAndUnloadsRules()
        {
            SetRules();
            var regionRules = Track(ScriptableObject.CreateInstance<DialogRuleDatabase>());
            regionRules.rules.Add(Rule("swampLine", "Nick", "Talk"));
            var nick = MakeSpeaker("Nick", Vector3.zero);

            var regionGo = Track(new GameObject("Swamp"));
            regionGo.SetActive(false);
            var region = regionGo.AddComponent<DialogRegion>();
            SetPrivate(region, "database", regionRules);
            yield return null;

            Assert.IsNull(nick.Speak("Talk"));
            regionGo.SetActive(true);
            Assert.IsTrue(director.IsLoaded(regionRules));
            Assert.IsNotNull(nick.Speak("Talk"));
            regionGo.SetActive(false);
            Assert.IsFalse(director.IsLoaded(regionRules));
        }

        [UnityTest]
        public IEnumerator Region_AddsWorldFactsOnlyWhileActive()
        {
            var englandOnly = Rule("arthur", "Knight", "Talk");
            englandOnly.criteria.Add(new CriterionDefinition { key = "region", value = "england" });
            englandOnly.criteria.Add(new CriterionDefinition { key = "grail_found", value = "false" });
            SetRules(englandOnly, Rule("generic", "Knight", "Talk", duration: 0.01f));
            var knight = MakeSpeaker("Knight", Vector3.zero);

            var regionGo = Track(new GameObject("England"));
            regionGo.SetActive(false);
            var region = regionGo.AddComponent<DialogRegion>();
            region.Facts.regionName = "england";
            region.Facts.facts.Add(new StaticFacts.Entry { key = "grail_found", value = "false" });
            yield return null;

            Assert.AreEqual("generic", knight.Speak("Talk").Rule.Name);
            yield return WaitUntil(() => !knight.IsSpeaking);

            regionGo.SetActive(true);
            var line = knight.Speak("Talk");
            Assert.AreEqual("arthur", line.Rule.Name);
            var record = director.Recorder.Latest(knight.Who);
            Assert.AreEqual("world: RegionFacts", record.SourceOf(WellKnownFacts.Region));
            Assert.AreEqual("england", record.Query.FormatValueAt(Enumerable.Range(0, record.Query.Count).First(i => record.Query.KeyAt(i) == WellKnownFacts.Region)));
            knight.StopLine();

            regionGo.SetActive(false);
            Assert.AreEqual("generic", knight.Speak("Talk").Rule.Name, "the region's facts are gone");
        }

        sealed class FakeLocalizer : IDialogLocalizer
        {
            public AudioClip Clip;
            public string GetText(string key, DialogLineDefinition line) => key == "GREET_1" ? "Bonjour !" : null;
            public AudioClip GetClip(string key, DialogLineDefinition line) => key == "GREET_1" ? Clip : null;
        }

        [UnityTest]
        public IEnumerator Localizer_ReplacesTextAndClip_AndFallsBack()
        {
            var greet = Rule("greet", "Nick", "Greet");
            greet.response.lines[0].key = "GREET_1";
            greet.response.lines[0].duration = 0;
            SetRules(greet, Rule("other", "Nick", "Other"));
            var clip = Track(AudioClip.Create("bonjour", 4410, 1, 44100, false)); // 0.1s
            director.Localizer = new FakeLocalizer { Clip = clip };

            var nick = MakeSpeaker("Nick", Vector3.zero);
            var source = nick.gameObject.AddComponent<AudioSource>();
            SetPrivate(nick, "audioSource", source);
            string subtitle = null;
            nick.onSubtitle.AddListener(t => subtitle = t);
            yield return null;

            nick.Speak("Greet");
            Assert.AreEqual("GREET_1", nick.CurrentKey);
            Assert.AreEqual("Bonjour !", nick.CurrentText);
            Assert.AreEqual("Bonjour !", subtitle);
            Assert.AreSame(clip, nick.CurrentClip);
            Assert.AreSame(clip, source.clip, "the localized clip is what plays");
            yield return WaitUntil(() => !nick.IsSpeaking);

            nick.Speak("Other");
            Assert.AreEqual("other_1", nick.CurrentKey, "no key or clip: <rule>_<n>");
            Assert.AreEqual("other", nick.CurrentText, "no translation: the authored text");
        }

        [UnityTest]
        public IEnumerator SaveAndLoad_RoundTripsThroughJson()
        {
            var intro = Rule("intro", "Nick", "Talk");
            intro.once = true;
            intro.scoreBonus = 1; // beats generic while available, instead of tying
            intro.remember.Add(new FactWriteDefinition { key = "mood", value = "grumpy" });
            SetRules(intro, Rule("generic", "Nick", "Talk", duration: 0.01f));
            database.name = "Global";
            var nick = MakeSpeaker("Nick", Vector3.zero);
            yield return null;

            Assert.AreEqual("intro", nick.Speak("Talk").Rule.Name);
            yield return WaitUntil(() => !nick.IsSpeaking);

            var region = Track(ScriptableObject.CreateInstance<DialogRuleDatabase>());
            region.name = "Swamp";
            var swampOnce = Rule("swampOnce", "Nick", "Swamp", duration: 0.01f);
            swampOnce.once = true;
            region.rules.Add(swampOnce);
            director.AddDatabase(region);
            nick.Speak("Swamp");
            yield return WaitUntil(() => !nick.IsSpeaking);
            director.RemoveDatabase(region);

            string json = director.SaveStateJson();
            StringAssert.Contains("grumpy", json, "symbols are saved by name");

            // Wipe everything, as if starting a new session, then load.
            director.LoadState(new DialogSaveData());
            Assert.AreEqual("intro", nick.Speak("Talk").Rule.Name, "a blank load resets once rules");
            nick.StopLine();

            director.LoadStateJson(json);
            Assert.AreEqual("generic", nick.Speak("Talk").Rule.Name, "the save remembers intro already played");
            nick.StopLine();
            Assert.IsTrue(nick.Memory.TryGet("mood", Time.timeAsDouble, out float mood));
            Assert.AreEqual(Symbol.Of("grumpy").AsValue, mood);

            region.Invalidate(); // fresh rule objects, as after a real reload
            director.AddDatabase(region);
            Assert.IsNull(nick.Speak("Swamp"), "state for a region loaded after the save is applied when it loads");
            StringAssert.Contains("swampOnce", director.SaveStateJson(), "and it is saved again");
        }

        [UnityTest]
        public IEnumerator EditingDatabase_HotReloadsRules()
        {
            SetRules(Rule("old", "Nick", "Talk", duration: 0.01f));
            var nick = MakeSpeaker("Nick", Vector3.zero);
            yield return null;
            Assert.AreEqual("old", nick.Speak("Talk").Rule.Name);
            yield return WaitUntil(() => !nick.IsSpeaking);

            SetRules(Rule("new", "Nick", "Talk"));
            Assert.AreEqual("new", nick.Speak("Talk").Rule.Name);
        }

        [UnityTest]
        public IEnumerator ReplaceDatabase_SwapsRulesAndRedirectsOldReferences()
        {
            SetRules(Rule("old", "Nick", "Talk", duration: 0.01f));
            var nick = MakeSpeaker("Nick", Vector3.zero);
            yield return null;
            Assert.AreEqual("old", nick.Speak("Talk").Rule.Name);
            yield return WaitUntil(() => !nick.IsSpeaking);

            var replacement = Track(ScriptableObject.CreateInstance<DialogRuleDatabase>());
            replacement.rules.Add(Rule("new", "Nick", "Talk"));
            director.AddDatabase(database); // load count 2, like two regions sharing it
            director.ReplaceDatabase(database, replacement);

            Assert.AreEqual("new", nick.Speak("Talk").Rule.Name);
            Assert.IsTrue(director.IsLoaded(database), "the old reference is redirected");
            director.RemoveDatabase(database);
            Assert.IsTrue(director.IsLoaded(replacement), "the load count carried over");
            director.RemoveDatabase(database);
            Assert.IsFalse(director.IsLoaded(replacement));
        }

        [UnityTest]
        public IEnumerator Director_RecordsLookups_ForTheDebugger()
        {
            var ask = Rule("ask", "Nick", "Talk");
            ask.then.Add(Then(FollowupTarget.Character, "Reply", "Rochelle"));
            SetRules(ask, Rule("reply", "Rochelle", "Reply"));
            var nick = MakeSpeaker("Nick", Vector3.zero);
            nick.gameObject.AddComponent<DialogDebugOverlay>();
            MakeSpeaker("Rochelle", Vector3.right);
            yield return null;

            Assert.IsNotNull(director.Recorder, "recording is on by default in the editor");
            nick.Speak("Nothing");
            nick.Speak("Talk");
            nick.Speak("Talk"); // busy: not asked, so not recorded
            yield return WaitUntil(() => started.Count == 2);

            var records = director.Recorder.Records;
            Assert.AreEqual(3, records.Count, string.Join("\n", records));
            Assert.IsNull(records[0].Winner, "a lookup with no rule is recorded as a miss");
            Assert.IsTrue(records[1].Started);
            Assert.AreEqual(Symbol.Of("Rochelle"), records[2].Speaker);
            Assert.AreEqual("engine", records[2].SourceOf(WellKnownFacts.Concept));
            Assert.AreEqual("event", records[2].SourceOf(WellKnownFacts.From), "followups carry who sent them");
            Assert.AreEqual("world: DialogDirector", records[2].SourceOf(WellKnownFacts.Map));
            Assert.IsTrue(records[2].Query.IsSymbol(WellKnownFacts.Map));
        }

        [UnityTest]
        public IEnumerator Area_WritesAreaFactWhileInside()
        {
            var area = Track(new GameObject("start_area"));
            var box = area.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one * 4;
            area.AddComponent<DialogArea>();

            var nick = MakeSpeaker("Nick", new Vector3(20, 0, 0));
            nick.gameObject.AddComponent<SphereCollider>().radius = 0.5f;
            nick.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            yield return new WaitForFixedUpdate();

            nick.GetComponent<Rigidbody>().position = Vector3.zero;
            nick.transform.position = Vector3.zero;
            yield return WaitUntil(() => nick.Memory.TryGet("area", Time.timeAsDouble, out _), 2f);
            Assert.IsTrue(nick.Memory.TryGet("area", Time.timeAsDouble, out float value));
            Assert.AreEqual(Symbol.Of("start_area").AsValue, value);

            nick.transform.position = new Vector3(20, 0, 0);
            yield return WaitUntil(() => !nick.Memory.TryGet("area", Time.timeAsDouble, out _), 2f);
            Assert.IsFalse(nick.Memory.TryGet("area", Time.timeAsDouble, out _));
        }

        [UnityTest]
        public IEnumerator StaticFactsAndNearbyFacts_ReachQueries()
        {
            var rule = Rule("penguinNearBatman", "Thug", "Spot");
            rule.criteria.Add(new CriterionDefinition { key = "faction", value = "penguin" });
            rule.criteria.Add(new CriterionDefinition { key = "near_Batman", value = "1" });
            rule.criteria.Add(new CriterionDefinition { key = "nearby_speakers", op = CriterionOp.GreaterOrEqual, value = "1" });
            SetRules(rule, Rule("generic", "Thug", "Spot"));

            var thug = MakeSpeaker("Thug", Vector3.zero);
            thug.gameObject.AddComponent<StaticFacts>().SetFact("faction", "penguin");
            thug.gameObject.SetActive(false);
            thug.gameObject.AddComponent<NearbySpeakerFacts>();
            thug.gameObject.SetActive(true);
            MakeSpeaker("Batman", Vector3.forward);
            yield return null;

            Assert.AreEqual("penguinNearBatman", thug.Speak("Spot").Rule.Name);
        }
    }

    /// <summary>Tests for the serializable data classes. These don't need Play Mode, but live here because they use UnityEngine types.</summary>
    public class DataTests
    {
        sealed class SequenceRandom : IRandomSource
        {
            readonly System.Random r = new System.Random(11);
            public float NextFloat() => (float)r.NextDouble();
        }

        static DialogResponse ResponseWith(int count, ResponseSelection selection)
        {
            var r = new DialogResponse { selection = selection };
            for (int i = 0; i < count; i++) r.lines.Add(new DialogLineDefinition { text = "v" + i });
            return r;
        }

        [Test]
        public void RuleDefinition_CompilesShorthandAndCriteria()
        {
            var def = new RuleDefinition { name = "r", who = "Nick", concept = "TalkIdle", once = true, cooldown = 3 };
            def.criteria.Add(new CriterionDefinition { key = "survivors", op = CriterionOp.GreaterOrEqual, value = "3" });
            def.criteria.Add(new CriterionDefinition { key = "map", value = "swamp" });
            def.remember.Add(new FactWriteDefinition { key = "said_r", value = "1" });
            def.then.Add(new FollowupDefinition { target = FollowupTarget.Character, character = "Rochelle", concept = "Reply", delay = 1 });

            var rule = def.ToRule();
            Assert.AreEqual(4, rule.Score);
            Assert.AreEqual(RuleFlags.Once, rule.Flags);
            Assert.AreSame(def.response, rule.Response);
            Assert.AreEqual(Symbol.Of("Rochelle"), rule.Followups[0].Character);

            var q = new Query();
            q.Set("who", "Nick");
            q.Set("concept", "TalkIdle");
            q.Set("survivors", 3);
            q.Set("map", "swamp");
            Assert.IsTrue(rule.Matches(q));
            q.Set("survivors", 2);
            Assert.IsFalse(rule.Matches(q));
        }

        [Test]
        public void Validate_FlagsProblems()
        {
            var db = ScriptableObject.CreateInstance<DialogRuleDatabase>();
            db.rules.Add(new RuleDefinition { name = "dup", concept = "x" });
            db.rules.Add(new RuleDefinition { name = "dup", concept = "x" });
            db.rules[1].then.Add(new FollowupDefinition { target = FollowupTarget.Character, concept = "y" });
            var errors = db.Validate();
            Assert.That(errors, Has.Some.Contains("duplicate"));
            Assert.That(errors, Has.Some.Contains("names none"));
            Object.DestroyImmediate(db);
        }

        [Test]
        public void BrokenRule_IsSkippedWithAnError()
        {
            var db = ScriptableObject.CreateInstance<DialogRuleDatabase>();
            db.rules.Add(new RuleDefinition { name = "good", concept = "x" });
            var bad = new RuleDefinition { name = "bad", concept = "x" };
            bad.then.Add(new FollowupDefinition { target = FollowupTarget.Character, concept = "y" }); // no character
            db.rules.Add(bad);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("could not compile rule 'bad'"));
            Assert.AreEqual(1, db.Compile().Rules.Count);
            Object.DestroyImmediate(db);
        }

        [Test]
        public void RandomNoRepeat_NeverRepeats()
        {
            var r = ResponseWith(3, ResponseSelection.RandomNoRepeat);
            var rng = new SequenceRandom();
            var last = r.Pick(rng);
            for (int i = 0; i < 200; i++)
            {
                var next = r.Pick(rng);
                Assert.AreNotSame(last, next);
                last = next;
            }
        }

        [Test]
        public void Shuffle_PlaysEachOncePerCycle()
        {
            var r = ResponseWith(5, ResponseSelection.Shuffle);
            var rng = new SequenceRandom();
            for (int cycle = 0; cycle < 10; cycle++)
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < 5; i++) Assert.IsTrue(seen.Add(r.Pick(rng).text));
            }
        }

        [Test]
        public void Sequential_Loops()
        {
            var r = ResponseWith(3, ResponseSelection.Sequential);
            var rng = new SequenceRandom();
            var texts = new List<string>();
            for (int i = 0; i < 4; i++) texts.Add(r.Pick(rng).text);
            CollectionAssert.AreEqual(new[] { "v0", "v1", "v2", "v0" }, texts);
        }

        [Test]
        public void EmptyResponse_PicksNull()
        {
            Assert.IsNull(new DialogResponse().Pick(new SequenceRandom()));
        }
    }
}
