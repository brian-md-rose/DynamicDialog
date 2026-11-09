# FAQ and troubleshooting

## My line never plays

Open **Window > Dynamic Dialog > Debugger** while playing, find the speaker's lookup in **Lookups** (tick **Misses
only** if no rule matched), and pick your rule under **Why didn't this rule play?**. The answer is one of these:

- **No lookup appears at all.** Nothing asked that speaker for that concept. Check the speaker's **Idle Concept**, the
  Dialog Vision's **Concept**, or the concept your code passes to `Speak`, against the rule's `concept`. Also check
  that the speaker isn't already talking (busy speakers ignore requests unless you interrupt) and that **Can Speak**
  is on.
- **A condition failed.** The answer names the condition and the value it saw. A fact that isn't in the query fails
  every criterion on it, including `!=`. Use `missing` for "0 or not set yet", or add `default 0`.
- **Outscored.** A rule with more conditions matched too. Add a condition to your rule, or a `bonus`.
- **Lost a tie.** Another rule with the same score was picked at random. Add a condition, or change `weight`.
- **Can't play right now.** It's a `once` rule that already played, or it's on cooldown.
- **Not loaded.** Its database isn't in the director's **Databases** list, or its region isn't enabled.

If the rule isn't in the list at all, its file has an error (see the Console) or isn't loaded.

## The Debugger says "Lookup recording is off"

Turn on **Record Lookups** on the Dialog Director. Recording only runs in the editor and development builds.

## Two of my rules alternate at random

They have the same score and both match. That's intended when they're variations of one idea: then it's simpler to
make them one rule with several `say` lines. When one should win, give it another condition. The Rule Validator
lists rules with identical conditions.

## Nothing shows on screen, but the Debugger says the line played

- Is there a presenter? Add **GameObject > Dynamic Dialog > Speech Bubbles** or **Subtitle Bar**.
- **Speech bubbles** follow `Camera.main` unless **View Camera** is set. Check that your camera is tagged MainCamera,
  and that the speaker isn't farther than **Max Distance**, or off screen with **Offscreen** set to *Hide*.
- **No text in the bubbles:** TextMeshPro's Essential Resources aren't imported. Use **Window > TextMeshPro > Import
  TMP Essential Resources**.
- **GameObject > Dynamic Dialog has no Speech Bubbles item:** the uGUI or TextMeshPro package isn't installed. See
  [Optional packages](components.md#optional-packages).
- An audio-only line (`say audio clip`) has no text to show.

## Every line shows twice

Both the speech bubbles and the subtitle bar (or another presenter) are enabled. Each presenter shows every line.

## Followups don't happen

- The first line was interrupted or stopped. Interrupted lines send no followups.
- The receiver was busy talking, couldn't speak, or had no matching rule. Check its lookup in the Debugger: the
  followup's query has `from` set.
- `then any` and `then all` only reach speakers whose **Earshot Radius** covers the speaker. `then Name` does too when
  the director's **Named Followups Require Earshot** is on.
- `then Name` needs a speaker whose **Who** (or GameObject name) is exactly that name.

## A character forgot everything after I pressed Play again

Memory lasts for one Play Mode session. Use [save and load](save-load.md) to keep it.

## My `once` rule played again

- Its file was hot-reloaded, which resets that file's once and cooldown state.
- The rule was renamed and the save was made under the old name. Add a [`renamed`](save-load.md#renamed-rules) line.
- The other way round, a `once` rule without a `who` plays once in total, not once per character. To have each
  character say it once, use a memory fact instead (`if said_it missing` and `remember said_it = 1`).

## Saving the file doesn't change the running game

Unity has to reimport the file: switch back to the Unity window, check **Auto Refresh** (**Edit > Preferences >
Asset Pipeline**), or press Ctrl+R. See [Hot reload](hot-reload.md).

## "Rule has no concept, so it can answer any event"

The rule has no `concept` line, so it matches every concept: idle chatter, sightings, everything. Add a concept, or
an `@concept` default above it.

## Fact names: case, spaces, typos?

Names ignore case. They can't contain spaces (quote values that do). A typo makes a different fact, which is why the
[Rule Validator](debugging.md#rule-validator) suggests "Did you mean…?" and the [Rule Editor](rule-editor.md) offers
names from a string table.

## Is it fast enough for many characters?

A lookup through the components takes about a microsecond with thousands of rules and allocates no garbage. Most
lookups (idle polls, sightings) find nothing quickly. Lookup recording costs more and allocates, but only runs in the
editor and development builds.

## Does it work with domain reload off, IL2CPP, code stripping?

Yes. The package clears its static state each time Play Mode starts, uses no reflection at runtime, and needs no
`link.xml`. Subscribe to static events (`DialogEvents.On`, `AnyLineStarted`) in `OnEnable` or `Start`, not in static
constructors, since those subscriptions are cleared too.

## Can I use it without the Unity components?

Yes: `DynamicDialog.Core` has no Unity dependency. See [The engine without Unity](scripting.md#the-engine-without-unity).

## Can rules choose between player responses?

Rules pick what a character says from the situation; they don't present menus. You can build choices on top: show
your own menu, then call `Speak` with a concept for the chosen option, and use memory facts to record what was chosen.

## Where do I report a bug?

Include the package version, the Unity version, the rule file (or the smallest one that shows the problem), and the
Debugger's view of the lookup.
