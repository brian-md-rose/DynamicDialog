using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicDialog
{
    /// <summary>
    /// Reads rules from a spreadsheet saved as CSV or TSV, the way Dota's writer worked (talk slide 93):
    /// one row per recorded line, with columns for the criteria.
    /// <para>
    /// Known columns (case-insensitive; spaces and underscores ignored):
    /// <c>rule</c>, <c>who</c>, <c>concept</c>, <c>if</c> (criteria in .dlg syntax, e.g. "health &lt; 30; map == swamp"),
    /// <c>text</c>, <c>audio</c>, <c>duration</c>, <c>anim</c>, <c>key</c> (localization key), <c>remember</c> ("said_x = 1; world gossip += 1 for 10s"),
    /// <c>then</c> ("Rochelle Reply after 1s; any Cheer"), <c>once</c> (x/yes/true/1), <c>cooldown</c>, <c>weight</c>,
    /// <c>bonus</c>, <c>select</c>.
    /// Columns named notes, comment(s), description, status or actor, and columns starting with '#', are ignored.
    /// </para>
    /// <para>
    /// Any other column is a criterion on the fact it is named after. A cell of "swamp" means == swamp,
    /// and a cell can also hold "&lt; 30", "&gt;= 3", "!= nick", "30..60", "exists" or "missing".
    /// </para>
    /// <para>
    /// A row with an empty rule cell adds another variation to the rule above it. Several rows with
    /// the same rule name also add variations.
    /// </para>
    /// </summary>
    public static class SpreadsheetParser
    {
        enum Column
        {
            Ignore, Criterion, Rule, Who, Concept, If, Text, Audio, Duration, Anim, Key, Remember, Then, Once, Cooldown, Weight, Bonus, Select,
        }

        /// <summary>Reads a CSV or TSV spreadsheet. Bad cells are reported; the rest still load.</summary>
        /// <param name="text">The file's contents.</param>
        /// <param name="fileName">Used in messages. May be null.</param>
        /// <param name="delimiter">',' or '\t'. Pass '\0' to detect from the header row.</param>
        public static DialogSource Parse(string text, string fileName = null, char delimiter = '\0')
        {
            var result = new DialogSource();
            List<List<string>> rows;
            try
            {
                rows = ReadRows(text ?? "", delimiter);
            }
            catch (FormatException e)
            {
                result.Messages.Add(new AuthoringMessage(MessageSeverity.Error, e.Message, fileName));
                return result;
            }
            if (rows.Count == 0) return result;

            var header = rows[0];
            var columns = new Column[header.Count];
            bool hasRule = false, hasText = false;
            for (int c = 0; c < header.Count; c++)
            {
                columns[c] = Classify(header[c]);
                hasRule |= columns[c] == Column.Rule;
                hasText |= columns[c] == Column.Text;
            }
            if (!hasRule)
            {
                result.Messages.Add(new AuthoringMessage(MessageSeverity.Error, "No 'rule' column in the header row.", fileName, 1));
                return result;
            }
            if (!hasText)
                result.Messages.Add(new AuthoringMessage(MessageSeverity.Warning, "No 'text' column, so rules will have no lines.", fileName, 1));

            var byName = new Dictionary<string, RuleSource>(StringComparer.OrdinalIgnoreCase);
            RuleSource current = null;

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                int rowNo = r + 1;
                if (row.TrueForAll(string.IsNullOrWhiteSpace)) continue;

                string name = Cell(row, columns, Column.Rule);
                bool continuation;
                if (string.IsNullOrWhiteSpace(name))
                {
                    if (current == null)
                    {
                        result.Messages.Add(new AuthoringMessage(MessageSeverity.Error, "Row has no rule name and there is no rule above it.", fileName, rowNo));
                        continue;
                    }
                    continuation = true;
                }
                else if (byName.TryGetValue(name.Trim(), out var existing))
                {
                    if (existing != current)
                        result.Messages.Add(new AuthoringMessage(MessageSeverity.Warning,
                            $"Rows for rule '{existing.Name}' are not together; this row was added to it.", fileName, rowNo, existing.Name));
                    current = existing;
                    continuation = true;
                }
                else
                {
                    current = new RuleSource { Name = name.Trim(), SourceLine = rowNo };
                    byName[current.Name] = current;
                    result.Rules.Add(current);
                    continuation = false;
                }

                var line = new LineSource();
                for (int c = 0; c < columns.Length && c < row.Count; c++)
                {
                    string cell = row[c];
                    if (string.IsNullOrWhiteSpace(cell) || columns[c] == Column.Ignore || columns[c] == Column.Rule) continue;
                    cell = cell.Trim();
                    try
                    {
                        if (IsLineColumn(columns[c])) ApplyLineCell(line, columns[c], cell);
                        else if (continuation) CheckContinuationCell(result, current, columns[c], header[c], cell, fileName, rowNo);
                        else ApplyRuleCell(current, columns[c], header[c], cell);
                    }
                    catch (FormatException e)
                    {
                        result.Messages.Add(new AuthoringMessage(MessageSeverity.Error, $"Column '{header[c].Trim()}': {e.Message}", fileName, rowNo, current.Name));
                    }
                }
                if (line.Text != null || line.Audio != null) current.Lines.Add(line);
            }

            foreach (var rule in result.Rules)
                if (string.IsNullOrWhiteSpace(rule.Concept))
                    result.Messages.Add(new AuthoringMessage(MessageSeverity.Warning,
                        "Rule has no concept, so it can answer any event.", fileName, rule.SourceLine, rule.Name));
            return result;
        }

        static bool IsLineColumn(Column c) => c == Column.Text || c == Column.Audio || c == Column.Duration || c == Column.Anim || c == Column.Key;

        static void ApplyLineCell(LineSource line, Column column, string cell)
        {
            switch (column)
            {
                case Column.Text: line.Text = cell; break;
                case Column.Audio: line.Audio = cell; break;
                case Column.Duration: line.Duration = Syntax.ParseSeconds(cell); break;
                case Column.Anim: line.Anim = cell; break;
                case Column.Key: line.Key = cell; break;
            }
        }

        /// <summary>
        /// Rule-level cells only count on a rule's first row. Repeating them on later rows is fine;
        /// a different who or concept is probably a mistake, so it gets a warning.
        /// </summary>
        static void CheckContinuationCell(DialogSource result, RuleSource rule, Column column, string header, string cell, string file, int row)
        {
            string existing = column == Column.Who ? rule.Who : column == Column.Concept ? rule.Concept : null;
            if ((column == Column.Who || column == Column.Concept) && !string.Equals(existing, cell, StringComparison.OrdinalIgnoreCase))
                result.Messages.Add(new AuthoringMessage(MessageSeverity.Warning,
                    $"Column '{header.Trim()}' says '{cell}' but the rule's first row says '{existing}'. Only the first row counts.", file, row, rule.Name));
        }

        static void ApplyRuleCell(RuleSource rule, Column column, string header, string cell)
        {
            switch (column)
            {
                case Column.Who: rule.Who = cell; break;
                case Column.Concept: rule.Concept = cell; break;
                case Column.If:
                    rule.Criteria.AddRange(Syntax.ParseCriteria(Stream(cell)));
                    break;
                case Column.Criterion:
                    rule.Criteria.Add(CriterionFromColumn(header.Trim(), cell));
                    break;
                case Column.Remember:
                    foreach (var part in SplitStatements(cell))
                    {
                        var ts = Stream(part);
                        rule.Writes.Add(ts.Peek().Is("forget") ? ForgetAfterKeyword(ts) : Syntax.ParseRemember(ts));
                    }
                    break;
                case Column.Then:
                    foreach (var part in SplitStatements(cell)) rule.Followups.Add(Syntax.ParseThen(Stream(part)));
                    break;
                case Column.Once: rule.Once = Syntax.IsTruthy(cell); break;
                case Column.Cooldown: rule.Cooldown = Syntax.ParseSeconds(cell); break;
                case Column.Weight: rule.Weight = Syntax.ParseFloat(cell, "weight"); break;
                case Column.Bonus: rule.ScoreBonus = (int)Syntax.ParseFloat(cell, "bonus"); break;
                case Column.Select: rule.Selection = Syntax.ParseSelection(cell); break;
            }
        }

        static FactWriteSource ForgetAfterKeyword(TokenStream ts)
        {
            ts.Next();
            return Syntax.ParseForget(ts);
        }

        /// <summary>A criterion from a column named after a fact: "swamp", "&lt; 30", "30..60", "exists", "missing".</summary>
        static CriterionSource CriterionFromColumn(string key, string cell)
        {
            var tokens = Tokenizer.Tokenize(cell);
            if (tokens.Count == 0) throw new FormatException("Empty criterion.");
            var first = tokens[0];
            bool hasOp = !first.IsString && (first.Text.StartsWith("<") || first.Text.StartsWith(">") || first.Text.StartsWith("=")
                         || first.Text.StartsWith("!") || first.Is("in") || first.Is("exists") || first.Is("missing"));
            if (!hasOp && tokens.Count == 1 && !first.IsString && first.Text.Contains(".."))
                return new CriterionSource { Key = key, Op = CriterionOp.Between, Value = Syntax.ParseRange(first.Text).Item1, Value2 = Syntax.ParseRange(first.Text).Item2 };

            var all = new List<Token> { new Token(key, false) };
            if (!hasOp) all.Add(new Token("==", false));
            all.AddRange(tokens);
            var ts = new TokenStream(all);
            var c = Syntax.ParseCriterion(ts);
            ts.ExpectEnd($"the '{key}' criterion");
            return c;
        }

        static TokenStream Stream(string text) => new TokenStream(Tokenizer.Tokenize(text));

        /// <summary>Splits "a = 1; b += 2" on semicolons and newlines (not commas, which may be in quotes).</summary>
        static IEnumerable<string> SplitStatements(string cell)
        {
            foreach (var part in cell.Split(';', '\n'))
                if (!string.IsNullOrWhiteSpace(part)) yield return part.Trim();
        }

        static string Cell(List<string> row, Column[] columns, Column which)
        {
            for (int c = 0; c < columns.Length && c < row.Count; c++)
                if (columns[c] == which) return row[c];
            return null;
        }

        static Column Classify(string header)
        {
            string h = (header ?? "").Trim();
            if (h.Length == 0 || h.StartsWith("#")) return Column.Ignore;
            switch (h.ToLowerInvariant().Replace(" ", "").Replace("_", ""))
            {
                case "rule": case "name": case "id": case "rulename": return Column.Rule;
                case "who": case "speaker": case "character": return Column.Who;
                case "concept": case "event": return Column.Concept;
                case "if": case "criteria": case "conditions": case "condition": return Column.If;
                case "text": case "line": case "say": case "dialog": case "dialogue": case "subtitle": return Column.Text;
                case "audio": case "file": case "clip": case "vo": case "audiofile": return Column.Audio;
                case "duration": return Column.Duration;
                case "anim": case "animation": return Column.Anim;
                case "key": case "linekey": case "stringkey": case "loc": case "lockey": return Column.Key;
                case "remember": case "sets": case "write": case "writes": return Column.Remember;
                case "then": case "followup": case "followups": case "next": return Column.Then;
                case "once": return Column.Once;
                case "cooldown": return Column.Cooldown;
                case "weight": return Column.Weight;
                case "bonus": case "score": case "scorebonus": return Column.Bonus;
                case "select": case "selection": return Column.Select;
                case "notes": case "note": case "comment": case "comments": case "description": case "status": case "actor": return Column.Ignore;
                default: return Column.Criterion;
            }
        }

        // ---- CSV / TSV --------------------------------------------------------------------------

        /// <summary>RFC 4180 reader: quoted cells may hold delimiters, quotes ("") and newlines.</summary>
        internal static List<List<string>> ReadRows(string text, char delimiter = '\0')
        {
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            if (delimiter == '\0') delimiter = DetectDelimiter(text);

            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, cellStarted = false;
            int i = 0, n = text.Length;
            while (i < n)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < n && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i += 2;
                            continue;
                        }
                        quoted = false;
                        i++;
                        continue;
                    }
                    cell.Append(c);
                    i++;
                    continue;
                }

                if (c == '"' && !cellStarted)
                {
                    quoted = true;
                    cellStarted = true;
                    i++;
                }
                else if (c == delimiter)
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    cellStarted = false;
                    i++;
                }
                else if (c == '\r' || c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    cellStarted = false;
                    rows.Add(row);
                    row = new List<string>();
                    i += c == '\r' && i + 1 < n && text[i + 1] == '\n' ? 2 : 1;
                }
                else
                {
                    cell.Append(c);
                    cellStarted = true;
                    i++;
                }
            }
            if (quoted) throw new FormatException("A quoted cell is never closed.");
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }
            return rows;
        }

        static char DetectDelimiter(string text)
        {
            int end = text.IndexOfAny(new[] { '\r', '\n' });
            string first = end < 0 ? text : text.Substring(0, end);
            int tabs = 0, commas = 0;
            foreach (char c in first)
            {
                if (c == '\t') tabs++;
                else if (c == ',') commas++;
            }
            return tabs > commas ? '\t' : ',';
        }

        internal static string EscapeCell(string value, char delimiter = ',')
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needsQuotes = value.IndexOf(delimiter) >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0
                               || value[0] == ' ' || value[value.Length - 1] == ' ';
            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }
}
