using TMPro;
using UnityEngine;
#if DD_SAMPLE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DynamicDialog.Samples.DialogUI
{
    /// <summary>
    /// Switches between presenters with the number keys, so you can compare them on the same conversation:
    /// 1 speech bubbles, 2 subtitle bar, 3 the custom chat log, 4 all three. Space makes Cy shout, which
    /// interrupts Cy's current line.
    /// </summary>
    /// <remarks>
    /// A presenter shows lines while it is enabled; switching is just enabling and disabling components.
    /// Several presenters can run at once (mode 4), and each shows every line.
    /// </remarks>
    public class PresenterShowcase : MonoBehaviour
    {
        [Tooltip("The package's Dialog Speech Bubbles.")]
        public DialogPresenterBehaviour speechBubbles;
        [Tooltip("The package's Dialog Subtitle Bar.")]
        public DialogPresenterBehaviour subtitleBar;
        [Tooltip("This sample's custom presenter.")]
        public DialogPresenterBehaviour chatLog;
        [Tooltip("The chat log's panel, hidden with it.")]
        public GameObject chatLogPanel;
        [Tooltip("Who shouts when Space is pressed.")]
        public DialogSpeaker shouter;
        [Tooltip("Shows the keys and which mode is on.")]
        public TMP_Text help;

        [Range(1, 4)] public int mode = 1;

        static readonly string[] ModeNames = { "Speech bubbles", "Subtitle bar", "Custom presenter (chat log)", "All three" };

        void Start() => Apply();

        void Update()
        {
            for (int m = 1; m <= 4; m++)
                if (NumberPressed(m)) { mode = m; Apply(); }
            if (SpacePressed() && shouter != null) shouter.Speak("Shout", interrupt: true);
        }

        /// <summary>Turns the presenters for <see cref="mode"/> on and the others off.</summary>
        public void Apply()
        {
            bool bubbles = mode == 1 || mode == 4, bar = mode == 2 || mode == 4, log = mode == 3 || mode == 4;
            if (speechBubbles != null) speechBubbles.enabled = bubbles;
            if (subtitleBar != null) subtitleBar.enabled = bar;
            if (chatLog != null) chatLog.enabled = log;
            if (chatLogPanel != null) chatLogPanel.SetActive(log);
            if (help == null) return;
            var text = new System.Text.StringBuilder();
            for (int m = 1; m <= 4; m++)
                text.Append(m == mode ? "<b><color=#ffd34d>" : "<color=#ffffffb0>").Append(m).Append("  ").Append(ModeNames[m - 1])
                    .Append(m == mode ? "</color></b>" : "</color>").Append('\n');
            text.Append("<color=#ffffffb0>Space  Cy shouts (interrupts)</color>");
            help.text = text.ToString();
        }

        static bool NumberPressed(int n)
        {
#if DD_SAMPLE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (n)
            {
                case 1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
                case 2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
                case 3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
                default: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Alpha0 + n) || Input.GetKeyDown(KeyCode.Keypad0 + n);
#else
            return false;
#endif
        }

        static bool SpacePressed()
        {
#if DD_SAMPLE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        /// <summary>Switches to mode 1-4, as the number keys do.</summary>
        public void SetMode(int newMode) { mode = Mathf.Clamp(newMode, 1, 4); Apply(); }
    }
}
