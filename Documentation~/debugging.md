# Debugger and Rule Validator

"Why didn't my line play?" is the question every writer asks. The **Debugger** answers it for the running game, and
the **Rule Validator** finds mistakes before you press Play.

## Debugger

Open **Window > Dynamic Dialog > Debugger**. It has three tabs.

### Lookups (Play Mode)

Every time a speaker asks for a line, the director records the lookup. The list on the left shows them newest
first: the time, the speaker, the concept and the result, colored **green** (the line played), **yellow** (a rule
matched but its line didn't play, for example because another listener won a `then any`), or **red** (no rule
matched).

The toolbar has **Live**/**Paused** (freeze the list while you read it), **Clear**, **Misses only** (just the red
ones), and a search field that filters by speaker, concept or winning rule.

Select a lookup to see:

- **The outcome**: the winner and its score, whether it was picked from tied rules, and whether it played. Below it,
  what the winner does: its fact writes, game events and followups.
- **Facts**: every fact in the query, its value, and where it came from: `world memory`, a speaker's memory
  (`Nick memory`), a fact provider (its type name, `world: …` for world providers), `event`, or `engine`.
- **Rules tested**: each rule the matcher looked at, with its score and what happened to it, such as the condition
  that failed and the value it saw. To stay fast, the matcher only tests rules in the buckets for this speaker and
  concept, and stops once scores fall below the best match; rules it didn't need to test aren't listed.
- **Why didn't this rule play?** Pick any loaded rule that could answer this concept, and get a one-line answer:
  - *a condition failed*, naming the condition and the value it saw (or that the fact was missing);
  - *outscored*: it matched, but a more specific rule had a higher score;
  - *lost a tie*: it matched with the same score, and the other rule was picked at random;
  - *can't play right now*: it is a `once` rule that already played, or it is on cooldown;
  - *not loaded*: its conditions pass, but its rule set wasn't loaded for this lookup.
- **Copy into Test Query** copies the speaker, concept and facts into the Test Query tab, so you can change a fact and
  see what would happen.

Recording needs **Record Lookups** on the Dialog Director (on by default). It only runs in the editor and development
builds, so release builds pay nothing for it.

### Coverage (Play Mode)

How many of the loaded rules have played this session, overall and per rule set, and how often each one played.
Rules that have never played are listed first, in red; **Unplayed only** hides the rest. Play through your content and
check which lines nobody has heard yet. Used `once` rules are marked.

### Test Query (any time)

Type a **Who**, a **Concept** and facts, one per line (`map = swamp`, `health = 20`), pick which databases to include,
and click **Find Best Rule**. Every rule that answers the concept is listed, ranked, with the same verdicts as above.
Rules are compiled fresh from the project's databases, so this works without Play Mode and ignores `once` and
cooldown state. Use it to check a new rule before you play.

## Overlays in the Game view

Two IMGUI components help while playtesting. Neither is meant to ship.

- **Debug Overlay** (**Add Component > Dynamic Dialog > Debug > Debug Overlay**) shows a label over each speaker: its
  name, its current line, and its latest lookup, in the same green, yellow and red. **Only Misses** shows only lookups
  that found nothing, which is the quickest way to spot speakers asking for concepts that have no rules.
- **Subtitle Overlay** shows each line as a subtitle, optionally with its rule name and concept.

Turning on **Log Lines** or **Log No Match** on the Dialog Director writes the same information to the Console.

## Rule Validator

Open **Window > Dynamic Dialog > Rule Validator** and click **Check All Rules**. It checks every rule database in the
project together, so it can see across files: a followup in one file can be answered by a rule in another.

| Severity | It finds |
|---|---|
| Error | Rules that can never match, because two of their conditions contradict each other (`health < 10` and `health > 50`) |
| Warning | Followups no rule answers, or that only other speakers answer |
| Warning | Fact names used once that look like a typo of another ("Did you mean…?"), and the same for event names |
| Warning | Duplicate rule names |
| Warning | `renamed` mistakes: a former name that is still another rule's name, or two rules claiming the same former name |
| Warning | Rules with no concept, and rules with no lines, facts, events or followups |
| Info | Facts that no rule writes and aren't built in (fine when your code provides them), two rules with exactly the same conditions, and writes to characters who have no rules |

Click a message to select its database, or **Open** to open the file at that line. The **Errors**, **Warnings** and
**Info** buttons filter the list.

List the facts your own fact providers add (`health, weapon, armed`) in **Facts from code**, so they aren't reported
as unknown.

Each database's inspector has **Check Rules**, which runs the same checks on that database alone. The
[Rule Editor](rule-editor.md) runs them as you type.

## From code

- `DialogDirector.Instance.Recorder` (a `DialogRecorder`) holds the recorded lookups: `Records`, and the `Recorded`
  and `Started` events. Each `LookupRecord` has the query, fact sources, rules tested and winner.
- `RuleDiagnostics.Explain(rule, record)` gives the "why didn't this rule play?" answer as a `RuleExplanation`
  (`Verdict` and `Text`). `RuleDiagnostics.EvaluateAll(sets, query, now)` ranks every rule for a query, like Test Query.
- `RuleAnalyzer` runs the validator's checks on rule sources, for your own build checks.
