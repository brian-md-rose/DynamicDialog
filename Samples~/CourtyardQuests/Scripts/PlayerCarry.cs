using DynamicDialog;
using UnityEngine;

namespace DynamicDialog.Samples.Courtyard
{
    /// <summary>
    /// Lets the player carry one <see cref="Carryable"/> at a time. What's carried is a world fact,
    /// <c>player_holding</c>, added by <see cref="CourtyardWorldFacts"/> ("nothing" when empty-handed).
    /// Picking up and dropping give the player the OnPickUp and OnDrop concepts, with <c>item</c> set.
    /// </summary>
    public class PlayerCarry : MonoBehaviour
    {
        public static PlayerCarry Instance { get; private set; }
        public static readonly Symbol Nothing = Symbol.Of("nothing");

        public Transform holdPoint;
        public float pickupRange = 1.1f;

        public Carryable Held { get; private set; }
        public Symbol HeldTag => Held != null ? Held.Tag : Nothing;

        DialogSpeaker speaker;

        void Awake() => speaker = GetComponent<DialogSpeaker>();
        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        public Carryable FindNearest()
        {
            Carryable best = null;
            float bestSq = pickupRange * pickupRange;
            foreach (var c in Carryable.All)
            {
                if (!c.CanPickUp) continue;
                float sq = (c.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = c; }
            }
            return best;
        }

        public void Toggle()
        {
            if (Held != null) Drop();
            else
            {
                var c = FindNearest();
                if (c != null) PickUp(c);
            }
        }

        void PickUp(Carryable c)
        {
            Held = c;
            c.AttachTo(holdPoint != null ? holdPoint : transform);
            Say("OnPickUp", c.Tag);
        }

        void Drop()
        {
            var c = Held;
            Held = null;
            Vector3 spot = transform.position + transform.forward * 0.5f;
            spot.y = 0f;
            c.PlaceAt(spot, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            Say("OnDrop", c.Tag);
        }

        /// <summary>Hands whatever the player holds to someone else, e.g. the crutch to Jun.</summary>
        public Carryable GiveAway(Transform newHolder)
        {
            var c = Held;
            if (c == null) return null;
            Held = null;
            c.AttachTo(newHolder);
            c.CanPickUp = false;
            return c;
        }

        void Say(string concept, Symbol item)
        {
            if (speaker != null) speaker.Speak(concept, q => q.Set(Symbol.Of("item"), item), interrupt: true);
        }
    }
}
