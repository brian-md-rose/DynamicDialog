using TMPro;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// One speech bubble: a background, the line's text, an optional name label and a tail that points at the
    /// speaker. <see cref="DialogSpeechBubbles"/> creates, places and recycles these from a prefab; restyle a
    /// copy of the "Dialog Speech Bubble" prefab (sprites, colors, fonts, padding) and assign it there.
    /// </summary>
    /// <remarks>
    /// The bubble sizes itself to its text, up to <see cref="maxWidth"/>, using the full line so it doesn't grow
    /// while the typewriter reveals it. Override <see cref="Layout"/> for a different arrangement.
    /// </remarks>
    [AddComponentMenu("Dynamic Dialog/Presentation/Dialog Speech Bubble")]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class DialogSpeechBubble : MonoBehaviour
    {
        /// <summary>The line's text.</summary>
        [Tooltip("The line's text.")]
        public TMP_Text body;

        /// <summary>Optional: shows the speaker's name above the text.</summary>
        [Tooltip("Optional: shows the speaker's name above the text.")]
        public TMP_Text nameLabel;

        /// <summary>Optional: a tail that points at the speaker. Draw it pointing down, with its pivot on the edge it joins the bubble at.</summary>
        [Tooltip("Optional: a tail that points at the speaker. Draw it pointing down, pivot at the top (where it joins the bubble).")]
        public RectTransform tail;

        /// <summary>Space between the bubble's edge and its text, in canvas units.</summary>
        [Header("Layout")]
        [Tooltip("Space between the bubble's edge and its text (x: left and right, y: top and bottom).")]
        public Vector2 padding = new Vector2(18f, 12f);

        /// <summary>Space between the name label and the text, in canvas units.</summary>
        [Tooltip("Space between the name label and the text.")]
        public float nameSpacing = 2f;

        /// <summary>The narrowest the bubble gets, in canvas units.</summary>
        [Min(0f)] public float minWidth = 80f;

        /// <summary>The widest the bubble gets; longer lines wrap. In canvas units.</summary>
        [Min(10f)] public float maxWidth = 380f;

        /// <summary>Seconds to fade in.</summary>
        [Header("Fading")]
        [Min(0f)] public float fadeInSeconds = 0.12f;

        /// <summary>Seconds to fade out after a line ends.</summary>
        [Min(0f)] public float fadeOutSeconds = 0.25f;

        /// <summary>Seconds to fade out after a line is interrupted.</summary>
        [Min(0f)] public float interruptedFadeSeconds = 0.08f;

        CanvasGroup group;
        RectTransform rect;
        float alpha;
        bool showing, visible = true, fast;
        int totalCharacters;

        /// <summary>The line shown, or the last one while it fades out.</summary>
        public DialogPresentedLine Line { get; private set; }

        /// <summary>Whether a line is being shown (true until <see cref="Hide"/>, even while hidden off screen).</summary>
        public bool IsShowing => showing;

        /// <summary>Whether the bubble has faded out completely and can be reused.</summary>
        public bool IsFadedOut => !showing && alpha <= 0f;

        /// <summary>The bubble's size in canvas units, from the last <see cref="Layout"/>.</summary>
        public Vector2 Size => RectTransform.sizeDelta;

        /// <summary>This bubble's RectTransform.</summary>
        public RectTransform RectTransform => rect != null ? rect : rect = (RectTransform)transform;

        /// <summary>The current opacity, 0 to 1.</summary>
        public float Alpha => alpha;

        /// <summary>Characters of the line visible now (after the typewriter).</summary>
        public int VisibleCharacters => body != null ? Mathf.Min(body.maxVisibleCharacters, totalCharacters) : 0;

        /// <summary>Visible characters in the whole line (rich-text tags don't count).</summary>
        public int TotalCharacters => totalCharacters;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
        }

        /// <summary>Shows <paramref name="line"/>, fading in from the current opacity.</summary>
        public virtual void Show(DialogPresentedLine line)
        {
            Line = line;
            showing = true;
            fast = false;
            if (nameLabel != null)
            {
                nameLabel.text = line.SpeakerName;
                nameLabel.gameObject.SetActive(!string.IsNullOrEmpty(line.SpeakerName));
            }
            if (body != null)
            {
                body.text = line.Text ?? string.Empty;
                body.maxVisibleCharacters = 0;
            }
            Layout();
            if (body != null)
            {
                body.ForceMeshUpdate();
                totalCharacters = body.textInfo.characterCount;
            }
        }

        /// <summary>Fades the bubble out. <paramref name="interrupted"/> uses the faster <see cref="interruptedFadeSeconds"/>.</summary>
        public virtual void Hide(bool interrupted)
        {
            showing = false;
            fast = interrupted;
        }

        /// <summary>Hides the bubble without ending its line, e.g. while its speaker is off screen. It fades in again when set back to true.</summary>
        public void SetVisible(bool value) => visible = value;

        /// <summary>Advances fading and the typewriter. <see cref="DialogSpeechBubbles"/> calls this every frame.</summary>
        /// <param name="now">The current <see cref="Time.timeAsDouble"/>.</param>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="typewriter">Reveal settings, or null to show the whole line.</param>
        public virtual void Tick(double now, float deltaTime, DialogTypewriter typewriter)
        {
            float target = showing && visible ? 1f : 0f;
            float seconds = target > alpha ? fadeInSeconds : fast ? interruptedFadeSeconds : fadeOutSeconds;
            alpha = seconds <= 0f ? target : Mathf.MoveTowards(alpha, target, deltaTime / seconds);
            if (group != null) group.alpha = alpha;

            if (body != null && showing)
                body.maxVisibleCharacters = typewriter == null
                    ? totalCharacters
                    : typewriter.VisibleCharacters(totalCharacters, Line.Elapsed(now), Line.Duration);
        }

        /// <summary>
        /// Sizes the bubble to its text and arranges the name label and text inside it.
        /// Override to lay the bubble out differently.
        /// </summary>
        public virtual void Layout()
        {
            float innerMax = Mathf.Max(1f, maxWidth - padding.x * 2f);
            Vector2 textSize = Vector2.zero, nameSize = Vector2.zero;
            if (body != null)
            {
                textSize = body.GetPreferredValues(body.text, innerMax, 0f);
                textSize.x = Mathf.Min(textSize.x, innerMax);
                textSize = body.GetPreferredValues(body.text, Mathf.Max(1f, textSize.x), 0f);
            }
            bool hasName = nameLabel != null && nameLabel.gameObject.activeSelf && !string.IsNullOrEmpty(nameLabel.text);
            if (hasName)
            {
                nameSize = nameLabel.GetPreferredValues(nameLabel.text, innerMax, 0f);
                nameSize.x = Mathf.Min(nameSize.x, innerMax);
            }

            float innerWidth = Mathf.Max(textSize.x, nameSize.x);
            float width = Mathf.Clamp(innerWidth + padding.x * 2f, minWidth, Mathf.Max(minWidth, maxWidth));
            float nameBlock = hasName ? nameSize.y + nameSpacing : 0f;
            float height = textSize.y + nameBlock + padding.y * 2f;
            RectTransform.sizeDelta = new Vector2(width, height);

            if (hasName) Place(nameLabel.rectTransform, top: padding.y, height: nameSize.y);
            if (body != null) Place(body.rectTransform, top: padding.y + nameBlock, height: textSize.y);
        }

        void Place(RectTransform r, float top, float height)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -top);
            r.sizeDelta = new Vector2(-padding.x * 2f, height);
        }

        /// <summary>Points the tail at <paramref name="target"/>, given in this bubble's parent's local space.</summary>
        public virtual void PointTailAt(Vector2 target)
        {
            if (tail == null) return;
            var r = RectTransform;
            var size = r.sizeDelta;
            var center = (Vector2)r.localPosition + (new Vector2(0.5f, 0.5f) - r.pivot) * size;
            var box = new Rect(center - size * 0.5f, size);
            var edge = DialogScreenPlacement.EdgePoint(box, target);
            var dir = target - center;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.down;
            tail.localPosition = edge - (Vector2)r.localPosition;
            tail.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
        }
    }
}
