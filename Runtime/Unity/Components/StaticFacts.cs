using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Fixed facts set in the inspector, e.g. "gender = female" or "faction = penguin".
    /// On a speaker they describe the character; on the director they describe the world.
    /// </summary>
    [AddComponentMenu("Dynamic Dialog/Static Facts")]
    public class StaticFacts : MonoBehaviour, IFactProvider
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            [Tooltip("A number, true/false, or a name.")]
            public string value;
        }

        [SerializeField] List<Entry> facts = new List<Entry>();

        Symbol[] keys;
        float[] values;
        bool[] symbols;

        public void AddFacts(Query query)
        {
            if (keys == null) Cache();
            for (int i = 0; i < keys.Length; i++)
                if (!keys[i].IsNone) query.Set(keys[i], values[i], symbols[i]);
        }

        /// <summary>Changes or adds a fact at runtime.</summary>
        public void SetFact(string key, string value)
        {
            int i = facts.FindIndex(e => string.Equals(e.key, key, StringComparison.OrdinalIgnoreCase));
            var entry = new Entry { key = key, value = value };
            if (i >= 0) facts[i] = entry;
            else facts.Add(entry);
            keys = null;
        }

        void Cache()
        {
            keys = new Symbol[facts.Count];
            values = new float[facts.Count];
            symbols = new bool[facts.Count];
            for (int i = 0; i < facts.Count; i++)
            {
                keys[i] = Symbol.Of(facts[i].key?.Trim());
                values[i] = FactValue.Parse(facts[i].value, out symbols[i]);
            }
        }

        void OnValidate() => keys = null;
    }
}
