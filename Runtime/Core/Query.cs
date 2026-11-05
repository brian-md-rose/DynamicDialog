using System;
using System.Collections.Generic;
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

        public Query(int capacity = 64)
        {
            capacity = Math.Max(4, capacity);
            keys = new Symbol[capacity];
            values = new float[capacity];
            symbolFlags = new bool[capacity];
        }

        public int Count => count;
        public Symbol KeyAt(int index) => keys[index];
        public float ValueAt(int index) => values[index];

        /// <summary>Whether the fact at <paramref name="index"/> was set from a symbol (a name), not a number.</summary>
        public bool IsSymbolAt(int index) => symbolFlags[index];

        public Symbol Concept
        {
            get => GetSymbol(WellKnownFacts.Concept);
            set => Set(WellKnownFacts.Concept, value);
        }

        public Symbol Who
        {
            get => GetSymbol(WellKnownFacts.Who);
            set => Set(WellKnownFacts.Who, value);
        }

        public void Clear() => count = 0;

        public void Set(Symbol key, float value) => Set(key, value, false);

        /// <param name="isSymbol">The value is a symbol ID. Only affects how tools display it.</param>
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

        public void Set(Symbol key, Symbol value) => Set(key, value.AsValue, true);
        public void Set(Symbol key, bool value) => Set(key, value ? 1f : 0f);
        public void Set(string key, float value) => Set(Symbol.Of(key), value);
        public void Set(string key, string symbolValue) => Set(Symbol.Of(key), Symbol.Of(symbolValue).AsValue, true);
        public void Set(string key, bool value) => Set(Symbol.Of(key), value);

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

        public bool Contains(Symbol key) => IndexOf(key) >= 0;

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

        public Symbol GetSymbol(Symbol key)
        {
            return TryGet(key, out float v) && SymbolTable.TryFromValue(v, out Symbol s) ? s : Symbol.None;
        }

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

        public Query Clone()
        {
            var q = new Query(Math.Max(4, count));
            CopyTo(q);
            return q;
        }

        /// <summary>The value at <paramref name="index"/> as text: a name for symbols and well-known keys, otherwise a number.</summary>
        public string FormatValueAt(int index) => FactValue.Format(keys[index], values[index], symbolFlags[index]);

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

    /// <summary>Tiny free-list for queries, so hot paths don't allocate.</summary>
    public static class QueryPool
    {
        [ThreadStatic] static Stack<Query> pool;

        public static Query Get()
        {
            var p = pool ?? (pool = new Stack<Query>());
            if (p.Count > 0)
            {
                var q = p.Pop();
                q.Clear();
                return q;
            }
            return new Query();
        }

        public static void Release(Query query)
        {
            if (query == null) return;
            query.Clear();
            (pool ?? (pool = new Stack<Query>())).Push(query);
        }
    }
}
