using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DynamicDialog
{
    /// <summary>
    /// Reads the .dlg text format: one rule per block, one statement per line.
    /// <code>
    /// # Comments start with # or //
    /// @who Nick                     # default speaker for the rules below ("@who" alone clears it)
    /// @concept TalkIdle             # default concept
    /// @partition concept who        # fact keys to bucket rules by (optional)
    ///
    /// rule NickSwampIntro
    ///   who Nick
    ///   concept TalkIdle
    ///   if map == swamp and survivors >= 3
    ///   if said_intro missing        # 0 or not set
    ///   if health in 30..60
    ///   if zombies > 2 default 0     # a missing fact counts as 0
    ///   select shuffle               # random | norepeat | shuffle | sequential
    ///   say "Man, this swamp." audio nick_swamp_01 duration 3 anim Shrug
    ///   say "Cesspool of disease, is what this is."
    ///   remember said_intro = 1
    ///   remember speaking = 1 for 4s
    ///   remember world gossip += 1
    ///   remember Ellis heard_it = 1   # another character's memory
    ///   forget area
    ///   then Rochelle SwampReply after 1s   # or: then self|any|all Concept
    ///   do GiveItem glasses          # a game event with arguments, raised when the line starts
    ///   do StartWalking at end       # raised when the line ends (dropped if it is interrupted)
    ///   once
    ///   cooldown 30s
    ///   weight 2
    ///   bonus 1
    /// </code>
    /// </summary>
    public static class DialogScriptParser
    {
        /// <summary>Reads .dlg text. Statements with errors are skipped and reported; the rest still load.</summary>
        /// <param name="text">The file's contents.</param>
        /// <param name="fileName">Used in messages. May be null.</param>
        public static DialogSource Parse(string text, string fileName = null)
        {
            var result = new DialogSource();
            new Reader(result, fileName).Run(text ?? "");
            return result;
        }

        sealed class Reader
        {
            readonly DialogSource result;
            readonly string file;
            readonly HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RuleSource rule;
            string defaultWho;
            string defaultConcept;
            int lineNo;

            public Reader(DialogSource result, string file)
            {
                this.result = result;
                this.file = file;
            }

            public void Run(string text)
            {
                var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    lineNo = i + 1;
                    List<Token> tokens;
                    try
                    {
                        tokens = Tokenizer.Tokenize(lines[i]);
                    }
                    catch (FormatException e)
                    {
                        Error(e.Message);
                        continue;
                    }
                    if (tokens.Count == 0) continue;
                    try
                    {
                        Statement(new TokenStream(tokens));
                    }
                    catch (FormatException e)
                    {
                        Error(e.Message);
                    }
                }
                FinishRule();
            }

            void Statement(TokenStream ts)
            {
                var head = ts.Next();
                string word = head.IsString ? null : head.Text.ToLowerInvariant();

                if (word != null && word.StartsWith("@"))
                {
                    Directive(word, ts);
                    return;
                }

                if (word == "rule")
                {
                    FinishRule();
                    if (ts.AtEnd) throw new FormatException("'rule' needs a name.");
                    string name = ts.Next().Text;
                    ts.ExpectEnd("rule name");
                    rule = new RuleSource { Name = name, Who = defaultWho, Concept = defaultConcept, SourceLine = lineNo };
                    if (!names.Add(name)) Warning($"Rule '{name}' is defined more than once.");
                    return;
                }

                if (rule == null) throw new FormatException($"'{head.Text}' must be inside a rule. Start one with 'rule Name'.");

                switch (word)
                {
                    case "who":
                        rule.Who = ClearableName(ts, "who");
                        break;
                    case "concept":
                        rule.Concept = ClearableName(ts, "concept");
                        break;
                    case "if":
                    case "and":
                        foreach (var c in Syntax.ParseCriteria(ts)) rule.Criteria.Add(c);
                        break;
                    case "say":
                        rule.Lines.Add(Say(ts));
                        break;
                    case "select":
                        rule.Selection = Syntax.ParseSelection(ts.Next().Text);
                        ts.ExpectEnd("select");
                        break;
                    case "remember":
                        rule.Writes.Add(Syntax.ParseRemember(ts));
                        break;
                    case "forget":
                        rule.Writes.Add(Syntax.ParseForget(ts));
                        break;
                    case "then":
                        rule.Followups.Add(Syntax.ParseThen(ts));
                        break;
                    case "do":
                        rule.Events.Add(Syntax.ParseDo(ts));
                        break;
                    case "once":
                        rule.Once = true;
                        ts.ExpectEnd("once");
                        break;
                    case "cooldown":
                        rule.Cooldown = Syntax.ParseSeconds(ts.Next().Text);
                        ts.ExpectEnd("cooldown");
                        break;
                    case "weight":
                        rule.Weight = Syntax.ParseFloat(ts.Next().Text, "weight");
                        ts.ExpectEnd("weight");
                        break;
                    case "bonus":
                        rule.ScoreBonus = (int)Syntax.ParseFloat(ts.Next().Text, "bonus");
                        ts.ExpectEnd("bonus");
                        break;
                    default:
                        throw new FormatException($"Unknown statement '{head.Text}'. Expected who, concept, if, say, select, remember, forget, then, do, once, cooldown, weight or bonus.");
                }
            }

            void Directive(string word, TokenStream ts)
            {
                switch (word)
                {
                    case "@who":
                        defaultWho = ts.AtEnd ? null : ts.Next().Text;
                        ts.ExpectEnd("@who");
                        break;
                    case "@concept":
                        defaultConcept = ts.AtEnd ? null : ts.Next().Text;
                        ts.ExpectEnd("@concept");
                        break;
                    case "@partition":
                        result.PartitionKeys.Clear();
                        while (!ts.AtEnd) result.PartitionKeys.Add(ts.Next().Text);
                        break;
                    default:
                        throw new FormatException($"Unknown directive '{word}'. Expected @who, @concept or @partition.");
                }
            }

            static string ClearableName(TokenStream ts, string what)
            {
                if (ts.AtEnd) throw new FormatException($"'{what}' needs a name. Use '{what} any' to clear a default.");
                string v = ts.Next().Text;
                ts.ExpectEnd(what);
                return string.Equals(v, "any", StringComparison.OrdinalIgnoreCase) || v == "*" ? null : v;
            }

            static LineSource Say(TokenStream ts)
            {
                if (ts.AtEnd) throw new FormatException("'say' needs the line's text in quotes.");
                var line = new LineSource();
                var first = ts.Peek();
                if (first.IsString)
                {
                    line.Text = ts.Next().Text;
                }
                else if (!IsSayOption(first.Text))
                {
                    throw new FormatException("Put the line's text in double quotes: say \"Like this.\"");
                }

                while (!ts.AtEnd)
                {
                    string option = ts.Next().Text.ToLowerInvariant();
                    if (ts.AtEnd) throw new FormatException($"'{option}' needs a value.");
                    string value = ts.Next().Text;
                    switch (option)
                    {
                        case "audio": line.Audio = value; break;
                        case "duration": line.Duration = Syntax.ParseSeconds(value); break;
                        case "anim": line.Anim = value; break;
                        case "key": line.Key = value; break;
                        default: throw new FormatException($"Unknown 'say' option '{option}'. Expected audio, duration, anim or key.");
                    }
                }
                if (line.Text == null && line.Audio == null) throw new FormatException("'say' needs text or audio.");
                return line;
            }

            static bool IsSayOption(string word)
            {
                string w = word.ToLowerInvariant();
                return w == "audio" || w == "duration" || w == "anim" || w == "key";
            }

            void FinishRule()
            {
                if (rule == null) return;
                if (string.IsNullOrWhiteSpace(rule.Concept))
                    result.Messages.Add(new AuthoringMessage(MessageSeverity.Warning,
                        "Rule has no concept, so it can answer any event.", file, rule.SourceLine, rule.Name));
                result.Rules.Add(rule);
                rule = null;
            }

            void Error(string message) =>
                result.Messages.Add(new AuthoringMessage(MessageSeverity.Error, message, file, lineNo, rule?.Name));

            void Warning(string message) =>
                result.Messages.Add(new AuthoringMessage(MessageSeverity.Warning, message, file, lineNo, rule?.Name));
        }
    }

    // ---- Shared syntax (used by the .dlg parser and the spreadsheet parser) --------------------

    internal readonly struct Token
    {
        public readonly string Text;
        public readonly bool IsString;
        public Token(string text, bool isString)
        {
            Text = text;
            IsString = isString;
        }
        public bool Is(string word) => !IsString && string.Equals(Text, word, StringComparison.OrdinalIgnoreCase);
        public bool IsSeparator => !IsString && (Text == "," || Text == ";" || Is("and"));
        public override string ToString() => IsString ? "\"" + Text + "\"" : Text;
    }

    internal sealed class TokenStream
    {
        readonly List<Token> tokens;
        int pos;

        public TokenStream(List<Token> tokens) { this.tokens = tokens; }

        public bool AtEnd => pos >= tokens.Count;
        public Token Peek() => AtEnd ? default : tokens[pos];

        public Token Next()
        {
            if (AtEnd) throw new FormatException("Unexpected end of line.");
            return tokens[pos++];
        }

        public void ExpectEnd(string after)
        {
            if (!AtEnd) throw new FormatException($"Unexpected '{Peek()}' after {after}.");
        }
    }

    internal static class Tokenizer
    {
        /// <summary>Splits a line into words, quoted strings, operators and separators. Strips comments.</summary>
        public static List<Token> Tokenize(string line)
        {
            var tokens = new List<Token>();
            int i = 0, n = line.Length;
            while (i < n)
            {
                char c = line[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }
                if (c == '#' || (c == '/' && i + 1 < n && line[i + 1] == '/')) break; // comment
                if (c == '"' || c == '“' || c == '”')
                {
                    var sb = new StringBuilder();
                    i++;
                    bool closed = false;
                    while (i < n)
                    {
                        char d = line[i];
                        if (d == '\\' && i + 1 < n)
                        {
                            char e = line[i + 1];
                            sb.Append(e == 'n' ? '\n' : e);
                            i += 2;
                            continue;
                        }
                        if (d == '"' || d == '“' || d == '”')
                        {
                            closed = true;
                            i++;
                            break;
                        }
                        sb.Append(d);
                        i++;
                    }
                    if (!closed) throw new FormatException("Missing closing quote.");
                    tokens.Add(new Token(sb.ToString(), true));
                    continue;
                }
                if (c == ',' || c == ';')
                {
                    tokens.Add(new Token(c.ToString(), false));
                    i++;
                    continue;
                }
                if (IsOpChar(c) || ((c == '+' || c == '-') && i + 1 < n && line[i + 1] == '='))
                {
                    int start = i++;
                    while (i < n && IsOpChar(line[i])) i++;
                    tokens.Add(new Token(line.Substring(start, i - start), false));
                    continue;
                }
                {
                    int start = i;
                    while (i < n && !char.IsWhiteSpace(line[i]) && !IsOpChar(line[i]) && line[i] != '"'
                           && line[i] != ',' && line[i] != ';'
                           && !((line[i] == '+' || line[i] == '-') && i > start && i + 1 < n && line[i + 1] == '='))
                        i++;
                    tokens.Add(new Token(line.Substring(start, i - start), false));
                }
            }
            return tokens;
        }

        static bool IsOpChar(char c) => c == '<' || c == '>' || c == '=' || c == '!';
    }

    internal static class Syntax
    {
        /// <summary>Criteria separated by "and", "," or ";".</summary>
        public static List<CriterionSource> ParseCriteria(TokenStream ts)
        {
            var list = new List<CriterionSource>();
            while (!ts.AtEnd)
            {
                list.Add(ParseCriterion(ts));
                if (ts.AtEnd) break;
                if (!ts.Next().IsSeparator) throw new FormatException("Separate criteria with 'and', ',' or ';'.");
            }
            if (list.Count == 0) throw new FormatException("'if' needs a condition, e.g. 'if health < 30'.");
            return list;
        }

        /// <summary>
        /// One criterion: <c>key op value</c>, <c>key in a..b</c>, <c>key exists</c> or <c>key missing</c>,
        /// optionally followed by <c>default N</c>.
        /// </summary>
        public static CriterionSource ParseCriterion(TokenStream ts)
        {
            var keyToken = ts.Next();
            if (keyToken.IsString || keyToken.IsSeparator) throw new FormatException($"Expected a fact name, found '{keyToken}'.");
            var c = new CriterionSource { Key = keyToken.Text };
            if (ts.AtEnd) throw new FormatException($"'{c.Key}' needs a comparison, e.g. '{c.Key} == 1' or '{c.Key} exists'.");

            var opToken = ts.Next();
            string op = opToken.Text.ToLowerInvariant();
            switch (op)
            {
                case "==": case "=": case "is": c.Op = CriterionOp.Equals; break;
                case "!=": case "<>": c.Op = CriterionOp.NotEquals; break;
                case "<": c.Op = CriterionOp.Less; break;
                case "<=": c.Op = CriterionOp.LessOrEqual; break;
                case ">": c.Op = CriterionOp.Greater; break;
                case ">=": c.Op = CriterionOp.GreaterOrEqual; break;
                case "exists": c.Op = CriterionOp.Exists; break;
                case "missing": c.Op = CriterionOp.ZeroOrMissing; break;
                case "in": c.Op = CriterionOp.Between; break;
                default:
                    throw new FormatException($"Unknown comparison '{opToken.Text}' after '{c.Key}'. Use ==, !=, <, <=, >, >=, in, exists or missing.");
            }

            if (c.Op == CriterionOp.Between)
            {
                (c.Value, c.Value2) = ParseRange(ts.Next().Text);
            }
            else if (c.Op != CriterionOp.Exists && c.Op != CriterionOp.ZeroOrMissing)
            {
                if (ts.AtEnd) throw new FormatException($"'{c.Key} {opToken.Text}' needs a value.");
                c.Value = ts.Next().Text;
            }

            if (!ts.AtEnd && ts.Peek().Is("default"))
            {
                ts.Next();
                c.HasDefault = true;
                c.Default = ParseFloat(ts.Next().Text, "default");
            }
            return c;
        }

        /// <summary>"30..60" into ("30", "60").</summary>
        public static (string, string) ParseRange(string text)
        {
            int dots = text.IndexOf("..", StringComparison.Ordinal);
            if (dots <= 0 || dots + 2 >= text.Length) throw new FormatException($"Write a range as low..high, e.g. 30..60 (got '{text}').");
            string lo = text.Substring(0, dots), hi = text.Substring(dots + 2);
            ParseFloat(lo, "range");
            ParseFloat(hi, "range");
            return (lo, hi);
        }

        /// <summary><c>remember [speaker|world|Character] key (=|+=|-=) value [for 4s]</c></summary>
        public static FactWriteSource ParseRemember(TokenStream ts)
        {
            var w = new FactWriteSource();
            var first = ts.Next();
            var second = ts.Peek();
            Token keyToken;
            if (!ts.AtEnd && !IsAssignOp(second.Text))
            {
                ApplyScope(w, first.Text);
                keyToken = ts.Next();
            }
            else
            {
                keyToken = first;
            }
            w.Key = keyToken.Text;

            if (ts.AtEnd) throw new FormatException($"'remember {w.Key}' needs '= value', '+= value' or '-= value'.");
            string op = ts.Next().Text;
            if (!IsAssignOp(op)) throw new FormatException($"Expected =, += or -= after '{w.Key}', found '{op}'.");
            if (ts.AtEnd) throw new FormatException($"'remember {w.Key} {op}' needs a value.");
            string value = ts.Next().Text;
            if (op == "=" || op == "==")
            {
                w.Op = FactOp.Set;
                w.Value = value;
            }
            else
            {
                w.Op = FactOp.Add;
                float amount = ParseFloat(value, op);
                w.Value = (op == "-=" ? -amount : amount).ToString("R", CultureInfo.InvariantCulture);
            }

            if (!ts.AtEnd)
            {
                if (!ts.Next().Is("for")) throw new FormatException("Expected 'for <seconds>' at the end of 'remember'.");
                w.Duration = ParseSeconds(ts.Next().Text);
            }
            ts.ExpectEnd("remember");
            return w;
        }

        /// <summary><c>forget [speaker|world|Character] key</c></summary>
        public static FactWriteSource ParseForget(TokenStream ts)
        {
            var w = new FactWriteSource { Op = FactOp.Remove, Value = "0" };
            var first = ts.Next();
            if (!ts.AtEnd)
            {
                ApplyScope(w, first.Text);
                w.Key = ts.Next().Text;
            }
            else
            {
                w.Key = first.Text;
            }
            ts.ExpectEnd("forget");
            return w;
        }

        static void ApplyScope(FactWriteSource w, string scope)
        {
            if (string.Equals(scope, "speaker", StringComparison.OrdinalIgnoreCase) || string.Equals(scope, "self", StringComparison.OrdinalIgnoreCase))
                w.Scope = FactScope.Speaker;
            else if (string.Equals(scope, "world", StringComparison.OrdinalIgnoreCase))
                w.Scope = FactScope.World;
            else
            {
                w.Scope = FactScope.Character;
                w.Character = scope;
            }
        }

        static bool IsAssignOp(string s) => s == "=" || s == "==" || s == "+=" || s == "-=";

        /// <summary><c>then (self|any|all|Character) Concept [after 2s]</c></summary>
        public static FollowupSource ParseThen(TokenStream ts)
        {
            var f = new FollowupSource();
            string target = ts.Next().Text;
            switch (target.ToLowerInvariant())
            {
                case "self": f.Target = FollowupTarget.Self; break;
                case "any": f.Target = FollowupTarget.Any; break;
                case "all": f.Target = FollowupTarget.All; break;
                default:
                    f.Target = FollowupTarget.Character;
                    f.Character = target;
                    break;
            }
            if (ts.AtEnd) throw new FormatException($"'then {target}' needs a concept.");
            f.Concept = ts.Next().Text;
            if (!ts.AtEnd)
            {
                if (!ts.Next().Is("after")) throw new FormatException("Expected 'after <seconds>' at the end of 'then'.");
                f.Delay = ParseSeconds(ts.Next().Text);
            }
            ts.ExpectEnd("then");
            return f;
        }

        /// <summary>
        /// <c>do EventName [arg ...] [at start|at end]</c>. Arguments are words or quoted text; a quoted
        /// "at" is an argument, not the timing.
        /// </summary>
        public static DialogEventSource ParseDo(TokenStream ts)
        {
            if (ts.AtEnd) throw new FormatException("'do' needs an event name, e.g. 'do GiveItem glasses'.");
            var nameToken = ts.Next();
            if (nameToken.IsSeparator || (!nameToken.IsString && IsOperator(nameToken.Text)))
                throw new FormatException($"Expected an event name after 'do', found '{nameToken}'.");
            if (string.IsNullOrWhiteSpace(nameToken.Text)) throw new FormatException("'do' needs an event name.");
            var e = new DialogEventSource { Name = nameToken.Text.Trim() };

            var rest = new List<Token>();
            while (!ts.AtEnd) rest.Add(ts.Next());
            int n = rest.Count;
            if (n >= 2 && rest[n - 2].Is("at") && !rest[n - 1].IsString)
            {
                string when = rest[n - 1].Text.ToLowerInvariant();
                if (when == "end") e.Timing = DialogEventTiming.LineEnded;
                else if (when != "start") throw new FormatException($"Expected 'at start' or 'at end' at the end of 'do', found 'at {rest[n - 1].Text}'.");
                n -= 2;
            }
            for (int i = 0; i < n; i++)
            {
                if (rest[i].IsSeparator && !rest[i].Is("and"))
                    throw new FormatException($"Separate 'do' arguments with spaces, not '{rest[i].Text}'. Quote an argument that contains one.");
                e.Args.Add(rest[i].Text);
            }
            return e;
        }

        static bool IsOperator(string s) => s.Length > 0 && (s[0] == '<' || s[0] == '>' || s[0] == '=' || s[0] == '!' || s == "+=" || s == "-=");

        public static ResponseSelection ParseSelection(string text)
        {
            switch (text.ToLowerInvariant().Replace("-", "").Replace("_", ""))
            {
                case "random": return ResponseSelection.Random;
                case "norepeat": case "randomnorepeat": return ResponseSelection.RandomNoRepeat;
                case "shuffle": return ResponseSelection.Shuffle;
                case "sequential": case "sequence": case "inorder": return ResponseSelection.Sequential;
                default: throw new FormatException($"Unknown selection '{text}'. Use random, norepeat, shuffle or sequential.");
            }
        }

        /// <summary>"4", "4s", "1.5s" or "500ms" into seconds.</summary>
        public static float ParseSeconds(string text)
        {
            string t = text.Trim().ToLowerInvariant();
            float scale = 1f;
            if (t.EndsWith("ms")) { t = t.Substring(0, t.Length - 2); scale = 0.001f; }
            else if (t.EndsWith("s")) t = t.Substring(0, t.Length - 1);
            float v = ParseFloat(t, "time") * scale;
            if (v < 0f) throw new FormatException($"Time can't be negative ('{text}').");
            return v;
        }

        public static float ParseFloat(string text, string what)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                throw new FormatException($"Expected a number for {what}, found '{text}'.");
            return v;
        }

        public static bool IsTruthy(string text)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "x": case "y": case "yes": case "true": case "1": case "once": return true;
                default: return false;
            }
        }
    }
}
