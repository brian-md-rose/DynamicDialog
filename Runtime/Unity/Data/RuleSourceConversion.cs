using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Converts between the format-neutral <see cref="RuleSource"/> (what the parsers produce) and the
    /// serialized <see cref="RuleDefinition"/> stored in a <see cref="DialogRuleDatabase"/>.
    /// </summary>
    public static class RuleSourceConversion
    {
        /// <summary>Converts an authored rule into the serialized form stored in a database.</summary>
        /// <param name="s">The authored rule.</param>
        /// <param name="resolveAudio">Turns a clip name or path from the source into a clip. May return null.</param>
        public static RuleDefinition ToDefinition(this RuleSource s, Func<string, AudioClip> resolveAudio = null)
        {
            var d = new RuleDefinition
            {
                name = s.Name,
                who = s.Who,
                concept = s.Concept,
                once = s.Once,
                cooldown = s.Cooldown,
                weight = s.Weight,
                scoreBonus = s.ScoreBonus,
                sourceLine = s.SourceLine,
            };
            foreach (var c in s.Criteria)
                d.criteria.Add(new CriterionDefinition
                {
                    key = c.Key,
                    op = c.Op,
                    value = c.Value,
                    value2 = c.Value2,
                    useDefaultWhenMissing = c.HasDefault,
                    defaultWhenMissing = c.Default,
                });

            d.response.selection = s.Selection;
            foreach (var l in s.Lines)
                d.response.lines.Add(new DialogLineDefinition
                {
                    text = l.Text,
                    clip = string.IsNullOrWhiteSpace(l.Audio) || resolveAudio == null ? null : resolveAudio(l.Audio.Trim()),
                    duration = l.Duration,
                    animatorTrigger = l.Anim,
                    key = l.Key,
                });

            foreach (var w in s.Writes)
                d.remember.Add(new FactWriteDefinition
                {
                    scope = w.Scope,
                    character = w.Character,
                    key = w.Key,
                    op = w.Op,
                    value = w.Value,
                    duration = w.Duration,
                });

            foreach (var f in s.Followups)
                d.then.Add(new FollowupDefinition { target = f.Target, character = f.Character, concept = f.Concept, delay = f.Delay });
            return d;
        }

        /// <summary>The reverse, for exporting to .dlg and for the analyzer. Clips are written by name.</summary>
        public static RuleSource ToSource(this RuleDefinition d)
        {
            var s = new RuleSource
            {
                Name = d.name,
                Who = d.who,
                Concept = d.concept,
                Once = d.once,
                Cooldown = d.cooldown,
                Weight = d.weight,
                ScoreBonus = d.scoreBonus,
                SourceLine = d.sourceLine,
            };
            foreach (var c in d.criteria)
                s.Criteria.Add(new CriterionSource
                {
                    Key = c.key,
                    Op = c.op,
                    Value = c.value,
                    Value2 = c.value2,
                    HasDefault = c.useDefaultWhenMissing,
                    Default = c.defaultWhenMissing,
                });

            if (d.response != null)
            {
                s.Selection = d.response.selection;
                foreach (var l in d.response.lines)
                    s.Lines.Add(new LineSource
                    {
                        Text = l.text,
                        Audio = l.clip != null ? l.clip.name : null,
                        Duration = l.duration,
                        Anim = l.animatorTrigger,
                        Key = l.key,
                    });
            }

            foreach (var w in d.remember)
                s.Writes.Add(new FactWriteSource
                {
                    Scope = w.scope,
                    Character = w.character,
                    Key = w.key,
                    Op = w.op,
                    Value = w.value,
                    Duration = w.duration,
                });

            foreach (var f in d.then)
                s.Followups.Add(new FollowupSource { Target = f.target, Character = f.character, Concept = f.concept, Delay = f.delay });
            return s;
        }

        /// <summary>Every rule in the database as an authored rule, for exporting and checking.</summary>
        public static List<RuleSource> ToSources(this DialogRuleDatabase database)
        {
            var list = new List<RuleSource>(database.rules.Count);
            foreach (var r in database.rules)
                if (r != null) list.Add(r.ToSource());
            return list;
        }
    }
}
