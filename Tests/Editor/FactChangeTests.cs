using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    /// <summary>Fact-change notifications from <see cref="FactMemory"/> and <see cref="DialogEngine"/>.</summary>
    public class FactChangeTests
    {
        ManualClock clock;
        DialogEngine engine;
        List<FactChange> changes;
        int resets;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualClock();
            engine = new DialogEngine(clock, new SystemRandomSource(1));
            changes = new List<FactChange>();
            resets = 0;
            engine.FactChanged += c => changes.Add(c);
            engine.FactsReset += () => resets++;
        }

        [Test]
        public void Set_ReportsOldAndNewValues_AndSameValueIsSilent()
        {
            var key = Symbol.Of("crutch_returned");
            engine.World.Set(key, 1f, clock.Now);
            engine.World.Set(key, 1f, clock.Now);
            engine.World.Set(key, 2f, clock.Now);

            Assert.AreEqual(2, changes.Count);
            Assert.IsTrue(changes[0].IsWorld);
            Assert.AreEqual(FactChangeKind.Set, changes[0].Kind);
            Assert.IsFalse(changes[0].HadOldValue);
            Assert.AreEqual(1f, changes[0].NewValue);
            Assert.IsTrue(changes[1].HadOldValue);
            Assert.AreEqual(1f, changes[1].OldValue);
            Assert.AreEqual(2f, changes[1].NewValue);
            Assert.AreEqual("world.crutch_returned: 1 -> 2", changes[1].ToString());
        }

        [Test]
        public void CharacterMemory_SaysWhose()
        {
            var mei = engine.GetOrCreateCharacter("Mei");
            mei.Memory.Set(Symbol.Of("mood"), Symbol.Of("happy"), clock.Now);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(Symbol.Of("Mei"), changes[0].Character);
            Assert.IsFalse(changes[0].IsWorld);
            Assert.AreSame(mei.Memory, changes[0].Memory);
            Assert.AreEqual(Symbol.Of("happy"), changes[0].NewSymbol);
        }

        [Test]
        public void AddAndRemove()
        {
            var key = Symbol.Of("chats");
            engine.World.Add(key, 1f, clock.Now);
            engine.World.Add(key, 0f, clock.Now); // no change
            engine.World.Add(key, 2f, clock.Now);
            engine.World.Remove(key);
            engine.World.Remove(key); // already gone

            Assert.AreEqual(new[] { FactChangeKind.Set, FactChangeKind.Set, FactChangeKind.Removed }, changes.Select(c => c.Kind).ToArray());
            Assert.AreEqual(3f, changes[1].NewValue);
            Assert.AreEqual(3f, changes[2].OldValue);
            Assert.IsFalse(changes[2].HasNewValue);
        }

        [Test]
        public void RuleWritesAreReported()
        {
            engine.Matcher.Add(new RuleSet("test", new[]
            {
                new RuleBuilder("Give").Who("Jun").Concept("OnTalk").RememberWorld("crutch_returned", 1).Increment("thanks").Build(),
            }));
            var jun = engine.GetOrCreateCharacter("Jun");
            engine.Commit(engine.Ask(jun, "OnTalk"));

            Assert.AreEqual(2, changes.Count);
            Assert.IsTrue(changes.Any(c => c.IsWorld && c.Key == Symbol.Of("crutch_returned")));
            Assert.IsTrue(changes.Any(c => c.Character == Symbol.Of("Jun") && c.Key == Symbol.Of("thanks")));
        }

        [Test]
        public void Expiry_IsReportedByPurge_AndExpiredFactsStopCountingAtOnce()
        {
            var key = Symbol.Of("speaking");
            engine.World.Set(key, 1f, clock.Now, duration: 4f);
            changes.Clear();

            clock.Advance(3);
            engine.PurgeExpiredFacts();
            Assert.AreEqual(0, changes.Count, "not due yet");

            clock.Advance(2);
            var q = new Query();
            engine.World.WriteTo(q, clock.Now);
            Assert.IsFalse(q.Contains(key), "an expired fact is gone for queries before any purge");
            Assert.AreEqual(0, changes.Count, "building a query reports nothing");

            engine.PurgeExpiredFacts();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(FactChangeKind.Expired, changes[0].Kind);
            Assert.AreEqual(1f, changes[0].OldValue);
            Assert.AreEqual(0, engine.World.Count);

            engine.PurgeExpiredFacts();
            Assert.AreEqual(1, changes.Count, "reported once");
        }

        [Test]
        public void Expiry_IsReportedByTheFirstRead_IfThatComesBeforeThePurge()
        {
            var key = Symbol.Of("speaking");
            engine.World.Set(key, 1f, clock.Now, duration: 1f);
            changes.Clear();
            clock.Advance(2);
            Assert.IsFalse(engine.World.TryGet(key, clock.Now, out _));
            Assert.AreEqual(FactChangeKind.Expired, changes.Single().Kind);
            engine.PurgeExpiredFacts();
            Assert.AreEqual(1, changes.Count, "reported once");
        }

        [Test]
        public void ResettingAfterExpiry_IsANewFact()
        {
            var key = Symbol.Of("speaking");
            engine.World.Set(key, 1f, clock.Now, duration: 1f);
            clock.Advance(2);
            engine.World.Set(key, 1f, clock.Now, duration: 1f);
            Assert.AreEqual(new[] { FactChangeKind.Set, FactChangeKind.Expired, FactChangeKind.Set }, changes.Select(c => c.Kind).ToArray(),
                "the second set isn't silent: the old value had expired");
            Assert.IsFalse(changes[2].HadOldValue);
        }

        [Test]
        public void LoadState_RaisesOneReset_NotOneChangePerFact()
        {
            engine.World.Set(Symbol.Of("a"), 1f, clock.Now);
            engine.GetOrCreateCharacter("Mei").Memory.Set(Symbol.Of("b"), 2f, clock.Now);
            engine.GetOrCreateCharacter("Jun").Memory.Set(Symbol.Of("c"), 3f, clock.Now);
            var save = engine.SaveState();
            changes.Clear();

            engine.LoadState(save);
            Assert.AreEqual(1, resets);
            Assert.AreEqual(0, changes.Count);

            engine.World.Clear();
            Assert.AreEqual(2, resets, "clearing one memory outside a load resets too");
        }

        [Test]
        public void RemovedCharacters_StopReporting()
        {
            var mei = engine.GetOrCreateCharacter("Mei");
            Assert.IsTrue(engine.RemoveCharacter(Symbol.Of("Mei")));
            mei.Memory.Set(Symbol.Of("x"), 1f, clock.Now);
            Assert.AreEqual(0, changes.Count);
        }

        [Test]
        public void StandaloneMemory_HasNoOwner()
        {
            var memory = new FactMemory();
            FactChange? seen = null;
            memory.Changed += c => seen = c;
            memory.Set("x", 1f, 0);
            Assert.IsTrue(seen.HasValue);
            Assert.IsTrue(seen.Value.IsWorld);
            Assert.IsTrue(memory.Owner.IsNone);
        }
    }
}
