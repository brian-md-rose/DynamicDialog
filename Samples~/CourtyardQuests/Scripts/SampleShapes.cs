using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// Makes the courtyard's primitive scenery. Materials are copies of the render pipeline's default material
    /// (what a new cube gets), tinted, so the sample looks right in Built-in, URP and HDRP projects without
    /// shipping pipeline-specific material assets.
    /// </summary>
    public static class SampleShapes
    {
        static readonly Dictionary<(Color, float), Material> materials = new Dictionary<(Color, float), Material>();
        static Material template;

        /// <summary>Hide flags given to everything made here. Edit-mode previews set DontSave so nothing is saved into the scene.</summary>
        public static HideFlags Flags { get; set; }

        /// <summary>A material in the pipeline's default shader with this color, shared by everything that asks for it.</summary>
        public static Material Material(Color color, float smoothness = 0.15f)
        {
            if (materials.TryGetValue((color, smoothness), out var cached) && cached != null) return cached;
            if (template == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                template = probe.GetComponent<Renderer>().sharedMaterial;
                Destroy(probe);
            }
            var material = new Material(template) { name = "Sample " + ColorUtility.ToHtmlStringRGB(color), color = color, hideFlags = Flags };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness); // URP, HDRP
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness); // Built-in Standard
            materials[(color, smoothness)] = material;
            return material;
        }

        /// <summary>Destroys every material made here (after an edit-mode preview).</summary>
        public static void ReleaseMaterials()
        {
            foreach (var m in materials.Values) Destroy(m);
            materials.Clear();
        }

        /// <summary>
        /// A primitive placed in world space (parented with its world position kept) and scaled locally. Keeps its
        /// collider unless <paramref name="collider"/> is false.
        /// </summary>
        public static GameObject Block(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Color color, bool collider = true, float smoothness = 0.15f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.hideFlags = Flags;
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color, smoothness);
            if (!collider) Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>A primitive with no collider, placed relative to its parent.</summary>
        public static GameObject Part(string name, Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.hideFlags = Flags;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            return go;
        }

        /// <summary>An empty child at a local position.</summary>
        public static Transform Empty(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name) { hideFlags = Flags }.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        /// <summary>Destroy now in edit mode, at the end of the frame in Play Mode.</summary>
        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
