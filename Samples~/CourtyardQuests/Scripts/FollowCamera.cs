using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>Follows a target from a fixed offset, smoothly.</summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 4.2f, -4.6f);
        public float smoothing = 6f;

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 focus = target.position + Vector3.up * 0.35f;
            transform.position = Vector3.Lerp(transform.position, focus + offset, smoothing * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(focus - transform.position);

            // Check against the player's head and feet so a canopy that hides only half of them still fades.
            Vector3 head = target.position + Vector3.up * 0.6f, feet = target.position + Vector3.up * 0.1f;
            foreach (var occluder in HideWhenBlockingView.All)
                occluder.SetHidden(occluder.Blocks(transform.position, head) || occluder.Blocks(transform.position, feet));
        }
    }
}
