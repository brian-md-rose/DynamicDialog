using UnityEngine;

namespace DynamicDialog
{
    /// <summary>Where a bubble goes on screen, from <see cref="DialogScreenPlacement.Place"/>.</summary>
    public readonly struct DialogBubblePlacement
    {
        /// <summary>Creates a placement.</summary>
        /// <param name="center">The bubble's center, in screen pixels.</param>
        /// <param name="target">The point the bubble's tail should point at, in screen pixels.</param>
        /// <param name="clamped">Whether the bubble was moved to stay on screen.</param>
        /// <param name="behindCamera">Whether the speaker is behind the camera.</param>
        public DialogBubblePlacement(Vector2 center, Vector2 target, bool clamped, bool behindCamera)
        {
            Center = center;
            Target = target;
            Clamped = clamped;
            BehindCamera = behindCamera;
        }

        /// <summary>The bubble's center, in screen pixels (y up).</summary>
        public Vector2 Center { get; }
        /// <summary>
        /// The point the bubble's tail should point at, in screen pixels: the speaker's anchor, or, when the
        /// speaker is behind the camera, the point on the screen edge in its direction.
        /// </summary>
        public Vector2 Target { get; }
        /// <summary>Whether the bubble was moved to stay on screen (the speaker is near or past an edge, or behind the camera).</summary>
        public bool Clamped { get; }
        /// <summary>Whether the speaker is behind the camera.</summary>
        public bool BehindCamera { get; }
    }

    /// <summary>Screen-space math for world-space bubbles: following a speaker and staying on screen.</summary>
    public static class DialogScreenPlacement
    {
        /// <summary>
        /// Places a bubble above a speaker's projected anchor, kept inside the screen. A speaker behind the
        /// camera gets its bubble on the screen edge in its direction (usually the bottom).
        /// </summary>
        /// <param name="screenPoint">The anchor from <see cref="Camera.WorldToScreenPoint(Vector3)"/>: pixels, with z the depth (negative behind the camera).</param>
        /// <param name="screenSize">The screen (or canvas) size in pixels.</param>
        /// <param name="bubbleSize">The bubble's size in pixels.</param>
        /// <param name="gap">Space between the anchor and the bubble's bottom edge, in pixels (room for the tail).</param>
        /// <param name="margin">The closest the bubble may come to a screen edge, in pixels.</param>
        public static DialogBubblePlacement Place(Vector3 screenPoint, Vector2 screenSize, Vector2 bubbleSize, float gap, float margin)
        {
            var target = new Vector2(screenPoint.x, screenPoint.y);
            bool behind = screenPoint.z < 0f;
            if (behind) target = EdgePointBehind(target, screenSize);

            var desired = target + new Vector2(0f, gap + bubbleSize.y * 0.5f);
            var center = new Vector2(
                ClampAxis(desired.x, bubbleSize.x * 0.5f + margin, screenSize.x - bubbleSize.x * 0.5f - margin),
                ClampAxis(desired.y, bubbleSize.y * 0.5f + margin, screenSize.y - bubbleSize.y * 0.5f - margin));
            bool clamped = behind || (center - desired).sqrMagnitude > 0.01f;
            return new DialogBubblePlacement(center, target, clamped, behind);
        }

        /// <summary>Whether a projected point is in front of the camera and inside the screen.</summary>
        /// <param name="screenPoint">From <see cref="Camera.WorldToScreenPoint(Vector3)"/>.</param>
        /// <param name="screenSize">The screen size in pixels.</param>
        public static bool IsOnScreen(Vector3 screenPoint, Vector2 screenSize) =>
            screenPoint.z >= 0f && screenPoint.x >= 0f && screenPoint.y >= 0f &&
            screenPoint.x <= screenSize.x && screenPoint.y <= screenSize.y;

        /// <summary>
        /// Where a rectangle's edge meets the ray from its center toward <paramref name="toward"/>: where to put a
        /// bubble's tail. Returns the bottom-center when <paramref name="toward"/> is the center itself.
        /// </summary>
        /// <param name="rect">The bubble's rectangle.</param>
        /// <param name="toward">The point the tail points at.</param>
        public static Vector2 EdgePoint(Rect rect, Vector2 toward)
        {
            var c = rect.center;
            var d = toward - c;
            if (d.sqrMagnitude < 1e-6f) return new Vector2(c.x, rect.yMin);
            float hx = rect.width * 0.5f, hy = rect.height * 0.5f;
            float tx = Mathf.Abs(d.x) > 1e-6f ? hx / Mathf.Abs(d.x) : float.PositiveInfinity;
            float ty = Mathf.Abs(d.y) > 1e-6f ? hy / Mathf.Abs(d.y) : float.PositiveInfinity;
            return c + d * Mathf.Min(tx, ty);
        }

        // A point behind the camera projects mirrored through the screen center. Flip it back and push it
        // out to the screen edge, so the bubble sits on the side the speaker is on.
        static Vector2 EdgePointBehind(Vector2 projected, Vector2 screenSize)
        {
            var center = screenSize * 0.5f;
            var d = center - projected;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.down;
            float tx = Mathf.Abs(d.x) > 1e-6f ? center.x / Mathf.Abs(d.x) : float.PositiveInfinity;
            float ty = Mathf.Abs(d.y) > 1e-6f ? center.y / Mathf.Abs(d.y) : float.PositiveInfinity;
            return center + d * Mathf.Min(tx, ty);
        }

        static float ClampAxis(float v, float min, float max) => min > max ? (min + max) * 0.5f : Mathf.Clamp(v, min, max);
    }
}
