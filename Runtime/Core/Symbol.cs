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

        /// <summary>The empty symbol (ID 0). Returned for null or empty names.</summary>
        public static readonly Symbol None = default;

        /// <summary>True for <see cref="None"/>.</summary>
        public bool IsNone => Id == 0;

        /// <summary>The spelling the symbol was first interned with, or null for <see cref="None"/>.</summary>
        public string Name => SymbolTable.NameOf(this);

        /// <summary>The symbol as a fact value. IDs stay below 2^24, so the float is exact.</summary>
        public float AsValue => Id;

        /// <summary>Returns the symbol for <paramref name="name"/>, interning it if it is new. Case is ignored.</summary>
        public static Symbol Of(string name) => SymbolTable.Intern(name);

        /// <summary>True when both symbols have the same ID.</summary>
        public bool Equals(Symbol other) => Id == other.Id;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Symbol s && s.Id == Id;

        /// <inheritdoc/>
        public override int GetHashCode() => Id;

        /// <summary>Orders symbols by ID (the order they were interned in), not alphabetically.</summary>
        public int CompareTo(Symbol other) => Id.CompareTo(other.Id);

        /// <summary>True when both symbols have the same ID.</summary>
        public static bool operator ==(Symbol a, Symbol b) => a.Id == b.Id;

        /// <summary>True when the symbols have different IDs.</summary>
        public static bool operator !=(Symbol a, Symbol b) => a.Id != b.Id;

        /// <summary>The symbol's name, or "&lt;none&gt;".</summary>
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

        /// <summary>Returns the symbol for <paramref name="name"/>, adding it if it is new. Same as <see cref="Symbol.Of"/>.</summary>
        /// <exception cref="InvalidOperationException">The table already holds <see cref="MaxId"/> symbols.</exception>
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
        /// <returns>False when the name has never been interned.</returns>
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

        /// <summary>The name a symbol was interned with, or null for <see cref="Symbol.None"/> or an unknown ID.</summary>
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

        /// <summary>How many symbols have been interned.</summary>
        public static int Count
        {
            get { lock (Gate) return Names.Count - 1; }
        }
    }

    /// <summary>Fact keys the engine and the bundled components read or write.</summary>
    public static class WellKnownFacts
    {
        /// <summary>"concept": the kind of line being asked for ("TalkIdle", "OnSee", "OnHurt").</summary>
        public static readonly Symbol Concept = Symbol.Of("concept");

        /// <summary>"who": the character doing the speaking.</summary>
        public static readonly Symbol Who = Symbol.Of("who");

        /// <summary>"rand": a fresh random number in [0, 100) on every query, for "rand &lt; 30" rules.</summary>
        public static readonly Symbol Random = Symbol.Of("rand");

        /// <summary>"from": on a followup's query, the character whose line sent it.</summary>
        public static readonly Symbol From = Symbol.Of("from");

        /// <summary>"map": the active scene's name, added by the Dialog Director.</summary>
        public static readonly Symbol Map = Symbol.Of("map");

        /// <summary>"speakers_talking": how many lines are playing, added by the Dialog Director.</summary>
        public static readonly Symbol SpeakersTalking = Symbol.Of("speakers_talking");

        /// <summary>"object": what a Dialog Vision saw, on its OnSee query.</summary>
        public static readonly Symbol Object = Symbol.Of("object");

        /// <summary>"object_distance": how far away the seen object is, on a Dialog Vision's OnSee query.</summary>
        public static readonly Symbol ObjectDistance = Symbol.Of("object_distance");

        /// <summary>"area": the Dialog Area a speaker is standing in, kept in its memory.</summary>
        public static readonly Symbol Area = Symbol.Of("area");

        /// <summary>"nearby_speakers": how many other speakers are in earshot, added by Nearby Speaker Facts.</summary>
        public static readonly Symbol NearbySpeakers = Symbol.Of("nearby_speakers");

        /// <summary>"region": the active region's name, added by Dialog Region and Dialog Addressable Region.</summary>
        public static readonly Symbol Region = Symbol.Of("region");

        /// <summary>Prefix of the "near_&lt;who&gt;" facts that Nearby Speaker Facts adds for each speaker in earshot.</summary>
        public const string NearPrefix = "near_";
    }
}
