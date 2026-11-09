# Changelog

## [Unreleased]
### Added
- Rule Editor window (**Window > Dynamic Dialog > Rule Editor**) for `.dlg` files, moved in from the ProjectConverse test project: searchable name pickers backed by a `DialogStringTable` asset, live problem checking, links between rules (followup answers, rules that set or check a fact), drag to reorder, group by speaker, copy and paste as `.dlg` text, undo, and a text preview.
- Editor tests that round-trip `.dlg` files (including every `.dlg` in the project and the samples) through the Rule Editor's document model and check the rules don't change.
- XML doc comments on every public type and member. The `Tests~/DotNet` Core and Unity builds now generate documentation and fail on any undocumented public member.
- `WellKnownFacts` now holds every built-in fact key: `From`, `Map`, `SpeakersTalking`, `Object`, `ObjectDistance`, `Area`, `NearbySpeakers`, `Region`, and `NearPrefix` ("near_"), alongside `Concept`, `Who` and `Random`.
- `DialogRuleDatabase.SourcePath` (read-only).
- Tests for every renamed member's old name.

#### Rules that trigger game code
- **`do` statement** in `.dlg` files: `do GiveItem glasses` raises a named game event with optional text arguments when the rule's line starts (right after its facts are written). `do StartWalking at end` raises it when the line ends instead, and, like followups, not at all if the line is interrupted. Quote an argument that contains spaces, or a literal `at`.
- The same in every other format: a **`Do` column** in spreadsheets (`GiveItem glasses; StartWalking at end`), an **Events** list on inspector rules (`RuleDefinition.events`, `DialogEventDefinition`), and a **Do (game events)** section in the Rule Editor, with event names in a new **Events** category of the Rule Editor String Table and links to other rules that raise the same event.
- Core API: `DialogEvent` (name, `Args`, `Arg`/`FloatArg`/`IntArg`, `Timing`), `DialogEventTiming`, `Rule.Events`, `RuleBuilder.Do`/`DoAtEnd`, `DialogEventSource`, `RuleSource.Events`, and `DialogConductor.EventRaised`.
- Unity API: `DialogEvents.On("GiveItem", (speaker, e) => ...)` returns an `IDisposable`; also `DialogEvents.Off`, `Any`, `Raise`, `HasHandlers` and `Clear`. A handler that throws is logged and doesn't stop the others. Subscriptions are cleared when Play Mode starts.
- **Dialog Event Listener** component: event name, optional "only from" speaker, and UnityEvents with and without the first argument.
- **Fact-change notifications:** `FactMemory.Changed` and `DialogEngine.FactChanged` report every change to world or character memory, from rules or code (`FactChange`: key, whose memory, kind, old and new value). Setting a fact to the value it already has is silent. Expired facts are reported as `FactChangeKind.Expired` by `FactMemory.PurgeExpired`/`DialogEngine.PurgeExpiredFacts`, which the Dialog Director calls every frame (cheap when nothing is due), or by the first read of that fact. `FactMemory.Reset` and `DialogEngine.FactsReset` replace per-fact changes on `Clear`, `Load` and `LoadState` (one reset per load). Also `FactMemory.Owner` and a `TryGet` overload that says whether the value is a symbol.
- **Dialog Fact Listener** component: watches one fact (world, this speaker, or a named character) and raises UnityEvents with the new value, as a number and as text, plus `onSet` and `onRemoved`. It sends the current value when enabled and after a save is loaded.
- The Debugger's Lookups tab shows what the winning rule does: its fact writes, events and followups.
- The Rule Validator reports a rule that only raises events as doing something, and warns about an event name used once that is close to one used elsewhere ("Did you mean 'GiveItem'?").
- Logging lines on the Dialog Director also logs each event.

#### Presentation
- **Dialog Speech Bubbles** and **Dialog Subtitle Bar**: production dialog UI, built with uGUI and TextMeshPro, shipped as restylable
  prefabs (`Runtime/Presentation/Prefabs`) and added with **GameObject > Dynamic Dialog > Speech Bubbles / Subtitle Bar**.
  - Speech bubbles follow their speaker, fade in and out, size themselves to the text, and point a tail at the speaker. Overlapping
    bubbles are moved apart (newer above, or below at the top of the screen), or only the newest is shown (`DialogOverlapMode`).
    A speaker at the screen edge or behind the camera keeps its bubble on the nearest edge, or it is hidden (`DialogOffscreenMode`).
  - The subtitle bar shows a row per line with an optional colored speaker name, stacking up to a limit or replacing.
  - Both have a typewriter reveal (`DialogTypewriter`) that speeds up when needed so a line is fully shown before it ends.
  - **Dialog Bubble Anchor** component sets where a speaker's bubble points (for example, the head bone).
  - These live in their own assemblies (`DynamicDialog.Presentation`, `DynamicDialog.Presentation.Editor`), which only compile when
    uGUI and TextMeshPro are installed (`com.unity.ugui` 2.x on Unity 6, or `com.unity.ugui` plus `com.unity.textmeshpro` before).
    There is no new package dependency.
- **Presenter interface for your own UI:** `IDialogPresenter` (`OnLineStarted`, `OnLineEnded`, `OnLineInterrupted`), registered
  with `DialogPresenters.Register`/`Unregister` (also `All`, `Playing`, `IsRegistered`), or by deriving from `DialogPresenterBehaviour`.
  `DialogPresentedLine` carries the speaker, line, variation, localized text, start time and duration. A presenter that throws is
  logged and doesn't stop the others; one registered mid-line is told about lines already playing.
- Reusable presentation logic: `DialogTypewriter`, `DialogOverlap` (`SelectVisible`, `SeparateVertically`), `DialogScreenPlacement`
  (`Place`, `IsOnScreen`, `EdgePoint`) and `DialogBubblePlacement`.
- `DialogSpeaker.CurrentStartTime` and `CurrentDuration`.
- `Tests~/DotNet/Presentation` compile- and doc-checks the presentation assembly against a Unity project's compiled uGUI and TextMeshPro.

#### Save data that lasts
- **Save versions.** `DialogSaveData.CurrentVersion` is 2. 0.6 wrote version 1, and version 0 means no version was written; both
  load as the 0.6 format. `DialogSaveData.Upgrade()` brings data up to date in place, and loading calls it for you. It also
  repairs half-filled data: missing lists, empty entries, negative counts and invalid times. A save from a newer version loads
  what this version understands and is reported. Loading never throws for any of these.
- **`renamed` statement** in `.dlg` files: `renamed OldName [Older ...]` lists a rule's former names. Saved once and cooldown
  state recorded under a former name is applied to the renamed rule, and saved again under its new name. Also supported as a
  spreadsheet **`Renamed`** column (also `Renamed From` or `Former Names`; names separated by spaces, commas or semicolons), an
  inspector **Former Names** list (`RuleDefinition.formerNames`), and a **Renamed from** field in the Rule Editor. After a
  `once` or cooldown rule is renamed there, the Rule Editor offers **Keep Saves**, which adds the old name.
- The Rule Validator warns when a former name is still another rule's name in the same database (that rule gets the state),
  or when two rules list the same former name. It notes a rule that lists its own name.
- **Unmatched rule state is kept, not lost.** A saved state that matches no rule in a loaded database, because the rule was
  removed or renamed without `renamed`, is not applied. It is kept, written into later saves, and applies again if the rule
  returns or gains a `renamed` line, including after a hot reload. The Dialog Director logs a warning naming each one.
  `DialogDirector.DiscardUnmatchedRuleStates()` drops them on purpose.
- **Load reports:** `DialogLoadReport` (`SavedVersion`, `Upgraded`, `FromNewerVersion`, `RenamedRules`, `UnmatchedRules`,
  `WaitingRules`, `HasWarnings`, and a readable `ToString()`) and `RuleRename`. Get one from `DialogDirector.LastLoadReport`,
  which is updated as region databases load, or from the new
  `DialogSaveExtensions.LoadState(engine, data, out DialogLoadReport report)` overload.
- Core API: `Rule.FormerNames`, `Rule.WasNamed`, a `formerNames` parameter on the `Rule` constructor, `RuleBuilder.Renamed(...)`,
  `RuleSet.FindRule(name, includeFormerNames)`, `RuleSource.FormerNames`, `RuleStateSave.Clone()` and `ToString()`,
  `DialogSaveExtensions.CaptureState(set, rule, now)`, and an `ApplyRuleStates` overload that fills a report. If two saved
  states reach the same rule, one under its name and one under a former name, they are combined. The rule counts as used if
  either says so, the play counts are added, and the most recent play sets the cooldown.
- Tests: loading a 0.6 save (as data and as 0.6 JSON), saves without a version and from a newer version, renamed and removed
  rules, region databases loaded after the save, and `renamed` in `.dlg`, spreadsheets, the validator and the Rule Editor.

#### Compatibility and robustness
- **Play Mode without a domain reload** (Enter Play Mode Options with *Reload Domain* off) now works. When Play Mode starts,
  the package clears the previous session's static state: `DialogDirector.Instance`, `DialogSpeaker.AnyLineStarted` and
  `AnyLineEnded` subscribers, `DialogEvents` handlers, registered presenters, the `DialogTag.All` list and the `near_<who>` key
  cache. Rule databases rebuild their compiled rule set in each new session, so `once`, cooldown and variation-order state
  never carries over from the last time you pressed Play. `DialogDirector.Initialized` is deliberately kept, since editor
  tools subscribe to it once and outlive Play Mode.
- `DialogConductor.ActiveLineCount`: how many lines are playing, without allocating.
- Tests: an Edit Mode test that enters Play Mode twice with domain reload off, Play Mode tests for the reset itself, and
  allocation tests that go through the real components (director built-in facts, Static Facts, Nearby Speaker Facts).
  `AllocationMeasureTests` logs garbage per lookup using the profiler's "GC Allocated In Frame" counter.
- Checked for 1.0:
  - Unity 2021.3.45f2 and 6000.5.5f1, with no optional packages (no errors or warnings, and the optional assemblies are
    skipped), and with all of them (Addressables, Localization, uGUI and TextMeshPro).
  - A Windows player (Mono) with Managed Stripping Level High runs a smoke scene: a conversation with a followup, `do`
    events in code and in a Dialog Event Listener's UnityEvent, both presenters, and save/load. The package uses no
    reflection at runtime and needs no `link.xml`.
  - An Android IL2CPP player with High stripping builds, and the Play Mode reset survives stripping.

#### Samples
- **First Bark** sample: the smallest scene, with one character, one rule file (three rules) and a speech bubble.
- **Courtyard Quests** sample: the Willow Clinic courtyard, a playable scene with five characters, three fetch quests and
  pigeons. It covers talking, seeing, areas, pick-ups, facts from code, memory, game events (`do GiveItem`,
  `do StartWalking at end`), fact-change notifications (quest markers watch world facts instead of rule names),
  characters reacting to each other, and the package's speech bubbles. It uses Kenney CC0 art (Mini Characters,
  Input Prompts, Interface Sounds), with each pack's license, and only the files it uses (4.4 MB of art).
- **Dialog UI** sample: the speech bubbles and subtitle bar side by side, a restyled bubble made as a prefab variant,
  and a custom presenter (a chat log on `DialogPresenterBehaviour`). Number keys switch between them, and Space
  interrupts a line.
- Every sample works in any render pipeline: scenery is built from primitives at Play time with the pipeline's default
  material, and there are no pipeline-specific assets. Samples that take input use the Input System package when it is
  installed (an asmdef version define) and the Input Manager otherwise, with no new dependency.
- Importing a sample into a project without TextMeshPro's Essential Resources now imports them, so the speech bubbles and
  subtitle bar show text straight away (`DialogTmpEssentials`, in the presentation editor assembly).
- Checked: each sample imported through Package Manager into new Unity 6.5 projects (Built-in with the Input Manager,
  and URP with the Input System only), plus First Bark in Unity 2021.3, and played with no console errors.

#### Documentation
- **A manual in `Documentation~/`**: getting started (about ten minutes, ending with First Bark), concepts, the `.dlg`
  language reference (every statement, including `do`, `at end` and `renamed`), spreadsheets and the inspector, the Rule
  Editor, a component reference, scripting, the Debugger and Rule Validator, hot reload, presenters (restyling and
  writing your own `IDialogPresenter`), localization, saving and loading, regions, upgrading from 0.x, and FAQ and
  troubleshooting.
- `DocumentationTests` keeps the manual accurate: every `.dlg` and spreadsheet example in the manual and README must parse
  without errors or warnings, and every C# example must match code in a `DocumentationSnippets.cs` file under `Tests/`,
  which the test assemblies compile (the core examples also run).
- The README is shorter and points into the manual. It credits Elan Ruskin's GDC 2012 talk as the inspiration.

### Changed (samples)
- Robot Banter no longer uses `FindObjectsByType(FindObjectsSortMode)`, which is deprecated in Unity 6.5 and showed two
  compiler warnings on import.

### Changed (compatibility and performance)
- **Less garbage per lookup.** A lookup through the components (the director's `map` and `speakers_talking` facts, and
  Nearby Speaker Facts) allocated on every query: the scene name string, and boxed enumerators. It now allocates nothing.
  In the Unity 6.5 editor that was 200 B per lookup with three speakers. With lookup recording on (the default in the
  editor and development builds), a lookup allocates less than half as much (in .NET, 7.4 KB down to 2.5 KB; in the Unity
  test above, 4.2 KB down to 1.5 KB): the matcher trace is reused, source labels are cached, and the query is copied once
  instead of twice.
- `DialogRuleDatabase.RuleSet` returns a freshly compiled set the first time it is read in each Play Mode session.
  With domain reload on (Unity's default), nothing changes. With it off, code that kept a `RuleSet` from an earlier session
  holds a stale copy. *Migration:* read `database.RuleSet` again rather than caching it across sessions.

### Changed (save data)
- 0.6 saves load unchanged; nothing needs migrating. Saves written now (version 2) have the same fields, so 0.6 can still read them.
- `DialogSaveExtensions.LoadState` upgrades the `DialogSaveData` you pass in place: it sets `version`, replaces missing lists,
  and removes empty entries. The rule states it returns are copies, so the director's cooldown aging no longer changes your
  data. *Migration:* if you need the original data unchanged, pass a copy.
- The Dialog Director no longer drops kept rule states when a region database unloads, if they match none of its rules.
  It also logs a warning on load when saved states match no rule. Because of this, a save can keep entries for rules removed
  long ago. *Migration:* call `DialogDirector.DiscardUnmatchedRuleStates()` once the warning shows they really are gone.
- **Spreadsheets:** a column headed `Renamed`, `Renamed From` or `Former Names` now holds former rule names. Before, it was a
  criterion on a fact of that name. *Migration:* rename such a column, or use an `If` column (`renamed == x`).
- `Rule`'s constructor gained an optional last parameter, `formerNames`. Source code still compiles, but assemblies compiled
  against the old constructor must be recompiled.

### Changed (presentation)
- The IMGUI overlays are labelled as debug tools: they moved to **Add Component > Dynamic Dialog > Debug** (*Subtitle Overlay*,
  *Debug Overlay*), and their docs point to the new prefabs for shipping. Existing scenes are unaffected.

### Changed (API freeze for 1.0)
These renames keep the old name as an `[Obsolete]` member that forwards to the new one, so 0.6 code still compiles, with a warning that names the replacement.

| 0.6 | 1.0 |
|---|---|
| `DialogConductor.FromFact` | `WellKnownFacts.From` |
| `DialogDirector.MapFact` | `WellKnownFacts.Map` |
| `DialogDirector.SpeakersTalkingFact` | `WellKnownFacts.SpeakersTalking` |
| `DialogArea.AreaFact` | `WellKnownFacts.Area` |
| `DialogVision.ObjectFact`, `ObjectDistanceFact` | `WellKnownFacts.Object`, `ObjectDistance` |
| `NearbySpeakerFacts.NearbySpeakersFact` | `WellKnownFacts.NearbySpeakers` |
| `RegionFacts.RegionFact` | `WellKnownFacts.Region` |
| `DialogSpeaker.AnyLineFinished` | `DialogSpeaker.AnyLineEnded` |
| `DialogSpeaker.onLineFinished` | `DialogSpeaker.onLineEnded` (handlers wired in scenes and prefabs carry over) |
| `DialogConductor.LineFinished(line, completed)` | `DialogConductor.EndLine(line, completed)` |
| `MatchResult.Success` | `MatchResult.Matched` |
| `DialogRuleDatabase.sourcePath` | `DialogRuleDatabase.SourcePath` (saved assets keep their value) |
| `DialogRuleEditorWindow.ShowWindow()` | `DialogRuleEditorWindow.Open()` |

Other changes:
- Line ends are called "ended" everywhere, matching `DialogConductor.LineEnded`.
- `DialogEngine.Trace`, `DialogEngine.Recorder` and `DialogConductor.NamedFollowupsRequireEarshot` are properties instead of fields.
- `MatchTrace.Query`, `Winner` and `TieCount` are read-only, and `Entries` is an `IReadOnlyList<TraceEntry>`. The matcher fills them in.
- `RuleRuntimeState.Disabled`, `TimesFired` and `LastFiredTime` are read-only. Use `Reset()` or `RuleSet.ResetState()` to clear them.
- `DialogValidatorWindow.Open()` and `DialogRuleEditorWindow.Open(path)` return the window, like `DialogDebuggerWindow.Open()`.
- The Rule Validator knows the `region` fact, so rules that test it are no longer reported as using an unknown fact.

### Removed
- `QueryPool`. Nothing used it. If you did, keep a `Query` of your own and call `Clear()` on it.

### Breaking changes and migration
- **`IDialogLineHandler.OnLineFinished` is now `OnLineEnded`.** An interface method can't keep its old name, so classes that implement `IDialogLineHandler` must rename that method. The parameters are the same.
- **These are now internal:** `FloatMath`, `FactValue.IsSymbolKey`, `SpreadsheetParser.ReadRows` and `EscapeCell`, `Rule.MarkFired`, `DialogDirector.ReplaceDatabase`, the database custom inspector (`DialogRuleDatabaseEditor`), `DialogValidatorWindow.Run` and `KnownFacts`, `DialogStringTable.AllCategories` and `Describe`, and the Rule Editor's document model (`DlgDocument`, `DlgRule`, `DlgCriterion`, `DlgLine`, `DlgWrite`, `DlgFollowup`). They were implementation details with no supported replacement. `Rule.MarkFired` is done for you by `DialogEngine.Commit`, and `ReplaceDatabase` by hot reload.
- **Fields that became properties** still compile from source. Assemblies built against 0.6 need recompiling, and those members can't be passed as `ref` or `out` any more.
- **Setting `MatchTrace` or `RuleRuntimeState` values** from game code no longer compiles. Only the engine writes them.
- **A spreadsheet column headed `Do`** now holds game events. Before, it was a criterion on a fact called `do`. Rename such a column (for example to `If`, as `do == value`) to keep the old meaning.
- **Building a query no longer purges expired facts** from memory. They are still left out of every query and read, and are removed by the director's per-frame check (or `DialogEngine.PurgeExpiredFacts()` if you use the engine without a director). `FactMemory.Count`, which already counted expired entries not yet purged, may count them a little longer.
- **`Rule`'s constructor has a new last optional parameter, `events`.** Source code that calls it still compiles; assemblies built against 0.6 need recompiling.

## [0.1.0] - 2026-11-05
### Added
- Core engine: symbols, queries, interval criteria, rules, partitioned rule sets, best-match matcher with weighted tie-breaking and tracing.
- Fact memory with expiry and save/load; rule write-back; once and cooldown rules.
- `DialogEngine`: query building in precedence order, `Ask`, `AskBest` ("then any"), `AskEach` ("then all"), `Commit`.
- NUnit tests, plus a .NET harness in `Tests~/DotNet`.

## [0.2.0] - 2026-11-05
### Added
- `DialogConductor` (core): starts lines, schedules followups when a line completes, resolves `then self/character/any/all`, handles interrupts (stopped lines drop their followups), and adds a `from` fact to followups.
- `FactValue` and `CriterionOp`: parse authored text into fact values and criteria.
- Unity layer: `DialogDirector`, `DialogSpeaker` (audio, animator triggers, subtitles, idle chatter), `DialogVision` and `DialogTag`, `DialogRegion`, `DialogArea`, `StaticFacts`, `NearbySpeakerFacts`, `DialogSubtitleOverlay`.
- `DialogRuleDatabase` asset with inspector-editable rules, response variations (random, no-repeat, shuffle, sequential), validation, and hot reload during Play Mode.
- Conductor tests, and Play Mode tests for the components.

## [0.3.0] - 2026-11-05
### Added
- `.dlg` text format: `DialogScriptParser` (core) and `DialogScriptImporter`. Supports `@who`, `@concept` and `@partition` defaults, and reports errors with line numbers.
- Spreadsheet format (`.dlgcsv`, `.dlgtsv`): `SpreadsheetParser` and `DialogSpreadsheetImporter`. One row per line; any unknown column is a criterion named after its header.
- Audio clip lookup by name or path during import.
- Hot reload: saving a rule file during Play Mode swaps the rules into the running director (`DialogDirector.ReplaceDatabase`).
- `RuleAnalyzer` and the **Rule Validator** window: unanswered followups, contradictory conditions, fact-name typos, duplicates, rules that do nothing.
- Database inspector: Check Rules, Export .dlg, Export Recording Script.
- Assets > Create templates for `.dlg` and `.dlgcsv`.
### Changed
- `ResponseSelection` moved to the core assembly (same namespace).
- `RuleDefinition.ToRule()` now builds through `RuleSource`, shared with the importers.

## [0.4.0] - 2026-11-05
### Added
- `DialogRecorder` and `LookupRecord`: every lookup with a copy of its query, where each fact came from, the rules tested, the winner, and whether it played.
- `RuleDiagnostics`: `Explain` says why a rule did or didn't play (failed condition with the value seen, outscored, lost a tie, once/cooldown, not loaded). `EvaluateAll` ranks every rule for a query.
- **Dialog Debugger** window: Lookups (with "why didn't this rule play?"), Coverage, and Test Query (works without Play Mode).
- `DialogDebugOverlay`: in-game labels over each speaker.
- Director setting **Record Lookups** (editor and development builds).
- Symbol-typed values: queries, criteria, fact writes and memory remember when a value is a name, so tools show `map = swamp` instead of an ID.
### Fixed
- Saved memory stored symbol values as IDs, which change between runs. They are now saved by name (`FactMemoryEntry.SymbolValue`).

## [0.5.0] - 2026-11-05
### Added
- `DialogAddressableRegion` (optional `DynamicDialog.Addressables` assembly, compiled only when `com.unity.addressables` is installed): streams a region's rule database in on enable and releases it on disable, including when disabled mid-load.
- `RegionFacts`: world facts that exist only while a region is active, including a `region` fact with the region's name. Used by both region components.
- Benchmark data shared by a .NET benchmark runner (`Tests~/DotNet/Bench`) and Play Mode performance tests (zero-allocation checks for `FindBest` and `Ask`).
### Changed
- The matcher loads each query into a direct-indexed fact table before testing rules: 1.33 µs down to 0.84 µs per lookup at 10,000 rules.

## [0.6.0] - 2026-11-05
### Added
- Line keys (`say ... key X`, a `Key` spreadsheet column, `DialogLineDefinition.key`) and `LineKeys` (key, else clip name, else `<rule>_<n>`).
- `IDialogLocalizer` and `DialogDirector.Localizer`: speakers play translated subtitles and clips, falling back to the authored ones. `DialogSpeaker.CurrentKey`, `CurrentText` and `CurrentClip`.
- `DialogUnityLocalizer` (optional `DynamicDialog.Localization` assembly, compiled only when `com.unity.localization` is installed).
- Export String Table CSV for translators.
- Save/load: `DialogSaveData`, `DialogEngine.SaveState/LoadState`, and `DialogDirector.SaveStateJson/LoadStateJson`. State for unloaded region databases is kept, aged, saved, and reapplied when they load.
- The Robot Banter sample.
### Changed
- The subtitle overlay shows localized text.
