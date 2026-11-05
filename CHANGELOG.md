# Changelog

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
