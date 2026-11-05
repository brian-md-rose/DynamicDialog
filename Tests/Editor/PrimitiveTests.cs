using NUnit.Framework;

namespace DynamicDialog.Tests
{
    public class SymbolTests
    {
        [Test]
        public void Interning_IsCaseInsensitive_AndKeepsFirstSpelling()
        {
            var a = Symbol.Of("SymbolTests_Nick");
            var b = Symbol.Of("symboltests_NICK");
            Assert.AreEqual(a, b);
            Assert.AreEqual("SymbolTests_Nick", b.Name);
        }

        [Test]
        public void EmptyName_IsNone()
        {
            Assert.IsTrue(Symbol.Of("").IsNone);
            Assert.IsTrue(Symbol.Of(null).IsNone);
        }

        [Test]
        public void SymbolValue_RoundTrips()
        {
            var s = Symbol.Of("SymbolTests_Rochelle");
            Assert.IsTrue(SymbolTable.TryFromValue(s.AsValue, out var back));
            Assert.AreEqual(s, back);
            Assert.IsFalse(SymbolTable.TryFromValue(1.5f, out _));
            Assert.IsFalse(SymbolTable.TryFromValue(0f, out _));
        }
    }

    public class FloatMathTests
    {
        [Test]
        public void NextUp_And_NextDown_AreAdjacent()
        {
            Assert.Greater(FloatMath.NextUp(5f), 5f);
            Assert.Less(FloatMath.NextDown(5f), 5f);
            Assert.AreEqual(float.Epsilon, FloatMath.NextUp(0f));
            Assert.AreEqual(float.Epsilon, FloatMath.NextUp(-0f));
            Assert.AreEqual(-float.Epsilon, FloatMath.NextDown(0f));
            Assert.AreEqual(0f, FloatMath.NextUp(-float.Epsilon));
            Assert.AreEqual(-float.MaxValue, FloatMath.NextUp(float.NegativeInfinity));
            Assert.AreEqual(float.PositiveInfinity, FloatMath.NextUp(float.MaxValue));
            Assert.Greater(FloatMath.NextUp(-3f), -3f);
        }
    }

    public class CriterionTests
    {
        static readonly Symbol Hp = Symbol.Of("hp");

        [Test]
        public void ComparisonsAreIntervals()
        {
            Assert.IsTrue(Criterion.Eq(Hp, 5).Test(true, 5));
            Assert.IsFalse(Criterion.Eq(Hp, 5).Test(true, 5.0001f));

            Assert.IsFalse(Criterion.Gt(Hp, 5).Test(true, 5));
            Assert.IsTrue(Criterion.Gt(Hp, 5).Test(true, FloatMath.NextUp(5)));
            Assert.IsTrue(Criterion.Ge(Hp, 5).Test(true, 5));

            Assert.IsFalse(Criterion.Lt(Hp, 5).Test(true, 5));
            Assert.IsTrue(Criterion.Lt(Hp, 5).Test(true, 4.9f));
            Assert.IsTrue(Criterion.Le(Hp, 5).Test(true, 5));

            Assert.IsTrue(Criterion.Between(Hp, 30, 60).Test(true, 30));
            Assert.IsTrue(Criterion.Between(Hp, 30, 60).Test(true, 60));
            Assert.IsFalse(Criterion.Between(Hp, 30, 60).Test(true, 61));
        }

        [Test]
        public void NotEqual_IsInvertedInterval()
        {
            var c = Criterion.NotEq(Hp, 5);
            Assert.IsFalse(c.Test(true, 5));
            Assert.IsTrue(c.Test(true, 6));
        }

        [Test]
        public void MissingFact_RejectsByDefault_EvenForNotEqual()
        {
            Assert.IsFalse(Criterion.Eq(Hp, 0).Test(false, 0));
            Assert.IsFalse(Criterion.NotEq(Hp, 5).Test(false, 0));
            Assert.IsFalse(Criterion.Exists(Hp).Test(false, 0));
            Assert.IsTrue(Criterion.Exists(Hp).Test(true, -1000));
        }

        [Test]
        public void ZeroOrMissing_PassesWhenAbsent()
        {
            var c = Criterion.ZeroOrMissing(Hp);
            Assert.IsTrue(c.Test(false, 0));
            Assert.IsTrue(c.Test(true, 0));
            Assert.IsFalse(c.Test(true, 1));
        }

        [Test]
        public void NaN_NeverMatches()
        {
            Assert.IsFalse(Criterion.Exists(Hp).Test(true, float.NaN));
            Assert.IsFalse(Criterion.Ge(Hp, 0).Test(true, float.NaN));
        }

        [Test]
        public void SymbolEquality()
        {
            var c = Criterion.Eq("map", "swamp");
            Assert.IsTrue(c.Test(true, Symbol.Of("SWAMP").AsValue));
            Assert.IsFalse(c.Test(true, Symbol.Of("mall").AsValue));
            Assert.IsTrue(c.IsExactMatch(out _));
            Assert.IsFalse(c.WhenMissing(0).IsExactMatch(out _));
        }
    }

    public class QueryTests
    {
        [Test]
        public void KeysStaySorted_AndSetOverwrites()
        {
            var q = new Query(4);
            for (int i = 0; i < 20; i++) q.Set(Symbol.Of("QueryTests_k" + (19 - i)), i);
            q.Set(Symbol.Of("QueryTests_k3"), 100);

            Assert.AreEqual(20, q.Count);
            for (int i = 1; i < q.Count; i++) Assert.Less(q.KeyAt(i - 1).Id, q.KeyAt(i).Id);
            Assert.IsTrue(q.TryGet(Symbol.Of("QueryTests_k3"), out float v));
            Assert.AreEqual(100f, v);
        }

        [Test]
        public void Remove_And_Clone()
        {
            var q = new Query();
            q.Set("a", 1);
            q.Set("b", 2);
            var copy = q.Clone();
            Assert.IsTrue(q.Remove(Symbol.Of("a")));
            Assert.IsFalse(q.Contains(Symbol.Of("a")));
            Assert.IsTrue(copy.Contains(Symbol.Of("a")));
        }

        [Test]
        public void WhoAndConcept_RoundTripAsSymbols()
        {
            var q = new Query();
            q.Who = Symbol.Of("Nick");
            q.Concept = Symbol.Of("TalkIdle");
            Assert.AreEqual(Symbol.Of("nick"), q.Who);
            Assert.AreEqual(Symbol.Of("talkidle"), q.Concept);
        }
    }

    public class FactMemoryTests
    {
        [Test]
        public void FactsExpire()
        {
            var m = new FactMemory();
            m.Set("speaking", 1, now: 10, duration: 4);
            Assert.IsTrue(m.TryGet("speaking", 13.9, out _));
            Assert.IsFalse(m.TryGet("speaking", 14, out _));
            Assert.AreEqual(0, m.Count, "expired entries are purged on read");
        }

        [Test]
        public void Add_StartsFromZero_AndKeepsExpiry()
        {
            var m = new FactMemory();
            m.Add(Symbol.Of("seen_barrels"), 1, now: 0);
            m.Add(Symbol.Of("seen_barrels"), 1, now: 1);
            Assert.IsTrue(m.TryGet("seen_barrels", 2, out float v));
            Assert.AreEqual(2f, v);

            m.Set("streak", 5, now: 0, duration: 10);
            m.Add(Symbol.Of("streak"), 1, now: 5);
            Assert.IsTrue(m.TryGet("streak", 9.9, out v));
            Assert.AreEqual(6f, v);
            Assert.IsFalse(m.TryGet("streak", 10, out _));
        }

        [Test]
        public void WriteTo_SkipsExpired()
        {
            var m = new FactMemory();
            m.Set("a", 1, 0);
            m.Set("b", 1, 0, duration: 1);
            var q = new Query();
            m.WriteTo(q, now: 5);
            Assert.IsTrue(q.Contains(Symbol.Of("a")));
            Assert.IsFalse(q.Contains(Symbol.Of("b")));
        }

        [Test]
        public void SaveAndLoad_RebaseExpiry()
        {
            var m = new FactMemory();
            m.Set("permanent", 3, now: 100);
            m.Set("temporary", 1, now: 100, duration: 10);
            var saved = m.Save(now: 104);

            var loaded = new FactMemory();
            loaded.Load(saved, now: 0);
            Assert.IsTrue(loaded.TryGet("permanent", 1e9, out float p));
            Assert.AreEqual(3f, p);
            Assert.IsTrue(loaded.TryGet("temporary", 5.9, out _));
            Assert.IsFalse(loaded.TryGet("temporary", 6.1, out _));
        }
    }
}
