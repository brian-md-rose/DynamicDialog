using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DynamicDialog.Editor
{
    /// <summary>
    /// Window > Dynamic Dialog > Debugger (talk slide 98: "a rich set of debugging tools").
    /// <list type="bullet">
    /// <item><b>Lookups</b>: every query while playing, with each fact's source, the rules tested and why each failed.
    /// Pick any rule to ask "why didn't this play?".</item>
    /// <item><b>Coverage</b>: how often each loaded rule has played this session, and which never have.</item>
    /// <item><b>Test Query</b>: type facts and see which rule would win, without entering Play Mode.</item>
    /// </list>
    /// </summary>
    public class DialogDebuggerWindow : EditorWindow
    {
        enum Tab { Lookups, Coverage, TestQuery }

        static readonly string[] TabNames = { "Lookups", "Coverage", "Test Query" };

        [SerializeField] Tab tab;
        [SerializeField] string filter = "";
        [SerializeField] bool missesOnly;
        [SerializeField] bool paused;
        [SerializeField] bool unplayedOnly;
        [SerializeField] string testWho = "";
        [SerializeField] string testConcept = "";
        [SerializeField] string testFacts = "";
        [SerializeField] List<string> excludedDatabases = new List<string>();

        int selectedId = -1;
        int whyNotIndex;
        List<LookupRecord> frozen;
        Vector2 listScroll, detailScroll, coverageScroll, testScroll;
        List<RuleExplanation> testResults;
        string testError;
        DialogRecorder subscribed;
        GUIStyle wrap, rich;
        List<(string path, DialogRuleDatabase db)> databaseCache;

        void OnFocus() => databaseCache = null;

        List<(string path, DialogRuleDatabase db)> Databases => databaseCache ?? (databaseCache = ProjectDatabases());

        /// <summary>Opens the debugger window.</summary>
        [MenuItem("Window/Dynamic Dialog/Debugger")]
        public static DialogDebuggerWindow Open() => GetWindow<DialogDebuggerWindow>("Dialog Debugger");

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Subscribe(null);
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
            {
                Subscribe(null);
                frozen = null;
                selectedId = -1;
            }
            Repaint();
        }

        /// <summary>The playing director, without creating one in Edit Mode.</summary>
        static DialogDirector LiveDirector => EditorApplication.isPlaying ? DialogDirector.Instance : null;

        void Subscribe(DialogRecorder recorder)
        {
            if (subscribed == recorder) return;
            if (subscribed != null)
            {
                subscribed.Recorded -= OnRecorded;
                subscribed.Started -= OnRecorded;
            }
            subscribed = recorder;
            if (subscribed != null)
            {
                subscribed.Recorded += OnRecorded;
                subscribed.Started += OnRecorded;
            }
        }

        void OnRecorded(LookupRecord _)
        {
            if (!paused) Repaint();
        }

        void OnGUI()
        {
            if (wrap == null)
            {
                wrap = new GUIStyle(EditorStyles.label) { wordWrap = true };
                rich = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
            }

            tab = (Tab)GUILayout.Toolbar((int)tab, TabNames);
            switch (tab)
            {
                case Tab.Lookups: DrawLookups(); break;
                case Tab.Coverage: DrawCoverage(); break;
                case Tab.TestQuery: DrawTestQuery(); break;
            }
        }

        // ---- Lookups ------------------------------------------------------------------------

        void DrawLookups()
        {
            var director = LiveDirector;
            if (director == null)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see lookups. Use the Test Query tab to try rules without playing.", MessageType.Info);
                return;
            }
            var recorder = director.Recorder;
            if (recorder == null)
            {
                EditorGUILayout.HelpBox("Lookup recording is off. Turn on 'Record Lookups' on the Dialog Director.", MessageType.Warning);
                return;
            }
            Subscribe(recorder);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool nowPaused = GUILayout.Toggle(paused, paused ? "Paused" : "Live", EditorStyles.toolbarButton, GUILayout.Width(55));
                if (nowPaused != paused)
                {
                    paused = nowPaused;
                    frozen = paused ? recorder.Records.ToList() : null;
                }
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(45)))
                {
                    recorder.Clear();
                    frozen = paused ? new List<LookupRecord>() : null;
                    selectedId = -1;
                }
                missesOnly = GUILayout.Toggle(missesOnly, "Misses only", EditorStyles.toolbarButton, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                filter = EditorGUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            }

            var records = (paused && frozen != null ? (IReadOnlyList<LookupRecord>)frozen : recorder.Records)
                .Where(Matches).Reverse().ToList();
            var selected = records.FirstOrDefault(r => r.Id == selectedId);

            using (new EditorGUILayout.HorizontalScope())
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Width(Mathf.Max(220, position.width * 0.35f)));
                foreach (var r in records)
                {
                    string color = !r.Matched ? "#ff7070" : r.Started ? "#6fcf5f" : "#e0b030";
                    string label = $"<color=#999999>{r.Time:0.0}s</color> <b>{r.Speaker}</b> {r.Concept} → <color={color}>{(r.Matched ? r.Winner.Name : "no match")}</color>";
                    var style = r.Id == selectedId ? "SelectionRect" : "label";
                    if (GUILayout.Button(label, new GUIStyle(style) { richText = true, alignment = TextAnchor.MiddleLeft }))
                    {
                        selectedId = r.Id;
                        whyNotIndex = 0;
                    }
                }
                if (records.Count == 0) GUILayout.Label("No lookups yet.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();

                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                if (selected != null) DrawRecord(selected, director);
                else GUILayout.Label("Select a lookup.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
            }
        }

        bool Matches(LookupRecord r)
        {
            if (missesOnly && r.Matched) return false;
            if (string.IsNullOrWhiteSpace(filter)) return true;
            string f = filter.Trim();
            return Contains(r.Speaker.Name, f) || Contains(r.Concept.Name, f) || (r.Winner != null && Contains(r.Winner.Name, f));
        }

        static bool Contains(string s, string f) => s != null && s.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0;

        void DrawRecord(LookupRecord r, DialogDirector director)
        {
            string outcome = !r.Matched
                ? "<color=#ff7070>No rule matched.</color>"
                : $"Winner: <b>{r.Winner.Name}</b> (score {r.Winner.Score}{(r.TieCount > 1 ? $", picked from {r.TieCount} tied" : "")}). " +
                  (r.Started ? "<color=#6fcf5f>Played.</color>" : "<color=#e0b030>Not played</color> (another speaker won, or the speaker was busy).");
            GUILayout.Label($"<b>#{r.Id}</b>  {r.Speaker} asked about <b>{r.Concept}</b> at {r.Time:0.00}s\n{outcome}", rich);

            if (GUILayout.Button("Copy into Test Query", GUILayout.Width(160)))
            {
                testWho = r.Speaker.Name;
                testConcept = r.Concept.Name;
                testFacts = FactsToText(r.Query, skipWellKnown: true);
                tab = Tab.TestQuery;
                RunTest();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Facts ({r.Query.Count})", EditorStyles.boldLabel);
            for (int i = 0; i < r.Query.Count; i++)
            {
                var key = r.Query.KeyAt(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(key.Name, GUILayout.Height(16), GUILayout.Width(160));
                    EditorGUILayout.SelectableLabel(r.Query.FormatValueAt(i), GUILayout.Height(16), GUILayout.Width(120));
                    GUILayout.Label(r.SourceOf(key) ?? "", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Rules tested ({r.Tested.Count})", EditorStyles.boldLabel);
            if (r.Tested.Count == 0) GUILayout.Label("None: no rule has this speaker and concept.", EditorStyles.miniLabel);
            foreach (var t in r.Tested)
            {
                var e = RuleDiagnostics.Explain(t.Rule, r);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(Icon(e.Verdict), GUILayout.Width(18), GUILayout.Height(18));
                    GUILayout.Label($"<b>{t.Rule.Name}</b> <color=#999999>({t.Rule.Score})</color>  {e.Text}", rich);
                }
            }
            GUILayout.Label("Rules in other buckets (other speakers or concepts) and rules scoring below the winner aren't tested.", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Why didn't this rule play?", EditorStyles.boldLabel);
            var candidates = CandidateRules(director, r).ToList();
            if (candidates.Count == 0)
            {
                GUILayout.Label("No loaded rule answers this concept.", EditorStyles.miniLabel);
                return;
            }
            whyNotIndex = Mathf.Clamp(whyNotIndex, 0, candidates.Count - 1);
            whyNotIndex = EditorGUILayout.Popup(whyNotIndex, candidates.Select(c => $"{c.Name} ({c.Score})").ToArray());
            var why = RuleDiagnostics.Explain(candidates[whyNotIndex], r);
            EditorGUILayout.HelpBox(why.Text, why.Verdict == RuleVerdict.Won ? MessageType.Info : MessageType.Warning);
        }

        /// <summary>Loaded rules that answer the lookup's concept (or any concept), for the "why not" picker.</summary>
        static IEnumerable<Rule> CandidateRules(DialogDirector director, LookupRecord r)
        {
            float concept = r.Concept.AsValue;
            return director.Engine.Matcher.Sets.SelectMany(s => s.Rules)
                .Where(rule => !rule.Criteria.Any(c => c.Key == WellKnownFacts.Concept && c.IsExactMatch(out float v) && v != concept))
                .OrderByDescending(rule => rule.Score).ThenBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase);
        }

        // ---- Coverage -----------------------------------------------------------------------

        void DrawCoverage()
        {
            var director = LiveDirector;
            if (director == null)
            {
                EditorGUILayout.HelpBox("Enter Play Mode and play through some content; this shows which rules have and haven't played.", MessageType.Info);
                return;
            }

            var sets = director.Engine.Matcher.Sets;
            int total = sets.Sum(s => s.Rules.Count);
            int played = sets.Sum(s => s.Rules.Count(r => r.State.TimesFired > 0));
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"{played} of {total} rules have played ({(total == 0 ? 0 : 100f * played / total):0}%)");
                GUILayout.FlexibleSpace();
                unplayedOnly = GUILayout.Toggle(unplayedOnly, "Unplayed only", EditorStyles.toolbarButton);
            }

            coverageScroll = EditorGUILayout.BeginScrollView(coverageScroll);
            foreach (var set in sets)
            {
                int setPlayed = set.Rules.Count(r => r.State.TimesFired > 0);
                EditorGUILayout.LabelField($"{set.Name}  ({setPlayed}/{set.Rules.Count})", EditorStyles.boldLabel);
                foreach (var rule in set.Rules.OrderBy(r => r.State.TimesFired).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (unplayedOnly && rule.State.TimesFired > 0) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string count = rule.State.TimesFired == 0 ? "<color=#ff7070>never</color>" : $"{rule.State.TimesFired}×";
                        GUILayout.Label(count, rich, GUILayout.Width(50));
                        GUILayout.Label(rule.Name + (rule.State.Disabled ? "  <color=#999999>(once, used)</color>" : ""), rich);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // ---- Test query ---------------------------------------------------------------------

        void DrawTestQuery()
        {
            EditorGUILayout.HelpBox("Type facts, one per line (map = swamp, health = 20), and see which rule wins. " +
                "Rules are compiled fresh from the project's databases, so once/cooldown state is ignored.", MessageType.None);

            testWho = EditorGUILayout.TextField("Who", testWho);
            testConcept = EditorGUILayout.TextField("Concept", testConcept);
            EditorGUILayout.LabelField("Facts");
            testFacts = EditorGUILayout.TextArea(testFacts, GUILayout.MinHeight(60));

            var databases = Databases;
            EditorGUILayout.LabelField("Databases", EditorStyles.boldLabel);
            foreach (var (path, db) in databases)
            {
                bool on = !excludedDatabases.Contains(path);
                bool now = EditorGUILayout.ToggleLeft($"{db.name}  <color=#999999>{path} ({db.rules.Count})</color>", on, rich);
                if (now != on)
                {
                    if (now) excludedDatabases.Remove(path);
                    else excludedDatabases.Add(path);
                }
            }

            if (GUILayout.Button("Find Best Rule", GUILayout.Height(24))) RunTest();

            if (testError != null) EditorGUILayout.HelpBox(testError, MessageType.Error);
            if (testResults == null) return;

            testScroll = EditorGUILayout.BeginScrollView(testScroll);
            if (testResults.Count == 0) GUILayout.Label("No rule answers this concept.", EditorStyles.centeredGreyMiniLabel);
            foreach (var e in testResults)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(Icon(e.Verdict), GUILayout.Width(18), GUILayout.Height(18));
                    GUILayout.Label($"<b>{e.Rule.Name}</b> <color=#999999>({e.Rule.Score})</color>  {e.Text}", rich);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        internal void RunTest()
        {
            testError = null;
            try
            {
                var query = BuildTestQuery(testWho, testConcept, testFacts);
                databaseCache = null;
                var sets = Databases
                    .Where(d => !excludedDatabases.Contains(d.path))
                    .Select(d => d.db.Compile())
                    .ToList();
                testResults = RuleDiagnostics.EvaluateAll(sets, query, 0);
            }
            catch (FormatException e)
            {
                testError = e.Message;
                testResults = null;
            }
            Repaint();
        }

        internal List<RuleExplanation> TestResults => testResults;

        internal static Query BuildTestQuery(string who, string concept, string facts)
        {
            var q = new Query();
            int lineNo = 0;
            foreach (var raw in (facts ?? "").Replace("\r", "").Split('\n'))
            {
                lineNo++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;
                int eq = line.IndexOfAny(new[] { '=', ':' });
                string key = (eq < 0 ? line : line.Substring(0, eq)).Trim();
                string value = eq < 0 ? "1" : line.Substring(eq + 1).Trim().Trim('"');
                if (key.Length == 0) throw new FormatException($"Line {lineNo}: expected 'name = value'.");
                float v = FactValue.Parse(value, out bool isSymbol);
                q.Set(Symbol.Of(key), v, isSymbol);
            }
            if (!string.IsNullOrWhiteSpace(concept)) q.Set(WellKnownFacts.Concept, Symbol.Of(concept.Trim()));
            if (!string.IsNullOrWhiteSpace(who)) q.Set(WellKnownFacts.Who, Symbol.Of(who.Trim()));
            if (!q.Contains(WellKnownFacts.Random)) q.Set(WellKnownFacts.Random, 50f);
            return q;
        }

        static string FactsToText(Query q, bool skipWellKnown)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < q.Count; i++)
            {
                var key = q.KeyAt(i);
                if (skipWellKnown && (key == WellKnownFacts.Concept || key == WellKnownFacts.Who)) continue;
                sb.Append(key.Name).Append(" = ").Append(q.FormatValueAt(i)).Append('\n');
            }
            return sb.ToString();
        }

        static List<(string path, DialogRuleDatabase db)> ProjectDatabases()
        {
            var list = new List<(string, DialogRuleDatabase)>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(DialogRuleDatabase)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var db = AssetDatabase.LoadAssetAtPath<DialogRuleDatabase>(path);
                if (db != null) list.Add((path, db));
            }
            return list.OrderBy(d => d.Item1, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static GUIContent Icon(RuleVerdict v)
        {
            switch (v)
            {
                case RuleVerdict.Won: return EditorGUIUtility.IconContent("TestPassed");
                case RuleVerdict.LostTie:
                case RuleVerdict.Outscored: return EditorGUIUtility.IconContent("TestInconclusive");
                case RuleVerdict.Unavailable:
                case RuleVerdict.NotLoaded: return EditorGUIUtility.IconContent("TestIgnored");
                default: return EditorGUIUtility.IconContent("TestFailed");
            }
        }
    }
}
