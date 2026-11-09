using System;
using System.Linq;
using NUnit.Framework;

namespace DynamicDialog.Tests
{
    /// <summary>
    /// The manual's core C# examples (Documentation~/scripting.md), compiled and run here.
    /// <see cref="DocumentationTests"/> checks that the manual's code blocks still match this file, ignoring whitespace.
    /// </summary>
    public class DocumentationSnippetTests
    {
        [Test]
        public void EngineWithoutUnity()
        {
            var clock = new ManualClock();
            var engine = new DialogEngine(clock);

            var source = DialogScriptParser.Parse(@"
rule SeeBarrel
  concept OnSee
  if object == barrel
  say ""A barrel.""
  remember barrels_seen += 1
");
            engine.Matcher.Add(new RuleSet("barrels", source.Rules.Select(r => r.ToRule())));

            var bot = engine.GetOrCreateCharacter("RedBot");
            var match = engine.Ask(bot, "OnSee", q => q.Set("object", "barrel"));
            if (match.Matched)
            {
                engine.Commit(match);   // when the line starts: writes facts, records once and cooldown
                Console.WriteLine(match.Rule.Name);
            }

            Assert.That(source.Messages, Is.Empty);
            Assert.That(match.Matched, Is.True);
            Assert.That(match.Rule.Name, Is.EqualTo("SeeBarrel"));
            Assert.That(bot.Memory.TryGet("barrels_seen", clock.Now, out float seen), Is.True);
            Assert.That(seen, Is.EqualTo(1f));
        }

        [Test]
        public void RuleBuilderExample()
        {
            var rule = new RuleBuilder("SecondBarrel")
                .Concept("OnSee")
                .Where(Criterion.Eq("object", "barrel"))
                .Where(Criterion.Ge("barrels_seen", 1))
                .Increment("barrels_seen")
                .ThenAny("Groan", delay: 1f)
                .Build();

            Assert.That(rule.Score, Is.EqualTo(3));
            Assert.That(rule.Followups.Length, Is.EqualTo(1));
        }
    }
}
