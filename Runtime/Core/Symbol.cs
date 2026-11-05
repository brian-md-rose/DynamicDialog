using System;
using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>
    /// An interned, case-insensitive identifier ("who", "Nick", "TalkIdle", ...).
    /// Symbols compare as integers, and their ID doubles as a fact value, so string equality
    /// becomes a numeric interval test like any other criterion (talk slides 52 and 116).
    /// </summary>
    public readonly struct Symbol : IEquatable<Symbol>, IComparable<Symbol>
    {
        /// <summary>Unique ID. 0 is <see cref="None"/>.</summary>
        public readonly int Id;

        internal Symbol(int id) { Id = id; }

        public static readonly Symbol None = default;

        public bool IsNone => Id == 0;

        /// <summary>The spelling the symbol was first interned with.</summary>
        public string Name => SymbolTable.NameOf(this);

        /// <summary>The symbol as a fact value. IDs stay below 2^24, so the float is exact.</summary>
        public float AsValue => Id;

        public static Symbol Of(string name) => SymbolTable.Intern(name);

        public bool Equals(Symbol other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Symbol s && s.Id == Id;
        public override int GetHashCode() => Id;
        public int CompareTo(Symbol other) => Id.CompareTo(other.Id);
        public static bool operator ==(Symbol a, Symbol b) => a.Id == b.Id;
        public static bool operator !=(Symbol a, Symbol b) => a.Id != b.Id;
        public override string ToString() => IsNone ? "<none>" : Name;
    }

    /// <summary>Process-wide symbol table. Thread-safe. Lookups ignore case.</summary>
    public static class SymbolTable
    {
        /// <summary>Largest ID that still round-trips exactly through a float fact value.</summary>
        public const int MaxId = 1 << 24;

        static readonly object Gate = new object();
        static readonly Dictionary<string, int> Ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        static readonly List<string> Names = new List<string> { null };

        public static Symbol Intern(string name)
        {
            if (string.IsNullOrEmpty(name)) return Symbol.None;
            lock (Gate)
            {
                if (Ids.TryGetValue(name, out int id)) return new Symbol(id);
                id = Names.Count;
                if (id >= MaxId) throw new InvalidOperationException("Symbol table is full.");
                Names.Add(name);
                Ids.Add(name, id);
                return new Symbol(id);
            }
        }

        /// <summary>Looks a name up without interning it.</summary>
        public static bool TryGet(string name, out Symbol symbol)
        {
            symbol = Symbol.None;
            if (string.IsNullOrEmpty(name)) return false;
            lock (Gate)
            {
                if (!Ids.TryGetValue(name, out int id)) return false;
                symbol = new Symbol(id);
                return true;
            }
        }

        public static string NameOf(Symbol symbol)
        {
            lock (Gate)
            {
                return symbol.Id > 0 && symbol.Id < Names.Count ? Names[symbol.Id] : null;
            }
        }

        /// <summary>Resolves a fact value back to a symbol, if it is a whole number that names one.</summary>
        public static bool TryFromValue(float value, out Symbol symbol)
        {
            symbol = Symbol.None;
            if (value < 1f || value >= MaxId || value != (int)value) return false;
            lock (Gate)
            {
                if ((int)value >= Names.Count) return false;
            }
            symbol = new Symbol((int)value);
            return true;
        }

        public static int Count
        {
            get { lock (Gate) return Names.Count - 1; }
        }
    }

    /// <summary>Fact keys the engine itself reads or writes.</summary>
    public static class WellKnownFacts
    {
        /// <summary>The kind of line being asked for ("TalkIdle", "OnSee", "OnHurt").</summary>
        public static readonly Symbol Concept = Symbol.Of("concept");

        /// <summary>The character doing the speaking.</summary>
        public static readonly Symbol Who = Symbol.Of("who");

        /// <summary>A fresh random number in [0, 100) on every query, for "rand &lt; 30" rules.</summary>
        public static readonly Symbol Random = Symbol.Of("rand");
    }
}
