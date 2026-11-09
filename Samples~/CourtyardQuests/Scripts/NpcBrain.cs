using DynamicDialog;
using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// Simple NPC movement: walk a list of waypoints, or wander around home when there are none.
    /// Stops while talking and turns to face whoever it's talking with.
    /// </summary>
    [RequireComponent(typeof(DialogSpeaker))]
    public class NpcBrain : MonoBehaviour
    {
        public bool canWalk = true;
        public float speed = 1.2f;
        public Transform[] waypoints;
        [Tooltip("Wander radius around the start position, used when there are no waypoints.")]
        public float wanderRadius = 2.5f;
        public Vector2 pauseRange = new Vector2(1f, 3f);
        [Tooltip("While speaking, face the nearest other speaker within this distance.")]
        public float faceRange = 3f;

        DialogSpeaker speaker;
        Vector3 home, target;
        int waypointIndex;
        float pauseUntil;
        Transform faceTarget;
        float faceUntil;

        void Awake()
        {
            speaker = GetComponent<DialogSpeaker>();
            home = transform.position;
        }

        void Start() => PickTarget();

        /// <summary>Stop and look at someone for a while (e.g. the player who just started a chat).</summary>
        public void FaceFor(Transform who, float seconds)
        {
            faceTarget = who;
            faceUntil = Time.time + seconds;
        }

        void Update()
        {
            Transform look = Time.time < faceUntil ? faceTarget : null;
            if (look == null && speaker.IsSpeaking) look = NearestOtherSpeaker();
            if (look != null)
            {
                Turn(look.position - transform.position);
                return;
            }

            if (!canWalk || Time.time < pauseUntil) return;
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.15f)
            {
                pauseUntil = Time.time + Random.Range(pauseRange.x, pauseRange.y);
                PickTarget();
                return;
            }
            Turn(to);
            transform.position += to.normalized * (speed * Time.deltaTime);
        }

        void Turn(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 8f * Time.deltaTime);
        }

        Transform NearestOtherSpeaker()
        {
            var director = speaker.Director;
            if (director == null) return null;
            Transform best = null;
            float bestSq = faceRange * faceRange;
            foreach (var s in director.Conductor.Speakers)
            {
                if (!(s is DialogSpeaker other) || other == null || other == speaker) continue;
                float sq = (other.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = other.transform; }
            }
            return best;
        }

        void PickTarget()
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
                target = waypoints[waypointIndex].position;
            }
            else
            {
                Vector2 r = Random.insideUnitCircle * wanderRadius;
                target = home + new Vector3(r.x, 0f, r.y);
            }
            target.y = transform.position.y;
        }
    }
}
