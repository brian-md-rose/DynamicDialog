using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// A set of authored rules: the global set, or one region's set (talk slide 110).
    /// Edited in the inspector for now; the .dlg and spreadsheet importers (M4) will write the same asset.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogRules", menuName = "Dynamic Dialog/Rule Database")]
    public class DialogRuleDatabase : ScriptableObject
    {
        [Tooltip("Fact keys to bucket rules by. Empty = concept, who.")]
        public List<string> partitionKeys = new List<string>();

        public List<RuleDefinition> rules = new List<RuleDefinition>();

        /// <summary>The .dlg or spreadsheet this database was imported from. Empty for inspector-made databases.</summary>
        [HideInInspector] public string sourcePath;

        public bool IsImported => !string.IsNullOrEmpty(sourcePath);

        [NonSerialized] RuleSet compiled;

        /// <summary>The compiled rule set, built on first use and cached until the asset changes.</summary>
        public RuleSet RuleSet => compiled ?? (compiled = Compile());

        public RuleSet Compile()
        {
            var built = new List<Rule>(rules.Count);
            foreach (var def in rules)
            {
                if (def == null) continue;
                try
                {
                    built.Add(def.ToRule());
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DynamicDialog] {name}: could not compile rule '{def.name}': {e.Message}", this);
                }
            }

            var keys = new List<Symbol>();
            foreach (var k in partitionKeys)
                if (!string.IsNullOrWhiteSpace(k)) keys.Add(Symbol.Of(k.Trim()));
            return new RuleSet(name, built, keys.ToArray());
        }

        /// <summary>Raised after the asset is edited, so a running director can swap in the new rules.</summary>
        public event Action<DialogRuleDatabase> Changed;

        /// <summary>Drops the cached rule set and every response's selection history.</summary>
        public void Invalidate()
        {
            compiled = null;
            foreach (var r in rules) r?.response?.ResetSelection();
            Changed?.Invoke(this);
        }

        /// <summary>Checks every rule and reports duplicate names. Returns the problems found.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rules)
            {
                if (r == null) continue;
                r.Validate(errors);
                if (!string.IsNullOrEmpty(r.name) && !names.Add(r.name)) errors.Add($"{r.name}: duplicate rule name.");
            }
            return errors;
        }

        void OnValidate() => Invalidate();
        void OnEnable() => compiled = null;
    }
}
