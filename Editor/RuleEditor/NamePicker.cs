using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DynamicDialog.Editor
{
    /// <summary>
    /// A searchable name field: looks like a popup, but opens a list you can filter by typing, move
    /// through with the arrow keys, and add a new name to the string table from.
    /// The pick arrives after the popup closes, so it's handed back the next time the field is drawn.
    /// </summary>
    internal static class NamePicker
    {
        static readonly int Hash = "DialogRuleEditor.NamePicker".GetHashCode();

        static EditorWindow resultOwner;
        static int resultId;
        static string resultValue;
        static bool hasResult;

        /// <summary>Draws the field. <paramref name="onAdd"/> is called when the user adds a new name (null = adding not allowed).</summary>
        public static string Field(Rect rect, GUIContent label, string current, string shown, bool warn,
            List<string> names, string noneLabel, string tooltip, Action<string> onAdd)
        {
            int id = GUIUtility.GetControlID(Hash, FocusType.Keyboard, rect);
            if (label != null) rect = EditorGUI.PrefixLabel(rect, id, label);

            string result = current;
            var e = Event.current;
            if (e.type == EventType.Layout && hasResult && resultId == id && resultOwner == EditorWindow.focusedWindow)
            {
                hasResult = false;
                result = resultValue;
            }

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && rect.Contains(e.mousePosition))
                    {
                        GUIUtility.keyboardControl = id;
                        Open(rect, id, current, names, noneLabel, onAdd);
                        e.Use();
                    }
                    break;
                case EventType.KeyDown:
                    if (GUIUtility.keyboardControl == id &&
                        (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
                    {
                        Open(rect, id, current, names, noneLabel, onAdd);
                        e.Use();
                    }
                    break;
                case EventType.Repaint:
                    var old = GUI.backgroundColor;
                    if (warn) GUI.backgroundColor = new Color(1f, 0.6f, 0.55f);
                    EditorStyles.popup.Draw(rect, new GUIContent(shown, tooltip), id, false, rect.Contains(e.mousePosition));
                    GUI.backgroundColor = old;
                    break;
            }
            return result;
        }

        static void Open(Rect rect, int id, string current, List<string> names, string noneLabel, Action<string> onAdd)
        {
            var owner = EditorWindow.focusedWindow;
            PopupWindow.Show(rect, new NamePickerPopup(rect.width, current, names, noneLabel, onAdd != null, (value, add) =>
            {
                if (add) onAdd?.Invoke(value);
                resultOwner = owner;
                resultId = id;
                resultValue = value;
                hasResult = true;
                if (owner != null)
                {
                    owner.Focus();
                    owner.Repaint();
                }
            }));
        }
    }

    internal class NamePickerPopup : PopupWindowContent
    {
        const float RowHeight = 20f;
        const float SearchHeight = 26f;
        const string SearchControl = "DialogNamePickerSearch";

        struct Item
        {
            public string label;
            public string value;
            public bool add;
        }

        readonly float width;
        readonly string current;
        readonly List<string> names;
        readonly string noneLabel;
        readonly bool canAdd;
        readonly Action<string, bool> onPick;

        string search = "";
        List<Item> items;
        int highlight;
        Vector2 scroll;
        bool focusSearch = true;
        bool scrollToHighlight = true;
        GUIStyle rowStyle, addStyle;

        public NamePickerPopup(float width, string current, List<string> names, string noneLabel, bool canAdd, Action<string, bool> onPick)
        {
            this.width = Mathf.Max(width, 220f);
            this.current = current ?? "";
            this.names = names;
            this.noneLabel = noneLabel;
            this.canAdd = canAdd;
            this.onPick = onPick;
            Rebuild();
            highlight = Mathf.Max(0, items.FindIndex(i => !i.add && i.value == this.current));
        }

        public override Vector2 GetWindowSize()
        {
            int rows = names.Count + (noneLabel != null ? 1 : 0) + (canAdd ? 1 : 0);
            return new Vector2(width, Mathf.Clamp(SearchHeight + Mathf.Max(rows, 2) * RowHeight + 22f, 90f, 360f));
        }

        public override void OnOpen() => editorWindow.wantsMouseMove = true;

        void Rebuild()
        {
            items = new List<Item>();
            string s = search.Trim();
            if (noneLabel != null && s.Length == 0) items.Add(new Item { label = noneLabel, value = "" });

            IEnumerable<string> matches = names;
            if (s.Length > 0)
            {
                // Names that start with the search first, then names that contain it anywhere.
                matches = names.Where(n => n.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(n => n.StartsWith(s, StringComparison.OrdinalIgnoreCase) ? 0 : 1);
            }
            items.AddRange(matches.Select(n => new Item { label = n, value = n }));

            if (canAdd && s.Length > 0 && !names.Contains(s, StringComparer.Ordinal))
                items.Add(new Item { label = $"Add “{s}” to the table", value = s, add = true });
        }

        public override void OnGUI(Rect rect)
        {
            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(EditorStyles.label) { padding = new RectOffset(22, 6, 2, 2), clipping = TextClipping.Clip };
                addStyle = new GUIStyle(rowStyle) { fontStyle = FontStyle.Italic };
            }

            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.DownArrow:
                        highlight = Mathf.Min(highlight + 1, items.Count - 1);
                        scrollToHighlight = true;
                        e.Use();
                        break;
                    case KeyCode.UpArrow:
                        highlight = Mathf.Max(highlight - 1, 0);
                        scrollToHighlight = true;
                        e.Use();
                        break;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        if (highlight >= 0 && highlight < items.Count) Pick(items[highlight]);
                        e.Use();
                        return;
                    case KeyCode.Escape:
                        editorWindow.Close();
                        e.Use();
                        return;
                }
            }

            var searchRect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, 18);
            GUI.SetNextControlName(SearchControl);
            string ns = EditorGUI.TextField(searchRect, search, EditorStyles.toolbarSearchField);
            if (focusSearch)
            {
                EditorGUI.FocusTextInControl(SearchControl);
                focusSearch = false;
            }
            if (ns != search)
            {
                search = ns;
                Rebuild();
                highlight = 0;
                scroll = Vector2.zero;
            }

            var listRect = new Rect(rect.x, rect.y + SearchHeight, rect.width, rect.height - SearchHeight - 18);
            var view = new Rect(0, 0, listRect.width - (items.Count * RowHeight > listRect.height ? 14 : 0), items.Count * RowHeight);
            if (scrollToHighlight && e.type == EventType.Repaint)
            {
                float top = highlight * RowHeight;
                if (top < scroll.y) scroll.y = top;
                else if (top + RowHeight > scroll.y + listRect.height) scroll.y = top + RowHeight - listRect.height;
                scrollToHighlight = false;
            }

            scroll = GUI.BeginScrollView(listRect, scroll, view);
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(0, i * RowHeight, view.width, RowHeight);
                var item = items[i];
                if (e.type == EventType.MouseMove && r.Contains(e.mousePosition) && highlight != i)
                {
                    highlight = i;
                    editorWindow.Repaint();
                }
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    Pick(item);
                    e.Use();
                }
                if (e.type == EventType.Repaint)
                {
                    if (i == highlight)
                        EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.36f, 0.53f) : new Color(0.24f, 0.49f, 0.91f, 0.45f));
                    bool isCurrent = !item.add && item.value == current;
                    (item.add ? addStyle : rowStyle).Draw(r, new GUIContent(item.label), false, false, isCurrent, false);
                    if (isCurrent) GUI.Label(new Rect(r.x + 5, r.y + 1, 16, r.height), "✓");
                    else if (item.add) GUI.Label(new Rect(r.x + 5, r.y + 1, 16, r.height), "+");
                }
            }
            GUI.EndScrollView();

            if (items.Count == 0)
                GUI.Label(new Rect(listRect.x + 8, listRect.y + 4, listRect.width - 16, 18), "No matches", EditorStyles.centeredGreyMiniLabel);

            string hint = canAdd ? "Type to filter or add · ↑↓ · Enter" : "Type to filter · ↑↓ · Enter";
            GUI.Label(new Rect(rect.x, rect.yMax - 18, rect.width, 18), hint, EditorStyles.centeredGreyMiniLabel);
        }

        void Pick(Item item)
        {
            onPick(item.value, item.add);
            editorWindow.Close();
        }
    }
}
