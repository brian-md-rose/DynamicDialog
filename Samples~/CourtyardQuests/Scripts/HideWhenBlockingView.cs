using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// Put this on scenery (like trees) that can get between the camera and the player.
    /// <see cref="FollowCamera"/> switches it to shadows-only while it blocks the view, so the
    /// player stays visible and the tree's shadow still falls on the ground.
    /// </summary>
    public class HideWhenBlockingView : MonoBehaviour
    {
        public static readonly List<HideWhenBlockingView> All = new List<HideWhenBlockingView>();

        Renderer[] renderers;
        bool hidden;

        void Awake() => renderers = GetComponentsInChildren<Renderer>();
        void OnEnable() => All.Add(this);
        void OnDisable() { All.Remove(this); SetHidden(false); }

        /// <summary>True if any part of this object's renderers crosses the segment from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public bool Blocks(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.001f) return false;
            var ray = new Ray(from, dir / length);
            foreach (var r in renderers)
                if (r != null && r.bounds.IntersectRay(ray, out float distance) && distance < length)
                    return true;
            return false;
        }

        public void SetHidden(bool hide)
        {
            if (hide == hidden || renderers == null) return;
            hidden = hide;
            foreach (var r in renderers)
                if (r != null) r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }
    }
}
