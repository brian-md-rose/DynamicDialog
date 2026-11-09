using UnityEngine;

namespace DynamicDialog.Samples.FirstBark
{
    /// <summary>
    /// Gives a character a body when the scene starts: a colored capsule with a visor.
    /// </summary>
    /// <remarks>
    /// The body is made at Play time with the render pipeline's default material, so the sample looks the same
    /// in Built-in, URP and HDRP projects. It has nothing to do with dialog; swap it for your own model.
    /// </remarks>
    public class SimpleBody : MonoBehaviour
    {
        [Tooltip("Body color.")]
        public Color color = new Color(0.35f, 0.6f, 1f);

        [Tooltip("Also put a round patch of floor under the character.")]
        public bool floor = true;

        void Awake()
        {
            Part("Body", PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), Vector3.one, color);
            Part("Visor", PrimitiveType.Cube, new Vector3(0f, 1.45f, 0.4f), new Vector3(0.6f, 0.15f, 0.2f), new Color(0.1f, 0.1f, 0.12f));
            if (floor)
                Part("Floor", PrimitiveType.Cylinder, new Vector3(0f, -0.05f, 0f), new Vector3(6f, 0.05f, 6f), new Color(0.45f, 0.52f, 0.45f));
        }

        void Part(string partName, PrimitiveType shape, Vector3 localPosition, Vector3 scale, Color tint)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().material.color = tint;
        }

        void OnDrawGizmos()
        {
            // So the character can be seen in the Scene view before pressing Play.
            Gizmos.color = color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f));
        }
    }
}
