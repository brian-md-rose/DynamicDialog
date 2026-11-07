using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>An authored criterion. Values are text: numbers, true/false, or names like "swamp".</summary>
    [Serializable]
    public class CriterionDefinition
    {
        /// <summary>The fact tested.</summary>
        public string key;

        /// <summary>The comparison.</summary>
        public CriterionOp op = CriterionOp.Equals;

        /// <summary>The value compared with (the lower bound for Between).</summary>
        public string value;

        /// <summary>Upper bound, used only by <see cref="CriterionOp.Between"/>.</summary>
        [Tooltip("Upper bound, used only by Between.")]
        public string value2;

        /// <summary>Test <see cref="defaultWhenMissing"/> instead of failing when the fact is missing.</summary>
        [Tooltip("Test this value instead of rejecting the rule when the fact is missing.")]
        public bool useDefaultWhenMissing;

        /// <summary>The value tested for a missing fact, when <see cref="useDefaultWhenMissing"/> is set.</summary>
        public float defaultWhenMissing;

        /// <summary>Builds the core criterion.</summary>
        public Criterion ToCriterion()
        {
            var c = FactValue.CreateCriterion(Symbol.Of(key), op, FactValue.Parse(value), FactValue.Parse(value2));
            return useDefaultWhenMissing && op != CriterionOp.ZeroOrMissing ? c.WhenMissing(defaultWhenMissing) : c;
        }
    }

    /// <summary>An authored fact write ("remember" or "forget").</summary>
    [Serializable]
    public class FactWriteDefinition
    {
        /// <summary>Whose memory is written.</summary>
        public FactScope scope = FactScope.Speaker;

        /// <summary>The character whose memory is written, for <see cref="FactScope.Character"/>.</summary>
        [Tooltip("Only for the Character scope.")]
        public string character;

        /// <summary>The fact written.</summary>
        public string key;

        /// <summary>How the fact changes.</summary>
        public FactOp op = FactOp.Set;

        /// <summary>The value to set, or the amount to add.</summary>
        public string value = "1";

        /// <summary>Seconds until the fact expires. 0 means never.</summary>
        [Tooltip("Seconds until the fact expires. 0 = never.")]
        [Min(0f)]
        public float duration;

        /// <summary>Builds the core fact write.</summary>
        public FactWrite ToFactWrite() =>
            new FactWrite(scope, Symbol.Of(key), op, FactValue.Parse(value), duration, Symbol.Of(character));
    }

    /// <summary>An authored followup ("then").</summary>
    [Serializable]
    public class FollowupDefinition
    {
        /// <summary>Who receives the concept.</summary>
        public FollowupTarget target = FollowupTarget.Character;

        /// <summary>The receiver, for <see cref="FollowupTarget.Character"/>.</summary>
        [Tooltip("Only for the Character target.")]
        public string character;

        /// <summary>The concept sent.</summary>
        public string concept;

        /// <summary>Extra seconds to wait after the line finishes.</summary>
        [Min(0f)]
        public float delay;

        /// <summary>Builds the core followup.</summary>
        public Followup ToFollowup() =>
            new Followup(target, Symbol.Of(concept), Symbol.Of(character), delay);
    }

    /// <summary>An authored game event ("do").</summary>
    [Serializable]
    public class DialogEventDefinition
    {
        /// <summary>The event's name, e.g. "GiveItem".</summary>
        public string name;

        /// <summary>Its arguments, as text.</summary>
        [Tooltip("Arguments passed to handlers, as text.")]
        public List<string> args = new List<string>();

        /// <summary>When it is raised.</summary>
        [Tooltip("Line Started: when the line starts (with the rule's facts). Line Ended: when the line ends, and only if it wasn't interrupted.")]
        public DialogEventTiming timing = DialogEventTiming.LineStarted;

        /// <summary>Builds the core event.</summary>
        public DialogEvent ToDialogEvent() => new DialogEvent(name, args, timing);
    }

    /// <summary>
    /// A rule as authored in the inspector or produced by an importer. Compiled into a core
    /// <see cref="Rule"/> whose payload is <see cref="response"/>.
    /// </summary>
    [Serializable]
    public class RuleDefinition
    {
        /// <summary>The rule's name.</summary>
        public string name;

        /// <summary>Shorthand for a "who == X" criterion. Empty lets anyone use the rule.</summary>
        [Tooltip("Shorthand for a 'who == X' criterion. Leave empty to let anyone use the rule.")]
        public string who;

        /// <summary>Shorthand for a "concept == X" criterion.</summary>
        [Tooltip("Shorthand for a 'concept == X' criterion.")]
        public string concept;

        /// <summary>Conditions that must all pass.</summary>
        public List<CriterionDefinition> criteria = new List<CriterionDefinition>();

        /// <summary>The line's variations.</summary>
        public DialogResponse response = new DialogResponse();

        /// <summary>Facts written when the rule plays.</summary>
        public List<FactWriteDefinition> remember = new List<FactWriteDefinition>();

        /// <summary>Concepts sent when the line finishes.</summary>
        public List<FollowupDefinition> then = new List<FollowupDefinition>();

        /// <summary>Game events raised when the rule plays ("do").</summary>
        [Tooltip("Game events raised when the rule plays, for DialogEvents.On or a Dialog Event Listener.")]
        public List<DialogEventDefinition> events = new List<DialogEventDefinition>();

        /// <summary>The rule plays only once.</summary>
        [Tooltip("Disable the rule after it plays once.")]
        public bool once;

        /// <summary>Seconds before the rule can play again.</summary>
        [Min(0f)] public float cooldown;

        /// <summary>Relative chance among tied rules.</summary>
        [Min(0f)] public float weight = 1f;

        /// <summary>Added to the criteria count to make the score.</summary>
        public int scoreBonus;

        /// <summary>Names the rule had before ("renamed" in .dlg), so saved once and cooldown state still finds it.</summary>
        [Tooltip("Names this rule had before. Saved games that remember the rule under an old name (for once and cooldown) still apply to it.")]
        public List<string> formerNames = new List<string>();


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
            if (events != null)
                for (int i = 0; i < events.Count; i++)
                    if (events[i] == null || string.IsNullOrWhiteSpace(events[i].name)) errors.Add($"{label}: event {i} has no name.");
            if (response == null || response.lines.Count == 0)
                errors.Add($"{label}: has no lines (fine for logic-only rules).");
            return errors.Count == before;
        }
    }
}
