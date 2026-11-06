using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DynamicDialog;
using UnityEngine;

namespace DynamicDialog.Editor
{
    [Serializable]
    public class DlgCriterion
    {
        public string key = "";
        public CriterionOp op = CriterionOp.Equals;
        /// <summary>True when <see cref="value"/> is a name (from the Values table) rather than a number.</summary>
        public bool valueIsName;
        public string value = "1";
        public string value2 = "1";
        public bool hasDefault;
        public float defaultValue;

        public bool UsesValue => op != CriterionOp.Exists && op != CriterionOp.ZeroOrMissing;
    }

    [Serializable]
    public class DlgLine
    {
        public string text = "";
        public string audio = "";
        public float duration;
        public string anim = "";
        public string key = "";
    }

    [Serializable]
    public class DlgWrite
    {
        public FactScope scope = FactScope.Speaker;
        public string character = "";
        public string key = "";
        public FactOp op = FactOp.Set;
        public bool valueIsName;
        public string value = "1";
        public float duration;
    }

    [Serializable]
    public class DlgFollowup
    {
        public FollowupTarget target = FollowupTarget.Character;
        public string character = "";
        public string concept = "";
        public float delay;
    }

    [Serializable]
    public class DlgRule
    {
        /// <summary>Comment lines written above the rule, kept as raw text (each line starts with # or //, or is blank).</summary>
        public string comment = "";
        public string name = "NewRule";
        public string who = "";
        public string concept = "";
        public List<DlgCriterion> criteria = new List<DlgCriterion>();
        public ResponseSelection selection = ResponseSelection.RandomNoRepeat;
        public List<DlgLine> lines = new List<DlgLine>();
        public List<DlgWrite> writes = new List<DlgWrite>();
        public List<DlgFollowup> followups = new List<DlgFollowup>();
        public bool once;
        public float cooldown;
        public float weight = 1f;
        public int bonus;

        // Editor-only UI state, kept here so it survives undo and domain reloads.
        public bool foldConditions = true, foldLines = true, foldWrites = true, foldFollowups = true, foldOptions = true, foldComment;

        public DlgRule Clone()
        {
            var json = JsonUtility.ToJson(this);
            return JsonUtility.FromJson<DlgRule>(json);
        }
    }

    /// <summary>
    /// An editable copy of one .dlg file. Loads through DynamicDialog's parser and saves through its
    /// writer, keeping the file's comments: the text above the first rule stays at the top, comments
    /// above a rule travel with that rule, and comments inside a rule move to just above it.
    /// </summary>
    public class DlgDocument : ScriptableObject
    {
        public string path;
        public string header = "";
        public string trailer = "";
        public List<string> partitionKeys = new List<string>();
        public List<DlgRule> rules = new List<DlgRule>();

        /// <summary>Notes about what loading changed (comments moved, directives expanded).</summary>
        [NonSerialized] public List<string> loadNotes = new List<string>();

        public static DlgDocument CreateEmpty(string path)
        {
            var doc = CreateInstance<DlgDocument>();
            doc.hideFlags = HideFlags.HideAndDontSave;
            doc.path = path;
            return doc;
        }

        // ---- Loading -----------------------------------------------------------------------------

        /// <summary>Parses .dlg text. Returns null (with the parser's messages) if the text has errors.</summary>
        public static DlgDocument Load(string path, string text, out List<AuthoringMessage> messages)
        {
            var source = DialogScriptParser.Parse(text, path);
            messages = source.Messages.ToList();
            if (source.HasErrors) return null;

            var doc = CreateEmpty(path);
            doc.partitionKeys = source.PartitionKeys.ToList();

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var kinds = new LineKind[lines.Length];
            var comments = new string[lines.Length];
            int directives = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                SplitComment(lines[i], out string code, out string comment);
                comments[i] = comment;
                if (code.Trim().Length > 0) kinds[i] = code.TrimStart().StartsWith("@") ? LineKind.Directive : LineKind.Code;
                else kinds[i] = comment != null ? LineKind.Comment : LineKind.Blank;
                if (kinds[i] == LineKind.Directive) directives++;
            }

            int movedComments = 0;
            var starts = source.Rules.Select(r => r.SourceLine - 1).ToList();
            int preambleStart = 0;
            for (int r = 0; r < source.Rules.Count; r++)
            {
                int start = starts[r];
                int end = r + 1 < starts.Count ? starts[r + 1] : lines.Length;

                // Comment and blank lines between the previous rule's last statement and this rule.
                var preamble = new List<string>();
                for (int i = preambleStart; i < start; i++)
                    if (kinds[i] == LineKind.Comment || kinds[i] == LineKind.Blank) preamble.Add(lines[i].TrimEnd());

                // This rule's body ends at its last statement; comments after that belong to the next rule.
                int lastCode = start;
                for (int i = start + 1; i < end; i++)
                    if (kinds[i] == LineKind.Code) lastCode = i;

                var inner = new List<string>();
                for (int i = start; i <= lastCode; i++)
                {
                    if (comments[i] == null) continue;
                    inner.Add(comments[i].Trim());
                    movedComments++;
                }

                var rule = FromSource(source.Rules[r]);
                if (r == 0)
                {
                    doc.header = JoinTrimmed(preamble, trimEnd: true);
                    rule.comment = JoinTrimmed(inner, trimEnd: true);
                }
                else
                {
                    rule.comment = JoinTrimmed(preamble.Concat(inner), trimEnd: false);
                }
                doc.rules.Add(rule);
                preambleStart = lastCode + 1;
            }

            var tail = new List<string>();
            for (int i = preambleStart; i < lines.Length; i++)
                if (kinds[i] == LineKind.Comment || kinds[i] == LineKind.Blank) tail.Add(lines[i].TrimEnd());
            if (source.Rules.Count == 0) doc.header = JoinTrimmed(tail, trimEnd: true);
            else doc.trailer = JoinTrimmed(tail, trimEnd: true);

            if (movedComments > 0)
                doc.loadNotes.Add($"{movedComments} comment(s) inside rule bodies will be moved above their rule when you save.");
            if (directives > 0)
                doc.loadNotes.Add("@who / @concept defaults are written into each rule when you save (@partition is kept).");
            return doc;
        }

        enum LineKind { Blank, Comment, Code, Directive }

        /// <summary>Splits a line at its comment (# or //), ignoring markers inside quoted strings.</summary>
        internal static void SplitComment(string line, out string code, out string comment)
        {
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '\\') i++;
                    else if (c == '"' || c == '“' || c == '”') quoted = false;
                    continue;
                }
                if (c == '"' || c == '“' || c == '”') { quoted = true; continue; }
                if (c == '#' || (c == '/' && i + 1 < line.Length && line[i + 1] == '/'))
                {
                    code = line.Substring(0, i);
                    comment = line.Substring(i);
                    return;
                }
            }
            code = line;
            comment = null;
        }

        static string JoinTrimmed(IEnumerable<string> lines, bool trimEnd)
        {
            var list = lines.ToList();
            while (list.Count > 0 && list[0].Trim().Length == 0) list.RemoveAt(0);
            if (trimEnd) while (list.Count > 0 && list[list.Count - 1].Trim().Length == 0) list.RemoveAt(list.Count - 1);
            return string.Join("\n", list);
        }

        static DlgRule FromSource(RuleSource s)
        {
            var r = new DlgRule
            {
                name = s.Name ?? "",
                who = s.Who ?? "",
                concept = s.Concept ?? "",
                selection = s.Selection,
                once = s.Once,
                cooldown = s.Cooldown,
                weight = s.Weight,
                bonus = s.ScoreBonus,
            };
            foreach (var c in s.Criteria)
            {
                FactValue.Parse(c.Value, out bool isName);
                r.criteria.Add(new DlgCriterion
                {
                    key = c.Key ?? "", op = c.Op, value = c.Value ?? "1", value2 = c.Value2 ?? "1",
                    valueIsName = isName, hasDefault = c.HasDefault, defaultValue = c.Default,
                });
            }
            foreach (var l in s.Lines)
                r.lines.Add(new DlgLine { text = l.Text ?? "", audio = l.Audio ?? "", duration = l.Duration, anim = l.Anim ?? "", key = l.Key ?? "" });
            foreach (var w in s.Writes)
            {
                FactValue.Parse(w.Value, out bool isName);
                r.writes.Add(new DlgWrite
                {
                    scope = w.Scope, character = w.Character ?? "", key = w.Key ?? "", op = w.Op,
                    value = w.Value ?? "1", valueIsName = isName && w.Op == FactOp.Set, duration = w.Duration,
                });
            }
            foreach (var f in s.Followups)
                r.followups.Add(new DlgFollowup { target = f.Target, character = f.Character ?? "", concept = f.Concept ?? "", delay = f.Delay });
            return r;
        }

        // ---- Saving ------------------------------------------------------------------------------

        public static RuleSource ToSource(DlgRule r)
        {
            var s = new RuleSource
            {
                Name = r.name?.Trim() ?? "",
                Who = Clean(r.who),
                Concept = Clean(r.concept),
                Selection = r.selection,
                Once = r.once,
                Cooldown = Mathf.Max(0f, r.cooldown),
                Weight = r.weight,
                ScoreBonus = r.bonus,
            };
            foreach (var c in r.criteria)
            {
                var cs = new CriterionSource { Key = c.key?.Trim() ?? "", Op = c.op, HasDefault = c.hasDefault, Default = c.defaultValue };
                if (c.op == CriterionOp.Between) { cs.Value = Number(c.value); cs.Value2 = Number(c.value2); }
                else if (c.UsesValue) cs.Value = c.valueIsName ? (c.value?.Trim() ?? "") : Number(c.value);
                s.Criteria.Add(cs);
            }
            foreach (var l in r.lines)
            {
                bool audioOnly = string.IsNullOrEmpty(l.text) && !string.IsNullOrEmpty(l.audio);
                s.Lines.Add(new LineSource
                {
                    Text = audioOnly ? null : l.text ?? "",
                    Audio = Clean(l.audio),
                    Duration = Mathf.Max(0f, l.duration),
                    Anim = Clean(l.anim),
                    Key = Clean(l.key),
                });
            }
            foreach (var w in r.writes)
            {
                var ws = new FactWriteSource
                {
                    Scope = w.scope, Character = w.scope == FactScope.Character ? Clean(w.character) : null,
                    Key = w.key?.Trim() ?? "", Op = w.op, Duration = w.op == FactOp.Remove ? 0f : Mathf.Max(0f, w.duration),
                };
                ws.Value = w.op == FactOp.Remove ? "0" : w.op == FactOp.Set && w.valueIsName ? (w.value?.Trim() ?? "") : Number(w.value);
                s.Writes.Add(ws);
            }
            foreach (var f in r.followups)
                s.Followups.Add(new FollowupSource
                {
                    Target = f.target, Character = f.target == FollowupTarget.Character ? Clean(f.character) : null,
                    Concept = f.concept?.Trim() ?? "", Delay = Mathf.Max(0f, f.delay),
                });
            return s;
        }

        public IEnumerable<RuleSource> ToSources() => rules.Select(ToSource);

        public string ToText()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(header)) sb.Append(CommentBlock(header)).Append("\n\n");
            var keys = partitionKeys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            if (keys.Count > 0)
            {
                // Let the writer quote the keys the same way it quotes everything else.
                sb.Append(DialogScriptWriter.ToDlg(Enumerable.Empty<RuleSource>(), keys));
            }
            foreach (var rule in rules)
            {
                if (!string.IsNullOrWhiteSpace(rule.comment)) sb.Append(CommentBlock(rule.comment)).Append('\n');
                sb.Append(DialogScriptWriter.ToDlg(new[] { ToSource(rule) }));
            }
            if (!string.IsNullOrWhiteSpace(trailer)) sb.Append(CommentBlock(trailer)).Append('\n');
            return sb.ToString().TrimEnd('\n') + "\n";
        }

        /// <summary>Makes sure every non-blank line is a comment, so free text typed in the editor can't become a statement.</summary>
        static string CommentBlock(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n').Select(l =>
            {
                string t = l.TrimEnd();
                string s = t.TrimStart();
                if (s.Length == 0) return "";
                return s.StartsWith("#") || s.StartsWith("//") ? t : "# " + t;
            });
            return string.Join("\n", lines);
        }

        static string Clean(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        static string Number(string s)
        {
            float v = ParseNumber(s);
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        public static float ParseNumber(string s) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        public static string FormatNumber(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        // ---- Names used by the document (for the string table) ------------------------------------

        /// <summary>Visits every name of a category in the document; the visitor returns the replacement.</summary>
        public int VisitNames(DialogStringCategory category, Func<string, string> visit)
        {
            int count = 0;
            void V(ref string field)
            {
                if (string.IsNullOrEmpty(field)) return;
                count++;
                field = visit(field);
            }

            if (category == DialogStringCategory.Facts)
                for (int i = 0; i < partitionKeys.Count; i++) { var k = partitionKeys[i]; V(ref k); partitionKeys[i] = k; }

            foreach (var r in rules)
            {
                switch (category)
                {
                    case DialogStringCategory.Characters:
                        V(ref r.who);
                        foreach (var w in r.writes) if (w.scope == FactScope.Character) V(ref w.character);
                        foreach (var f in r.followups) if (f.target == FollowupTarget.Character) V(ref f.character);
                        break;
                    case DialogStringCategory.Concepts:
                        V(ref r.concept);
                        foreach (var f in r.followups) V(ref f.concept);
                        break;
                    case DialogStringCategory.Facts:
                        foreach (var c in r.criteria) V(ref c.key);
                        foreach (var w in r.writes) V(ref w.key);
                        break;
                    case DialogStringCategory.Values:
                        foreach (var c in r.criteria) if (c.UsesValue && c.op != CriterionOp.Between && c.valueIsName) V(ref c.value);
                        foreach (var w in r.writes) if (w.op == FactOp.Set && w.valueIsName) V(ref w.value);
                        break;
                    case DialogStringCategory.Animations:
                        foreach (var l in r.lines) V(ref l.anim);
                        break;
                    case DialogStringCategory.Audio:
                        foreach (var l in r.lines) V(ref l.audio);
                        break;
                }
            }
            return count;
        }

        public IEnumerable<string> NamesUsed(DialogStringCategory category)
        {
            var list = new List<string>();
            VisitNames(category, n => { list.Add(n); return n; });
            return list.Distinct(StringComparer.Ordinal);
        }

        public int CountUses(DialogStringCategory category, string name)
        {
            int n = 0;
            VisitNames(category, v => { if (v == name) n++; return v; });
            return n;
        }

        /// <summary>Names used by parsed rules from any source (e.g. other .dlg files), by category.</summary>
        public static IEnumerable<(DialogStringCategory, string)> NamesIn(IEnumerable<RuleSource> sources, IEnumerable<string> partition = null)
        {
            var doc = CreateEmpty(null);
            try
            {
                doc.rules = sources.Select(FromSource).ToList();
                if (partition != null) doc.partitionKeys = partition.ToList();
                return DialogStringTable.AllCategories.SelectMany(c => doc.NamesUsed(c).Select(n => (c, n))).ToList();
            }
            finally
            {
                DestroyImmediate(doc);
            }
        }
    }
}
