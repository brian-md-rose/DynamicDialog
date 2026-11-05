using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>An authored criterion. Values are text: numbers, true/false, or names like "swamp".</summary>
    [Serializable]
    public class CriterionDefinition
    {
        public string key;
        public CriterionOp op = CriterionOp.Equals;
        public string value;

        [Tooltip("Upper bound, used only by Between.")]
        public string value2;

        [Tooltip("Test this value instead of rejecting the rule when the fact is missing.")]
        public bool useDefaultWhenMissing;
        public float defaultWhenMissing;

        public Criterion ToCriterion()
        {
            var c = FactValue.CreateCriterion(Symbol.Of(key), op, FactValue.Parse(value), FactValue.Parse(value2));
            return useDefaultWhenMissing && op != CriterionOp.ZeroOrMissing ? c.WhenMissing(defaultWhenMissing) : c;
        }
    }

    [Serializable]
    public class FactWriteDefinition
    {
        public FactScope scope = FactScope.Speaker;

        [Tooltip("Only for the Character scope.")]
        public string character;

        public string key;
        public FactOp op = FactOp.Set;
        public string value = "1";

        [Tooltip("Seconds until the fact expires. 0 = never.")]
        [Min(0f)]
        public float duration;

        public FactWrite ToFactWrite() =>
            new FactWrite(scope, Symbol.Of(key), op, FactValue.Parse(value), duration, Symbol.Of(character));
    }

    [Serializable]
    public class FollowupDefinition
    {
        public FollowupTarget target = FollowupTarget.Character;

        [Tooltip("Only for the Character target.")]
        public string character;

        public string concept;

        [Min(0f)]
        public float delay;

        public Followup ToFollowup() =>
            new Followup(target, Symbol.Of(concept), Symbol.Of(character), delay);
    }

    /// <summary>
    /// A rule as authored in the inspector or produced by an importer. Compiled into a core
    /// <see cref="Rule"/> whose payload is <see cref="response"/>.
    /// </summary>
    [Serializable]
    public class RuleDefinition
    {
        public string name;

        [Tooltip("Shorthand for a 'who == X' criterion. Leave empty to let anyone use the rule.")]
        public string who;

        [Tooltip("Shorthand for a 'concept == X' criterion.")]
        public string concept;

        public List<CriterionDefinition> criteria = new List<CriterionDefinition>();
        public DialogResponse response = new DialogResponse();
        public List<FactWriteDefinition> remember = new List<FactWriteDefinition>();
        public List<FollowupDefinition> then = new List<FollowupDefinition>();

        [Tooltip("Disable the rule after it plays once.")]
        public bool once;

        [Min(0f)] public float cooldown;
        [Min(0f)] public float weight = 1f;
        public int scoreBonus;

        /// <summary>Line in the source file this rule was imported from (0 if made in the inspector).</summary>
        [HideInInspector] public int sourceLine;

        /// <summary>Builds the core rule. Its payload is <see cref="response"/>.</summary>
        public Rule ToRule() => this.ToSource().ToRule(response);

        /// <summary>Adds human-readable problems to <paramref name="errors"/>. Returns false if any were found.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            string label = string.IsNullOrEmpty(name) ? "<unnamed rule>" : name;
            if (string.IsNullOrWhiteSpace(name)) errors.Add($"{label}: has no name.");
            if (string.IsNullOrWhiteSpace(concept)) errors.Add($"{label}: has no concept, so it can match any event.");
            for (int i = 0; i < criteria.Count; i++)
                if (string.IsNullOrWhiteSpace(criteria[i].key)) errors.Add($"{label}: criterion {i} has no key.");
            for (int i = 0; i < remember.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(remember[i].key)) errors.Add($"{label}: remember {i} has no key.");
                if (remember[i].scope == FactScope.Character && string.IsNullOrWhiteSpace(remember[i].character))
                    errors.Add($"{label}: remember {i} targets a character but names none.");
            }
            for (int i = 0; i < then.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(then[i].concept)) errors.Add($"{label}: followup {i} has no concept.");
                if (then[i].target == FollowupTarget.Character && string.IsNullOrWhiteSpace(then[i].character))
                    errors.Add($"{label}: followup {i} targets a character but names none.");
            }
            if (response == null || response.lines.Count == 0)
                errors.Add($"{label}: has no lines (fine for logic-only rules).");
            return errors.Count == before;
        }
    }
}
