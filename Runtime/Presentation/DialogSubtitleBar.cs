using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// A subtitle bar: a screen-space <see cref="IDialogPresenter"/> that shows each line as a row of text,
    /// with an optional speaker name, a typewriter reveal and fades.
    /// </summary>
    /// <remarks>
    /// Use the "Dialog Subtitle Bar" prefab (GameObject &gt; Dynamic Dialog &gt; Subtitle Bar), or build your own:
    /// a Canvas, a container with a vertical layout group, and an inactive row template inside it whose
    /// children include a <see cref="TMP_Text"/>. Each line clones the template; rows are oldest at the top.
    /// </remarks>
    [AddComponentMenu("Dynamic Dialog/Presentation/Dialog Subtitle Bar")]
    public class DialogSubtitleBar : DialogPresenterBehaviour
    {
        /// <summary>Where rows go. Usually has a VerticalLayoutGroup.</summary>
        [Tooltip("Where rows go. Usually has a VerticalLayoutGroup.")]
        public RectTransform rowContainer;

        /// <summary>An inactive row to clone for each line. Its first <see cref="TMP_Text"/> child shows the text.</summary>
        [Tooltip("An inactive row to clone for each line. Its first TextMeshPro child shows the text.")]
        public GameObject rowTemplate;

        /// <summary>Put the speaker's name before the line.</summary>
        [Header("Speaker name")]
        [Tooltip("Put the speaker's name before the line.")]
        public bool showSpeakerName = true;

        /// <summary>How the name is written; {0} is the name. Rich text is allowed.</summary>
        [Tooltip("How the name is written; {0} is the name. Rich text is allowed.")]
        public string nameFormat = "<b>{0}:</b> ";

        /// <summary>The name's color.</summary>
        public Color nameColor = new Color(1f, 0.85f, 0.45f);

        /// <summary>When several speakers talk at once: a row each, or only the newest line.</summary>
        [Header("Overlap")]
        [Tooltip("When several speakers talk at once: a row each, or only the newest line.")]
        public DialogOverlapMode overlap = DialogOverlapMode.Stack;

        /// <summary>With <see cref="DialogOverlapMode.Stack"/>: the most rows shown, newest kept. 0 means no limit.</summary>
        [Tooltip("With Stack: the most rows shown, newest kept. 0 means no limit.")]
        [Min(0)] public int maxRows = 3;

        /// <summary>Seconds to fade a row in.</summary>
        [Header("Fading")]
        [Min(0f)] public float fadeInSeconds = 0.12f;

        /// <summary>Seconds to fade a row out after its line ends.</summary>
        [Min(0f)] public float fadeOutSeconds = 0.25f;

        /// <summary>Seconds to fade a row out after its line is interrupted.</summary>
        [Min(0f)] public float interruptedFadeSeconds = 0.08f;

        /// <summary>Typewriter reveal.</summary>
        [Header("Text")]
        public DialogTypewriter typewriter = new DialogTypewriter();

        /// <summary>One row of the bar.</summary>
        public sealed class Row
        {
            internal GameObject go;
            internal CanvasGroup group;
            internal TMP_Text text;
            internal int prefixCharacters, totalCharacters;
            internal float alpha;
            internal bool playing, fast;

            /// <summary>The row's line.</summary>
            public DialogPresentedLine Line { get; internal set; }
            /// <summary>The row's text component.</summary>
            public TMP_Text Text => text;
            /// <summary>Whether the line is still playing (false while the row fades out).</summary>
            public bool IsPlaying => playing;
            /// <summary>The row's opacity, 0 to 1.</summary>
            public float Alpha => alpha;
        }

        readonly List<Row> rows = new List<Row>();
        readonly List<Row> playingOldestFirst = new List<Row>();
        readonly List<Row> shown = new List<Row>();
        readonly Stack<Row> pool = new Stack<Row>();

        /// <summary>The rows on screen or fading out, oldest first.</summary>
        public IReadOnlyList<Row> Rows => rows;

        /// <inheritdoc/>
        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (var r in rows) Release(r);
            rows.Clear();
        }

        /// <inheritdoc/>
        public override void OnLineStarted(DialogPresentedLine line)
        {
            if (!line.HasText || rowTemplate == null) return;
            var row = Acquire();
            if (row == null) return;
            row.Line = line;
            row.playing = true;
            row.fast = false;
            row.alpha = 0f;
            row.go.transform.SetAsLastSibling();
            rows.Add(row);

            string prefix = showSpeakerName && !string.IsNullOrEmpty(line.SpeakerName)
                ? "<color=#" + ColorUtility.ToHtmlStringRGBA(nameColor) + ">" + string.Format(nameFormat, line.SpeakerName) + "</color>"
                : string.Empty;
            if (prefix.Length > 0)
            {
                row.text.text = prefix;
                row.text.ForceMeshUpdate();
                row.prefixCharacters = row.text.textInfo.characterCount;
            }
            else row.prefixCharacters = 0;
            row.text.text = prefix + line.Text;
            row.text.ForceMeshUpdate();
            row.totalCharacters = row.text.textInfo.characterCount;
            Tick(row, Time.timeAsDouble, 0f, true);
            Refresh(0f);
        }

        /// <inheritdoc/>
        public override void OnLineEnded(DialogPresentedLine line) => End(line, false);

        /// <inheritdoc/>
        public override void OnLineInterrupted(DialogPresentedLine line) => End(line, true);

        void End(DialogPresentedLine line, bool interrupted)
        {
            foreach (var r in rows)
                if (r.playing && r.Line.Line == line.Line)
                {
                    r.playing = false;
                    r.fast = interrupted;
                }
        }

        void LateUpdate() => Refresh(Time.deltaTime);

        void Refresh(float dt)
        {
            double now = Time.timeAsDouble;
            playingOldestFirst.Clear();
            foreach (var r in rows) if (r.playing) playingOldestFirst.Add(r);
            DialogOverlap.SelectVisible(playingOldestFirst, overlap, maxRows, shown);

            for (int i = rows.Count - 1; i >= 0; i--)
            {
                var r = rows[i];
                bool isShown = shown.Contains(r);
                Tick(r, now, dt, isShown);
                if (!r.playing && r.alpha <= 0f)
                {
                    Release(r);
                    rows.RemoveAt(i);
                }
                else
                {
                    // A playing row that is pushed out (Replace, or over maxRows) fades, then leaves the layout until it is shown again.
                    bool active = !r.playing || isShown || r.alpha > 0f;
                    if (r.go.activeSelf != active) r.go.SetActive(active);
                }
            }
        }

        void Tick(Row r, double now, float dt, bool visible)
        {
            float target = r.playing && visible ? 1f : 0f;
            float seconds = target > r.alpha ? fadeInSeconds : r.fast ? interruptedFadeSeconds : fadeOutSeconds;
            r.alpha = seconds <= 0f ? target : Mathf.MoveTowards(r.alpha, target, dt / seconds);
            r.group.alpha = r.alpha;
            if (r.playing)
            {
                int body = r.totalCharacters - r.prefixCharacters;
                r.text.maxVisibleCharacters = r.prefixCharacters +
                    (typewriter == null ? body : typewriter.VisibleCharacters(body, r.Line.Elapsed(now), r.Line.Duration));
            }
        }

        Row Acquire()
        {
            var row = pool.Count > 0 ? pool.Pop() : null;
            if (row == null || row.go == null)
            {
                var parent = rowContainer != null ? rowContainer : (RectTransform)rowTemplate.transform.parent;
                var go = Instantiate(rowTemplate, parent);
                go.name = rowTemplate.name.Replace("Template", "").Trim() + " (line)";
                var text = go.GetComponentInChildren<TMP_Text>(true);
                if (text == null)
                {
                    Debug.LogError("Dialog Subtitle Bar: the row template has no TextMeshPro text.", this);
                    Destroy(go);
                    return null;
                }
                var group = go.GetComponent<CanvasGroup>();
                if (group == null) group = go.AddComponent<CanvasGroup>();
                group.interactable = group.blocksRaycasts = false;
                row = new Row { go = go, text = text, group = group };
            }
            row.go.SetActive(true);
            return row;
        }

        void Release(Row r)
        {
            if (r.go == null) return;
            r.go.SetActive(false);
            pool.Push(r);
        }
    }
}
