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
