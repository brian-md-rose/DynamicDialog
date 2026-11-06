using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>What a speech bubble does when its speaker is off screen or behind the camera.</summary>
    public enum DialogOffscreenMode
    {
        /// <summary>Keep the bubble on the screen edge nearest the speaker, with its tail pointing their way.</summary>
        KeepOnScreen,
        /// <summary>Hide the bubble until the speaker is back on screen.</summary>
        Hide,
    }

    /// <summary>
    /// Speech bubbles over speakers' heads: a <see cref="IDialogPresenter"/> that shows each line in a
    /// <see cref="DialogSpeechBubble"/> following its speaker, fading in and out, with a typewriter reveal.
    /// Overlapping bubbles are moved apart, and bubbles stay on screen when their speaker is at the edge or
    /// behind the camera.
    /// </summary>
    /// <remarks>
    /// Put it on a screen-space Canvas (Overlay or Camera), or use the "Dialog Speech Bubbles" prefab
    /// (GameObject &gt; Dynamic Dialog &gt; Speech Bubbles). Bubbles point at a speaker's
    /// <see cref="DialogBubbleAnchor"/> if it has one, else at its position plus <see cref="heightOffset"/>.
    /// </remarks>
    [AddComponentMenu("Dynamic Dialog/Presentation/Dialog Speech Bubbles")]
    [RequireComponent(typeof(RectTransform))]
    public class DialogSpeechBubbles : DialogPresenterBehaviour
    {
        /// <summary>The bubble to show for each line. Restyle a copy of the "Dialog Speech Bubble" prefab.</summary>
        [Tooltip("The bubble to show for each line. Restyle a copy of the 'Dialog Speech Bubble' prefab.")]
        public DialogSpeechBubble bubblePrefab;

        /// <summary>The camera the speakers are seen through. Empty uses Camera.main.</summary>
        [Tooltip("The camera the speakers are seen through. Empty uses Camera.main.")]
        public Camera viewCamera;

        /// <summary>Height above a speaker's pivot that its bubble points at (unless it has a <see cref="DialogBubbleAnchor"/>).</summary>
        [Tooltip("Height above a speaker's pivot that its bubble points at (unless it has a Dialog Bubble Anchor).")]
        public float heightOffset = 2f;

        /// <summary>Hide bubbles of speakers farther than this from the camera, in world units. 0 means no limit.</summary>
        [Tooltip("Hide bubbles of speakers farther than this from the camera. 0 means no limit.")]
        [Min(0f)] public float maxDistance;

        /// <summary>When several speakers talk at once: one bubble each (moved apart), or only the newest.</summary>
        [Header("Overlap")]
        [Tooltip("When several speakers talk at once: one bubble each (moved apart), or only the newest.")]
        public DialogOverlapMode overlap = DialogOverlapMode.Stack;

        /// <summary>With <see cref="DialogOverlapMode.Stack"/>: the most bubbles shown at once, newest kept. 0 means no limit.</summary>
        [Tooltip("With Stack: the most bubbles shown at once, newest kept. 0 means no limit.")]
        [Min(0)] public int maxBubbles;

        /// <summary>Gap between bubbles moved apart, in canvas units.</summary>
        [Tooltip("Gap between bubbles moved apart, in canvas units.")]
        [Min(0f)] public float overlapSpacing = 6f;

        /// <summary>What to do when a speaker is off screen or behind the camera.</summary>
        [Header("Screen edges")]
        [Tooltip("What to do when a speaker is off screen or behind the camera.")]
        public DialogOffscreenMode offscreen = DialogOffscreenMode.KeepOnScreen;

        /// <summary>The closest a bubble comes to a screen edge, in canvas units.</summary>
        [Tooltip("The closest a bubble comes to a screen edge, in canvas units.")]
        [Min(0f)] public float screenMargin = 16f;

        /// <summary>Space between the speaker's anchor and the bubble's bottom edge, in canvas units (room for the tail).</summary>
        [Tooltip("Space between the speaker's anchor and the bubble's bottom edge, in canvas units (room for the tail).")]
        [Min(0f)] public float tailGap = 18f;

        /// <summary>Typewriter reveal.</summary>
        [Header("Text")]
        public DialogTypewriter typewriter = new DialogTypewriter();

        class Entry
        {
            public DialogSpeaker speaker;
            public DialogSpeechBubble bubble;
            public bool playing;
            public double startTime;
        }

        readonly Dictionary<DialogSpeaker, Entry> entries = new Dictionary<DialogSpeaker, Entry>();
        readonly List<Entry> order = new List<Entry>();
        readonly List<Entry> playingOldestFirst = new List<Entry>();
        readonly List<Entry> shown = new List<Entry>();
        readonly List<Entry> placed = new List<Entry>();
        readonly List<Rect> rects = new List<Rect>();
        readonly List<Vector2> targets = new List<Vector2>();
        readonly Stack<DialogSpeechBubble> pool = new Stack<DialogSpeechBubble>();
        Canvas canvas;

        /// <summary>The bubble showing <paramref name="speaker"/>'s line (or fading out), or null.</summary>
        public DialogSpeechBubble BubbleFor(DialogSpeaker speaker) =>
            speaker != null && entries.TryGetValue(speaker, out var e) ? e.bubble : null;

        /// <inheritdoc/>
        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (var e in order) Release(e.bubble);
            entries.Clear();
            order.Clear();
        }

        /// <inheritdoc/>
        public override void OnLineStarted(DialogPresentedLine line)
        {
            if (!line.HasText || line.Speaker == null || bubblePrefab == null) return;
            if (!entries.TryGetValue(line.Speaker, out var e))
            {
                e = new Entry { speaker = line.Speaker, bubble = Acquire() };
                entries[line.Speaker] = e;
            }
            order.Remove(e);
            order.Add(e);
            e.playing = true;
            e.startTime = line.StartTime;
            e.bubble.Show(line);
            e.bubble.Tick(Time.timeAsDouble, 0f, typewriter);
            LateUpdate();
        }

        /// <inheritdoc/>
        public override void OnLineEnded(DialogPresentedLine line) => End(line, interrupted: false);

        /// <inheritdoc/>
        public override void OnLineInterrupted(DialogPresentedLine line) => End(line, interrupted: true);

        void End(DialogPresentedLine line, bool interrupted)
        {
            if (line.Speaker == null || !entries.TryGetValue(line.Speaker, out var e) || e.bubble.Line.Line != line.Line) return;
            e.playing = false;
            e.bubble.Hide(interrupted);
        }

        void LateUpdate()
        {
            double now = Time.timeAsDouble;
            float dt = Time.deltaTime;

            // Drop bubbles whose speaker is gone or whose fade-out finished.
            for (int i = order.Count - 1; i >= 0; i--)
            {
                var e = order[i];
                if (e.speaker == null && e.playing) { e.playing = false; e.bubble.Hide(true); }
                if (!e.playing && e.bubble.IsFadedOut)
                {
                    Release(e.bubble);
                    order.RemoveAt(i);
                    entries.Remove(e.speaker); // works for a destroyed speaker too: same reference
                }
            }

            playingOldestFirst.Clear();
            foreach (var e in order) if (e.playing) playingOldestFirst.Add(e);
            playingOldestFirst.Sort((a, b) => a.startTime.CompareTo(b.startTime));
            DialogOverlap.SelectVisible(playingOldestFirst, overlap, maxBubbles, shown);

            var cam = viewCamera != null ? viewCamera : Camera.main;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.rootCanvas : null;
            if (cam == null || root == null)
            {
                foreach (var e in order) { e.bubble.SetVisible(false); e.bubble.Tick(now, dt, typewriter); }
                return;
            }

            float scale = Mathf.Max(0.0001f, root.scaleFactor);
            Vector2 screen = root.pixelRect.size;
            var uiCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            var container = (RectTransform)transform;

            placed.Clear();
            rects.Clear();
            targets.Clear();
            foreach (var e in order)
            {
                bool want = e.playing && shown.Contains(e);
                if (want && e.speaker != null)
                {
                    var world = DialogBubbleAnchor.PositionOf(e.speaker, heightOffset);
                    if (maxDistance > 0f && (world - cam.transform.position).sqrMagnitude > maxDistance * maxDistance) want = false;
                    else
                    {
                        Vector3 sp = cam.WorldToScreenPoint(world);
                        sp.x -= root.pixelRect.x;
                        sp.y -= root.pixelRect.y;
                        if (offscreen == DialogOffscreenMode.Hide && !DialogScreenPlacement.IsOnScreen(sp, screen)) want = false;
                        else
                        {
                            var size = e.bubble.Size * scale;
                            var p = DialogScreenPlacement.Place(sp, screen, size, tailGap * scale, screenMargin * scale);
                            placed.Add(e);
                            rects.Add(new Rect(p.Center - size * 0.5f, size));
                            targets.Add(p.Target);
                        }
                    }
                }
                e.bubble.SetVisible(want);
                e.bubble.Tick(now, dt, typewriter);
            }

            DialogOverlap.SeparateVertically(rects, overlapSpacing * scale, screen.y - screenMargin * scale);

            for (int i = 0; i < placed.Count; i++)
            {
                var bubble = placed[i].bubble;
                var r = rects[i];
                var pixelOffset = root.pixelRect.position;
                if (!ToLocal(container, r.center + pixelOffset, uiCamera, out var localCenter) ||
                    !ToLocal(container, targets[i] + pixelOffset, uiCamera, out var localTarget)) continue;
                var rt = bubble.RectTransform;
                rt.localPosition = localCenter + (rt.pivot - new Vector2(0.5f, 0.5f)) * rt.sizeDelta;
                bubble.PointTailAt(localTarget);
            }
        }

        static bool ToLocal(RectTransform container, Vector2 screenPoint, Camera uiCamera, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screenPoint, uiCamera, out local);

        DialogSpeechBubble Acquire()
        {
            DialogSpeechBubble b = null;
            while (pool.Count > 0 && b == null) b = pool.Pop();
            if (b == null) b = Instantiate(bubblePrefab, transform);
            b.gameObject.SetActive(true);
            return b;
        }

        void Release(DialogSpeechBubble b)
        {
            if (b == null) return;
            b.gameObject.SetActive(false);
            pool.Push(b);
        }
    }
}
