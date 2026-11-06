using System;
using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>Game time in seconds. The Unity layer provides Time.time (or a pausable game clock).</summary>
    public interface IClock
    {
        /// <summary>The current game time in seconds.</summary>
        double Now { get; }
    }

    /// <summary>A clock you advance by hand, for tests and tools.</summary>
    public sealed class ManualClock : IClock
    {
        /// <summary>The current time. Set it directly or call <see cref="Advance"/>.</summary>
        public double Now { get; set; }
        /// <summary>Moves time forward.</summary>
        public void Advance(double seconds) => Now += seconds;
    }

    /// <summary>One saved memory entry, keyed by name so it survives symbol IDs changing between runs.</summary>
    [Serializable]
    public struct FactMemoryEntry
    {
        /// <summary>The fact's name.</summary>
        public string Key;
        /// <summary>The fact's value (ignored on load when <see cref="SymbolValue"/> is set).</summary>
        public float Value;

        /// <summary>
        /// Set when the value is a symbol: its name. Symbol IDs differ between runs, so a saved
        /// symbol is stored by name and looked up again on load.
        /// </summary>
        public string SymbolValue;

        /// <summary>Seconds left before it expires. 0 or less means it never expires.</summary>
        public float RemainingSeconds;
    }

    /// <summary>How a fact changed, in a <see cref="FactChange"/>.</summary>
    public enum FactChangeKind : byte
    {
        /// <summary>The fact was stored or its value changed (set or added to).</summary>
        Set,

        /// <summary>The fact was forgotten (<see cref="FactMemory.Remove"/>, or "forget" in a rule).</summary>
        Removed,

        /// <summary>The fact's time ran out ("remember x = 1 for 4s").</summary>
        Expired,
    }

    /// <summary>
    /// One change to a fact in a <see cref="FactMemory"/>, made by a rule or by code.
    /// Raised by <see cref="FactMemory.Changed"/> and <see cref="DialogEngine.FactChanged"/>.
    /// </summary>
    public readonly struct FactChange
    {
        /// <summary>The memory that changed.</summary>
        public readonly FactMemory Memory;

        /// <summary>The fact.</summary>
        public readonly Symbol Key;

        /// <summary>What happened to it.</summary>
        public readonly FactChangeKind Kind;

        /// <summary>Whether the fact had a value before the change.</summary>
        public readonly bool HadOldValue;

        /// <summary>The value before the change (0 if there was none).</summary>
        public readonly float OldValue;

        /// <summary>The old value is a symbol.</summary>
        public readonly bool OldValueIsSymbol;

        /// <summary>The value after the change. 0 when the fact was removed or expired.</summary>
        public readonly float NewValue;

        /// <summary>The new value is a symbol (see <see cref="NewSymbol"/>).</summary>
        public readonly bool NewValueIsSymbol;

        /// <summary>Creates a change. <see cref="FactMemory"/> makes these; you rarely need to.</summary>
        /// <param name="memory">The memory that changed.</param>
        /// <param name="key">The fact.</param>
        /// <param name="kind">What happened to it.</param>
        /// <param name="hadOldValue">Whether it had a value before.</param>
        /// <param name="oldValue">The value before.</param>
        /// <param name="oldValueIsSymbol">The value before is a symbol.</param>
        /// <param name="newValue">The value after.</param>
        /// <param name="newValueIsSymbol">The value after is a symbol.</param>
        public FactChange(FactMemory memory, Symbol key, FactChangeKind kind, bool hadOldValue, float oldValue, bool oldValueIsSymbol,
            float newValue, bool newValueIsSymbol)
        {
            Memory = memory;
            Key = key;
            Kind = kind;
            HadOldValue = hadOldValue;
            OldValue = oldValue;
            OldValueIsSymbol = oldValueIsSymbol;
            NewValue = newValue;
            NewValueIsSymbol = newValueIsSymbol;
        }

        /// <summary>The character whose memory changed, or <see cref="Symbol.None"/> for world memory.</summary>
        public Symbol Character => Memory != null ? Memory.Owner : Symbol.None;

        /// <summary>True for a change to world memory (or to a memory no character owns).</summary>
        public bool IsWorld => Character.IsNone;

        /// <summary>Whether the fact has a value after the change.</summary>
        public bool HasNewValue => Kind == FactChangeKind.Set;

        /// <summary>The new value as a symbol, or <see cref="Symbol.None"/> if it isn't one.</summary>
        public Symbol NewSymbol => NewValueIsSymbol && SymbolTable.TryFromValue(NewValue, out Symbol s) ? s : Symbol.None;

        /// <summary>The change in readable form, e.g. "world.crutch_returned: missing -> 1".</summary>
        public override string ToString()
        {
            string owner = IsWorld ? "world" : Character.ToString();
            string before = HadOldValue ? FactValue.Format(Key, OldValue, OldValueIsSymbol) : "missing";
            string after = Kind == FactChangeKind.Set ? FactValue.Format(Key, NewValue, NewValueIsSymbol)
                : Kind == FactChangeKind.Expired ? "expired" : "removed";
            return $"{owner}.{Key}: {before} -> {after}";
        }
    }

    /// <summary>
    /// Persistent facts a character or the world remembers, written by code or by rules.
    /// Facts can expire, e.g. "Nick is speaking for the next 4 seconds" (talk slides 56 and 68).
    /// </summary>
    /// <remarks>
    /// <see cref="Changed"/> reports every change that alters a value, whether a rule or code made it;
    /// setting a fact to the value it already has is silent. An expired fact stops counting at once, and
    /// is removed and reported as <see cref="FactChangeKind.Expired"/> by <see cref="PurgeExpired"/> (which
    /// the Dialog Director calls every frame) or by the first read or write of that fact, whichever comes
    /// first. Building a query never reports anything. <see cref="Clear"/> and <see cref="Load"/> raise
    /// <see cref="Reset"/> instead of one change per fact.
    /// </remarks>
    public sealed class FactMemory
    {
        struct Entry
        {
            public float Value;
            public double ExpiresAt; // +inf = permanent
            public bool IsSymbol;
        }

        readonly Dictionary<Symbol, Entry> entries = new Dictionary<Symbol, Entry>();
        readonly List<KeyValuePair<Symbol, Entry>> expiredScratch = new List<KeyValuePair<Symbol, Entry>>();
        double nextExpiry = double.PositiveInfinity;
        // The latest time this memory has been told about, so Remove can tell an expired entry from a live one.
        double lastNow = double.NegativeInfinity;

        /// <summary>Creates an empty memory that no character owns.</summary>
        public FactMemory() { }

        internal FactMemory(Symbol owner) { Owner = owner; }

        /// <summary>The character this memory belongs to, or <see cref="Symbol.None"/> for world memory.</summary>
        public Symbol Owner { get; }

        /// <summary>Raised after a fact's value changes, or it is removed or expires.</summary>
        public event Action<FactChange> Changed;

        /// <summary>Raised after <see cref="Clear"/> or <see cref="Load"/> replaces the whole memory.</summary>
        public event Action<FactMemory> Reset;

        /// <summary>Number of stored entries, including expired ones not yet purged.</summary>
        public int Count => entries.Count;

        /// <summary>Stores a fact, replacing any existing value.</summary>
        /// <param name="key">The fact.</param>
        /// <param name="value">Its value.</param>
        /// <param name="now">The current game time.</param>
        /// <param name="duration">Seconds until it expires. 0 means never.</param>
        /// <param name="isSymbol">The value is a symbol ID.</param>
        public void Set(Symbol key, float value, double now, float duration = 0f, bool isSymbol = false)
        {
            bool had = TryGetEntry(key, now, out Entry old);
            Store(key, new Entry { Value = value, ExpiresAt = Expiry(now, duration), IsSymbol = isSymbol });
            if (!had || old.Value != value || old.IsSymbol != isSymbol)
                Raise(key, FactChangeKind.Set, had, old, value, isSymbol);
        }

        /// <summary>Stores a name as a fact's value.</summary>
        public void Set(Symbol key, Symbol value, double now, float duration = 0f) => Set(key, value.AsValue, now, duration, true);

        /// <summary>Stores a fact, replacing any existing value.</summary>
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
            Store(key, new Entry { Value = value, ExpiresAt = expires });
            if (!live || e.Value != value || e.IsSymbol)
                Raise(key, FactChangeKind.Set, live, e, value, false);
        }

        /// <summary>Forgets a fact. Returns false if it wasn't stored.</summary>
        public bool Remove(Symbol key)
        {
            if (!entries.TryGetValue(key, out Entry old)) return false;
            entries.Remove(key);
            // An entry whose time already ran out was waiting for PurgeExpired: report it as expired.
            Raise(key, old.ExpiresAt <= lastNow ? FactChangeKind.Expired : FactChangeKind.Removed, true, old, 0f, false);
            return true;
        }

        /// <summary>Forgets every fact. Raises <see cref="Reset"/>.</summary>
        public void Clear()
        {
            entries.Clear();
            nextExpiry = double.PositiveInfinity;
            Reset?.Invoke(this);
        }

        /// <summary>Gets a fact that hasn't expired. Returns false (and 0) if there is none.</summary>
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

        /// <summary>Gets a fact that hasn't expired. Returns false (and 0) if there is none.</summary>
        public bool TryGet(string key, double now, out float value) => TryGet(Symbol.Of(key), now, out value);

        /// <summary>Gets a fact that hasn't expired, and whether its value is a symbol. Returns false (and 0) if there is none.</summary>
        public bool TryGet(Symbol key, double now, out float value, out bool isSymbol)
        {
            if (TryGetEntry(key, now, out Entry e))
            {
                value = e.Value;
                isSymbol = e.IsSymbol;
                return true;
            }
            value = 0f;
            isSymbol = false;
            return false;
        }

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

        /// <summary>Copies every live fact into the query. Expired facts are skipped (<see cref="PurgeExpired"/> removes them).</summary>
        public void WriteTo(Query query, double now)
        {
            if (now > lastNow) lastNow = now;
            foreach (var kv in entries)
                if (kv.Value.ExpiresAt > now) query.Set(kv.Key, kv.Value.Value, kv.Value.IsSymbol);
        }

        /// <summary>
        /// Removes facts whose time has run out, raising <see cref="Changed"/> with
        /// <see cref="FactChangeKind.Expired"/> for each. Cheap when nothing is due. Returns how many expired.
        /// </summary>
        public int PurgeExpired(double now)
        {
            if (now > lastNow) lastNow = now;
            if (now < nextExpiry) return 0;
            expiredScratch.Clear();
            double next = double.PositiveInfinity;
            foreach (var kv in entries)
            {
                if (kv.Value.ExpiresAt <= now) expiredScratch.Add(kv);
                else if (kv.Value.ExpiresAt < next) next = kv.Value.ExpiresAt;
            }
            nextExpiry = next;
            foreach (var kv in expiredScratch) entries.Remove(kv.Key);

            // Raise after removing, so a handler sees the memory as it now is. Copy first: a handler may purge again.
            int count = expiredScratch.Count;
            if (count == 0 || Changed == null)
            {
                expiredScratch.Clear();
                return count;
            }
            var expired = expiredScratch.ToArray();
            expiredScratch.Clear();
            foreach (var kv in expired) Raise(kv.Key, FactChangeKind.Expired, true, kv.Value, 0f, false);
            return count;
        }

        /// <summary>Every live fact, with expiry stored as time remaining, for a save game.</summary>
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

        /// <summary>Replaces the contents with saved entries, re-basing expiry on <paramref name="now"/>. Raises <see cref="Reset"/>.</summary>
        public void Load(IEnumerable<FactMemoryEntry> saved, double now)
        {
            entries.Clear();
            nextExpiry = double.PositiveInfinity;
            if (now > lastNow) lastNow = now;
            if (saved != null)
            {
                foreach (var e in saved)
                {
                    if (string.IsNullOrEmpty(e.Key)) continue;
                    bool isSymbol = !string.IsNullOrEmpty(e.SymbolValue);
                    float value = isSymbol ? Symbol.Of(e.SymbolValue).AsValue : e.Value;
                    Store(Symbol.Of(e.Key), new Entry { Value = value, ExpiresAt = Expiry(now, e.RemainingSeconds), IsSymbol = isSymbol });
                }
            }
            Reset?.Invoke(this);
        }

        bool TryGetEntry(Symbol key, double now, out Entry entry)
        {
            if (now > lastNow) lastNow = now;
            if (entries.TryGetValue(key, out entry))
            {
                if (entry.ExpiresAt > now) return true;
                // Reading an expired fact purges it, and that is when it's reported.
                entries.Remove(key);
                Raise(key, FactChangeKind.Expired, true, entry, 0f, false);
            }
            entry = default;
            return false;
        }

        void Store(Symbol key, Entry entry)
        {
            entries[key] = entry;
            if (entry.ExpiresAt < nextExpiry) nextExpiry = entry.ExpiresAt;
        }

        void Raise(Symbol key, FactChangeKind kind, bool had, in Entry old, float newValue, bool newIsSymbol) =>
            Changed?.Invoke(new FactChange(this, key, kind, had, had ? old.Value : 0f, had && old.IsSymbol, newValue, newIsSymbol));

        static double Expiry(double now, float duration) => duration > 0f ? now + duration : double.PositiveInfinity;
    }
}
