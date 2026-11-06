using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Put next to a <see cref="DialogSpeaker"/> to say where its speech bubble points: a transform such as the
    /// head bone, plus an offset. Without one, bubbles use the speaker's position plus the bubble component's height offset.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dynamic Dialog/Dialog Bubble Anchor")]
    public class DialogBubbleAnchor : MonoBehaviour
    {
        /// <summary>The transform the bubble points at. Empty uses this GameObject's transform.</summary>
        [Tooltip("The transform the bubble points at, e.g. the head bone. Empty uses this GameObject.")]
        public Transform point;

        /// <summary>Offset from <see cref="point"/>, in world space.</summary>
        [Tooltip("Offset from the point, in world space.")]
        public Vector3 offset = new Vector3(0f, 0.3f, 0f);

        /// <summary>The world position bubbles point at.</summary>
        public Vector3 Position => (point != null ? point.position : transform.position) + offset;

        /// <summary>Where <paramref name="speaker"/>'s bubble points: its <see cref="DialogBubbleAnchor"/>, or its position plus <paramref name="heightOffset"/>.</summary>
        /// <param name="speaker">The speaker.</param>
        /// <param name="heightOffset">Height above the speaker's pivot to use when it has no anchor.</param>
        public static Vector3 PositionOf(DialogSpeaker speaker, float heightOffset)
        {
            var anchor = speaker.GetComponent<DialogBubbleAnchor>();
            return anchor != null ? anchor.Position : speaker.transform.position + Vector3.up * heightOffset;
        }
    }
}
