using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DynamicDialog
{
    /// <summary>
    /// Writes rules back out: as .dlg text (for moving inspector-made rules into text), or as a
    /// recording script for voice sessions (talk slide 93).
    /// </summary>
    public static class DialogScriptWriter
    {
        public static string ToDlg(IEnumerable<RuleSource> rules, IEnumerable<string> partitionKeys = null)
        {
            var sb = new StringBuilder();
            var keys = partitionKeys?.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            if (keys != null && keys.Count > 0) sb.Append("@partition ").Append(string.Join(" ", keys.Select(Word))).Append("\n\n");

            foreach (var r in rules)
            {
                sb.Append("rule ").Append(Word(r.Name)).Append('\n');
                if (!string.IsNullOrWhiteSpace(r.Who)) sb.Append("  who ").Append(Word(r.Who)).Append('\n');
                if (!string.IsNullOrWhiteSpace(r.Concept)) sb.Append("  concept ").Append(Word(r.Concept)).Append('\n');
                foreach (var c in r.Criteria) sb.Append("  if ").Append(Criterion(c)).Append('\n');
                if (r.Selection != ResponseSelection.RandomNoRepeat) sb.Append("  select ").Append(Selection(r.Selection)).Append('\n');
                foreach (var l in r.Lines)
                {
                    sb.Append("  say");
                    if (l.Text != null) sb.Append(' ').Append(Quote(l.Text));
                    if (!string.IsNullOrEmpty(l.Audio)) sb.Append(" audio ").Append(Word(l.Audio));
                    if (l.Duration > 0f) sb.Append(" duration ").Append(Num(l.Duration)).Append('s');
                    if (!string.IsNullOrEmpty(l.Anim)) sb.Append(" anim ").Append(Word(l.Anim));
                    if (!string.IsNullOrEmpty(l.Key)) sb.Append(" key ").Append(Word(l.Key));
                    sb.Append('\n');
                }
                foreach (var w in r.Writes) sb.Append("  ").Append(Write(w)).Append('\n');
                foreach (var f in r.Followups)
                {
                    string target = f.Target == FollowupTarget.Character ? Word(f.Character) : f.Target.ToString().ToLowerInvariant();
                    sb.Append("  then ").Append(target).Append(' ').Append(Word(f.Concept));
                    if (f.Delay > 0f) sb.Append(" after ").Append(Num(f.Delay)).Append('s');
                    sb.Append('\n');
                }
                if (r.Once) sb.Append("  once\n");
                if (r.Cooldown > 0f) sb.Append("  cooldown ").Append(Num(r.Cooldown)).Append("s\n");
                if (r.Weight != 1f) sb.Append("  weight ").Append(Num(r.Weight)).Append('\n');
                if (r.ScoreBonus != 0) sb.Append("  bonus ").Append(r.ScoreBonus.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// A CSV for recording sessions: one row per line, grouped by character, with the file name to record into.
        /// </summary>
        public static string ToRecordingScript(IEnumerable<RuleSource> rules)
        {
            var sb = new StringBuilder("Character,Rule,Concept,Take,Text,Audio File\n");
            var ordered = rules.OrderBy(r => r.Who ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(r => r.SourceLine);
            foreach (var r in ordered)
            {
                for (int i = 0; i < r.Lines.Count; i++)
                {
                    var l = r.Lines[i];
                    sb.Append(SpreadsheetParser.EscapeCell(string.IsNullOrWhiteSpace(r.Who) ? "(anyone)" : r.Who)).Append(',')
                      .Append(SpreadsheetParser.EscapeCell(r.Name)).Append(',')
                      .Append(SpreadsheetParser.EscapeCell(r.Concept)).Append(',')
                      .Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(SpreadsheetParser.EscapeCell(l.Text)).Append(',')
                      .Append(SpreadsheetParser.EscapeCell(l.Audio)).Append('\n');
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// A CSV for translators: one row per line with its key, source text, and who says it in which rule.
        /// Import it into a Unity Localization string table (or any other tool) keyed by the Key column.
        /// </summary>
        public static string ToStringTableCsv(IEnumerable<RuleSource> rules)
        {
            var sb = new StringBuilder("Key,Source,Character,Rule,Concept\n");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in rules)
                for (int i = 0; i < r.Lines.Count; i++)
                {
                    if (string.IsNullOrEmpty(r.Lines[i].Text)) continue;
                    string key = LineKeys.For(r, i);
                    if (!seen.Add(key)) continue;
                    sb.Append(SpreadsheetParser.EscapeCell(key)).Append(',')
                      .Append(SpreadsheetParser.EscapeCell(r.Lines[i].Text)).Append(',')
                      .Append(SpreadsheetParser.EscapeCell(r.Who)).Append(',')
                      .Append(SpreadsheetParser.EscapeCell(r.Name)).Append(',')
                      .Append(SpreadsheetParser.EscapeCell(r.Concept)).Append('\n');
                }
            return sb.ToString();
        }

        static string Criterion(CriterionSource c)
        {
            string s;
            switch (c.Op)
            {
                case CriterionOp.Equals: s = $"{Word(c.Key)} == {Value(c.Value)}"; break;
                case CriterionOp.NotEquals: s = $"{Word(c.Key)} != {Value(c.Value)}"; break;
                case CriterionOp.Less: s = $"{Word(c.Key)} < {Value(c.Value)}"; break;
                case CriterionOp.LessOrEqual: s = $"{Word(c.Key)} <= {Value(c.Value)}"; break;
                case CriterionOp.Greater: s = $"{Word(c.Key)} > {Value(c.Value)}"; break;
                case CriterionOp.GreaterOrEqual: s = $"{Word(c.Key)} >= {Value(c.Value)}"; break;
                case CriterionOp.Between: s = $"{Word(c.Key)} in {c.Value}..{c.Value2}"; break;
                case CriterionOp.Exists: s = $"{Word(c.Key)} exists"; break;
                case CriterionOp.ZeroOrMissing: s = $"{Word(c.Key)} missing"; break;
                default: throw new ArgumentOutOfRangeException();
            }
            if (c.HasDefault && c.Op != CriterionOp.ZeroOrMissing) s += " default " + Num(c.Default);
            return s;
        }

        static string Write(FactWriteSource w)
        {
            string scope = w.Scope == FactScope.World ? "world " : w.Scope == FactScope.Character ? Word(w.Character) + " " : "";
            if (w.Op == FactOp.Remove) return $"forget {scope}{Word(w.Key)}";
            string op = "=";
            string value = Value(w.Value);
            if (w.Op == FactOp.Add)
            {
                float amount = FactValue.Parse(w.Value);
                op = amount < 0 ? "-=" : "+=";
                value = Num(Math.Abs(amount));
            }
            string s = $"remember {scope}{Word(w.Key)} {op} {value}";
            if (w.Duration > 0f) s += $" for {Num(w.Duration)}s";
            return s;
        }

        static string Selection(ResponseSelection s)
        {
            switch (s)
            {
                case ResponseSelection.Random: return "random";
                case ResponseSelection.Shuffle: return "shuffle";
                case ResponseSelection.Sequential: return "sequential";
                default: return "norepeat";
            }
        }

        static string Num(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        static string Value(string v) => string.IsNullOrEmpty(v) ? "0" : Word(v);

        /// <summary>Leaves simple words bare; quotes anything the tokenizer would split.</summary>
        static string Word(string w)
        {
            if (string.IsNullOrEmpty(w)) return "\"\"";
            foreach (char c in w)
                if (char.IsWhiteSpace(c) || c == '"' || c == '#' || c == ',' || c == ';' || c == '<' || c == '>' || c == '=' || c == '!')
                    return Quote(w);
            if (w.Contains("//") || w.Contains("+=") || w.Contains("-=")) return Quote(w);
            return w;
        }

        static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }
}
