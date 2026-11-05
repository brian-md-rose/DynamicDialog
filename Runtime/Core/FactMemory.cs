using System;
using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>Game time in seconds. The Unity layer provides Time.time (or a pausable game clock).</summary>
    public interface IClock
    {
        double Now { get; }
    }

    /// <summary>A clock you advance by hand, for tests and tools.</summary>
    public sealed class ManualClock : IClock
    {
        public double Now { get; set; }
        public void Advance(double seconds) => Now += seconds;
    }

    /// <summary>One saved memory entry, keyed by name so it survives symbol IDs changing between runs.</summary>
    [Serializable]
    public struct FactMemoryEntry
    {
        public string Key;
        public float Value;

        /// <summary>
        /// Set when the value is a symbol: its name. Symbol IDs differ between runs, so a saved
        /// symbol is stored by name and looked up again on load.
        /// </summary>
        public string SymbolValue;

        /// <summary>Seconds left before it expires. 0 or less means it never expires.</summary>
        public float RemainingSeconds;
    }

    /// <summary>
    /// Persistent facts a character or the world remembers, written by code or by rules.
    /// Facts can expire, e.g. "Nick is speaking for the next 4 seconds" (talk slides 56 and 68).
    /// </summary>
    public sealed class FactMemory
    {
        struct Entry
        {
            public float Value;
            public double ExpiresAt; // +inf = permanent
            public bool IsSymbol;
        }

        readonly Dictionary<Symbol, Entry> entries = new Dictionary<Symbol, Entry>();
        readonly List<Symbol> expiredScratch = new List<Symbol>();

        /// <summary>Number of stored entries, including expired ones not yet purged.</summary>
        public int Count => entries.Count;

        public void Set(Symbol key, float value, double now, float duration = 0f, bool isSymbol = false)
        {
            entries[key] = new Entry { Value = value, ExpiresAt = Expiry(now, duration), IsSymbol = isSymbol };
        }

        public void Set(Symbol key, Symbol value, double now, float duration = 0f) => Set(key, value.AsValue, now, duration, true);

        public void Set(string key, float value, double now, float duration = 0f) => Set(Symbol.Of(key), value, now, duration);

        /// <summary>
        /// Adds to the stored value (a missing or expired fact counts as 0). A positive
        /// duration resets the expiry; otherwise an existing expiry is kept.
        /// </summary>
        public void Add(Symbol key, float delta, double now, float duration = 0f)
        {
            bool live = TryGetEntry(key, now, out Entry e);
            float value = (live ? e.Value : 0f) + delta;
            double expires = duration > 0f ? Expiry(now, duration) : live ? e.ExpiresAt : double.PositiveInfinity;
            entries[key] = new Entry { Value = value, ExpiresAt = expires };
        }

        public bool Remove(Symbol key) => entries.Remove(key);

        public void Clear() => entries.Clear();

        public bool TryGet(Symbol key, double now, out float value)
        {
            if (TryGetEntry(key, now, out Entry e))
            {
                value = e.Value;
                return true;
            }
            value = 0f;
            return false;
        }

        public bool TryGet(string key, double now, out float value) => TryGet(Symbol.Of(key), now, out value);

        /// <summary>Applies one write from a rule.</summary>
        public void Apply(in FactWrite write, double now)
        {
            switch (write.Op)
            {
                case FactOp.Set: Set(write.Key, write.Value, now, write.Duration, write.ValueIsSymbol); break;
                case FactOp.Add: Add(write.Key, write.Value, now, write.Duration); break;
                case FactOp.Remove: Remove(write.Key); break;
            }
        }

        /// <summary>Copies every live fact into the query and purges expired ones.</summary>
        public void WriteTo(Query query, double now)
        {
            expiredScratch.Clear();
            foreach (var kv in entries)
            {
                if (kv.Value.ExpiresAt <= now) expiredScratch.Add(kv.Key);
                else query.Set(kv.Key, kv.Value.Value, kv.Value.IsSymbol);
            }
            foreach (var k in expiredScratch) entries.Remove(k);
        }

        public List<FactMemoryEntry> Save(double now)
        {
            var list = new List<FactMemoryEntry>(entries.Count);
            foreach (var kv in entries)
            {
                if (kv.Value.ExpiresAt <= now) continue;
                list.Add(new FactMemoryEntry
                {
                    Key = kv.Key.Name,
                    Value = kv.Value.Value,
                    SymbolValue = kv.Value.IsSymbol && SymbolTable.TryFromValue(kv.Value.Value, out Symbol s) ? s.Name : null,
                    RemainingSeconds = double.IsPositiveInfinity(kv.Value.ExpiresAt) ? 0f : (float)(kv.Value.ExpiresAt - now),
                });
            }
            return list;
        }

        /// <summary>Replaces the contents with saved entries, re-basing expiry on <paramref name="now"/>.</summary>
        public void Load(IEnumerable<FactMemoryEntry> saved, double now)
        {
            entries.Clear();
            if (saved == null) return;
            foreach (var e in saved)
            {
                if (string.IsNullOrEmpty(e.Key)) continue;
                if (!string.IsNullOrEmpty(e.SymbolValue)) Set(Symbol.Of(e.Key), Symbol.Of(e.SymbolValue).AsValue, now, e.RemainingSeconds, true);
                else Set(Symbol.Of(e.Key), e.Value, now, e.RemainingSeconds);
            }
        }

        bool TryGetEntry(Symbol key, double now, out Entry entry)
        {
            if (entries.TryGetValue(key, out entry))
            {
                if (entry.ExpiresAt > now) return true;
                entries.Remove(key);
            }
            return false;
        }

        static double Expiry(double now, float duration) => duration > 0f ? now + duration : double.PositiveInfinity;
    }
}
