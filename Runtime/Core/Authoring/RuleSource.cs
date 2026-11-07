using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>How a response picks among its recorded variations (talk slide 65).</summary>
    public enum ResponseSelection
    {
        /// <summary>Uniformly random. May repeat.</summary>
        Random,

        /// <summary>Random, but never the same variation twice in a row.</summary>
        RandomNoRepeat,

        /// <summary>Plays every variation in a random order before any repeats.</summary>
        Shuffle,

        /// <summary>In authored order, looping.</summary>
        Sequential,
    }

    /// <summary>How serious an <see cref="AuthoringMessage"/> is.</summary>
    public enum MessageSeverity
    {
        /// <summary>A note; nothing is wrong.</summary>
        Info,
        /// <summary>Probably a mistake, but the rules still load.</summary>
        Warning,
        /// <summary>The statement or rule could not be read and was skipped.</summary>
        Error,
    }

    /// <summary>A problem found while reading or checking authored rules.</summary>
    public readonly struct AuthoringMessage
    {
        /// <summary>How serious it is.</summary>
        public readonly MessageSeverity Severity;
        /// <summary>What is wrong, in words a writer can act on.</summary>
        public readonly string Text;

        /// <summary>The source file, if known.</summary>
        public readonly string File;

        /// <summary>1-based line (or spreadsheet row), or 0 if unknown.</summary>
        public readonly int Line;

        /// <summary>The rule the message is about, if any.</summary>
        public readonly string Rule;

        /// <summary>Creates a message.</summary>
        public AuthoringMessage(MessageSeverity severity, string text, string file = null, int line = 0, string rule = null)
        {
            Severity = severity;
            Text = text;
            File = file;
            Line = line;
            Rule = rule;
        }

        /// <summary>"file(line): severity: [rule] text", Console style.</summary>
        public override string ToString()
        {
            string where = File != null ? (Line > 0 ? $"{File}({Line})" : File) : (Line > 0 ? $"line {Line}" : null);
            string prefix = where != null ? where + ": " : "";
            string rule = Rule != null ? $"[{Rule}] " : "";
            return $"{prefix}{Severity.ToString().ToLowerInvariant()}: {rule}{Text}";
        }
    }

    /// <summary>An authored criterion before compilation. Values stay as text until compiled.</summary>
    public sealed class CriterionSource
    {
        /// <summary>The fact tested.</summary>
        public string Key;
        /// <summary>The comparison.</summary>
        public CriterionOp Op;
        /// <summary>The value compared with, as text (the lower bound for <see cref="CriterionOp.Between"/>).</summary>
        public string Value;
        /// <summary>The upper bound, for <see cref="CriterionOp.Between"/>.</summary>
        public string Value2;
        /// <summary>Test <see cref="Default"/> when the fact is missing ("default" in .dlg).</summary>
        public bool HasDefault;
        /// <summary>The value tested for a missing fact, when <see cref="HasDefault"/> is set.</summary>
        public float Default;
    }

    /// <summary>One authored variation of a line ("say" in .dlg).</summary>
    public sealed class LineSource
    {
        /// <summary>Subtitle text. Null for an audio-only line.</summary>
        public string Text;
        /// <summary>An audio clip name or path, resolved by the Unity importer.</summary>
        public string Audio;
        /// <summary>Seconds the line lasts, or 0 for the clip's length or an estimate from the text.</summary>
        public float Duration;
        /// <summary>Animator trigger set when the line starts.</summary>
        public string Anim;

        /// <summary>Localization key. Empty means: the audio clip's name, else "&lt;rule&gt;_&lt;n&gt;".</summary>
        public string Key;
    }

    /// <summary>An authored fact write ("remember" or "forget" in .dlg).</summary>
    public sealed class FactWriteSource
    {
        /// <summary>Whose memory is written.</summary>
        public FactScope Scope = FactScope.Speaker;
        /// <summary>The character whose memory is written, for <see cref="FactScope.Character"/>.</summary>
        public string Character;
        /// <summary>The fact written.</summary>
        public string Key;
        /// <summary>How the fact changes.</summary>
        public FactOp Op = FactOp.Set;
        /// <summary>The value to set, or the amount to add, as text.</summary>
        public string Value = "1";
        /// <summary>Seconds until the fact expires. 0 means never.</summary>
        public float Duration;
    }

    /// <summary>An authored followup ("then" in .dlg).</summary>
    public sealed class FollowupSource
    {
        /// <summary>Who receives the concept.</summary>
        public FollowupTarget Target = FollowupTarget.Character;
        /// <summary>The receiver, for <see cref="FollowupTarget.Character"/>.</summary>
        public string Character;
        /// <summary>The concept sent.</summary>
        public string Concept;
        /// <summary>Extra seconds to wait after the line finishes.</summary>
        public float Delay;
    }

    /// <summary>An authored game event ("do" in .dlg).</summary>
    public sealed class DialogEventSource
    {
        /// <summary>The event's name.</summary>
        public string Name;
        /// <summary>Its arguments, as text.</summary>
        public readonly List<string> Args = new List<string>();
        /// <summary>When it is raised ("at end" in .dlg for <see cref="DialogEventTiming.LineEnded"/>).</summary>
        public DialogEventTiming Timing = DialogEventTiming.LineStarted;
    }

    /// <summary>
    /// One rule as written by a writer, in any format (.dlg text, spreadsheet, inspector).
    /// Parsers produce these; the Unity importer turns them into a <c>DialogRuleDatabase</c>.
    /// </summary>
    public sealed class RuleSource
    {
        /// <summary>The rule's name.</summary>
        public string Name;
        /// <summary>Shorthand for "who == X". Empty means anyone.</summary>
        public string Who;
        /// <summary>Shorthand for "concept == X".</summary>
        public string Concept;
        /// <summary>Conditions ("if").</summary>
        public readonly List<CriterionSource> Criteria = new List<CriterionSource>();
        /// <summary>How a variation is picked ("select").</summary>
        public ResponseSelection Selection = ResponseSelection.RandomNoRepeat;
        /// <summary>Variations of the line ("say").</summary>
        public readonly List<LineSource> Lines = new List<LineSource>();
        /// <summary>Facts written when the rule plays.</summary>
        public readonly List<FactWriteSource> Writes = new List<FactWriteSource>();
        /// <summary>Concepts sent when the line finishes.</summary>
        public readonly List<FollowupSource> Followups = new List<FollowupSource>();
        /// <summary>Game events raised when the rule plays ("do").</summary>
        public readonly List<DialogEventSource> Events = new List<DialogEventSource>();
        /// <summary>The rule plays only once.</summary>
        public bool Once;
        /// <summary>Seconds before it can play again.</summary>
        public float Cooldown;
        /// <summary>Relative chance among tied rules.</summary>
        public float Weight = 1f;
        /// <summary>Added to the criteria count to make the score ("bonus").</summary>
        public int ScoreBonus;
        /// <summary>Names the rule had before ("renamed"), so saved once and cooldown state still finds it.</summary>
        public readonly List<string> FormerNames = new List<string>();

        /// <summary>Where the rule starts in its source, for messages.</summary>
        public int SourceLine;

        /// <summary>Builds the core rule, with <paramref name="response"/> as its payload.</summary>
        public Rule ToRule(object response = null)
        {
            var criteria = new List<Criterion>(Criteria.Count + 2);
            if (!string.IsNullOrWhiteSpace(Who)) criteria.Add(Criterion.Eq(WellKnownFacts.Who, Symbol.Of(Who.Trim())));
            if (!string.IsNullOrWhiteSpace(Concept)) criteria.Add(Criterion.Eq(WellKnownFacts.Concept, Symbol.Of(Concept.Trim())));
            foreach (var c in Criteria)
            {
                float value = FactValue.Parse(c.Value, out bool valueIsSymbol);
                var built = FactValue.CreateCriterion(Symbol.Of(c.Key), c.Op, value, FactValue.Parse(c.Value2), valueIsSymbol);
                criteria.Add(c.HasDefault && c.Op != CriterionOp.ZeroOrMissing ? built.WhenMissing(c.Default) : built);
            }

            var writes = new List<FactWrite>(Writes.Count);
            foreach (var w in Writes)
            {
                float value = FactValue.Parse(w.Value, out bool valueIsSymbol);
                writes.Add(new FactWrite(w.Scope, Symbol.Of(w.Key), w.Op, value, w.Duration, Symbol.Of(w.Character), valueIsSymbol));
            }

            var followups = new List<Followup>(Followups.Count);
            foreach (var f in Followups)
                followups.Add(new Followup(f.Target, Symbol.Of(f.Concept), Symbol.Of(f.Character), f.Delay));

            var events = new List<DialogEvent>(Events.Count);
            foreach (var e in Events)
                events.Add(new DialogEvent(e.Name, e.Args, e.Timing));

            return new Rule(Name, criteria, response, writes, followups,
                Once ? RuleFlags.Once : RuleFlags.None, Cooldown, Weight, ScoreBonus, events, FormerNames);
        }
    }

    /// <summary>The result of reading one source file.</summary>
    public sealed class DialogSource
    {
        /// <summary>The rules read, in file order.</summary>
        public readonly List<RuleSource> Rules = new List<RuleSource>();
        /// <summary>Errors and warnings found while reading.</summary>
        public readonly List<AuthoringMessage> Messages = new List<AuthoringMessage>();

        /// <summary>Fact keys for partitioning, from a "@partition" directive. Empty means the default.</summary>
        public readonly List<string> PartitionKeys = new List<string>();

        /// <summary>True when any message is an error.</summary>
        public bool HasErrors
        {
            get
            {
                foreach (var m in Messages)
                    if (m.Severity == MessageSeverity.Error) return true;
                return false;
            }
        }
    }
}
