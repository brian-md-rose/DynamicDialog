using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DynamicDialog.Editor
{
    /// <summary>
    /// Checks every rule database in the project together: followups nobody answers, contradictory
    /// conditions, likely typos in fact names, duplicate names. Click a message to jump to the rule.
    /// </summary>
    public class DialogValidatorWindow : EditorWindow
    {
        const string KnownFactsPref = "DynamicDialog.KnownFacts";

        struct Row
        {
            public AuthoringMessage Message;
            public UnityEngine.Object Asset;
        }

        readonly List<Row> rows = new List<Row>();
        Vector2 scroll;
        bool showErrors = true, showWarnings = true, showInfo;
        string knownFactsText;
        int databaseCount;
        GUIStyle wrapStyle;

        /// <summary>Fact names your code provides, entered in the window, so they aren't reported as unknown.</summary>
        internal static IEnumerable<string> KnownFacts =>
            EditorPrefs.GetString(KnownFactsPref, "").Split(new[] { ',', ' ', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
        [MenuItem("Window/Dynamic Dialog/Rule Validator")]

        /// <summary>Opens the validator and checks every rule database in the project.</summary>
        public static DialogValidatorWindow Open()
        {
            var w = GetWindow<DialogValidatorWindow>("Dialog Rules");
            w.Run();
            return w;
        }

        void OnEnable() => knownFactsText = EditorPrefs.GetString(KnownFactsPref, "");

        internal void Run()
        {
            rows.Clear();
            var groups = new List<RuleSourceGroup>();
            var assets = new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(DialogRuleDatabase)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var db = AssetDatabase.LoadAssetAtPath<DialogRuleDatabase>(path);
                if (db == null) continue;
                string label = db.IsImported ? db.SourcePath : path;
                assets[label] = AssetDatabase.LoadMainAssetAtPath(label) ?? db;
                groups.Add(new RuleSourceGroup(label, db.ToSources()));
                foreach (var e in db.Validate())
                    rows.Add(new Row { Message = new AuthoringMessage(MessageSeverity.Warning, e, label), Asset = assets[label] });
            }
            databaseCount = groups.Count;

            foreach (var m in RuleAnalyzer.Analyze(groups, KnownFacts))
                rows.Add(new Row { Message = m, Asset = m.File != null && assets.TryGetValue(m.File, out var a) ? a : null });

            rows.Sort((x, y) =>
            {
                int s = y.Message.Severity.CompareTo(x.Message.Severity);
                if (s != 0) return s;
                int f = string.Compare(x.Message.File, y.Message.File, StringComparison.OrdinalIgnoreCase);
                return f != 0 ? f : x.Message.Line.CompareTo(y.Message.Line);
            });
            Repaint();
        }

        void OnGUI()
        {
            if (wrapStyle == null) wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };

            int errors = rows.Count(r => r.Message.Severity == MessageSeverity.Error);
            int warnings = rows.Count(r => r.Message.Severity == MessageSeverity.Warning);
            int infos = rows.Count - errors - warnings;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Check All Rules", EditorStyles.toolbarButton, GUILayout.Width(110))) Run();
                GUILayout.FlexibleSpace();
                showErrors = GUILayout.Toggle(showErrors, $"Errors {errors}", EditorStyles.toolbarButton);
                showWarnings = GUILayout.Toggle(showWarnings, $"Warnings {warnings}", EditorStyles.toolbarButton);
                showInfo = GUILayout.Toggle(showInfo, $"Info {infos}", EditorStyles.toolbarButton);
            }

            EditorGUI.BeginChangeCheck();
            knownFactsText = EditorGUILayout.TextField(new GUIContent("Facts from code",
                "Fact names your own fact providers add (e.g. health, weapon), separated by commas. They won't be reported as unknown."), knownFactsText);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(KnownFactsPref, knownFactsText);

            EditorGUILayout.LabelField($"{databaseCount} rule databases checked.", EditorStyles.miniLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var row in rows)
            {
                var m = row.Message;
                if (m.Severity == MessageSeverity.Error && !showErrors) continue;
                if (m.Severity == MessageSeverity.Warning && !showWarnings) continue;
                if (m.Severity == MessageSeverity.Info && !showInfo) continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(Icon(m.Severity), GUILayout.Width(20), GUILayout.Height(20));
                    string where = m.File != null ? System.IO.Path.GetFileName(m.File) + (m.Line > 0 ? $":{m.Line}" : "") : "";
                    string text = (m.Rule != null ? $"[{m.Rule}] " : "") + m.Text + (where.Length > 0 ? $"   ({where})" : "");
                    if (GUILayout.Button(text, wrapStyle) && row.Asset != null)
                    {
                        EditorGUIUtility.PingObject(row.Asset);
                        Selection.activeObject = row.Asset;
                    }
                    if (row.Asset != null && m.Line > 0 && GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(44)))
                        AssetDatabase.OpenAsset(row.Asset, m.Line);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        static GUIContent Icon(MessageSeverity s) =>
            EditorGUIUtility.IconContent(s == MessageSeverity.Error ? "console.erroricon.sml" : s == MessageSeverity.Warning ? "console.warnicon.sml" : "console.infoicon.sml");
    }
}
