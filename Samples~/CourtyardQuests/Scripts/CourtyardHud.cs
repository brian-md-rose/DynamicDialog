using System.Collections.Generic;
using DynamicDialog;
using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// Button prompts ("Talk to Rosa", "Pick up crutch") drawn with Kenney Input Prompts icons for the
    /// device in use, a talk icon over whoever the player can talk to, and a controls hint.
    /// Plays a little blip sound per character while they talk.
    /// The speech bubbles themselves come from the package's Dialog Speech Bubbles prefab.
    /// </summary>
    public class CourtyardHud : MonoBehaviour
    {
        public Camera viewCamera;
        public CourtyardPlayer player;
        public float heightOffset = 0.95f;
        public int fontSize = 16;

        [Header("Talk blips")]
        public AudioSource blipSource;
        public AudioClip[] blips;
        [Range(0f, 1f)] public float blipVolume = 0.35f;
        public float blipInterval = 0.11f;

        struct Prompt
        {
            public Texture2D icon;
            public string key;   // shown when there is no icon
            public string label; // may be null (icon only)
            public Prompt(Texture2D icon, string key, string label) { this.icon = icon; this.key = key; this.label = label; }
        }

        GUIStyle hint, helpStyle, panel;
        float nextBlip;
        readonly List<Prompt> row = new List<Prompt>();

        void Update()
        {
            var director = DialogDirector.Instance;
            if (director == null || director.Conductor == null || blipSource == null || blips == null || blips.Length == 0) return;
            if (Time.time < nextBlip) return;
            foreach (var s in director.Conductor.Speakers)
            {
                if (!(s is DialogSpeaker speaker) || speaker == null || !speaker.IsSpeaking) continue;
                // Each character gets their own pitch, so you can tell who is talking.
                int hash = speaker.Who.Name.GetHashCode();
                blipSource.pitch = 0.8f + (hash & 0xff) / 255f * 0.7f + Random.Range(-0.05f, 0.05f);
                blipSource.PlayOneShot(blips[Random.Range(0, blips.Length)], blipVolume);
                nextBlip = Time.time + blipInterval;
                break;
            }
        }

        void OnGUI()
        {
            var cam = viewCamera != null ? viewCamera : Camera.main;
            var director = DialogDirector.Instance;
            if (cam == null || director == null || director.Conductor == null) return;
            EnsureStyles();


            if (player == null) return;
            var glyphs = InputGlyphs.Instance != null ? InputGlyphs.Instance.Current : null;

            // Context prompts along the bottom of the screen.
            row.Clear();
            if (player.Target != null)
            {
                row.Add(new Prompt(glyphs?.talk, "[E]", "Talk to " + player.Target.Who.Name));
                if (!player.Target.IsSpeaking && glyphs?.talk != null) DrawTalkIcon(cam, player.Target, glyphs.talk);
            }
            var carry = player.GetComponent<PlayerCarry>();
            if (carry != null)
            {
                if (carry.Held != null) row.Add(new Prompt(glyphs?.carry, "[F]", "Drop " + carry.Held.Tag.Name));
                else
                {
                    var near = carry.FindNearest();
                    if (near != null) row.Add(new Prompt(glyphs?.carry, "[F]", "Pick up " + near.Tag.Name));
                }
            }
            if (row.Count > 0) DrawRow(row, Screen.height - 64f, 34f, hint, centered: true);

            // Controls hint in the top-left corner.
            row.Clear();
            if (glyphs != null && glyphs.move != null && glyphs.move.Length > 0)
                for (int i = 0; i < glyphs.move.Length; i++)
                    row.Add(new Prompt(glyphs.move[i], "", i == glyphs.move.Length - 1 ? "move" : null));
            else row.Add(new Prompt(null, "WASD", "move"));
            row.Add(new Prompt(glyphs?.talk, "E", "talk"));
            row.Add(new Prompt(glyphs?.carry, "F", "pick up / drop"));
            row.Add(new Prompt(glyphs?.debug, "Tab", "debug overlay"));
            DrawRow(row, 8f, 26f, helpStyle, centered: false);
        }

        /// <summary>Draws icon + label pairs in a row, using the key's name where an icon is missing.</summary>
        void DrawRow(List<Prompt> items, float y, float iconSize, GUIStyle style, bool centered)
        {
            const float gap = 4f, spacing = 16f;
            float width = 0f;
            foreach (var it in items) width += ItemWidth(it, iconSize, style, gap, spacing);
            float x = centered ? (Screen.width - width) * 0.5f : 12f;
            // Dark backing so the white Kenney icons read against the bright courtyard.
            GUI.Box(new Rect(x - 8f, y - 5f, width + 16f - spacing + 4f, iconSize + 10f), GUIContent.none, panel);
            foreach (var it in items)
            {
                if (it.icon != null)
                {
                    GUI.DrawTexture(new Rect(x, y, iconSize, iconSize), it.icon, ScaleMode.ScaleToFit);
                    x += iconSize + gap;
                }
                else if (!string.IsNullOrEmpty(it.key))
                {
                    float kw = style.CalcSize(new GUIContent(it.key)).x;
                    GUI.Label(new Rect(x, y, kw, iconSize), it.key, style);
                    x += kw + gap;
                }
                if (!string.IsNullOrEmpty(it.label))
                {
                    float lw = style.CalcSize(new GUIContent(it.label)).x;
                    GUI.Label(new Rect(x, y, lw, iconSize), it.label, style);
                    x += lw + spacing;
                }
            }
        }

        static float ItemWidth(Prompt it, float iconSize, GUIStyle style, float gap, float spacing)
        {
            float w = 0f;
            if (it.icon != null) w += iconSize + gap;
            else if (!string.IsNullOrEmpty(it.key)) w += style.CalcSize(new GUIContent(it.key)).x + gap;
            if (!string.IsNullOrEmpty(it.label)) w += style.CalcSize(new GUIContent(it.label)).x + spacing;
            return w;
        }

        /// <summary>A small bobbing button icon over the head of whoever the player can talk to.</summary>
        void DrawTalkIcon(Camera cam, DialogSpeaker target, Texture2D icon)
        {
            Vector3 screen = cam.WorldToScreenPoint(target.transform.position + Vector3.up * (heightOffset + 0.05f));
            if (screen.z <= 0f) return;
            const float size = 42f;
            float bob = Mathf.Sin(Time.time * 4f) * 3f;
            GUI.DrawTexture(new Rect(screen.x - size * 0.5f, Screen.height - screen.y - size + bob, size, size), icon, ScaleMode.ScaleToFit);
        }

        void EnsureStyles()
        {
            if (hint != null) return;
            hint = new GUIStyle(GUI.skin.label) { fontSize = fontSize + 2, alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            hint.normal.textColor = Color.white;
            helpStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize - 2, alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            helpStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            var dark = new Texture2D(1, 1);
            dark.SetPixel(0, 0, new Color(0.08f, 0.1f, 0.12f, 0.72f));
            dark.Apply();
            panel = new GUIStyle(GUIStyle.none);
            panel.normal.background = dark;
        }
    }
}
