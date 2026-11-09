using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>Feeds the Animator's "Speed" parameter from how fast this transform moves.</summary>
    public class CharacterAnimation : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");

        public Animator animator;
        Vector3 lastPosition;
        float smoothed;

        void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            lastPosition = transform.position;
        }

        void LateUpdate()
        {
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            smoothed = Mathf.Lerp(smoothed, speed, 15f * Time.deltaTime);
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetFloat(SpeedId, smoothed);
        }
    }
}
