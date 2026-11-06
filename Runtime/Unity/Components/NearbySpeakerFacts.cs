using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Adds who is around: "nearby_speakers" (how many other speakers are in earshot) and
    /// "near_&lt;who&gt;" = 1 for each of them. Covers criteria like "three survivors are present".
    /// </summary>
    [RequireComponent(typeof(DialogSpeaker))]
    [AddComponentMenu("Dynamic Dialog/Nearby Speaker Facts")]
    public class NearbySpeakerFacts : MonoBehaviour, IFactProvider
    {
        /// <summary>Old name of <see cref="WellKnownFacts.NearbySpeakers"/>.</summary>
        [Obsolete("Use WellKnownFacts.NearbySpeakers.")]
        public static readonly Symbol NearbySpeakersFact = WellKnownFacts.NearbySpeakers;

        static readonly Dictionary<Symbol, Symbol> nearKeys = new Dictionary<Symbol, Symbol>();

        DialogSpeaker self;

        void Awake() => self = GetComponent<DialogSpeaker>();

        /// <inheritdoc/>
        public void AddFacts(Query query)
        {
            var director = self.Director;
            if (director == null) return;
            int count = 0;
            foreach (var other in director.Conductor.Speakers)
            {
                if (ReferenceEquals(other, self) || !self.CanHear(other)) continue;
                count++;
                query.Set(NearKey(other.Character.Who), 1f);
            }
            query.Set(WellKnownFacts.NearbySpeakers, count);
        }

        /// <summary>The "near_&lt;who&gt;" key for a character, cached so queries don't build strings.</summary>
        public static Symbol NearKey(Symbol who)
        {
            if (!nearKeys.TryGetValue(who, out var key)) nearKeys[who] = key = Symbol.Of(WellKnownFacts.NearPrefix + who.Name);
            return key;
        }
    }
}
