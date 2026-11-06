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

    /// <summary>How a <see cref="FactWrite"/> changes the stored fact.</summary>
    public enum FactOp : byte
    {
        /// <summary>Replaces the value.</summary>
        Set,

        /// <summary>Adds to the current value (a missing fact counts as 0).</summary>
        Add,

        /// <summary>Deletes the fact.</summary>
        Remove,
    }

    /// <summary>A fact a rule writes back to memory when it plays (talk slides 36 and 68).</summary>
    public readonly struct FactWrite
    {
        /// <summary>Whose memory is written.</summary>
        public readonly FactScope Scope;

        /// <summary>The character whose memory is written, for <see cref="FactScope.Character"/>.</summary>
        public readonly Symbol Character;

        /// <summary>The fact written.</summary>
        public readonly Symbol Key;

        /// <summary>How the fact changes.</summary>
        public readonly FactOp Op;

        /// <summary>The value to set, or the amount to add.</summary>
        public readonly float Value;

        /// <summary>Seconds until the fact expires. 0 or less means it never expires.</summary>
        public readonly float Duration;

        /// <summary>The value is a symbol (a name), so memory and tools keep treating it as one.</summary>
        public readonly bool ValueIsSymbol;

        /// <summary>Creates a write.</summary>
        /// <param name="scope">Whose memory to write.</param>
        /// <param name="key">The fact to write.</param>
        /// <param name="op">How to change it.</param>
        /// <param name="value">The value to set, or the amount to add.</param>
        /// <param name="duration">Seconds until it expires. 0 means never.</param>
        /// <param name="character">The character, required for <see cref="FactScope.Character"/>.</param>
        /// <param name="valueIsSymbol">The value is a name. Only kept for <see cref="FactOp.Set"/>.</param>
        /// <exception cref="ArgumentException">A <see cref="FactScope.Character"/> write has no character.</exception>
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

        /// <summary>The write in readable form, e.g. "world.gossip += 1".</summary>
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
        /// <summary>Who receives the concept.</summary>
        public readonly FollowupTarget Target;

        /// <summary>The receiver, for <see cref="FollowupTarget.Character"/>.</summary>
        public readonly Symbol Character;

        /// <summary>The concept the receiver is asked for.</summary>
        public readonly Symbol Concept;

        /// <summary>Extra seconds to wait after the line finishes.</summary>
        public readonly float Delay;

        /// <summary>Creates a followup.</summary>
        /// <param name="target">Who receives it.</param>
        /// <param name="concept">The concept to send.</param>
        /// <param name="character">The receiver, required for <see cref="FollowupTarget.Character"/>.</param>
        /// <param name="delay">Extra seconds to wait after the line finishes.</param>
        /// <exception cref="ArgumentException">The concept is missing, or a character followup names no character.</exception>
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

        /// <summary>The followup in .dlg form, e.g. "then Rochelle Reply after 1s".</summary>
        public override string ToString()
        {
            string who = Target == FollowupTarget.Character ? Character.ToString() : Target.ToString().ToLowerInvariant();
            return Delay > 0f ? $"then {who} {Concept} after {Delay}s" : $"then {who} {Concept}";
        }
    }

    /// <summary>When a rule's <see cref="DialogEvent"/> is raised.</summary>
    public enum DialogEventTiming : byte
    {
        /// <summary>When the line starts, right after the rule's facts are written. The default.</summary>
        LineStarted,

        /// <summary>When the line ends, if it played to the end ("do Name at end" in .dlg). Dropped if the line is stopped early, like followups.</summary>
        LineEnded,
    }

    /// <summary>
    /// A named game event a rule raises when it plays ("do GiveItem glasses" in .dlg), so game code can
    /// react to dialog without matching rule names. Arguments are plain text; use <see cref="Arg"/>,
    /// <see cref="FloatArg"/> or <see cref="IntArg"/> to read them.
    /// </summary>
    public readonly struct DialogEvent
    {
        static readonly string[] NoArgs = Array.Empty<string>();
        readonly string[] args;

        /// <summary>The event's name, e.g. "GiveItem". Handlers match it ignoring case.</summary>
        public readonly string Name;

        /// <summary>When the event is raised.</summary>
        public readonly DialogEventTiming Timing;

        /// <summary>Creates an event.</summary>
        /// <param name="name">The event's name.</param>
        /// <param name="args">Its arguments, as text. May be null.</param>
        /// <param name="timing">When it is raised.</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        public DialogEvent(string name, IEnumerable<string> args = null, DialogEventTiming timing = DialogEventTiming.LineStarted)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An event needs a name.", nameof(name));
            Name = name.Trim();
            this.args = args?.Select(a => a ?? "").ToArray() ?? NoArgs;
            Timing = timing;
        }

        /// <summary>The arguments, as written.</summary>
        public IReadOnlyList<string> Args => args ?? NoArgs;

        /// <summary>How many arguments there are.</summary>
        public int ArgCount => args?.Length ?? 0;

        /// <summary>Argument <paramref name="index"/>, or <paramref name="fallback"/> if there are fewer arguments.</summary>
        public string Arg(int index, string fallback = null) =>
            args != null && index >= 0 && index < args.Length ? args[index] : fallback;

        /// <summary>Argument <paramref name="index"/> as a number, or <paramref name="fallback"/> if it is missing or not a number.</summary>
        public float FloatArg(int index, float fallback = 0f) =>
            float.TryParse(Arg(index), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;

        /// <summary>Argument <paramref name="index"/> as a whole number, or <paramref name="fallback"/> if it is missing or not one.</summary>
        public int IntArg(int index, int fallback = 0) =>
            int.TryParse(Arg(index), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int v) ? v : fallback;

        /// <summary>Whether this event is called <paramref name="name"/> (ignoring case).</summary>
        public bool Is(string name) => string.Equals(Name, name?.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>The event in .dlg form, e.g. "do GiveItem glasses" or "do StartWalking at end".</summary>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder("do ").Append(Name);
            foreach (var a in Args) sb.Append(' ').Append(a.Length == 0 || a.Any(char.IsWhiteSpace) ? "\"" + a + "\"" : a);
            if (Timing == DialogEventTiming.LineEnded) sb.Append(" at end");
            return sb.ToString();
        }
    }

    /// <summary>Options on a rule.</summary>
    [Flags]
    public enum RuleFlags : byte
    {
        /// <summary>No options.</summary>
        None = 0,

        /// <summary>The rule disables itself after it plays once (talk slide 142).</summary>
        Once = 1,
    }

    /// <summary>
    /// Per-session state for a rule: whether a once rule has been used, how often it played, and when.
    /// Saved and restored by rule name. The engine updates it; game code reads it.
    /// </summary>
    public sealed class RuleRuntimeState
    {
        /// <summary>True once a <see cref="RuleFlags.Once"/> rule has played.</summary>
        public bool Disabled { get; internal set; }

        /// <summary>How many times the rule has played this session.</summary>
        public int TimesFired { get; internal set; }

        /// <summary>Game time the rule last played, or negative infinity if it never has.</summary>
        public double LastFiredTime { get; internal set; } = double.NegativeInfinity;

        /// <summary>Forgets that the rule ever played.</summary>
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
        /// <summary>The rule's name. Save data and the debugger identify rules by it.</summary>
        public readonly string Name;

        /// <summary>Sorted by key ID so they can be checked against a query in one pass.</summary>
        public readonly Criterion[] Criteria;

        /// <summary>The number of criteria plus any score bonus. Higher scores win.</summary>
        public readonly int Score;

        /// <summary>Relative chance when several rules tie on score. Default 1.</summary>
        public readonly float Weight;

        /// <summary>Options, such as <see cref="RuleFlags.Once"/>.</summary>
        public readonly RuleFlags Flags;

        /// <summary>Seconds after playing before the rule can match again. 0 means no cooldown.</summary>
        public readonly float Cooldown;

        /// <summary>Whatever the game does when this rule wins: a response group, an action list, and so on.</summary>
        public readonly object Response;

        /// <summary>Facts written to memory when the rule plays.</summary>
        public readonly FactWrite[] Writes;

        /// <summary>Concepts sent when the rule's line finishes.</summary>
        public readonly Followup[] Followups;

        /// <summary>Game events raised when the rule plays ("do" in .dlg), in authored order.</summary>
        public readonly DialogEvent[] Events;

        /// <summary>
        /// Names the rule had before it was renamed ("renamed" in .dlg). Save data written under one of
        /// these names is applied to this rule, so renaming a rule keeps its once and cooldown state.
        /// </summary>
        public readonly string[] FormerNames;

        /// <summary>Once and cooldown state for this session.</summary>
        public readonly RuleRuntimeState State = new RuleRuntimeState();

        /// <summary>Creates a rule. <see cref="RuleBuilder"/> is usually easier.</summary>
        /// <param name="name">The rule's name.</param>
        /// <param name="criteria">Conditions that must all pass.</param>
        /// <param name="response">The payload returned when the rule wins.</param>
        /// <param name="writes">Facts written when it plays.</param>
        /// <param name="followups">Concepts sent when its line finishes.</param>
        /// <param name="flags">Options, such as <see cref="RuleFlags.Once"/>.</param>
        /// <param name="cooldown">Seconds before it can play again.</param>
        /// <param name="weight">Relative chance among tied rules. Negative values count as 0.</param>
        /// <param name="scoreBonus">Added to the criteria count to make the score.</param>
        /// <param name="events">Game events raised when it plays.</param>
        /// <param name="formerNames">Names the rule had before, so saves made under them still apply. Blank names are ignored.</param>
        /// <exception cref="ArgumentException">A criterion has no key.</exception>
        public Rule(string name, IEnumerable<Criterion> criteria, object response = null,
            IEnumerable<FactWrite> writes = null, IEnumerable<Followup> followups = null,
            RuleFlags flags = RuleFlags.None, float cooldown = 0f, float weight = 1f, int scoreBonus = 0,
            IEnumerable<DialogEvent> events = null, IEnumerable<string> formerNames = null)
        {
            Events = events?.ToArray() ?? Array.Empty<DialogEvent>();
            FormerNames = formerNames?.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToArray() ?? Array.Empty<string>();
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

        /// <summary>Whether <paramref name="name"/> is one of <see cref="FormerNames"/> (ignoring case).</summary>
        public bool WasNamed(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();
            foreach (var n in FormerNames)
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Records that the rule played.</summary>
        internal void MarkFired(double now)
        {
            State.TimesFired++;
            State.LastFiredTime = now;
            if ((Flags & RuleFlags.Once) != 0) State.Disabled = true;
        }

        /// <summary>The name and score, e.g. "NickSwampIntro (score 5)".</summary>
        public override string ToString() => $"{Name} (score {Score})";
    }

    /// <summary>Fluent helper for building rules in code and tests.</summary>
    public sealed class RuleBuilder
    {
        readonly string name;
        readonly List<Criterion> criteria = new List<Criterion>();
        readonly List<FactWrite> writes = new List<FactWrite>();
        readonly List<Followup> followups = new List<Followup>();
        readonly List<DialogEvent> events = new List<DialogEvent>();
        readonly List<string> formerNames = new List<string>();
        object response;
        RuleFlags flags;
        float cooldown;
        float weight = 1f;
        int scoreBonus;

        /// <summary>Starts a rule called <paramref name="name"/>.</summary>
        public RuleBuilder(string name) { this.name = name; }

        /// <summary>Adds "who == <paramref name="who"/>".</summary>
        public RuleBuilder Who(string who) => Where(Criterion.Eq(WellKnownFacts.Who, Symbol.Of(who)));

        /// <summary>Adds "concept == <paramref name="concept"/>".</summary>
        public RuleBuilder Concept(string concept) => Where(Criterion.Eq(WellKnownFacts.Concept, Symbol.Of(concept)));

        /// <summary>Adds a criterion.</summary>
        public RuleBuilder Where(Criterion criterion) { criteria.Add(criterion); return this; }

        /// <summary>Sets the payload returned when the rule wins.</summary>
        public RuleBuilder Response(object payload) { response = payload; return this; }

        /// <summary>Sets a fact in the speaker's memory when the rule plays.</summary>
        public RuleBuilder Remember(string key, float value, float duration = 0f) =>
            Write(new FactWrite(FactScope.Speaker, Symbol.Of(key), FactOp.Set, value, duration));

        /// <summary>Sets a fact in world memory when the rule plays.</summary>
        public RuleBuilder RememberWorld(string key, float value, float duration = 0f) =>
            Write(new FactWrite(FactScope.World, Symbol.Of(key), FactOp.Set, value, duration));

        /// <summary>Adds <paramref name="amount"/> to a fact in the speaker's memory when the rule plays.</summary>
        public RuleBuilder Increment(string key, float amount = 1f) =>
            Write(new FactWrite(FactScope.Speaker, Symbol.Of(key), FactOp.Add, amount));

        /// <summary>Adds any fact write.</summary>
        public RuleBuilder Write(FactWrite write) { writes.Add(write); return this; }

        /// <summary>Sends <paramref name="concept"/> to <paramref name="character"/> when the line finishes.</summary>
        public RuleBuilder Then(string character, string concept, float delay = 0f) =>
            Then(new Followup(FollowupTarget.Character, Symbol.Of(concept), Symbol.Of(character), delay));

        /// <summary>Sends <paramref name="concept"/> back to the speaker when the line finishes.</summary>
        public RuleBuilder ThenSelf(string concept, float delay = 0f) =>
            Then(new Followup(FollowupTarget.Self, Symbol.Of(concept), default, delay));

        /// <summary>Sends <paramref name="concept"/> to everyone in earshot; the best reply plays.</summary>
        public RuleBuilder ThenAny(string concept, float delay = 0f) =>
            Then(new Followup(FollowupTarget.Any, Symbol.Of(concept), default, delay));

        /// <summary>Sends <paramref name="concept"/> to everyone in earshot; each one with a match plays.</summary>
        public RuleBuilder ThenAll(string concept, float delay = 0f) =>
            Then(new Followup(FollowupTarget.All, Symbol.Of(concept), default, delay));

        /// <summary>Adds any followup.</summary>
        public RuleBuilder Then(Followup followup) { followups.Add(followup); return this; }

        /// <summary>Raises the game event <paramref name="name"/> when the line starts.</summary>
        public RuleBuilder Do(string name, params string[] args) => Do(new DialogEvent(name, args));

        /// <summary>Raises the game event <paramref name="name"/> when the line ends, if it played to the end.</summary>
        public RuleBuilder DoAtEnd(string name, params string[] args) => Do(new DialogEvent(name, args, DialogEventTiming.LineEnded));

        /// <summary>Adds any game event.</summary>
        public RuleBuilder Do(DialogEvent dialogEvent) { events.Add(dialogEvent); return this; }

        /// <summary>Names the rule had before, so saves made under them still apply ("renamed" in .dlg).</summary>
        public RuleBuilder Renamed(params string[] names) { if (names != null) formerNames.AddRange(names); return this; }

        /// <summary>The rule can play only once.</summary>
        public RuleBuilder Once() { flags |= RuleFlags.Once; return this; }

        /// <summary>Seconds after playing before the rule can match again.</summary>
        public RuleBuilder Cooldown(float seconds) { cooldown = seconds; return this; }

        /// <summary>Relative chance when the rule ties with others on score.</summary>
        public RuleBuilder Weight(float w) { weight = w; return this; }

        /// <summary>Added to the criteria count to make the score.</summary>
        public RuleBuilder ScoreBonus(int bonus) { scoreBonus = bonus; return this; }

        /// <summary>Creates the rule.</summary>
        public Rule Build() => new Rule(name, criteria, response, writes, followups, flags, cooldown, weight, scoreBonus, events, formerNames);
    }
}
