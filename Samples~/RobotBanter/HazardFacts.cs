using UnityEngine;

namespace DynamicDialog.Samples
{
    /// <summary>
    /// Adds "in_danger" (1 when near the glowing barrel) to the robot's facts, and makes the robot
    /// shout about it, interrupting whatever it was saying.
    /// </summary>
    public class HazardFacts : MonoBehaviour, IFactProvider
    {
        public Transform hazard;
        public float radius = 2.5f;

        static readonly Symbol InDanger = Symbol.Of("in_danger");
        DialogSpeaker speaker;
        bool wasInDanger;

        bool IsInDanger => hazard != null && (hazard.position - transform.position).sqrMagnitude < radius * radius;

        void Awake() => speaker = GetComponent<DialogSpeaker>();

        public void AddFacts(Query query) => query.Set(InDanger, IsInDanger);

        void Update()
        {
            bool now = IsInDanger;
            if (now && !wasInDanger && speaker != null) speaker.Speak("InDanger", interrupt: true);
            wasInDanger = now;
        }
    }
}
