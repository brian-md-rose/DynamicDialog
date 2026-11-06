using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// World facts that only exist while a region is loaded (talk slide 111): the region's name,
    /// plus any fixed facts. Regions add this to the director's world providers while active.
    /// </summary>
    [Serializable]
    public class RegionFacts : IFactProvider
    {
        /// <summary>Old name of <see cref="WellKnownFacts.Region"/>.</summary>
        [Obsolete("Use WellKnownFacts.Region.")]
        public static readonly Symbol RegionFact = WellKnownFacts.Region;

        /// <summary>Sets the "region" fact while the region is loaded. Empty means don't set it.</summary>
        [Tooltip("Sets the 'region' fact while this region is loaded. Empty = don't set it.")]
        public string regionName;

        /// <summary>Extra world facts that exist only while the region is loaded. Call <see cref="Invalidate"/> after changing them at runtime.</summary>
        [Tooltip("Extra world facts that exist only while this region is loaded.")]
        public List<StaticFacts.Entry> facts = new List<StaticFacts.Entry>();

        [NonSerialized] Symbol[] keys;
        [NonSerialized] float[] values;
        [NonSerialized] bool[] symbols;

        /// <summary>True when there is no region name and no facts.</summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(regionName) && (facts == null || facts.Count == 0);

        /// <inheritdoc/>
        public void AddFacts(Query query)
        {
            if (keys == null) Cache();
            if (!string.IsNullOrWhiteSpace(regionName)) query.Set(WellKnownFacts.Region, Symbol.Of(regionName.Trim()));
            for (int i = 0; i < keys.Length; i++)
                if (!keys[i].IsNone) query.Set(keys[i], values[i], symbols[i]);
        }

        /// <summary>Call after changing <see cref="facts"/> at runtime.</summary>
        public void Invalidate() => keys = null;

        void Cache()
        {
            int n = facts?.Count ?? 0;
            keys = new Symbol[n];
            values = new float[n];
            symbols = new bool[n];
            for (int i = 0; i < n; i++)
            {
                keys[i] = Symbol.Of(facts[i].key?.Trim());
                values[i] = FactValue.Parse(facts[i].value, out symbols[i]);
            }
        }
    }
}
