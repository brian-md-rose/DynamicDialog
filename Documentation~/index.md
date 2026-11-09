# Dynamic Dialog manual

Dynamic Dialog picks what your characters say from a database of rules. When something happens, a character asks
"what should I say about this?". The package collects every fact it knows about the moment (the world, the
speaker, the event), finds every rule whose conditions all hold, and plays the most specific one. Rules can write
facts back into memory, start conversations by handing a concept to another character, and raise game events
for your code.

You write the rules in plain text (`.dlg` files), in a spreadsheet, in the inspector or in the Rule Editor window.
While the game runs, the Debugger shows every lookup and tells you why a rule did or didn't play.

## Contents

**Start here**

- [Getting started](getting-started.md): install the package, make a talking character and write your first rules, in about ten minutes.
- [Concepts](concepts.md): facts, queries, memory and scopes, scoring, followups, once and cooldown.

**Writing rules**

- [The `.dlg` language](dlg-reference.md): every statement and directive, with examples.
- [Spreadsheets and the inspector](authoring-formats.md): the other two ways to write the same rules.
- [Rule Editor](rule-editor.md): edit `.dlg` files with drop-downs instead of typing the syntax.

**Reference**

- [Components](components.md): every component in the package and its settings.
- [Scripting](scripting.md): triggering lines, game events, fact changes, your own facts, and the engine without Unity.

**Tools**

- [Debugger and Rule Validator](debugging.md): lookups, "why didn't this rule play?", coverage, test queries and rule checks.
- [Hot reload](hot-reload.md): edit rules while the game is playing.

**Topics**

- [Showing lines and custom presenters](presenters.md): the speech bubbles and subtitle bar, restyling them, and writing your own `IDialogPresenter`.
- [Localization](localization.md): line keys, Unity Localization, and your own localizer.
- [Saving and loading](save-load.md): save data, versions, and renaming rules without breaking saves.
- [Regions](regions.md): loading and streaming rules with your levels.

**Help**

- [Upgrading from 0.x](upgrading.md): renamed APIs and what changed.
- [FAQ and troubleshooting](faq.md).

## Samples

The package has four samples. Import them from Package Manager (select Dynamic Dialog, then the **Samples** tab).

| Sample | What it shows |
|---|---|
| **First Bark** | The smallest scene: one character, one rule file and a speech bubble. [Getting started](getting-started.md) builds it from scratch. |
| **Robot Banter** | Two robots talking about what they see: specific rules beating general ones, memory, conversations, replies from whoever is in earshot, idle chatter and interruptions. |
| **Courtyard Quests** | A playable courtyard with five characters and three fetch quests: vision, areas, facts from code, game events (`do`), fact listeners and speech bubbles. |
| **Dialog UI** | The speech bubbles and subtitle bar side by side, a restyled bubble, and a custom presenter. |

## Requirements

- Unity 2021.3 LTS through Unity 6 (6000.x), any render pipeline.
- The speech bubbles and subtitle bar need uGUI and TextMeshPro, which Unity's templates include. Addressables and
  Unity Localization are optional. Without any of them, the rest of the package works. See [Components](components.md#optional-packages).

## Credit

Dynamic Dialog is inspired by Elan Ruskin's GDC 2012 talk *"Rule Databases for Contextual Dialog and Game Logic"*,
which describes the system Valve used for character dialog in *Left 4 Dead* and later games. The talk is well worth
watching. This package is an independent implementation; it uses no material from the talk.
