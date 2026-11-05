using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// A trigger volume that writes "area" = <c>areaName</c> into the memory of speakers inside it,
    /// e.g. the "start area" criterion in talk slide 67. It can also give each entering speaker a concept.
    /// Needs a trigger collider, and a Rigidbody on either the area or the speaker.
    /// </summary>
    [AddComponentMenu("Dynamic Dialog/Dialog Area")]
    public class DialogArea : MonoBehaviour
    {
        public static readonly Symbol AreaFact = Symbol.Of("area");

        [SerializeField] string areaName;

        [Tooltip("Optional concept given to a speaker when it enters, with the area as a fact.")]
        [SerializeField] string enterConcept;

        public Symbol Area => Symbol.Of(string.IsNullOrWhiteSpace(areaName) ? gameObject.name : areaName.Trim());

        void OnTriggerEnter(Collider other)
        {
            var speaker = other.GetComponentInParent<DialogSpeaker>();
            if (speaker == null || speaker.Memory == null) return;
            var director = speaker.Director;
            if (director == null) return;

            speaker.Memory.Set(AreaFact, Area, director.Engine.Clock.Now);
            if (!string.IsNullOrWhiteSpace(enterConcept)) speaker.Speak(enterConcept);
        }

        void OnTriggerExit(Collider other)
        {
            var speaker = other.GetComponentInParent<DialogSpeaker>();
            if (speaker == null || speaker.Memory == null || speaker.Director == null) return;

            // Only clear it if the speaker hasn't already walked into another area.
            double now = speaker.Director.Engine.Clock.Now;
            if (speaker.Memory.TryGet(AreaFact, now, out float current) && current == Area.AsValue)
                speaker.Memory.Remove(AreaFact);
        }
    }
}
