using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DynamicDialog.Editor
{
    /// <summary>Inspector for rule databases: check rules, export to .dlg, export a recording script.</summary>
    [CustomEditor(typeof(DialogRuleDatabase))]
    internal class DialogRuleDatabaseEditor : UnityEditor.Editor
    {
        List<AuthoringMessage> messages;
        Vector2 scroll;

        public override void OnInspectorGUI()
        {
            var db = (DialogRuleDatabase)target;

            if (db.IsImported)
            {
                EditorGUILayout.HelpBox($"Imported from {db.SourcePath}. Edit that file; this view is read-only.", MessageType.Info);
                if (GUILayout.Button("Open Source File"))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(db.SourcePath));
            }

            EditorGUILayout.LabelField($"{db.rules.Count} rules, {db.rules.Sum(r => r?.response?.lines.Count ?? 0)} lines", EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Check Rules")) messages = Check(db);
                if (GUILayout.Button("Export .dlg…")) ExportDlg(db);
                if (GUILayout.Button("Export Recording Script…")) ExportRecordingScript(db);
            }
            if (GUILayout.Button("Export String Table CSV…")) ExportStringTable(db);

            if (messages != null)
            {
                if (messages.Count == 0)
                {
                    EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
                }
                else
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(220));
                    foreach (var m in messages)
                        EditorGUILayout.HelpBox((m.Rule != null ? $"[{m.Rule}] " : "") + m.Text, ToMessageType(m.Severity));
                    EditorGUILayout.EndScrollView();
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(db.IsImported))
                DrawDefaultInspector();
        }

        internal static List<AuthoringMessage> Check(DialogRuleDatabase db)
        {
            string label = db.IsImported ? db.SourcePath : AssetDatabase.GetAssetPath(db);
            var list = db.Validate().Select(e => new AuthoringMessage(MessageSeverity.Warning, e, label)).ToList();
            list.AddRange(RuleAnalyzer.Analyze(new[] { new RuleSourceGroup(label, db.ToSources()) }, DialogValidatorWindow.KnownFacts));
            return list.OrderByDescending(m => m.Severity).ToList();
        }

        static void ExportDlg(DialogRuleDatabase db)
        {
            string path = EditorUtility.SaveFilePanelInProject("Export rules as .dlg", db.name + (db.IsImported ? "_export" : ""), "dlg", "Choose where to save the .dlg file.");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, DialogScriptWriter.ToDlg(db.ToSources(), db.partitionKeys));
            AssetDatabase.ImportAsset(path);
            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
        }

        static void ExportRecordingScript(DialogRuleDatabase db)
        {
            string path = EditorUtility.SaveFilePanel("Export recording script", "", db.name + "_recording.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, DialogScriptWriter.ToRecordingScript(db.ToSources()));
            EditorUtility.RevealInFinder(path);
        }

        static void ExportStringTable(DialogRuleDatabase db)
        {
            string path = EditorUtility.SaveFilePanel("Export string table for translators", "", db.name + "_strings.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, DialogScriptWriter.ToStringTableCsv(db.ToSources()));
            EditorUtility.RevealInFinder(path);
        }

        internal static MessageType ToMessageType(MessageSeverity s) =>
            s == MessageSeverity.Error ? MessageType.Error : s == MessageSeverity.Warning ? MessageType.Warning : MessageType.Info;
    }
}
