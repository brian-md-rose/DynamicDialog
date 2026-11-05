using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Every few seconds, looks for <see cref="DialogTag"/>s in range and in view, nearest first,
    /// and asks the speaker for an OnSee line about each until one plays. Each tag then rests
    /// for a cooldown. Writers use memory facts to make second and third sightings say something else.
    /// </summary>
    [RequireComponent(typeof(DialogSpeaker))]
    [AddComponentMenu("Dynamic Dialog/Dialog Vision")]
    public class DialogVision : MonoBehaviour
    {
        public static readonly Symbol ObjectFact = Symbol.Of("object");
        public static readonly Symbol ObjectDistanceFact = Symbol.Of("object_distance");

        [SerializeField] string concept = "OnSee";
        [SerializeField, Min(0.1f)] float pollInterval = 2f;
        [SerializeField, Min(0f)] float range = 10f;
        [SerializeField, Range(1f, 360f)] float fieldOfView = 120f;

        [Tooltip("Seconds before the same tag is considered again.")]
        [SerializeField, Min(0f)] float tagCooldown = 20f;

        [Tooltip("Where the speaker looks from. Empty uses this transform.")]
        [SerializeField] Transform eyes;

        [SerializeField] bool requireLineOfSight = true;
        [SerializeField] LayerMask occluders = ~0;

        readonly Dictionary<DialogTag, double> restingUntil = new Dictionary<DialogTag, double>();
        readonly List<(DialogTag tag, float distance)> visible = new List<(DialogTag, float)>();
        readonly RaycastHit[] hits = new RaycastHit[16];
        DialogSpeaker speaker;
        double nextPoll;
        Symbol conceptSymbol;

        void Awake() => speaker = GetComponent<DialogSpeaker>();

        void OnEnable()
        {
            conceptSymbol = Symbol.Of(concept);
            nextPoll = Time.timeAsDouble + Random.value * pollInterval;
        }

        void Update()
        {
            double now = Time.timeAsDouble;
            if (now < nextPoll) return;
            nextPoll = now + pollInterval;
            if (!speaker.IsAvailable) return;

            CollectVisible(now);
            foreach (var (tag, distance) in visible)
            {
                restingUntil[tag] = now + tagCooldown;
                var line = speaker.Speak(conceptSymbol, q =>
                {
                    q.Set(ObjectFact, tag.Tag);
                    q.Set(ObjectDistanceFact, distance);
                });
                if (line != null) break;
            }
        }

        /// <summary>Fills <c>visible</c> with tags in range, in view and not resting, nearest first.</summary>
        void CollectVisible(double now)
        {
            visible.Clear();
            Transform eye = eyes != null ? eyes : transform;
            Vector3 origin = eye.position;
            float cosHalfFov = Mathf.Cos(fieldOfView * 0.5f * Mathf.Deg2Rad);

            foreach (var tag in DialogTag.All)
            {
                if (tag == null || tag.transform.IsChildOf(transform)) continue;
                if (restingUntil.TryGetValue(tag, out double until) && now < until) continue;

                Vector3 to = tag.FocusPosition - origin;
                float distance = to.magnitude;
                if (distance > range) continue;
                if (distance > 0.001f && fieldOfView < 360f && Vector3.Dot(eye.forward, to / distance) < cosHalfFov) continue;
                if (requireLineOfSight && !HasLineOfSight(origin, to, distance, tag)) continue;
                visible.Add((tag, distance));
            }
            visible.Sort((a, b) => a.distance.CompareTo(b.distance));
        }

        bool HasLineOfSight(Vector3 origin, Vector3 to, float distance, DialogTag tag)
        {
            if (distance < 0.001f) return true;
            int count = Physics.RaycastNonAlloc(origin, to / distance, hits, distance, occluders, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var t = hits[i].transform;
                if (t.IsChildOf(transform)) continue;   // our own colliders
                if (t.IsChildOf(tag.transform)) continue; // the thing we're looking at
                if (hits[i].distance < distance - 0.05f) return false;
            }
            return true;
        }

        void OnValidate() => conceptSymbol = Symbol.Of(concept);

        void OnDrawGizmosSelected()
        {
            Transform eye = eyes != null ? eyes : transform;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(eye.position, range);
            Vector3 left = Quaternion.AngleAxis(-fieldOfView * 0.5f, eye.up) * eye.forward;
            Vector3 right = Quaternion.AngleAxis(fieldOfView * 0.5f, eye.up) * eye.forward;
            Gizmos.DrawRay(eye.position, left * range);
            Gizmos.DrawRay(eye.position, right * range);
        }
    }
}
