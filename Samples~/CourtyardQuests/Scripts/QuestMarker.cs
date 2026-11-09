using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// Floats an "info" icon over a character who has a quest for the player. When the world fact
    /// <see cref="doneFact"/> is set (the rules write it with <c>remember world crutch_returned = 1</c>), it shows
    /// a check mark briefly and then hides for good. Hidden while the character is talking, so it doesn't sit on
    /// top of the speech bubble.
    /// </summary>
    /// <remarks>
    /// Watching a fact, rather than the name of the rule that sets it, keeps working when rules are renamed,
    /// reworded or split up. <see cref="DialogEngine.FactChanged"/> reports every change to memory.
    /// </remarks>
    public class QuestMarker : MonoBehaviour
    {
        public DialogSpeaker speaker;
        [Tooltip("World fact that means the quest is done.")]
        public string doneFact = "crutch_returned";
        public Texture2D openIcon;
        public Texture2D doneIcon;
        [Tooltip("Same spot as the talk prompt icon, which replaces this marker while the player is in range.")]
        public float height = 1.0f;
        public float size = 46f;
        public float doneSeconds = 3f;

        float doneUntil = -1f;
        bool complete;
        DialogEngine engine;

        void Update()
        {
            // Subscribe once the director is up (it may start after this component).
            var director = DialogDirector.Instance;
            var current = director != null ? director.Engine : null;
            if (current == engine) return;
            if (engine != null) engine.FactChanged -= OnFactChanged;
            engine = current;
            if (engine == null) return;
            engine.FactChanged += OnFactChanged;
            if (engine.World.TryGet(doneFact, Time.timeAsDouble, out _)) complete = true; // already done (a loaded save)
        }

        void OnDisable()
        {
            if (engine != null) engine.FactChanged -= OnFactChanged;
            engine = null;
        }

        void OnFactChanged(FactChange change)
        {
            if (complete || change.Memory != engine.World || change.Key != Symbol.Of(doneFact) || !change.HasNewValue) return;
            complete = true;
            doneUntil = Time.time + doneSeconds;
        }

        void OnGUI()
        {
            if (speaker == null) return;
            Texture2D icon = !complete ? openIcon : Time.time < doneUntil ? doneIcon : null;
            if (icon == null || (speaker.IsSpeaking && !complete)) return;
            // The talk prompt icon takes this spot while the player is in range.
            if (!complete && PlayerCarry.Instance != null)
            {
                var player = PlayerCarry.Instance.GetComponent<CourtyardPlayer>();
                if (player != null && player.Target == speaker) return;
            }
            var cam = Camera.main;
            if (cam == null) return;

            float lift = complete ? 0.5f : 0f; // sit above the speech bubble while the thank-you line plays
            Vector3 screen = cam.WorldToScreenPoint(speaker.transform.position + Vector3.up * (height + lift));
            if (screen.z <= 0f) return;
            float bob = Mathf.Sin(Time.time * 2.5f) * 4f;
            GUI.DrawTexture(new Rect(screen.x - size * 0.5f, Screen.height - screen.y - size + bob, size, size), icon, ScaleMode.ScaleToFit);
        }
    }
}
