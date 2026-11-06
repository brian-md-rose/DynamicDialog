using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicDialog;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DynamicDialog.Editor
{
    /// <summary>
    /// Edits .dlg rule files: add, remove, reorder and change rules. Names (characters, concepts,
    /// facts, values, animations, audio) come from a <see cref="DialogStringTable"/> through searchable drop-downs.
    /// Window > Dynamic Dialog > Rule Editor, or right-click a .dlg file > Dynamic Dialog > Edit in Rule Editor.
    /// </summary>
    public class DialogRuleEditorWindow : EditorWindow
    {
        const string TablePref = "DialogRuleEditor.TableGuid";
        const int FileSettingsIndex = -2;
        const double ValidateDelay = 0.6;
        const int MaxLinks = 12;
        static readonly string[] Tabs = { "Rules", "String Table" };

        [SerializeField] DlgDocument doc;
        [SerializeField] string docPath;
        [SerializeField] string savedText;
        [SerializeField] long loadedWriteTicks;
        [SerializeField] DialogStringTable table;
        [SerializeField] int tab;
        [SerializeField] int selected = -1;
        [SerializeField] string search = "";
        [SerializeField] float listWidth = 260f;
        [SerializeField] string tableSearch = "";
        [SerializeField] List<string> notes = new List<string>();
        [SerializeField] bool groupBySpeaker;
        [SerializeField] bool showProblems = true;
        [SerializeField] bool foldLinks = true;
        [SerializeField] bool foldPreview;
        [SerializeField] List<string> history = new List<string>();

        Vector2 listScroll, detailScroll, tableScroll, messageScroll;
        List<AuthoringMessage> messages;
        Dictionary<string, MessageSeverity> ruleSeverity = new Dictionary<string, MessageSeverity>(StringComparer.OrdinalIgnoreCase);
        double validateAt = -1;
        string loadErrorPath;
        bool externalChange;
        bool draggingSplitter;
        readonly List<Action> pending = new List<Action>();
        readonly string[] addText = new string[DialogStringTable.AllCategories.Length];
        readonly bool[] tableFold = Enumerable.Repeat(true, DialogStringTable.AllCategories.Length).ToArray();
        readonly HashSet<string> collapsedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int typingControl, typingGroup;
        [NonSerialized] GUIStyle wrapArea, rowTitle, rowSubtitle, groupFoldout, linkStyle, previewStyle, badStyle;
        [NonSerialized] Texture errorIcon, warnIcon, infoIcon;

        // Rule list state, rebuilt every GUI pass.
        readonly List<int> visibleRows = new List<int>();
        readonly Dictionary<int, Rect> rowRects = new Dictionary<int, Rect>();
        int listControl, dragIndex = -1, dropIndex = -1;
        Vector2 dragStart;
        bool dragging, scrollToSelected;

        // ---- Opening ------------------------------------------------------------------------------

        [MenuItem("Window/Dynamic Dialog/Rule Editor")]
        public static DialogRuleEditorWindow ShowWindow()
        {
            var w = GetWindow<DialogRuleEditorWindow>();
            w.minSize = new Vector2(640, 360);
            w.Show();
            return w;
        }

        public static void Open(string path)
        {
            var w = ShowWindow();
            if (w.doc != null && string.Equals(w.docPath, path, StringComparison.OrdinalIgnoreCase)) return;
            if (w.ConfirmDiscard()) w.LoadFromDisk(path);
        }

        [MenuItem("Assets/Dynamic Dialog/Edit in Rule Editor", false, 30)]
        static void OpenSelected() => Open(AssetDatabase.GetAssetPath(Selection.activeObject));

        [MenuItem("Assets/Dynamic Dialog/Edit in Rule Editor", true)]
        static bool OpenSelectedValidate() => IsDlg(AssetDatabase.GetAssetPath(Selection.activeObject));

        [InitializeOnLoadMethod]
        static void HookInspectorHeader()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += editor =>
            {
                if (editor.targets.Length != 1) return;
                string path = AssetDatabase.GetAssetPath(editor.target);
                if (!IsDlg(path)) return;
                if (GUILayout.Button("Open in Rule Editor")) Open(path);
            };
        }

        static bool IsDlg(string path) => !string.IsNullOrEmpty(path) && path.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase);

        // ---- Lifetime -----------------------------------------------------------------------------

        void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += Tick;
            wantsMouseMove = true;
            if (table == null) table = FindTable();
            if (doc == null && !string.IsNullOrEmpty(docPath) && File.Exists(docPath)) LoadFromDisk(docPath, quiet: true);
            else if (doc != null) ScheduleValidate(0);
            saveChangesMessage = "This .dlg file has unsaved changes. Save them?";
            UpdateDirty();
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= Tick;
        }

        void OnDestroy()
        {
            if (doc != null) DestroyImmediate(doc);
        }

        void OnFocus() => CheckExternalChange();

        public override void SaveChanges()
        {
            Save();
            base.SaveChanges();
        }

        void OnUndoRedo()
        {
            if (doc != null) selected = Mathf.Clamp(selected, FileSettingsIndex, doc.rules.Count - 1);
            UpdateDirty();
            ScheduleValidate();
            Repaint();
        }

        /// <summary>Re-checks the file shortly after edits stop, so problems stay current without a button press.</summary>
        void Tick()
        {
            if (validateAt < 0 || EditorApplication.timeSinceStartup < validateAt) return;
            validateAt = -1;
            Validate();
        }

        void ScheduleValidate(double delay = ValidateDelay) => validateAt = EditorApplication.timeSinceStartup + delay;

        // ---- File handling ------------------------------------------------------------------------

        bool LoadFromDisk(string path, bool quiet = false)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                if (!quiet) EditorUtility.DisplayDialog("Dialog Rule Editor", $"Couldn't read {path}:\n{e.Message}", "OK");
                return false;
            }

            var loaded = DlgDocument.Load(path, text, out var parseMessages);
            if (doc != null) DestroyImmediate(doc);
            bool sameFile = string.Equals(docPath, path, StringComparison.OrdinalIgnoreCase);
            doc = loaded;
            docPath = path;
            externalChange = false;
            loadedWriteTicks = File.GetLastWriteTimeUtc(path).Ticks;
            notes = new List<string>();
            if (!sameFile) history.Clear();

            if (loaded == null)
            {
                loadErrorPath = path;
                savedText = null;
                messages = parseMessages.Where(m => m.Severity == MessageSeverity.Error).ToList();
                ruleSeverity.Clear();
                showProblems = true;
                UpdateDirty();
                return false;
            }

            loadErrorPath = null;
            notes.AddRange(loaded.loadNotes);
            int missing = DialogStringTable.AllCategories.Sum(c => loaded.NamesUsed(c).Count(n => table != null && !table.Contains(c, n)));
            if (table != null && missing > 0)
                notes.Add($"{missing} name(s) in this file aren't in the string table yet. Use String Table > Import From Open File to add them.");
            else if (table == null)
                notes.Add("No string table yet. Create one in the String Table tab to fill the drop-downs.");

            savedText = loaded.ToText();
            selected = loaded.rules.Count > 0 ? Mathf.Clamp(selected, 0, loaded.rules.Count - 1) : FileSettingsIndex;
            UpdateDirty();
            Validate();
            return true;
        }

        void Save()
        {
            if (doc == null) return;
            string text = doc.ToText();
            var check = DialogScriptParser.Parse(text, docPath);
            if (check.HasErrors)
            {
                Validate();
                showProblems = true;
                if (!EditorUtility.DisplayDialog("Dialog Rule Editor",
                        "The saved file would have errors (see the Problems panel). Save anyway?", "Save Anyway", "Cancel"))
                    return;
            }

            try
            {
                File.WriteAllText(docPath, text);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Dialog Rule Editor", $"Couldn't write {docPath}:\n{e.Message}", "OK");
                return;
            }
            savedText = text;
            loadedWriteTicks = File.GetLastWriteTimeUtc(docPath).Ticks;
            externalChange = false;
            if (table != null) AssetDatabase.SaveAssetIfDirty(table);
            AssetDatabase.ImportAsset(docPath);
            UpdateDirty();
            Validate();
            ShowNotification(new GUIContent("Saved " + Path.GetFileName(docPath)), 1.5);
        }

        void CheckExternalChange()
        {
            if (doc == null || string.IsNullOrEmpty(docPath) || !File.Exists(docPath)) return;
            long ticks = File.GetLastWriteTimeUtc(docPath).Ticks;
            if (ticks == loadedWriteTicks) return;
            if (!hasUnsavedChanges) LoadFromDisk(docPath, quiet: true);
            else externalChange = true;
            Repaint();
        }

        bool ConfirmDiscard()
        {
            if (!hasUnsavedChanges) return true;
            int choice = EditorUtility.DisplayDialogComplex("Unsaved Changes",
                $"Save changes to {Path.GetFileName(docPath)}?", "Save", "Cancel", "Don't Save");
            if (choice == 1) return false;
            if (choice == 0) Save();
            return true;
        }

        void NewFile()
        {
            if (!ConfirmDiscard()) return;
            string path = EditorUtility.SaveFilePanelInProject("New .dlg file", "NewDialog", "dlg", "Choose where to create the rule file.");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, "# Dialog rules. Edit with Window > Dynamic Dialog > Rule Editor.\n");
            AssetDatabase.ImportAsset(path);
            LoadFromDisk(path);
        }

        void UpdateDirty()
        {
            hasUnsavedChanges = doc != null && savedText != null && doc.ToText() != savedText;
            string name = doc != null ? Path.GetFileNameWithoutExtension(docPath) : "Rule Editor";
            titleContent = new GUIContent((hasUnsavedChanges ? "*" : "") + name, "Dialog Rule Editor");
        }

        // ---- String table -------------------------------------------------------------------------

        static DialogStringTable FindTable()
        {
            string guid = EditorPrefs.GetString(TablePref, "");
            if (!string.IsNullOrEmpty(guid))
            {
                var t = AssetDatabase.LoadAssetAtPath<DialogStringTable>(AssetDatabase.GUIDToAssetPath(guid));
                if (t != null) return t;
            }
            var first = AssetDatabase.FindAssets("t:" + nameof(DialogStringTable)).FirstOrDefault();
            return first != null ? AssetDatabase.LoadAssetAtPath<DialogStringTable>(AssetDatabase.GUIDToAssetPath(first)) : null;
        }

        void SetTable(DialogStringTable t)
        {
            table = t;
            if (t != null) EditorPrefs.SetString(TablePref, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t)));
            ScheduleValidate(0);
        }

        void CreateTable()
        {
            string folder = !string.IsNullOrEmpty(docPath) ? Path.GetDirectoryName(docPath)?.Replace('\\', '/') : "Assets";
            string path = EditorUtility.SaveFilePanelInProject("Create string table", "DialogStringTable", "asset",
                "Choose where to save the names the rule editor offers in its drop-downs.", folder);
            if (string.IsNullOrEmpty(path)) return;
            var t = CreateInstance<DialogStringTable>();
            AssetDatabase.CreateAsset(t, path);
            SetTable(t);
            if (doc != null) ImportNames(doc.ToSources(), doc.partitionKeys, record: false);
            AssetDatabase.SaveAssetIfDirty(t);
            notes.RemoveAll(n => n.Contains("string table"));
        }

        int ImportNames(IEnumerable<RuleSource> sources, IEnumerable<string> partition, bool record = true)
        {
            if (table == null) return 0;
            var names = DlgDocument.NamesIn(sources, partition)
                .Where(x => !table.Contains(x.Item1, x.Item2))
                .OrderBy(x => x.Item2, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (names.Count == 0) return 0;
            if (record) Undo.RecordObject(table, "Import Dialog Names");
            int added = names.Count(x => table.Add(x.Item1, x.Item2));
            EditorUtility.SetDirty(table);
            ScheduleValidate();
            return added;
        }

        void ImportFromAllFiles()
        {
            var sources = new List<RuleSource>();
            var partition = new List<string>();
            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(IsDlg))
            {
                var parsed = DialogScriptParser.Parse(File.ReadAllText(path), path);
                sources.AddRange(parsed.Rules);
                partition.AddRange(parsed.PartitionKeys);
            }
            if (doc != null) sources.AddRange(doc.ToSources());
            int added = ImportNames(sources, partition);
            ShowNotification(new GUIContent(added > 0 ? $"Added {added} name(s)" : "Every name is already in the table"), 1.5);
        }

        void AddToTable(DialogStringCategory category, string name)
        {
            if (table == null || string.IsNullOrWhiteSpace(name) || table.Contains(category, name.Trim())) return;
            Undo.RecordObject(table, "Add Dialog Name");
            table.Add(category, name);
            EditorUtility.SetDirty(table);
            ScheduleValidate();
        }

        // ---- Editing helpers ----------------------------------------------------------------------

        void Record(string undoName, bool typing)
        {
            Undo.RecordObject(doc, undoName);
            if (typing)
            {
                int kc = GUIUtility.keyboardControl;
                if (kc != 0 && kc == typingControl) Undo.CollapseUndoOperations(typingGroup);
                else
                {
                    typingControl = kc;
                    typingGroup = Undo.GetCurrentGroup();
                }
            }
            else typingControl = 0;
        }

        void Set<T>(ref T field, T value, bool typing = false)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            Record("Edit Dialog Rule", typing);
            field = value;
            Changed();
        }

        void SetText(ref string field, string value) => Set(ref field, value ?? "", typing: true);

        /// <summary>Runs a structural change (add/remove/move) after the GUI pass, so layout stays consistent.</summary>
        void Modify(string undoName, Action change)
        {
            pending.Add(() =>
            {
                Record(undoName, false);
                change();
                Changed();
            });
            Repaint(); // also called from context menus, which run outside OnGUI
        }

        void Changed()
        {
            UpdateDirty();
            ScheduleValidate();
        }

        void RunPending()
        {
            if (pending.Count == 0) return;
            var list = pending.ToList();
            pending.Clear();
            foreach (var a in list) a();
            Repaint();
        }

        bool HasRule => doc != null && selected >= 0 && selected < doc.rules.Count;

        /// <summary>Selects a rule (or the file settings), remembering where you came from for Back.</summary>
        void Select(int index, bool remember = true)
        {
            if (doc == null || index == selected) return;
            if (remember && HasRule)
            {
                history.Add(doc.rules[selected].name);
                if (history.Count > 50) history.RemoveAt(0);
            }
            selected = index;
            detailScroll = Vector2.zero;
            scrollToSelected = true;
            GUI.FocusControl(null);
            Repaint();
        }

        void GoBack()
        {
            while (history.Count > 0)
            {
                string name = history[history.Count - 1];
                history.RemoveAt(history.Count - 1);
                int index = doc.rules.FindIndex(r => r.name == name);
                if (index >= 0 && index != selected)
                {
                    Select(index, remember: false);
                    return;
                }
            }
        }

        // ---- GUI ----------------------------------------------------------------------------------

        void InitStyles()
        {
            if (badStyle != null && errorIcon != null) return; // styles survive script reloads; check the last ones set
            wrapArea = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            rowTitle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip };
            rowSubtitle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
            groupFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            linkStyle = new GUIStyle(EditorStyles.linkLabel) { wordWrap = false, clipping = TextClipping.Clip, margin = new RectOffset(4, 4, 1, 1) };
            previewStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = false, font = EditorStyles.standardFont, richText = false };
            badStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = new Color(1f, 0.6f, 0.35f) } };
            errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml").image;
            warnIcon = EditorGUIUtility.IconContent("console.warnicon.sml").image;
            infoIcon = EditorGUIUtility.IconContent("console.infoicon.sml").image;
        }

        void OnGUI()
        {
            InitStyles();
            HandleShortcuts();
            DrawToolbar();

            if (tab == 1)
            {
                DrawTableTab();
            }
            else if (doc == null)
            {
                DrawNoDocument();
            }
            else
            {
                DrawBanners();
                DrawRulesTab();
            }
            DrawProblems();
            RunPending();
        }

        void HandleShortcuts()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.S && (e.control || e.command) && doc != null)
            {
                pending.Add(Save);
                e.Use();
                return;
            }
            if (doc == null || tab != 0 || EditorGUIUtility.editingTextField) return;

            // Keys below act on the rule list, so they only apply when no field has keyboard focus.
            bool listFocused = GUIUtility.keyboardControl == 0;
            if (e.type == EventType.KeyDown && listFocused)
            {
                int pos = visibleRows.IndexOf(selected);
                int step = e.keyCode == KeyCode.DownArrow ? 1 : e.keyCode == KeyCode.UpArrow ? -1 : 0;
                if (step != 0 && visibleRows.Count > 0)
                {
                    int next = visibleRows[Mathf.Clamp(pos < 0 ? 0 : pos + step, 0, visibleRows.Count - 1)];
                    pending.Add(() => Select(next, remember: false));
                    e.Use();
                }
                else if (e.keyCode == KeyCode.LeftArrow && e.alt)
                {
                    pending.Add(GoBack);
                    e.Use();
                }
            }

            if ((e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand) && listFocused)
            {
                bool run = e.type == EventType.ExecuteCommand;
                switch (e.commandName)
                {
                    case "Copy":
                        if (!HasRule) return;
                        if (run) CopyRule(selected);
                        e.Use();
                        break;
                    case "Paste":
                        if (run) PasteRules();
                        e.Use();
                        break;
                    case "Duplicate":
                        if (!HasRule) return;
                        if (run) DuplicateRule();
                        e.Use();
                        break;
                    case "Delete":
                    case "SoftDelete":
                        if (!HasRule) return;
                        if (run) RemoveRule(notify: true);
                        e.Use();
                        break;
                }
            }
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (EditorGUILayout.DropdownButton(new GUIContent("Open"), FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(55)))
                    ShowOpenMenu();
                if (GUILayout.Button("New…", EditorStyles.toolbarButton, GUILayout.Width(45))) NewFile();

                using (new EditorGUI.DisabledScope(doc == null))
                {
                    if (GUILayout.Button(new GUIContent("Save", "Ctrl+S"), EditorStyles.toolbarButton, GUILayout.Width(45))) pending.Add(Save);
                    if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(50)))
                        pending.Add(() => { if (ConfirmDiscard()) LoadFromDisk(docPath); });
                }

                string file = doc != null || loadErrorPath != null ? docPath : "(no file open)";
                GUILayout.Label((hasUnsavedChanges ? "* " : "") + file, EditorStyles.toolbarButton, GUILayout.MinWidth(80));
                GUILayout.FlexibleSpace();

                tab = GUILayout.Toolbar(tab, Tabs, EditorStyles.toolbarButton, GUILayout.Width(180));
                GUILayout.Space(6);
                var t = (DialogStringTable)EditorGUILayout.ObjectField(table, typeof(DialogStringTable), false, GUILayout.Width(170));
                if (t != table) SetTable(t);

                if (EditorGUILayout.DropdownButton(GUIContent.none, FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(20)))
                {
                    var menu = new GenericMenu();
                    if (!string.IsNullOrEmpty(docPath))
                    {
                        menu.AddItem(new GUIContent("Show File in Project"), false, () => EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(docPath)));
                        menu.AddItem(new GUIContent("Open File in Text Editor"), false, () => InternalEditorUtility.OpenFileAtLineExternal(docPath, 1));
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Show File in Project"));
                        menu.AddDisabledItem(new GUIContent("Open File in Text Editor"));
                    }
                    menu.AddItem(new GUIContent("Debugger Window"), false, () => EditorApplication.ExecuteMenuItem("Window/Dynamic Dialog/Debugger"));
                    menu.AddSeparator("");
                    menu.AddDisabledItem(new GUIContent("Shortcuts: ↑↓ select · Ctrl+D duplicate · Del remove · Ctrl+C/V copy & paste · Alt+← back"));
                    menu.ShowAsContext();
                }
            }
        }

        void ShowOpenMenu()
        {
            var menu = new GenericMenu();
            var files = AssetDatabase.GetAllAssetPaths().Where(IsDlg).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var p in files)
            {
                string path = p;
                menu.AddItem(new GUIContent(path.Replace("/", "∕")), path == docPath && doc != null, () => Open(path));
            }
            if (files.Count == 0) menu.AddDisabledItem(new GUIContent("No .dlg files in the project"));
            menu.ShowAsContext();
        }

        void DrawNoDocument()
        {
            GUILayout.Space(10);
            if (loadErrorPath != null)
            {
                EditorGUILayout.HelpBox($"{loadErrorPath} has syntax errors, so it can't be edited here without losing lines. " +
                                        "Fix the errors listed below in a text editor, then reload.", MessageType.Error);
                using (new EditorGUILayout.HorizontalScope())
                {
                    int line = messages?.FirstOrDefault().Line ?? 1;
                    if (GUILayout.Button("Open in Text Editor")) InternalEditorUtility.OpenFileAtLineExternal(loadErrorPath, Mathf.Max(1, line));
                    if (GUILayout.Button("Reload")) pending.Add(() => LoadFromDisk(loadErrorPath));
                }
                GUILayout.FlexibleSpace();
                return;
            }

            EditorGUILayout.HelpBox("Open a .dlg file to edit its rules, or create a new one.", MessageType.Info);
            foreach (var p in AssetDatabase.GetAllAssetPaths().Where(IsDlg).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string path = p;
                if (GUILayout.Button(path, EditorStyles.linkLabel)) pending.Add(() => LoadFromDisk(path));
            }
            GUILayout.FlexibleSpace();
        }

        void DrawBanners()
        {
            if (externalChange)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(new GUIContent(warnIcon), GUILayout.Width(20));
                    GUILayout.Label("The file changed on disk while you have unsaved edits here.", EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("Reload From Disk", GUILayout.Width(120))) pending.Add(() => LoadFromDisk(docPath));
                    if (GUILayout.Button("Keep Mine", GUILayout.Width(80)))
                    {
                        externalChange = false;
                        loadedWriteTicks = File.GetLastWriteTimeUtc(docPath).Ticks;
                    }
                }
            }
            for (int i = 0; i < notes.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(new GUIContent(infoIcon), GUILayout.Width(20));
                    GUILayout.Label(notes[i], EditorStyles.wordWrappedMiniLabel);
                    int index = i;
                    if (GUILayout.Button("×", GUILayout.Width(20))) pending.Add(() => notes.RemoveAt(index));
                }
            }
        }

        // ---- Rules tab ----------------------------------------------------------------------------

        void DrawRulesTab()
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(listWidth)))
                    DrawRuleList();
                DrawSplitter();
                using (new EditorGUILayout.VerticalScope())
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    if (selected == FileSettingsIndex) DrawFileSettings();
                    else if (HasRule) DrawRule(doc.rules[selected]);
                    else EditorGUILayout.HelpBox("Select a rule on the left, or add one with +.", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        void DrawSplitter()
        {
            var rect = GUILayoutUtility.GetRect(4, 4, GUILayout.ExpandHeight(true), GUILayout.Width(4));
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y, 1, rect.height), new Color(0, 0, 0, 0.3f));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            var e = Event.current;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition)) { draggingSplitter = true; e.Use(); }
            else if (draggingSplitter && e.type == EventType.MouseDrag)
            {
                listWidth = Mathf.Clamp(listWidth + e.delta.x, 160f, position.width - 300f);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp) draggingSplitter = false;
        }

        void DrawRuleList()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                groupBySpeaker = GUILayout.Toggle(groupBySpeaker, new GUIContent("By speaker", "Group the rules by who says them"),
                    EditorStyles.miniButton, GUILayout.Width(72));
            }

            listControl = GUIUtility.GetControlID(FocusType.Passive);
            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            visibleRows.Clear();
            rowRects.Clear();

            DrawListRow(FileSettingsIndex, "File settings", "Header comment, @partition", false, 0);
            visibleRows.Add(FileSettingsIndex);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dupes = new HashSet<string>(doc.rules.Where(r => !names.Add(r.name ?? "")).Select(r => r.name ?? ""), StringComparer.OrdinalIgnoreCase);
            var shown = Enumerable.Range(0, doc.rules.Count).Where(i => Matches(doc.rules[i], search)).ToList();

            if (groupBySpeaker)
            {
                var groups = shown.GroupBy(i => doc.rules[i].who ?? "", StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g.Key.Length == 0 ? 0 : 1).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                {
                    string key = g.Key;
                    bool open = !collapsedGroups.Contains(key) || !string.IsNullOrWhiteSpace(search);
                    var header = GUILayoutUtility.GetRect(10, 20, GUILayout.ExpandWidth(true));
                    if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(header, new Color(0f, 0f, 0f, 0.1f));
                    bool now = EditorGUI.Foldout(new Rect(header.x + 4, header.y + 1, header.width - 8, 18), open,
                        $"{(key.Length == 0 ? "Anyone" : key)}  ({g.Count()})", true, groupFoldout);
                    if (now != open)
                    {
                        if (now) collapsedGroups.Remove(key);
                        else collapsedGroups.Add(key);
                    }
                    if (!open) continue;
                    foreach (int i in g) DrawRuleRow(i, dupes, showWho: false, indent: 10);
                }
            }
            else
            {
                foreach (int i in shown) DrawRuleRow(i, dupes, showWho: true, indent: 0);
            }

            if (shown.Count == 0 && doc.rules.Count > 0)
                GUILayout.Label($"No rules match “{search}”.", EditorStyles.centeredGreyMiniLabel);

            HandleListDrag(canDrag: !groupBySpeaker && string.IsNullOrWhiteSpace(search));
            EditorGUILayout.EndScrollView();
            KeepSelectedInView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("+", "Add a rule below the selected one"))) AddRule();
                using (new EditorGUI.DisabledScope(!HasRule))
                {
                    if (GUILayout.Button(new GUIContent("Duplicate", "Copy the selected rule (Ctrl+D)"))) DuplicateRule();
                    if (GUILayout.Button(new GUIContent("−", "Remove the selected rule (Delete)"))) RemoveRule();
                    using (new EditorGUI.DisabledScope(selected <= 0))
                        if (GUILayout.Button(new GUIContent("▲", "Move up"), GUILayout.Width(24))) MoveRule(-1);
                    using (new EditorGUI.DisabledScope(!HasRule || selected >= doc.rules.Count - 1))
                        if (GUILayout.Button(new GUIContent("▼", "Move down"), GUILayout.Width(24))) MoveRule(1);
                }
            }
            string count = shown.Count == doc.rules.Count ? $"{doc.rules.Count} rules" : $"{shown.Count} of {doc.rules.Count} rules";
            GUILayout.Label(count + (groupBySpeaker || !string.IsNullOrWhiteSpace(search) ? "" : " · drag to reorder"), EditorStyles.centeredGreyMiniLabel);
        }

        void DrawRuleRow(int i, HashSet<string> dupes, bool showWho, float indent)
        {
            var r = doc.rules[i];
            string concept = string.IsNullOrEmpty(r.concept) ? "any concept" : r.concept;
            var parts = new List<string>();
            if (showWho) parts.Add(string.IsNullOrEmpty(r.who) ? "anyone" : r.who);
            parts.Add(concept);
            if (r.criteria.Count > 0) parts.Add($"{r.criteria.Count} if");
            parts.Add($"{r.lines.Count} line{(r.lines.Count == 1 ? "" : "s")}");
            if (r.followups.Count > 0) parts.Add("→ " + r.followups.Count);
            if (r.once) parts.Add("once");

            bool problem = string.IsNullOrWhiteSpace(r.name) || dupes.Contains(r.name ?? "");
            DrawListRow(i, string.IsNullOrWhiteSpace(r.name) ? "(unnamed)" : r.name, string.Join(" · ", parts), problem, indent);
            visibleRows.Add(i);
        }

        static bool Matches(DlgRule r, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            bool Has(string s) => !string.IsNullOrEmpty(s) && s.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            return Has(r.name) || Has(r.who) || Has(r.concept) || r.lines.Any(l => Has(l.text))
                   || r.criteria.Any(c => Has(c.key) || Has(c.value)) || r.writes.Any(w => Has(w.key))
                   || r.followups.Any(f => Has(f.concept) || Has(f.character)) || Has(r.comment);
        }

        void DrawListRow(int index, string title, string subtitle, bool problem, float indent)
        {
            var rect = GUILayoutUtility.GetRect(10, 34, GUILayout.ExpandWidth(true));
            rowRects[index] = rect;
            var e = Event.current;
            Texture icon = null;
            if (index >= 0 && ruleSeverity.TryGetValue(doc.rules[index].name ?? "", out var sev))
                icon = sev == MessageSeverity.Error ? errorIcon : sev == MessageSeverity.Warning ? warnIcon : null;

            if (e.type == EventType.Repaint)
            {
                if (index == selected) EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.36f, 0.53f) : new Color(0.24f, 0.49f, 0.91f, 0.45f));
                else if (rect.Contains(e.mousePosition)) EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.1f));
                if (dragging && index == dragIndex) EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.2f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), new Color(0, 0, 0, 0.12f));
            }
            float right = icon != null ? 20 : 0;
            var old = GUI.contentColor;
            if (problem) GUI.contentColor = new Color(1f, 0.55f, 0.45f);
            GUI.Label(new Rect(rect.x + 6 + indent, rect.y + 2, rect.width - 8 - indent - right, 16), index == FileSettingsIndex ? "⚙ " + title : title, rowTitle);
            GUI.contentColor = old;
            GUI.Label(new Rect(rect.x + 6 + indent, rect.y + 17, rect.width - 8 - indent, 14), subtitle, rowSubtitle);
            if (icon != null) GUI.DrawTexture(new Rect(rect.xMax - 20, rect.y + 2, 16, 16), icon, ScaleMode.ScaleToFit);

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                // Selection changes wait for the end of the pass, so this event's layout stays consistent.
                pending.Add(() =>
                {
                    Select(index);
                    scrollToSelected = false;
                });
                if (e.button == 0 && index >= 0)
                {
                    dragIndex = index;
                    dragStart = e.mousePosition;
                    GUIUtility.hotControl = listControl;
                }
                else if (e.button == 1 && index >= 0) ShowRuleMenu(index);
                e.Use();
            }
        }

        void ShowRuleMenu(int index)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate"), false, () => { selected = index; DuplicateRule(); });
            menu.AddItem(new GUIContent("Copy as .dlg Text"), false, () => CopyRule(index));
            menu.AddItem(new GUIContent("Paste Rules Below"), false, () => { selected = index; PasteRules(); });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Add Rule Below"), false, () => { selected = index; AddRule(); });
            if (index > 0) menu.AddItem(new GUIContent("Move to Top"), false, () => MoveRuleTo(index, 0));
            else menu.AddDisabledItem(new GUIContent("Move to Top"));
            if (index < doc.rules.Count - 1) menu.AddItem(new GUIContent("Move to Bottom"), false, () => MoveRuleTo(index, doc.rules.Count - 1));
            else menu.AddDisabledItem(new GUIContent("Move to Bottom"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Remove"), false, () => { selected = index; RemoveRule(notify: true); });
            menu.ShowAsContext();
        }

        /// <summary>Drag a rule row up or down to move it (only in the plain, unfiltered list).</summary>
        void HandleListDrag(bool canDrag)
        {
            var e = Event.current;
            if (dragIndex < 0) return;
            var type = e.GetTypeForControl(listControl);
            if (type == EventType.MouseDrag)
            {
                if (!canDrag) return;
                if (!dragging && (e.mousePosition - dragStart).sqrMagnitude > 16f) dragging = true;
                if (dragging)
                {
                    dropIndex = doc.rules.Count;
                    for (int i = 0; i < doc.rules.Count; i++)
                    {
                        if (!rowRects.TryGetValue(i, out var r)) continue;
                        if (e.mousePosition.y < r.center.y) { dropIndex = i; break; }
                    }
                    e.Use();
                    Repaint();
                }
            }
            else if (type == EventType.MouseUp)
            {
                if (GUIUtility.hotControl == listControl) GUIUtility.hotControl = 0;
                e.Use();
                if (dragging && dropIndex >= 0)
                {
                    int to = dropIndex > dragIndex ? dropIndex - 1 : dropIndex;
                    if (to != dragIndex) MoveRuleTo(dragIndex, to);
                }
                dragging = false;
                dragIndex = dropIndex = -1;
                Repaint();
            }
            else if (e.type == EventType.Repaint && dragging && dropIndex >= 0)
            {
                float y;
                if (dropIndex < doc.rules.Count && rowRects.TryGetValue(dropIndex, out var r)) y = r.y;
                else if (rowRects.TryGetValue(doc.rules.Count - 1, out var last)) y = last.yMax;
                else return;
                var any = rowRects.Values.First();
                EditorGUI.DrawRect(new Rect(any.x, y - 1, any.width, 2), new Color(0.3f, 0.6f, 1f));
            }
        }

        void KeepSelectedInView()
        {
            if (!scrollToSelected || Event.current.type != EventType.Repaint) return;
            var view = GUILayoutUtility.GetLastRect();
            if (!rowRects.TryGetValue(selected, out var row)) { scrollToSelected = false; return; }
            float y = listScroll.y;
            if (row.y < y) y = row.y;
            else if (row.yMax > y + view.height) y = row.yMax - view.height;
            scrollToSelected = false;
            if (!Mathf.Approximately(y, listScroll.y))
            {
                listScroll.y = y;
                Repaint();
            }
        }

        string UniqueRuleName(string baseName)
        {
            var taken = new HashSet<string>(doc.rules.Select(r => r.name), StringComparer.OrdinalIgnoreCase);
            if (!taken.Contains(baseName)) return baseName;
            for (int i = 2; ; i++)
                if (!taken.Contains(baseName + i)) return baseName + i;
        }

        void AddRule()
        {
            Modify("Add Dialog Rule", () =>
            {
                var current = HasRule ? doc.rules[selected] : null;
                InsertNewRule(current?.who ?? "", current?.concept ?? "", (current?.who ?? "") + "NewRule");
            });
        }

        /// <summary>Inserts a rule with one empty line below the selection and selects it. Call inside Modify.</summary>
        void InsertNewRule(string who, string concept, string name)
        {
            var rule = new DlgRule { name = UniqueRuleName(name), who = who, concept = concept };
            rule.lines.Add(new DlgLine());
            int at = HasRule ? selected + 1 : doc.rules.Count;
            doc.rules.Insert(at, rule);
            Select(at);
        }

        void DuplicateRule()
        {
            Modify("Duplicate Dialog Rule", () =>
            {
                var copy = doc.rules[selected].Clone();
                copy.name = UniqueRuleName(copy.name);
                copy.comment = "";
                doc.rules.Insert(selected + 1, copy);
                Select(selected + 1, remember: false);
            });
        }

        void RemoveRule(bool notify = false)
        {
            string name = HasRule ? doc.rules[selected].name : null;
            Modify("Remove Dialog Rule", () =>
            {
                doc.rules.RemoveAt(selected);
                selected = Mathf.Min(selected, doc.rules.Count - 1);
                if (selected < 0) selected = FileSettingsIndex;
                if (notify) ShowNotification(new GUIContent($"Removed {name} (Ctrl+Z to undo)"), 1.5);
            });
        }

        void MoveRule(int delta) => MoveRuleTo(selected, selected + delta);

        void MoveRuleTo(int from, int to)
        {
            Modify("Move Dialog Rule", () =>
            {
                if (from < 0 || from >= doc.rules.Count || to < 0 || to >= doc.rules.Count || from == to) return;
                var rule = doc.rules[from];
                doc.rules.RemoveAt(from);
                doc.rules.Insert(to, rule);
                selected = to;
                scrollToSelected = true;
            });
        }

        static string RuleText(DlgRule rule)
        {
            string text = DialogScriptWriter.ToDlg(new[] { DlgDocument.ToSource(rule) }).TrimEnd('\n');
            if (string.IsNullOrWhiteSpace(rule.comment)) return text;
            var comment = string.Join("\n", rule.comment.Split('\n').Select(l =>
            {
                string s = l.Trim();
                return s.Length == 0 || s.StartsWith("#") || s.StartsWith("//") ? l.TrimEnd() : "# " + l.TrimEnd();
            }));
            return comment + "\n" + text;
        }

        void CopyRule(int index)
        {
            EditorGUIUtility.systemCopyBuffer = RuleText(doc.rules[index]) + "\n";
            ShowNotification(new GUIContent($"Copied {doc.rules[index].name}"), 1.0);
        }

        /// <summary>Pastes .dlg text from the clipboard as new rules below the selection (renamed if the names are taken).</summary>
        void PasteRules()
        {
            string text = EditorGUIUtility.systemCopyBuffer;
            var parsed = string.IsNullOrWhiteSpace(text) ? null : DlgDocument.Load("clipboard", text, out _);
            try
            {
                if (parsed == null || parsed.rules.Count == 0)
                {
                    ShowNotification(new GUIContent("The clipboard doesn't hold any .dlg rules"), 1.5);
                    return;
                }
                var rules = parsed.rules.Select(r => r.Clone()).ToList();
                if (rules.Count > 0 && string.IsNullOrWhiteSpace(rules[0].comment)) rules[0].comment = parsed.header;
                Modify("Paste Dialog Rules", () =>
                {
                    int at = HasRule ? selected + 1 : doc.rules.Count;
                    foreach (var r in rules)
                    {
                        r.name = UniqueRuleName(r.name);
                        doc.rules.Insert(at++, r);
                    }
                    Select(at - 1);
                    ShowNotification(new GUIContent($"Pasted {rules.Count} rule(s)"), 1.0);
                });
            }
            finally
            {
                if (parsed != null) DestroyImmediate(parsed);
            }
        }

        // ---- File settings ------------------------------------------------------------------------

        void DrawFileSettings()
        {
            Section("File settings");
            GUILayout.Label("Header comment (top of the file)", EditorStyles.miniBoldLabel);
            SetText(ref doc.header, EditorGUILayout.TextArea(doc.header ?? "", wrapArea, GUILayout.MinHeight(60)));

            GUILayout.Space(8);
            GUILayout.Label(new GUIContent("@partition keys", "Fact keys the engine buckets rules by. Leave empty for the default."), EditorStyles.miniBoldLabel);
            for (int i = 0; i < doc.partitionKeys.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string v = NamePopup(null, doc.partitionKeys[i], DialogStringCategory.Facts, null);
                    if (v != doc.partitionKeys[i]) { Record("Edit Partition", false); doc.partitionKeys[i] = v; Changed(); }
                    int index = i;
                    if (RemoveButton()) Modify("Remove Partition Key", () => doc.partitionKeys.RemoveAt(index));
                }
            }
            if (GUILayout.Button("Add Partition Key", GUILayout.Width(140))) Modify("Add Partition Key", () => doc.partitionKeys.Add(""));

            GUILayout.Space(8);
            GUILayout.Label("Trailing comment (end of the file)", EditorStyles.miniBoldLabel);
            SetText(ref doc.trailer, EditorGUILayout.TextArea(doc.trailer ?? "", wrapArea, GUILayout.MinHeight(40)));
            EditorGUILayout.HelpBox("Comment lines that don't start with # or // get '# ' added when you save.", MessageType.None);

            GUILayout.Space(8);
            Section("Overview");
            var speakers = doc.rules.GroupBy(r => string.IsNullOrEmpty(r.who) ? "(anyone)" : r.who).OrderByDescending(g => g.Count());
            foreach (var g in speakers)
                GUILayout.Label($"{g.Key}: {g.Count()} rule(s), {g.Sum(r => r.lines.Count)} line(s)", EditorStyles.miniLabel);
            int words = doc.rules.Sum(r => r.lines.Sum(l => (l.text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length));
            GUILayout.Label($"{doc.rules.Sum(r => r.lines.Count)} lines, about {words} words in total.", EditorStyles.miniBoldLabel);
        }

        // ---- One rule -----------------------------------------------------------------------------

        static readonly CriterionOp[] OpValues =
        {
            CriterionOp.Equals, CriterionOp.NotEquals, CriterionOp.Less, CriterionOp.LessOrEqual, CriterionOp.Greater,
            CriterionOp.GreaterOrEqual, CriterionOp.Between, CriterionOp.Exists, CriterionOp.ZeroOrMissing,
        };
        static readonly string[] OpLabels = { "==", "!=", "<", "<=", ">", ">=", "in range", "exists", "missing" };
        static readonly string[] ValueModes = { "Number", "Name" };

        static readonly ResponseSelection[] SelectionValues =
            { ResponseSelection.RandomNoRepeat, ResponseSelection.Random, ResponseSelection.Shuffle, ResponseSelection.Sequential };
        static readonly string[] SelectionLabels =
            { "Random, no repeats (default)", "Random", "Shuffle (all before repeating)", "Sequential" };

        static readonly FactScope[] ScopeValues = { FactScope.Speaker, FactScope.World, FactScope.Character };
        static readonly string[] ScopeLabels = { "Speaker", "World", "Character" };
        static readonly string[] WriteOpLabels = { "=", "+=", "forget" };
        static readonly FactOp[] WriteOpValues = { FactOp.Set, FactOp.Add, FactOp.Remove };

        static readonly FollowupTarget[] TargetValues = { FollowupTarget.Character, FollowupTarget.Self, FollowupTarget.Any, FollowupTarget.All };
        static readonly string[] TargetLabels = { "Character", "Self (same speaker)", "Any listener (best reply)", "All listeners" };

        void DrawRule(DlgRule rule)
        {
            var dup = doc.rules.Count(r => string.Equals(r.name, rule.name, StringComparison.OrdinalIgnoreCase)) > 1;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(history.Count == 0))
                    if (GUILayout.Button(new GUIContent("◀ Back", history.Count > 0 ? $"Back to {history[history.Count - 1]} (Alt+←)" : "Nothing to go back to"),
                            EditorStyles.miniButton, GUILayout.Width(56)))
                        pending.Add(GoBack);
                GUILayout.Label($"Rule {selected + 1} of {doc.rules.Count}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Copy", "Copy this rule as .dlg text (Ctrl+C)"), EditorStyles.miniButton, GUILayout.Width(44)))
                    CopyRule(selected);
            }

            DrawRuleProblems(rule);

            Section("Rule");
            SetText(ref rule.name, EditorGUILayout.TextField(new GUIContent("Name", "Unique name, shown in the debugger and used by save data."), rule.name));
            if (string.IsNullOrWhiteSpace(rule.name)) EditorGUILayout.HelpBox("Give the rule a name.", MessageType.Error);
            else if (dup) EditorGUILayout.HelpBox("Another rule has this name. Names should be unique.", MessageType.Warning);
            Set(ref rule.who, NamePopup(new GUIContent("Who", "The speaker this rule is for."), rule.who, DialogStringCategory.Characters, "(anyone)"));
            Set(ref rule.concept, NamePopup(new GUIContent("Concept", "The event this rule answers."), rule.concept, DialogStringCategory.Concepts, "(any concept)"));

            // Conditions.
            if (Foldout(ref rule.foldConditions, $"Conditions (if) — {rule.criteria.Count}"))
            {
                for (int i = 0; i < rule.criteria.Count; i++) DrawCriterion(rule, i);
                if (rule.criteria.Count == 0) GUILayout.Label("No conditions: matches whenever who and concept match.", EditorStyles.miniLabel);
                if (GUILayout.Button("Add Condition", GUILayout.Width(120)))
                    Modify("Add Condition", () => rule.criteria.Add(new DlgCriterion { key = FirstName(DialogStringCategory.Facts) }));
            }

            // Lines.
            if (Foldout(ref rule.foldLines, $"Lines (say) — {rule.lines.Count}"))
            {
                int si = Array.IndexOf(SelectionValues, rule.selection);
                int ns = EditorGUILayout.Popup(new GUIContent("Pick lines", "How the rule chooses among its lines."), Mathf.Max(0, si), SelectionLabels);
                Set(ref rule.selection, SelectionValues[ns]);
                for (int i = 0; i < rule.lines.Count; i++) DrawLine(rule, i);
                if (GUILayout.Button("Add Line", GUILayout.Width(120))) Modify("Add Line", () => rule.lines.Add(new DlgLine()));
            }

            // Fact writes.
            if (Foldout(ref rule.foldWrites, $"Remember / forget — {rule.writes.Count}"))
            {
                for (int i = 0; i < rule.writes.Count; i++) DrawWrite(rule, i);
                if (GUILayout.Button("Add Fact Change", GUILayout.Width(120)))
                    Modify("Add Fact Change", () => rule.writes.Add(new DlgWrite { key = FirstName(DialogStringCategory.Facts) }));
            }

            // Followups.
            if (Foldout(ref rule.foldFollowups, $"Then (followups) — {rule.followups.Count}"))
            {
                for (int i = 0; i < rule.followups.Count; i++) DrawFollowup(rule, i);
                if (GUILayout.Button("Add Followup", GUILayout.Width(120)))
                    Modify("Add Followup", () => rule.followups.Add(new DlgFollowup
                    {
                        character = FirstName(DialogStringCategory.Characters),
                        concept = FirstName(DialogStringCategory.Concepts),
                    }));
            }

            // Options.
            if (Foldout(ref rule.foldOptions, "Options"))
            {
                Set(ref rule.once, EditorGUILayout.Toggle(new GUIContent("Once", "Plays at most once."), rule.once));
                Set(ref rule.cooldown, Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Cooldown (s)", "Seconds before the rule can play again. 0 = none."), rule.cooldown)));
                Set(ref rule.weight, EditorGUILayout.FloatField(new GUIContent("Weight", "Relative chance among equally good matches. 1 = normal."), rule.weight));
                Set(ref rule.bonus, EditorGUILayout.IntField(new GUIContent("Score bonus", "Added to the rule's score, so it wins over rules with as many conditions."), rule.bonus));
            }

            if (Foldout(ref rule.foldComment, "Comment" + (string.IsNullOrWhiteSpace(rule.comment) ? "" : " ✎")))
            {
                SetText(ref rule.comment, EditorGUILayout.TextArea(rule.comment ?? "", wrapArea, GUILayout.MinHeight(40)));
                GUILayout.Label("Written above the rule. Lines that don't start with # or // get '# ' added.", EditorStyles.miniLabel);
            }

            if (Foldout(ref foldLinks, "Links to other rules")) DrawLinks(rule);

            if (Foldout(ref foldPreview, "Preview (.dlg text)"))
            {
                string text = RuleText(rule);
                float h = previewStyle.CalcHeight(new GUIContent(text), 10000f) + 6f;
                EditorGUILayout.SelectableLabel(text, previewStyle, GUILayout.Height(h));
            }
            GUILayout.Space(12);
        }

        void DrawRuleProblems(DlgRule rule)
        {
            if (messages == null || string.IsNullOrEmpty(rule.name)) return;
            foreach (var m in messages)
            {
                if (m.Severity == MessageSeverity.Info || !string.Equals(m.Rule, rule.name, StringComparison.OrdinalIgnoreCase)) continue;
                EditorGUILayout.HelpBox(m.Text, m.Severity == MessageSeverity.Error ? MessageType.Error : MessageType.Warning);
            }
        }

        void DrawCriterion(DlgRule rule, int i)
        {
            var c = rule.criteria[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(i == 0 ? "if" : "and", GUILayout.Width(24));
                Set(ref c.key, NamePopup(null, c.key, DialogStringCategory.Facts, null, GUILayout.MinWidth(90)));

                int oi = Array.IndexOf(OpValues, c.op);
                int no = EditorGUILayout.Popup(Mathf.Max(0, oi), OpLabels, GUILayout.Width(72));
                if (OpValues[no] != c.op)
                {
                    Record("Edit Condition", false);
                    c.op = OpValues[no];
                    bool namesAllowed = c.op == CriterionOp.Equals || c.op == CriterionOp.NotEquals;
                    if (!namesAllowed && c.valueIsName) { c.valueIsName = false; c.value = "0"; }
                    if (c.op == CriterionOp.Between && DlgDocument.ParseNumber(c.value2) < DlgDocument.ParseNumber(c.value)) c.value2 = c.value;
                    Changed();
                }

                if (c.op == CriterionOp.Between)
                {
                    NumberField(ref c.value, 50);
                    GUILayout.Label("..", GUILayout.Width(14));
                    NumberField(ref c.value2, 50);
                }
                else if (c.UsesValue)
                {
                    if (c.op == CriterionOp.Equals || c.op == CriterionOp.NotEquals) ValueField(ref c.valueIsName, ref c.value);
                    else NumberField(ref c.value, 60);
                }

                if (c.UsesValue)
                {
                    Set(ref c.hasDefault, GUILayout.Toggle(c.hasDefault, new GUIContent("default", "When the fact is missing, compare this value instead of failing."), GUILayout.Width(60)));
                    if (c.hasDefault) Set(ref c.defaultValue, EditorGUILayout.FloatField(c.defaultValue, GUILayout.Width(40)));
                }
                else GUILayout.FlexibleSpace();

                if (RemoveButton()) Modify("Remove Condition", () => rule.criteria.RemoveAt(i));
            }
        }

        void DrawLine(DlgRule rule, int i)
        {
            var l = rule.lines[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label((i + 1) + ".", GUILayout.Width(20));
                    SetText(ref l.text, EditorGUILayout.TextArea(l.text ?? "", wrapArea, GUILayout.MinHeight(20)));
                    using (new EditorGUI.DisabledScope(i == 0))
                        if (GUILayout.Button("▲", GUILayout.Width(22))) Modify("Move Line", () => Swap(rule.lines, i, i - 1));
                    using (new EditorGUI.DisabledScope(i == rule.lines.Count - 1))
                        if (GUILayout.Button("▼", GUILayout.Width(22))) Modify("Move Line", () => Swap(rule.lines, i, i + 1));
                    if (RemoveButton()) Modify("Remove Line", () => rule.lines.RemoveAt(i));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(24);
                    GUILayout.Label("anim", GUILayout.Width(32));
                    Set(ref l.anim, NamePopup(null, l.anim, DialogStringCategory.Animations, "(none)", GUILayout.MinWidth(70)));
                    GUILayout.Label("audio", GUILayout.Width(36));
                    Set(ref l.audio, NamePopup(null, l.audio, DialogStringCategory.Audio, "(none)", GUILayout.MinWidth(70)));
                    GUILayout.Label(new GUIContent("dur", "Seconds the line lasts. 0 = from the audio clip or text length."), GUILayout.Width(26));
                    Set(ref l.duration, Mathf.Max(0f, EditorGUILayout.FloatField(l.duration, GUILayout.Width(40))));
                    GUILayout.Label(new GUIContent("key", "Localization key. Empty = automatic."), GUILayout.Width(26));
                    SetText(ref l.key, EditorGUILayout.TextField(l.key ?? "", GUILayout.MinWidth(50)));
                }
                if (string.IsNullOrEmpty(l.text) && string.IsNullOrEmpty(l.audio))
                    GUILayout.Label("This line is empty.", EditorStyles.miniLabel);
                else if (!string.IsNullOrEmpty(l.text) && l.duration <= 0f && string.IsNullOrEmpty(l.audio))
                {
                    int words = l.text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
                    GUILayout.Label($"{l.text.Length} characters · {words} word{(words == 1 ? "" : "s")}", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        void DrawWrite(DlgRule rule, int i)
        {
            var w = rule.writes[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                int si = EditorGUILayout.Popup(Mathf.Max(0, Array.IndexOf(ScopeValues, w.scope)), ScopeLabels, GUILayout.Width(80));
                Set(ref w.scope, ScopeValues[si]);
                if (w.scope == FactScope.Character)
                    Set(ref w.character, NamePopup(null, w.character, DialogStringCategory.Characters, null, GUILayout.MinWidth(70)));
                Set(ref w.key, NamePopup(null, w.key, DialogStringCategory.Facts, null, GUILayout.MinWidth(90)));

                int oi = EditorGUILayout.Popup(Mathf.Max(0, Array.IndexOf(WriteOpValues, w.op)), WriteOpLabels, GUILayout.Width(60));
                if (WriteOpValues[oi] != w.op)
                {
                    Record("Edit Fact Change", false);
                    w.op = WriteOpValues[oi];
                    if (w.op != FactOp.Set && w.valueIsName) { w.valueIsName = false; w.value = "1"; }
                    Changed();
                }

                if (w.op == FactOp.Set) ValueField(ref w.valueIsName, ref w.value);
                else if (w.op == FactOp.Add) NumberField(ref w.value, 60);

                if (w.op != FactOp.Remove)
                {
                    GUILayout.Label(new GUIContent("for", "Seconds until the fact expires. 0 = keep it."), GUILayout.Width(22));
                    Set(ref w.duration, Mathf.Max(0f, EditorGUILayout.FloatField(w.duration, GUILayout.Width(40))));
                    GUILayout.Label("s", GUILayout.Width(10));
                }
                else GUILayout.FlexibleSpace();

                if (RemoveButton()) Modify("Remove Fact Change", () => rule.writes.RemoveAt(i));
            }
        }

        void DrawFollowup(DlgRule rule, int i)
        {
            var f = rule.followups[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("then", GUILayout.Width(30));
                int ti = EditorGUILayout.Popup(Mathf.Max(0, Array.IndexOf(TargetValues, f.target)), TargetLabels, GUILayout.Width(110));
                Set(ref f.target, TargetValues[ti]);
                if (f.target == FollowupTarget.Character)
                    Set(ref f.character, NamePopup(null, f.character, DialogStringCategory.Characters, null, GUILayout.MinWidth(70)));
                Set(ref f.concept, NamePopup(null, f.concept, DialogStringCategory.Concepts, null, GUILayout.MinWidth(90)));
                GUILayout.Label("after", GUILayout.Width(32));
                Set(ref f.delay, Mathf.Max(0f, EditorGUILayout.FloatField(f.delay, GUILayout.Width(40))));
                GUILayout.Label("s", GUILayout.Width(10));
                if (RemoveButton()) Modify("Remove Followup", () => rule.followups.RemoveAt(i));
            }

            // Which rules in this file could answer it, so the conversation can be followed (or continued).
            if (string.IsNullOrWhiteSpace(f.concept)) return;
            var answers = Answers(rule, f).ToList();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(34);
                if (answers.Count == 0)
                {
                    GUILayout.Label("Nothing in this file answers this yet.", badStyle);
                    string who = FollowupSpeaker(rule, f) ?? "";
                    if (GUILayout.Button(new GUIContent("Create Answer", $"Add a rule for {(who.Length > 0 ? who : "anyone")} answering {f.concept}"),
                            EditorStyles.miniButton, GUILayout.Width(96)))
                        Modify("Add Answer Rule", () => InsertNewRule(who, f.concept, who + f.concept));
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    GUILayout.Label("answered by", EditorStyles.miniLabel, GUILayout.Width(66));
                    foreach (int a in answers.Take(4)) RuleLink(a, compact: true);
                    if (answers.Count > 4) GUILayout.Label($"+{answers.Count - 4} more", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        // ---- Links between rules ------------------------------------------------------------------

        /// <summary>The character a followup goes to, or null when it goes to whoever is listening.</summary>
        static string FollowupSpeaker(DlgRule caller, DlgFollowup f)
        {
            switch (f.target)
            {
                case FollowupTarget.Character: return string.IsNullOrWhiteSpace(f.character) ? null : f.character;
                case FollowupTarget.Self: return string.IsNullOrWhiteSpace(caller.who) ? null : caller.who;
                default: return null;
            }
        }

        /// <summary>Rules whose who and concept fit a followup. Conditions aren't checked: these are rules that could answer.</summary>
        IEnumerable<int> Answers(DlgRule caller, DlgFollowup f)
        {
            string who = FollowupSpeaker(caller, f);
            for (int i = 0; i < doc.rules.Count; i++)
            {
                var r = doc.rules[i];
                if (string.IsNullOrEmpty(r.concept) || r.concept != f.concept) continue;
                if (who != null && !string.IsNullOrEmpty(r.who) && r.who != who) continue;
                if (f.target == FollowupTarget.Any || f.target == FollowupTarget.All)
                    if (!string.IsNullOrEmpty(r.who) && r.who == caller.who) continue; // listeners, not the speaker
                yield return i;
            }
        }

        void DrawLinks(DlgRule rule)
        {
            int self = doc.rules.IndexOf(rule);

            var callers = new List<int>();
            if (!string.IsNullOrEmpty(rule.concept))
                for (int i = 0; i < doc.rules.Count; i++)
                    if (doc.rules[i].followups.Any(f => Answers(doc.rules[i], f).Contains(self))) callers.Add(i);
            LinkList("Led to by (followups that reach this rule)", callers,
                string.IsNullOrEmpty(rule.concept) ? "This rule answers any concept, so it isn't linked." :
                "None. This rule only plays when game code sends " + rule.concept + ".");

            foreach (var key in rule.criteria.Select(c => c.key).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct())
            {
                var setters = Enumerable.Range(0, doc.rules.Count).Where(i => i != self && doc.rules[i].writes.Any(w => w.key == key)).ToList();
                LinkList($"Checks “{key}”, which is set by", setters, "No rule in this file; game code or another file sets it.");
            }

            foreach (var key in rule.writes.Select(w => w.key).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct())
            {
                var readers = Enumerable.Range(0, doc.rules.Count).Where(i => i != self && doc.rules[i].criteria.Any(c => c.key == key)).ToList();
                LinkList($"Remembers “{key}”, which is checked by", readers, "No rule in this file checks it.");
            }
        }

        void LinkList(string heading, List<int> rules, string emptyText)
        {
            GUILayout.Label(heading, EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.VerticalScope())
            {
                if (rules.Count == 0) GUILayout.Label(emptyText, EditorStyles.wordWrappedMiniLabel);
                foreach (int i in rules.Take(MaxLinks)) RuleLink(i, compact: false);
                if (rules.Count > MaxLinks) GUILayout.Label($"…and {rules.Count - MaxLinks} more", EditorStyles.miniLabel);
            }
            GUILayout.Space(4);
        }

        void RuleLink(int index, bool compact)
        {
            var r = doc.rules[index];
            string label = compact ? r.name : $"{r.name}    {(string.IsNullOrEmpty(r.who) ? "anyone" : r.who)} · {(string.IsNullOrEmpty(r.concept) ? "any concept" : r.concept)}";
            var content = new GUIContent(label, "Go to " + r.name);
            var options = compact ? new[] { GUILayout.MaxWidth(linkStyle.CalcSize(content).x) } : new GUILayoutOption[0];
            if (GUILayout.Button(content, linkStyle, options)) pending.Add(() => Select(index));
            EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
        }

        // ---- Field helpers ------------------------------------------------------------------------

        /// <summary>
        /// A searchable drop-down of the table's names for a category. A value that isn't in the table stays
        /// selected (marked "not in table") with a + button to add it, so loading never loses data.
        /// </summary>
        string NamePopup(GUIContent label, string current, DialogStringCategory category, string noneLabel, params GUILayoutOption[] options)
        {
            current = current ?? "";
            var names = table != null ? table.Get(category) : new List<string>();
            bool hasCurrent = current.Length > 0;
            bool missing = hasCurrent && !names.Contains(current);

            string shown = !hasCurrent ? noneLabel ?? $"(choose {Singular(category)})" : current + (missing ? "  (not in table)" : "");
            string tip = missing ? $"'{current}' isn't in the {category} table." : hasCurrent ? current : "";
            var rect = EditorGUILayout.GetControlRect(label != null, EditorGUIUtility.singleLineHeight, EditorStyles.popup, options);
            Action<string> onAdd = table != null ? n => AddToTable(category, n) : (Action<string>)null;
            string result = NamePicker.Field(rect, label, current, shown, missing || (!hasCurrent && noneLabel == null), names, noneLabel, tip, onAdd);

            if (missing && table != null &&
                GUILayout.Button(new GUIContent("+", $"Add '{current}' to the {category} table"), EditorStyles.miniButton, GUILayout.Width(20)))
            {
                string name = current;
                pending.Add(() => AddToTable(category, name));
            }
            return result;
        }

        static string Singular(DialogStringCategory category)
        {
            switch (category)
            {
                case DialogStringCategory.Characters: return "character";
                case DialogStringCategory.Concepts: return "concept";
                case DialogStringCategory.Facts: return "fact";
                case DialogStringCategory.Values: return "value";
                case DialogStringCategory.Animations: return "animation";
                default: return "clip";
            }
        }


        void ValueField(ref bool isName, ref string value)
        {
            int mode = EditorGUILayout.Popup(isName ? 1 : 0, ValueModes, GUILayout.Width(66));
            if ((mode == 1) != isName)
            {
                Record("Edit Value", false);
                isName = mode == 1;
                value = isName ? FirstName(DialogStringCategory.Values) : "0";
                Changed();
            }
            if (isName) Set(ref value, NamePopup(null, value, DialogStringCategory.Values, null, GUILayout.MinWidth(80)));
            else NumberField(ref value, 60);
        }

        void NumberField(ref string value, float width)
        {
            float old = DlgDocument.ParseNumber(value);
            float v = EditorGUILayout.FloatField(old, GUILayout.Width(width));
            if (!v.Equals(old)) Set(ref value, DlgDocument.FormatNumber(v));
        }

        string FirstName(DialogStringCategory category) => table != null ? table.Get(category).FirstOrDefault() ?? "" : "";

        static bool RemoveButton() => GUILayout.Button(new GUIContent("×", "Remove"), GUILayout.Width(22));

        static void Swap<T>(List<T> list, int a, int b) => (list[a], list[b]) = (list[b], list[a]);

        static void Section(string title)
        {
            GUILayout.Space(4);
            GUILayout.Label(title, EditorStyles.boldLabel);
        }

        static bool Foldout(ref bool state, string title)
        {
            GUILayout.Space(6);
            state = EditorGUILayout.Foldout(state, title, true, EditorStyles.foldoutHeader);
            return state;
        }

        // ---- String table tab ---------------------------------------------------------------------

        void DrawTableTab()
        {
            if (table == null)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("The string table holds the names the rule editor offers in its drop-downs: characters, " +
                                        "concepts, facts, values, animations and audio clips. Create one, or drag an existing table into the field in the toolbar.",
                    MessageType.Info);
                if (GUILayout.Button(doc != null ? "Create String Table (filled from the open file)" : "Create String Table", GUILayout.Height(26)))
                    pending.Add(CreateTable);
                GUILayout.FlexibleSpace();
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                tableSearch = EditorGUILayout.TextField(tableSearch, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));
                using (new EditorGUI.DisabledScope(doc == null))
                    if (GUILayout.Button(new GUIContent("Import From Open File", "Add every name the open .dlg file uses."), EditorStyles.toolbarButton))
                        pending.Add(() =>
                        {
                            int added = ImportNames(doc.ToSources(), doc.partitionKeys);
                            ShowNotification(new GUIContent(added > 0 ? $"Added {added} name(s)" : "Every name is already in the table"), 1.5);
                        });
                if (GUILayout.Button(new GUIContent("Import From All .dlg Files", "Add every name used by any .dlg file in the project."), EditorStyles.toolbarButton))
                    pending.Add(ImportFromAllFiles);
                if (GUILayout.Button("Sort A–Z", EditorStyles.toolbarButton))
                    pending.Add(() =>
                    {
                        Undo.RecordObject(table, "Sort Dialog Names");
                        foreach (var c in DialogStringTable.AllCategories) table.Get(c).Sort(StringComparer.OrdinalIgnoreCase);
                        EditorUtility.SetDirty(table);
                    });
            }

            tableScroll = EditorGUILayout.BeginScrollView(tableScroll);
            foreach (var category in DialogStringTable.AllCategories) DrawTableCategory(category);
            EditorGUILayout.EndScrollView();
        }

        void DrawTableCategory(DialogStringCategory category)
        {
            int ci = (int)category;
            var list = table.Get(category);
            GUILayout.Space(4);
            tableFold[ci] = EditorGUILayout.Foldout(tableFold[ci], $"{category} ({list.Count})", true, EditorStyles.foldoutHeader);
            if (!tableFold[ci]) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(DialogStringTable.Describe(category), EditorStyles.wordWrappedMiniLabel);
                for (int i = 0; i < list.Count; i++)
                {
                    string old = list[i];
                    if (!string.IsNullOrEmpty(tableSearch) && old.IndexOf(tableSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string renamed = EditorGUILayout.DelayedTextField(old);
                        if (renamed != old)
                        {
                            int index = i;
                            pending.Add(() => RenameEntry(category, index, old, renamed));
                        }
                        int uses = doc != null ? doc.CountUses(category, old) : 0;
                        GUILayout.Label(doc != null ? (uses == 0 ? "unused" : $"{uses} use{(uses == 1 ? "" : "s")}") : "",
                            EditorStyles.miniLabel, GUILayout.Width(52));
                        if (RemoveButton())
                        {
                            int index = i;
                            pending.Add(() =>
                            {
                                Undo.RecordObject(table, "Remove Dialog Name");
                                list.RemoveAt(index);
                                EditorUtility.SetDirty(table);
                                ScheduleValidate();
                            });
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    string control = "DialogTableAdd" + ci;
                    var e = Event.current;
                    bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                                 && GUI.GetNameOfFocusedControl() == control;
                    GUI.SetNextControlName(control);
                    addText[ci] = EditorGUILayout.TextField(addText[ci] ?? "");
                    if (GUILayout.Button("Add", GUILayout.Width(52)) || enter)
                    {
                        string name = addText[ci]?.Trim();
                        if (enter) e.Use();
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (table.Contains(category, name)) ShowNotification(new GUIContent($"'{name}' is already in {category}"), 1.5);
                            else
                            {
                                pending.Add(() => AddToTable(category, name));
                                addText[ci] = "";
                            }
                        }
                        if (enter) GUI.FocusControl(control);
                    }
                }
            }
        }

        void RenameEntry(DialogStringCategory category, int index, string oldName, string newName)
        {
            newName = newName?.Trim();
            var list = table.Get(category);
            if (string.IsNullOrEmpty(newName) || index >= list.Count || list[index] != oldName) return;
            if (table.Contains(category, newName))
            {
                ShowNotification(new GUIContent($"'{newName}' is already in {category}"), 1.5);
                return;
            }

            int uses = doc != null ? doc.CountUses(category, oldName) : 0;
            bool renameUses = uses > 0 && EditorUtility.DisplayDialog("Rename",
                $"'{oldName}' is used {uses} time(s) in {Path.GetFileName(docPath)}. Rename those uses to '{newName}' too?",
                "Rename Uses Too", "Table Only");

            Undo.RecordObjects(doc != null ? new Object[] { table, doc } : new Object[] { table }, "Rename Dialog Name");
            list[index] = newName;
            EditorUtility.SetDirty(table);
            if (renameUses)
            {
                doc.VisitNames(category, v => v == oldName ? newName : v);
                Changed();
            }
            else ScheduleValidate();
        }

        // ---- Problems -----------------------------------------------------------------------------

        void Validate()
        {
            if (doc == null) return;
            string file = Path.GetFileName(docPath);
            var list = new List<AuthoringMessage>();

            for (int i = 0; i < doc.rules.Count; i++)
            {
                var r = doc.rules[i];
                string name = string.IsNullOrWhiteSpace(r.name) ? $"#{i + 1}" : r.name;
                if (string.IsNullOrWhiteSpace(r.name)) list.Add(new AuthoringMessage(MessageSeverity.Error, "Rule has no name.", file, 0, name));
                if (r.lines.Count == 0 && r.writes.Count == 0 && r.followups.Count == 0)
                    list.Add(new AuthoringMessage(MessageSeverity.Warning, "Rule has no lines, fact changes or followups, so it does nothing.", file, 0, name));
                if (r.criteria.Any(c => string.IsNullOrWhiteSpace(c.key)) || r.writes.Any(w => string.IsNullOrWhiteSpace(w.key)))
                    list.Add(new AuthoringMessage(MessageSeverity.Error, "A condition or fact change has no fact selected.", file, 0, name));
                if (r.followups.Any(f => string.IsNullOrWhiteSpace(f.concept) || (f.target == FollowupTarget.Character && string.IsNullOrWhiteSpace(f.character))))
                    list.Add(new AuthoringMessage(MessageSeverity.Error, "A followup is missing its character or concept.", file, 0, name));
                if (r.writes.Any(w => w.scope == FactScope.Character && string.IsNullOrWhiteSpace(w.character)))
                    list.Add(new AuthoringMessage(MessageSeverity.Error, "A fact change for a character has no character selected.", file, 0, name));
                if (r.lines.Any(l => string.IsNullOrEmpty(l.text) && string.IsNullOrEmpty(l.audio)))
                    list.Add(new AuthoringMessage(MessageSeverity.Warning, "A line has no text or audio.", file, 0, name));
            }

            if (table != null)
                foreach (var category in DialogStringTable.AllCategories)
                {
                    var missing = doc.NamesUsed(category).Where(n => !table.Contains(category, n)).ToList();
                    if (missing.Count > 0)
                        list.Add(new AuthoringMessage(MessageSeverity.Info,
                            $"Not in the {category} table: {string.Join(", ", missing)}", file));
                }

            var parsed = DialogScriptParser.Parse(doc.ToText(), file);
            list.AddRange(parsed.Messages.Where(m => m.Severity == MessageSeverity.Error));

            var known = table != null ? table.Get(DialogStringCategory.Facts) : Enumerable.Empty<string>();
            list.AddRange(RuleAnalyzer.Analyze(new[] { new RuleSourceGroup(file, doc.ToSources()) }, known));

            messages = list.OrderByDescending(m => m.Severity).ToList();
            ruleSeverity = messages.Where(m => m.Rule != null)
                .GroupBy(m => m.Rule, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Max(m => m.Severity), StringComparer.OrdinalIgnoreCase);
            validateAt = -1;
            Repaint();
        }

        void DrawProblems()
        {
            if (messages == null || (doc == null && loadErrorPath == null)) return;
            int errors = messages.Count(m => m.Severity == MessageSeverity.Error);
            int warnings = messages.Count(m => m.Severity == MessageSeverity.Warning);
            int infos = messages.Count - errors - warnings;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                showProblems = GUILayout.Toggle(showProblems, new GUIContent(showProblems ? "▾ Problems" : "▸ Problems", "Show or hide the problem list"),
                    EditorStyles.toolbarButton, GUILayout.Width(80));
                GUILayout.Label(new GUIContent(" " + errors, errorIcon, "Errors"), EditorStyles.miniLabel, GUILayout.Width(40));
                GUILayout.Label(new GUIContent(" " + warnings, warnIcon, "Warnings"), EditorStyles.miniLabel, GUILayout.Width(40));
                GUILayout.Label(new GUIContent(" " + infos, infoIcon, "Notes"), EditorStyles.miniLabel, GUILayout.Width(40));
                if (validateAt >= 0) GUILayout.Label("checking…", EditorStyles.miniLabel);
                else if (errors + warnings == 0) GUILayout.Label("No problems found.", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (doc != null && GUILayout.Button(new GUIContent("Check Now", "Problems are re-checked automatically as you edit."), EditorStyles.toolbarButton))
                    pending.Add(() => { Validate(); showProblems = true; });
            }
            if (!showProblems || messages.Count == 0) return;

            messageScroll = EditorGUILayout.BeginScrollView(messageScroll, GUILayout.MaxHeight(140));
            foreach (var m in messages)
            {
                string text = (m.Rule != null ? $"[{m.Rule}] " : "") + m.Text + (doc == null && m.Line > 0 ? $"  (line {m.Line})" : "");
                EditorGUILayout.HelpBox(text, m.Severity == MessageSeverity.Error ? MessageType.Error
                    : m.Severity == MessageSeverity.Warning ? MessageType.Warning : MessageType.Info);
                var rect = GUILayoutUtility.GetLastRect();
                var e = Event.current;
                if (doc != null && m.Rule != null && e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
                {
                    int index = doc.rules.FindIndex(r => string.Equals(r.name, m.Rule, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                    {
                        tab = 0;
                        Select(index);
                        e.Use();
                    }
                }
                if (doc != null && m.Rule != null) EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
