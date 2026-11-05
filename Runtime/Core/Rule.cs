using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicDialog
{
    /// <summary>Whose memory a rule writes a fact into.</summary>
    public enum FactScope : byte
    {
        /// <summary>The character who matched the rule.</summary>
        Speaker,

        /// <summary>The shared world memory.</summary>
        World,

        /// <summary>A named character (<see cref="FactWrite.Character"/>).</summary>
        Character,
    }

    public enum FactOp : byte
    {
        Set,
        /// <summary>Adds to the current value (a missing fact counts as 0).</summary>
        Add,
        Remove,
    }

    /// <summary>A fact a rule writes back to memory when it plays (talk slides 36 and 68).</summary>
    public readonly struct FactWrite
    {
        public readonly FactScope Scope;
        public readonly Symbol Character;
        public readonly Symbol Key;
        public readonly FactOp Op;
        public readonly float Value;

        /// <summary>Seconds until the fact expires. 0 or less means it never expires.</summary>
        public readonly float Duration;

        /// <summary>The value is a symbol (a name), so memory and tools keep treating it as one.</summary>
        public readonly bool ValueIsSymbol;

        public FactWrite(FactScope scope, Symbol key, FactOp op, float value, float duration = 0f, Symbol character = default, bool valueIsSymbol = false)
        {
            ValueIsSymbol = valueIsSymbol && op == FactOp.Set;
            if (scope == FactScope.Character && character.IsNone)
                throw new ArgumentException("A Character-scoped write needs a character.", nameof(character));
            Scope = scope;
            Character = character;
            Key = key;
            Op = op;
            Value = value;
            Duration = duration;
        }

        public override string ToString()
        {
            string target = Scope == FactScope.Character ? Character.ToString() : Scope.ToString().ToLowerInvariant();
            string op = Op == FactOp.Set ? $"= {FactValue.Format(Key, Value, ValueIsSymbol)}" : Op == FactOp.Add ? $"+= {Value}" : "removed";
            string dur = Duration > 0f ? $" for {Duration}s" : "";
            return $"{target}.{Key} {op}{dur}";
        }
    }

    /// <summary>Who receives a followup concept after a line finishes.</summary>
    public enum FollowupTarget : byte
    {
        /// <summary>The speaker who just finished (for splitting up monologues).</summary>
        Self,

        /// <summary>One named character (<see cref="Followup.Character"/>).</summary>
        Character,

        /// <summary>Everyone in earshot. Only the best-scoring reply plays (talk slides 73-76).</summary>
        Any,

        /// <summary>Everyone in earshot. Each one with a match plays.</summary>
        All,
    }

    /// <summary>
    /// A concept dispatched when a line finishes (talk slide 69). The receiver builds its query
    /// at that moment, not when the first line started, so a conversation ends on its own if
    /// the situation has changed (slide 78).
    /// </summary>
    public readonly struct Followup
    {
        public readonly FollowupTarget Target;
        public readonly Symbol Character;
        public readonly Symbol Concept;

        /// <summary>Extra seconds to wait after the line finishes.</summary>
        public readonly float Delay;

        public Followup(FollowupTarget target, Symbol concept, Symbol character = default, float delay = 0f)
        {
            if (target == FollowupTarget.Character && character.IsNone)
                throw new ArgumentException("A Character followup needs a character.", nameof(character));
            if (concept.IsNone) throw new ArgumentException("A followup needs a concept.", nameof(concept));
            Target = target;
            Character = character;
            Concept = concept;
            Delay = delay;
        }

        public override string ToString()
        {
            string who = Target == FollowupTarget.Character ? Character.ToString() : Target.ToString().ToLowerInvariant();
            return Delay > 0f ? $"then {who} {Concept} after {Delay}s" : $"then {who} {Concept}";
        }
    }

    [Flags]
    public enum RuleFlags : byte
    {
        None = 0,

        /// <summary>The rule disables itself after it plays once (talk slide 142).</summary>
        Once = 1,
    }

    /// <summary>Mutable per-session state for a rule. Saved and restored by rule name.</summary>
    public sealed class RuleRuntimeState
    {
        public bool Disabled;
        public int TimesFired;
        public double LastFiredTime = double.NegativeInfinity;

        public void Reset()
        {
            Disabled = false;
            TimesFired = 0;
            LastFiredTime = double.NegativeInfinity;
        }
    }

    /// <summary>
    /// A set of criteria that must all pass, plus what happens when the rule wins:
    /// a response payload, facts written to memory, and followups. Its score defaults to the
    /// number of criteria, so more specific rules beat general ones (talk slide 62).
    /// </summary>
    public sealed class Rule
    {
        public readonly string Name;

        /// <summary>Sorted by key ID so they can be checked against a query in one pass.</summary>
        public readonly Criterion[] Criteria;

        public readonly int Score;

        /// <summary>Relative chance when several rules tie on score. Default 1.</summary>
        public readonly float Weight;

        public readonly RuleFlags Flags;

        /// <summary>Seconds after playing before the rule can match again. 0 means no cooldown.</summary>
        public readonly float Cooldown;

        /// <summary>Whatever the game does when this rule wins: a response group, an action list, and so on.</summary>
        public readonly object Response;

        public readonly FactWrite[] Writes;
        public readonly Followup[] Followups;

        public readonly RuleRuntimeState State = new RuleRuntimeState();

        public Rule(string name, IEnumerable<Criterion> criteria, object response = null,
            IEnumerable<FactWrite> writes = null, IEnumerable<Followup> followups = null,
            RuleFlags flags = RuleFlags.None, float cooldown = 0f, float weight = 1f, int scoreBonus = 0)
        {
            Name = string.IsNullOrEmpty(name) ? "<unnamed>" : name;
            // OrderBy is a stable sort, so criteria sharing a key keep their authored order.
            Criteria = (criteria ?? Enumerable.Empty<Criterion>()).OrderBy(c => c.Key.Id).ToArray();
            if (Criteria.Any(c => c.Key.IsNone)) throw new ArgumentException($"Rule '{Name}' has a criterion with no key.");
            Score = Criteria.Length + scoreBonus;
            Weight = weight > 0f ? weight : 0f;
            Flags = flags;
            Cooldown = cooldown;
            Response = response;
            Writes = writes?.ToArray() ?? Array.Empty<FactWrite>();
            Followups = followups?.ToArray() ?? Array.Empty<Followup>();
        }

        /// <summary>Whether once/cooldown state lets the rule match at <paramref name="now"/>.</summary>
        public bool IsAvailable(double now)
        {
            if (State.Disabled) return false;
            return Cooldown <= 0f || now - State.LastFiredTime >= Cooldown;
        }

        /// <summary>Tests the criteria against a query (ignores once/cooldown state).</summary>
        public bool Matches(Query query) => FirstFailedCriterion(query) < 0;

        /// <summary>
        /// Walks the sorted criteria and the sorted query together (talk slide 104).
        /// Returns the index of the first failing criterion, or -1 if all pass.
        /// </summary>
        public int FirstFailedCriterion(Query query)
        {
            int qi = 0, qn = query.Count;
            for (int ci = 0; ci < Criteria.Length; ci++)
            {
                ref readonly Criterion c = ref Criteria[ci];
                int id = c.Key.Id;
                while (qi < qn && query.KeyAt(qi).Id < id) qi++;
                bool present = qi < qn && query.KeyAt(qi).Id == id;
                if (!c.Test(present, present ? query.ValueAt(qi) : 0f)) return ci;
            }
            return -1;
        }

        /// <summary>Records that the rule played.</summary>
        public void MarkFired(double now)
        {
            State.TimesFired++;
            State.LastFiredTime = now;
            if ((Flags & RuleFlags.Once) != 0) State.Disabled = true;
        }

        public override string ToString() => $"{Name} (score {Score})";
    }

    /// <summary>Fluent helper for building rules in code and tests.</summary>
    public sealed class RuleBuilder
    {
        readonly string name;
        readonly List<Criterion> criteria = new List<Criterion>();
        readonly List<FactWrite> writes = new List<FactWrite>();
        readonly List<Followup> followups = new List<Followup>();
        object response;
        RuleFlags flags;
        float cooldown;
        float weight = 1f;
        int scoreBonus;

        public RuleBuilder(string name) { this.name = name; }

        public RuleBuilder Who(string who) => Where(Criterion.Eq(WellKnownFacts.Who, Symbol.Of(who)));
        public RuleBuilder Concept(string concept) => Where(Criterion.Eq(WellKnownFacts.Concept, Symbol.Of(concept)));
        public RuleBuilder Where(Criterion criterion) { criteria.Add(criterion); return this; }

        public RuleBuilder Response(object payload) { response = payload; return this; }

        public RuleBuilder Remember(string key, float value, float duration = 0f) =>
            Write(new FactWrite(FactScope.Speaker, Symbol.Of(key), FactOp.Set, value, duration));

        public RuleBuilder RememberWorld(string key, float value, float duration = 0f) =>
            Write(new FactWrite(FactScope.World, Symbol.Of(key), FactOp.Set, value, duration));

        public RuleBuilder Increment(string key, float amount = 1f) =>
            Write(new FactWrite(FactScope.Speaker, Symbol.Of(key), FactOp.Add, amount));

        public RuleBuilder Write(FactWrite write) { writes.Add(write); return this; }

        public RuleBuilder Then(string character, string concept, float delay = 0f) =>
            Then(new Followup(FollowupTarget.Character, Symbol.Of(concept), Symbol.Of(character), delay));

        public RuleBuilder ThenSelf(string concept, float delay = 0f) =>
            Then(new Followup(FollowupTarget.Self, Symbol.Of(concept), default, delay));

        public RuleBuilder ThenAny(string concept, float delay = 0f) =>
            Then(new Followup(FollowupTarget.Any, Symbol.Of(concept), default, delay));

        public RuleBuilder ThenAll(string concept, float delay = 0f) =>
            Then(new Followup(FollowupTarget.All, Symbol.Of(concept), default, delay));

        public RuleBuilder Then(Followup followup) { followups.Add(followup); return this; }

        public RuleBuilder Once() { flags |= RuleFlags.Once; return this; }
        public RuleBuilder Cooldown(float seconds) { cooldown = seconds; return this; }
        public RuleBuilder Weight(float w) { weight = w; return this; }
        public RuleBuilder ScoreBonus(int bonus) { scoreBonus = bonus; return this; }

        public Rule Build() => new Rule(name, criteria, response, writes, followups, flags, cooldown, weight, scoreBonus);
    }
}
