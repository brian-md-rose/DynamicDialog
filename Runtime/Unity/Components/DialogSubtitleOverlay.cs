using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// A no-setup subtitle display using IMGUI, for prototypes and debugging.
    /// Ship your own UI by listening to <see cref="DialogSpeaker.AnyLineStarted"/> or a speaker's onSubtitle event.
    /// </summary>
    [AddComponentMenu("Dynamic Dialog/Subtitle Overlay (Debug)")]
    public class DialogSubtitleOverlay : MonoBehaviour
    {
        [Tooltip("Also show the rule name and concept for each line.")]
        [SerializeField] bool showRuleNames;
        [SerializeField, Min(8)] int fontSize = 18;

        readonly List<(DialogSpeaker speaker, DialogLine line, string text)> showing = new List<(DialogSpeaker, DialogLine, string)>();
        GUIStyle style;

        void OnEnable()
        {
            DialogSpeaker.AnyLineStarted += OnStarted;
            DialogSpeaker.AnyLineEnded += OnFinished;
        }

        void OnDisable()
        {
            DialogSpeaker.AnyLineStarted -= OnStarted;
            DialogSpeaker.AnyLineEnded -= OnFinished;
            showing.Clear();
        }

        void OnStarted(DialogSpeaker speaker, DialogLine line, DialogLineDefinition variation)
        {
            string text = speaker.CurrentText;
            if (string.IsNullOrEmpty(text) && !showRuleNames) return;
            showing.Add((speaker, line, text));
        }

        void OnFinished(DialogSpeaker speaker, DialogLine line, bool completed)
        {
            showing.RemoveAll(s => s.line == line);
        }

        void OnGUI()
        {
            if (showing.Count == 0) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, wordWrap = true, richText = true };
            style.fontSize = fontSize;

            var sb = new System.Text.StringBuilder();
            foreach (var (speaker, line, text) in showing)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("<b>").Append(speaker.Who).Append(":</b> ").Append(text);
                if (showRuleNames) sb.Append("  <color=#aaaaaa>[").Append(line.Rule.Name).Append(" / ").Append(line.Concept).Append("]</color>");
            }

            var rect = new Rect(Screen.width * 0.1f, 0f, Screen.width * 0.8f, Screen.height - 24f);
            var shadow = new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height);
            var content = sb.ToString();
            var old = GUI.color;
            GUI.color = Color.black;
            GUI.Label(shadow, content, style);
            GUI.color = old;
            GUI.Label(rect, content, style);
        }
    }
}
