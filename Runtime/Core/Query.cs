using System;
using System.Text;

namespace DynamicDialog
{
    /// <summary>
    /// A flat table of facts (key symbol to float value), kept sorted by key ID so a rule's
    /// sorted criteria can be checked against it in a single pass (talk slides 50 and 104).
    /// Setting a key that already exists overwrites it, so build queries from the most
    /// general source to the most local one.
    /// </summary>
    public sealed class Query
    {
        Symbol[] keys;
        float[] values;
        bool[] symbolFlags;
        int count;

        /// <summary>Creates an empty query.</summary>
        /// <param name="capacity">How many facts it holds before it has to grow.</param>
        public Query(int capacity = 64)
        {
            capacity = Math.Max(4, capacity);
            keys = new Symbol[capacity];
            values = new float[capacity];
            symbolFlags = new bool[capacity];
        }

        /// <summary>Number of facts.</summary>
        public int Count => count;

        /// <summary>The key of the fact at <paramref name="index"/> (0 to <see cref="Count"/> - 1, in key-ID order).</summary>
        public Symbol KeyAt(int index) => keys[index];

        /// <summary>The value of the fact at <paramref name="index"/>.</summary>
        public float ValueAt(int index) => values[index];

        /// <summary>Whether the fact at <paramref name="index"/> was set from a symbol (a name), not a number.</summary>
        public bool IsSymbolAt(int index) => symbolFlags[index];

        /// <summary>The "concept" fact, or <see cref="Symbol.None"/>.</summary>
        public Symbol Concept
        {
            get => GetSymbol(WellKnownFacts.Concept);
            set => Set(WellKnownFacts.Concept, value);
        }

        /// <summary>The "who" fact, or <see cref="Symbol.None"/>.</summary>
        public Symbol Who
        {
            get => GetSymbol(WellKnownFacts.Who);
            set => Set(WellKnownFacts.Who, value);
        }

        /// <summary>Removes every fact.</summary>
        public void Clear() => count = 0;

        /// <summary>Sets a fact to a number, replacing any existing value.</summary>
        /// <exception cref="ArgumentException"><paramref name="key"/> is <see cref="Symbol.None"/>.</exception>
        public void Set(Symbol key, float value) => Set(key, value, false);

        /// <summary>Sets a fact, replacing any existing value.</summary>
        /// <param name="key">The fact's key.</param>
        /// <param name="value">The value (a symbol's <see cref="Symbol.AsValue"/> for names).</param>
        /// <param name="isSymbol">The value is a symbol ID. Only affects how tools display it.</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is <see cref="Symbol.None"/>.</exception>
        public void Set(Symbol key, float value, bool isSymbol)
        {
            if (key.IsNone) throw new ArgumentException("Fact key cannot be None.", nameof(key));
            int i = IndexOf(key);
            if (i >= 0)
            {
                values[i] = value;
                symbolFlags[i] = isSymbol;
                return;
            }
            i = ~i;
            if (count == keys.Length)
            {
                Array.Resize(ref keys, count * 2);
                Array.Resize(ref values, count * 2);
                Array.Resize(ref symbolFlags, count * 2);
            }
            if (i < count)
            {
                Array.Copy(keys, i, keys, i + 1, count - i);
                Array.Copy(values, i, values, i + 1, count - i);
                Array.Copy(symbolFlags, i, symbolFlags, i + 1, count - i);
            }
            keys[i] = key;
            values[i] = value;
            symbolFlags[i] = isSymbol;
            count++;
        }

        /// <summary>Sets a fact to a name.</summary>
        public void Set(Symbol key, Symbol value) => Set(key, value.AsValue, true);

        /// <summary>Sets a fact to 1 (true) or 0 (false).</summary>
        public void Set(Symbol key, bool value) => Set(key, value ? 1f : 0f);

        /// <summary>Sets a fact to a number.</summary>
        public void Set(string key, float value) => Set(Symbol.Of(key), value);

        /// <summary>Sets a fact to a name, e.g. <c>q.Set("object", "barrel")</c>.</summary>
        public void Set(string key, string symbolValue) => Set(Symbol.Of(key), Symbol.Of(symbolValue).AsValue, true);

        /// <summary>Sets a fact to 1 (true) or 0 (false).</summary>
        public void Set(string key, bool value) => Set(Symbol.Of(key), value);

        /// <summary>Removes a fact. Returns false if it wasn't there.</summary>
        public bool Remove(Symbol key)
        {
            int i = IndexOf(key);
            if (i < 0) return false;
            count--;
            if (i < count)
            {
                Array.Copy(keys, i + 1, keys, i, count - i);
                Array.Copy(values, i + 1, values, i, count - i);
                Array.Copy(symbolFlags, i + 1, symbolFlags, i, count - i);
            }
            return true;
        }

        /// <summary>Whether the query has a fact for <paramref name="key"/>.</summary>
        public bool Contains(Symbol key) => IndexOf(key) >= 0;

        /// <summary>Gets a fact's value. Returns false (and 0) if it isn't set.</summary>
        public bool TryGet(Symbol key, out float value)
        {
            int i = IndexOf(key);
            if (i >= 0)
            {
                value = values[i];
                return true;
            }
            value = 0f;
            return false;
        }

        /// <summary>A fact's value as a symbol, or <see cref="Symbol.None"/> if it isn't set or isn't a name.</summary>
        public Symbol GetSymbol(Symbol key)
        {
            return TryGet(key, out float v) && SymbolTable.TryFromValue(v, out Symbol s) ? s : Symbol.None;
        }

        /// <summary>Replaces the contents of <paramref name="other"/> with a copy of this query's facts.</summary>
        public void CopyTo(Query other)
        {
            if (other.keys.Length < count)
            {
                other.keys = new Symbol[keys.Length];
                other.values = new float[keys.Length];
                other.symbolFlags = new bool[keys.Length];
            }
            Array.Copy(keys, other.keys, count);
            Array.Copy(values, other.values, count);
            Array.Copy(symbolFlags, other.symbolFlags, count);
            other.count = count;
        }

        /// <summary>A new query with the same facts.</summary>
        public Query Clone()
        {
            var q = new Query(Math.Max(4, count));
            CopyTo(q);
            return q;
        }

        /// <summary>The value at <paramref name="index"/> as text: a name for symbols and well-known keys, otherwise a number.</summary>
        public string FormatValueAt(int index) => FactValue.Format(keys[index], values[index], symbolFlags[index]);

        /// <summary>Whether the fact for <paramref name="key"/> is set and was set from a symbol.</summary>
        public bool IsSymbol(Symbol key)
        {
            int i = IndexOf(key);
            return i >= 0 && symbolFlags[i];
        }

        /// <summary>Binary search. Returns the index, or the bitwise complement of the insertion point.</summary>
        int IndexOf(Symbol key)
        {
            int lo = 0, hi = count - 1;
            int id = key.Id;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int midId = keys[mid].Id;
                if (midId == id) return mid;
                if (midId < id) lo = mid + 1;
                else hi = mid - 1;
            }
            return ~lo;
        }

        /// <summary>Every fact as "key=value", separated by "; ".</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(keys[i].Name).Append('=').Append(FormatValueAt(i));
            }
            return sb.ToString();
        }
    }
}
