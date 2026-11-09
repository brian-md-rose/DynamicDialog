# Asset Store listing draft: Dynamic Dialog 1.0.0

**Status: draft for review. Nothing here has been submitted or published.** Pricing is deliberately left out.
This folder ends in `~`, so Unity ignores it; leave it out of the package you upload (see *Before submitting* below).

Field names follow the Publisher Portal's package form. Character counts are approximate; check the portal's
current limits when pasting.

---

## Package title

Dynamic Dialog

*Alternative, if the plain name is too generic in search:* Dynamic Dialog: Contextual Barks and Conversations

## Category

Tools / Behavior AI. *Alternative:* Tools / Utilities, or Templates / Systems if offered. Pick what similar dialog
tools on the store use.

## Short description (summary, about 200 characters)

Characters say the right line at the right moment. Write barks and conversations as rules over world facts; the most
specific match wins. Hot reload, a "why didn't it play?" debugger, and speech bubbles included.

## Long description

**Dialog that reacts to the game, not a script.**

Most dialog tools are trees: the player picks an option, the tree moves on. Dynamic Dialog is for everything else:
the barks, comments, running gags and short exchanges that make characters feel aware of the world. When something
happens, a character asks "what should I say?". Dynamic Dialog gathers every fact about the moment (the map, the
speaker's health, what they can see, what they said before, who's nearby) and plays the line from the most specific
rule that matches.

Write a general line for "sees an enemy", a better one for "sees an enemy while hurt", and a better one still for
"sees the same enemy twice in the swamp". You never write the branching: the most specific rule wins on its own.
Rules can remember things, hand off to another character to start a conversation, and tell your code to do
something, so writers can add content without a programmer.

The approach follows Elan Ruskin's GDC 2012 talk on rule databases for contextual dialog. Dynamic Dialog is an
independent implementation built for Unity.

**Write rules the way you like**
- Plain-text `.dlg` files, readable and easy to diff and merge
- Spreadsheets (CSV/TSV) for writers who live in Excel or Google Sheets
- The inspector, or the Rule Editor window with searchable name pickers, live problem checking and links between rules
- Everything hot-reloads: save while playing and hear the change straight away

**Find out why a line didn't play**
- The Debugger records every lookup: the facts, the rules tested, and the winner
- Pick any rule and get a one-line answer: which condition failed and the value it saw, outscored, lost a tie, on cooldown
- Coverage shows which lines nobody has heard yet; Test Query tries a situation without pressing Play
- The Rule Validator catches typos in fact names, contradictions, unanswered followups and duplicate rules before you play

**Ready for a real game**
- Speech bubbles and a subtitle bar (uGUI and TextMeshPro) as restylable prefabs, or your own UI through a small presenter interface
- Game events from rules (`do GiveItem glasses`) and fact-change notifications, so game code never depends on rule names
- Versioned save data that survives renamed rules
- Localization, through Unity Localization or your own
- Region streaming, optionally through Addressables
- Zero garbage per lookup, and about 1 µs to pick a line from 10,000 rules
- Works with Enter Play Mode Options (no domain reload), IL2CPP code stripping and any render pipeline

**Four samples** show it working: the one-character *First Bark*, *Robot Banter*, the playable *Courtyard Quests*
(five characters, three fetch quests and pigeons), and *Dialog UI*. A full manual covers everything from a ten-minute
getting-started guide to the `.dlg` language reference.

## Key features (bullet list field)

- Rule-based contextual dialog: the most specific matching rule wins
- Write rules as `.dlg` text, spreadsheets (CSV/TSV), in the inspector, or in the Rule Editor window
- Hot reload of rules during Play Mode
- Memory per character and for the world, with expiring facts
- Once-only rules, cooldowns, weighted ties and line variations (random, shuffle, no-repeat, sequential)
- Conversations: hand a concept to a named character, yourself, or whoever is in earshot
- Interruptions: a new line cuts off the old one, and its followups are dropped
- Game events from rules, and notifications when facts change
- Built-in facts from components: vision, areas, nearby speakers, regions, static facts
- Debugger: every lookup recorded, "why didn't this rule play?", rule coverage, test queries
- Rule Validator for typos, contradictions and unanswered followups
- Speech bubbles and subtitle bar prefabs (uGUI + TextMeshPro) with a typewriter reveal; bring your own UI via `IDialogPresenter`
- Localization support (Unity Localization or custom), with a string table export for translators
- Recording script export for voice sessions
- Versioned save/load that keeps once and cooldown state across rule renames
- Region streaming, with optional Addressables support
- Zero allocations per lookup; core engine has no Unity dependency
- Unity 2021.3 LTS through Unity 6; any render pipeline; Input System or Input Manager in samples
- Full manual, XML-documented API, and four samples

## Technical details

- **Unity versions:** 2021.3 LTS through Unity 6 (6000.x). Tested on 2021.3.45f2 and 6000.5.5f1.
- **Render pipelines:** any. The package draws nothing of its own beyond uGUI, and the samples build their scenery at
  Play time with the active pipeline's default material. Samples checked in Built-in and URP; HDRP not yet checked.
- **Dependencies:** built-in modules only. Optional: uGUI and TextMeshPro (speech bubbles, subtitle bar), Addressables
  (region streaming), Localization, Input System (samples). Each optional feature compiles only when its package is
  installed.
- **Platforms:** all platforms Unity supports; no native code and no runtime reflection, so no `link.xml` is needed.
  Checked: Windows (Mono, High stripping) and Android (IL2CPP build).
- **Performance:** about 0.8 µs and 0 B per lookup with 10,000 rules (.NET 9, desktop PC); 0 B per lookup in the
  Unity 6 editor.
- **Source:** full C# source included.
- **Documentation:** manual included in the package (`Documentation~`), plus the README and changelog.
- **Third-party content:** Kenney art and sounds (CC0) in the Courtyard Quests sample. See `Third Party Notices.md`.

## Screenshots and media to capture

Aim for 1920×1080 or larger, Game view at 16:9, with the Unity UI dark theme for editor shots. Check the portal's
current sizes for the key images. Avoid showing other brands' controller glyphs (use keyboard prompts) and other
products' names or logos.

| # | Shot | Setup | Caption idea |
|---|---|---|---|
| 1 | **Hero: Courtyard Quests in play** | Courtyard Quests, two or three characters mid-conversation with speech bubbles, player near the fountain. Debug overlays off. | "Characters that notice the world and talk about it." |
| 2 | **A `.dlg` file beside the game** | Code editor with `CourtyardQuests.dlg` on the left, Game view on the right showing the line from the highlighted rule. | "Write rules in plain text. The most specific match wins." |
| 3 | **Debugger: why didn't it play?** | Debugger window, Lookups tab, a red or yellow lookup selected, *Why didn't this rule play?* answer visible ("condition failed: ..."). | "Know exactly why a line did or didn't play." |
| 4 | **Rule Editor** | Rule Editor window on `CourtyardQuests.dlg`: a rule selected, name pickers open, links to related rules visible. | "Or edit rules in a window, with checks as you type." |
| 5 | **Hot reload** | Two frames or a short clip: edit a line in `FirstBark.dlg`, save, Pip says the new line. Good as the video. | "Save while playing. Hear it straight away." |
| 6 | **Dialog UI: three presenters** | Dialog UI sample, key **4** (bubbles, subtitle bar and chat log at once). | "Speech bubbles, subtitles, or your own UI." |
| 7 | **Spreadsheet authoring** | A `.dlgcsv` open in a spreadsheet app next to the imported database inspector. | "Writers can work in spreadsheets." |
| 8 | **Rule Validator** | Validator window listing a typo warning ("Did you mean ...?") and an unanswered followup. | "Catch mistakes before you press Play." |
| 9 | **Robot Banter with overlays** | Robot Banter, Debug Overlay on, robots mid-banter with green/red lookup labels. | "See every lookup in the Game view." |
| 10 | **Game events** | Inspector of a Dialog Event Listener wired to a UnityEvent, plus the `do GiveItem glasses` line in the `.dlg`. | "Rules tell your code what to do." |

**Key images:** Icon 160×160, Card 420×280, Cover 1950×1300, Social 1200×630 (confirm in the portal). A speech-bubble
mark plus the name, over a blurred Courtyard Quests frame, works for all four.

**Video (optional, 60–90 s):** First Bark in 15 s (open, Play, edit, save, new line) → Courtyard Quests montage →
Debugger answering "why didn't it play?" → Dialog UI presenters.

## Before submitting (checklist, not done)

- [ ] Decide the price (not in this draft).
- [ ] Make the documentation and changelog reachable by buyers: `package.json` points at the private GitHub repository,
      which buyers can't open. Publish the manual (for example GitHub Pages) or point the URLs at a public page.
- [ ] Set up a support contact (email, forum thread or Discord) for the README's *Reporting bugs* section and the
      listing's support field.
- [ ] Decide whether to name the GDC talk in the listing. It is credited as inspiration, not endorsement; keep it that way.
- [ ] Leave out of the upload: `.github/`, `CI~/`, `Docs~/`, and `Tests~/DotNet/**/bin` and `obj`.
- [ ] Capture the screenshots above in a clean Unity 6 project with the package installed and the samples imported.
- [ ] Run the Asset Store Publishing Tools validator on the export.
