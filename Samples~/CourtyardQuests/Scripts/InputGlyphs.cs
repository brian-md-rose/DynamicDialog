using System;
using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// Kenney Input Prompts icons for each action, with one set for keyboard and one for gamepad.
    /// <see cref="Current"/> matches the device the player used last.
    /// </summary>
    public class InputGlyphs : MonoBehaviour
    {
        [Serializable]
        public class GlyphSet
        {
            public Texture2D[] move;
            public Texture2D talk;
            public Texture2D carry;
            public Texture2D debug;
        }

        public static InputGlyphs Instance { get; private set; }

        public GlyphSet keyboard = new GlyphSet();
        public GlyphSet gamepad = new GlyphSet();

        public GlyphSet Current => CourtyardInput.UsingGamepad ? gamepad : keyboard;

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }
        void Update() => CourtyardInput.Poll();
    }
}
