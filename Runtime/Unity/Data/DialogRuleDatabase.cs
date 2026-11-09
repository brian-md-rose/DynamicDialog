using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DynamicDialog
{
    /// <summary>
    /// A set of authored rules: the global set, or one region's set (talk slide 110).
    /// Made in the inspector (Assets > Create > Dynamic Dialog > Rule Database), or imported from a .dlg file
    /// or a spreadsheet. Add it to a Dialog Director or a Dialog Region to load its rules.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogRules", menuName = "Dynamic Dialog/Rule Database")]
    public class DialogRuleDatabase : ScriptableObject
    {
        /// <summary>Fact keys to bucket rules by. Empty means concept and who.</summary>
        [Tooltip("Fact keys to bucket rules by. Empty = concept, who.")]
        public List<string> partitionKeys = new List<string>();

        /// <summary>The rules, in authored order.</summary>
        public List<RuleDefinition> rules = new List<RuleDefinition>();

        // Written by the importers.
        [SerializeField, HideInInspector, FormerlySerializedAs("sourcePath")] internal string sourceAssetPath;

        /// <summary>The .dlg or spreadsheet this database was imported from. Empty for inspector-made databases.</summary>
        public string SourcePath => sourceAssetPath ?? "";

        /// <summary>Old name of <see cref="SourcePath"/>.</summary>
        [Obsolete("Use SourcePath.")]
        public string sourcePath => SourcePath;

        /// <summary>True when this database was imported from a file, so it is edited there, not in the inspector.</summary>
        public bool IsImported => !string.IsNullOrEmpty(sourceAssetPath);

        [NonSerialized] RuleSet compiled;
        [NonSerialized] int compiledSession;

        /// <summary>
        /// The compiled rule set, built on first use and cached until the asset changes, or until Play Mode
        /// starts again. That way <c>once</c> and cooldown state never carries over from an earlier Play Mode
        /// session, even when Enter Play Mode Options skip the domain reload.
        /// </summary>
        public RuleSet RuleSet
        {
            get
            {
                if (compiled != null && compiledSession != DialogStaticState.PlaySession)
                {
                    // Built in an earlier Play Mode session (or in Edit Mode), and kept because the domain wasn't reloaded.
                    compiled = null;
                    foreach (var r in rules) r?.response?.ResetSelection();
                }
                if (compiled == null)
                {
                    compiled = Compile();
                    compiledSession = DialogStaticState.PlaySession;
                }
                return compiled;
            }
        }

        /// <summary>Builds a new rule set from the current rules, without caching it. Rules that fail to compile are logged and skipped.</summary>
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
