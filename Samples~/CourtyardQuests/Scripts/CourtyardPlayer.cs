using DynamicDialog;
using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// WASD / arrow keys to walk (camera relative), E to talk to the nearest character,
    /// F to pick up or drop a <see cref="Carryable"/>, Tab to toggle the Dynamic Dialog debug overlays.
    /// Gamepad: left stick / d-pad, A (south) talk, X (west) pick up / drop, View (select) debug.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(DialogSpeaker), typeof(PlayerCarry))]
    public class CourtyardPlayer : MonoBehaviour
    {
        public float speed = 2.2f;
        public float turnSpeed = 12f;
        [Tooltip("How close a character has to be to talk to them.")]
        public float talkRange = 1.6f;
        public Transform cameraTransform;
        [Tooltip("Debug overlays toggled with Tab.")]
        public Behaviour[] debugOverlays;

        public DialogSpeaker Target { get; private set; }

        CharacterController controller;
        DialogSpeaker self;
        PlayerCarry carry;
        float fallSpeed;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            self = GetComponent<DialogSpeaker>();
            carry = GetComponent<PlayerCarry>();
        }

        void Update()
        {
            CourtyardInput.Poll();
            Move(CourtyardInput.Move);

            Target = FindNearestSpeaker();
            if (CourtyardInput.Talk) TalkToTarget();
            if (CourtyardInput.Carry) carry.Toggle();
            if (CourtyardInput.ToggleDebug)
                foreach (var b in debugOverlays) if (b != null) b.enabled = !b.enabled;
        }

        void Move(Vector2 input)
        {
            Vector3 forward = Vector3.forward, right = Vector3.right;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.Cross(Vector3.up, forward);
            }
            Vector3 move = (forward * input.y + right * input.x) * speed;

            fallSpeed = controller.isGrounded ? -1f : fallSpeed - 9.81f * Time.deltaTime;
            controller.Move((move + Vector3.up * fallSpeed) * Time.deltaTime);

            if (move.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), turnSpeed * Time.deltaTime);
        }

        DialogSpeaker FindNearestSpeaker()
        {
            var director = DialogDirector.Instance;
            if (director == null || director.Conductor == null) return null;
            DialogSpeaker best = null;
            float bestSq = talkRange * talkRange;
            foreach (var s in director.Conductor.Speakers)
            {
                if (!(s is DialogSpeaker speaker) || speaker == null || speaker == self) continue;
                float sq = (speaker.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = speaker; }
            }
            return best;
        }

        /// <summary>Talk to whoever is in range (what E / gamepad A does). Returns false if nobody answered.</summary>
        public bool TalkToTarget()
        {
            Target = FindNearestSpeaker();
            return Target != null && TalkTo(Target);
        }

        bool TalkTo(DialogSpeaker npc)
        {
            // Busy characters return null; the player can try again when they finish their line.
            var line = npc.Speak("OnTalk");
            if (line == null) return false;
            var brain = npc.GetComponent<NpcBrain>();
            if (brain != null) brain.FaceFor(transform, 4f);
            Vector3 to = npc.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(to);
            return true;
        }
    }
}
