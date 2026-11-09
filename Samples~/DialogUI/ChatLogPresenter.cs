using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace DynamicDialog.Samples.DialogUI
{
    /// <summary>
    /// A custom presenter: a chat log that lists the last few lines, newest at the bottom, with each speaker's
    /// name in their own color. Lines still playing are marked with a dot; interrupted lines are struck through.
    /// </summary>
    /// <remarks>
    /// Deriving from <see cref="DialogPresenterBehaviour"/> is all it takes to plug in your own UI: it registers
    /// with <see cref="DialogPresenters"/> while enabled, and you get <see cref="OnLineStarted"/>,
    /// <see cref="OnLineEnded"/> and <see cref="OnLineInterrupted"/> for every line any speaker plays.
    /// A plain C# class can do the same by implementing <see cref="IDialogPresenter"/> and calling
    /// <see cref="DialogPresenters.Register"/>.
    /// </remarks>
    public class ChatLogPresenter : DialogPresenterBehaviour
    {
        [Tooltip("The text the log is written into.")]
        public TMP_Text logText;

        [Tooltip("How many lines to keep.")]
        [Min(1)] public int maxEntries = 8;

        [Tooltip("Speaker name colors, given out in the order speakers first talk.")]
        public Color[] nameColors =
        {
            new Color(1f, 0.75f, 0.3f), new Color(0.45f, 0.8f, 1f), new Color(0.6f, 0.95f, 0.5f), new Color(1f, 0.55f, 0.7f),
        };

        enum State { Playing, Finished, Interrupted }

        sealed class Entry
        {
            public DialogSpeaker speaker;
            public string name, text;
            public double startTime;
            public State state;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly StringBuilder builder = new StringBuilder();
        readonly Dictionary<string, int> colorIndex = new Dictionary<string, int>();

        protected override void OnEnable()
        {
            base.OnEnable(); // registers this presenter; lines already playing are reported straight away
            Refresh();
        }

        public override void OnLineStarted(DialogPresentedLine line)
        {
            if (!line.HasText) return; // a line can have audio but no subtitle
            entries.Add(new Entry { speaker = line.Speaker, name = line.SpeakerName, text = line.Text, startTime = line.StartTime });
            while (entries.Count > maxEntries) entries.RemoveAt(0);
            Refresh();
        }

        public override void OnLineEnded(DialogPresentedLine line) => Finish(line, State.Finished);

        public override void OnLineInterrupted(DialogPresentedLine line) => Finish(line, State.Interrupted);

        void Finish(DialogPresentedLine line, State state)
        {
            // Match on speaker and start time: the same speaker can say the same text twice.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.speaker != line.Speaker || e.startTime != line.StartTime || e.state != State.Playing) continue;
                e.state = state;
                break;
            }
            Refresh();
        }

        void Refresh()
        {
            if (logText == null) return;
            builder.Clear();
            foreach (var e in entries)
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(e.state == State.Playing ? "<color=#ffffff>•</color> " : "  ");
                builder.Append("<b><color=#").Append(ColorUtility.ToHtmlStringRGB(ColorFor(e.name))).Append('>')
                       .Append(e.name).Append("</color></b>  ");
                if (e.state == State.Interrupted) builder.Append("<s>").Append(e.text).Append("</s> <i>(interrupted)</i>");
                else builder.Append(e.text);
            }
            logText.text = entries.Count == 0 ? "<i>No lines yet.</i>" : builder.ToString();
        }

        Color ColorFor(string speakerName)
        {
            if (nameColors == null || nameColors.Length == 0) return Color.white;
            // First come, first served, so each speaker keeps one color for as long as the log lives.
            if (!colorIndex.TryGetValue(speakerName, out int index)) colorIndex[speakerName] = index = colorIndex.Count;
            return nameColors[index % nameColors.Length];
        }
    }
}
