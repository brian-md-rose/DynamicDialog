# Upgrading from 0.x

1.0 keeps 0.6 content working. Your `.dlg` files, spreadsheets (with two exceptions below), Rule Database assets,
scenes and save games load unchanged. Most code compiles unchanged too: renamed members keep their old names as
`[Obsolete]` shims, which forward to the new ones and give a warning that names the replacement.

## Before you upgrade

Back up your project, or commit it to version control. Then update the package and let Unity recompile.

## Renamed members (old names still work, with a warning)

| 0.6 | 1.0 |
|---|---|
| `DialogConductor.FromFact` | `WellKnownFacts.From` |
| `DialogDirector.MapFact` | `WellKnownFacts.Map` |
| `DialogDirector.SpeakersTalkingFact` | `WellKnownFacts.SpeakersTalking` |
| `DialogArea.AreaFact` | `WellKnownFacts.Area` |
| `DialogVision.ObjectFact`, `ObjectDistanceFact` | `WellKnownFacts.Object`, `WellKnownFacts.ObjectDistance` |
| `NearbySpeakerFacts.NearbySpeakersFact` | `WellKnownFacts.NearbySpeakers` |
| `RegionFacts.RegionFact` | `WellKnownFacts.Region` |
| `DialogSpeaker.AnyLineFinished` | `DialogSpeaker.AnyLineEnded` |
| `DialogSpeaker.onLineFinished` | `DialogSpeaker.onLineEnded` (handlers wired in scenes and prefabs carry over) |
| `DialogConductor.LineFinished(line, completed)` | `DialogConductor.EndLine(line, completed)` |
| `MatchResult.Success` | `MatchResult.Matched` |
| `DialogRuleDatabase.sourcePath` | `DialogRuleDatabase.SourcePath` (saved assets keep their value) |
| `DialogRuleEditorWindow.ShowWindow()` | `DialogRuleEditorWindow.Open()` |

Fix the warnings when convenient: the shims will be removed in a future major version.

## Changes that need code edits

- **`IDialogLineHandler.OnLineFinished` is now `OnLineEnded`.** An interface method can't keep its old name, so
  rename the method in classes that implement `IDialogLineHandler`. The parameters are the same.
- **Some members are now internal**: `FloatMath`, `FactValue.IsSymbolKey`, `SpreadsheetParser.ReadRows` and
  `EscapeCell`, `Rule.MarkFired`, `DialogDirector.ReplaceDatabase`, the database inspector (`DialogRuleDatabaseEditor`),
  `DialogValidatorWindow.Run` and `KnownFacts`, `DialogStringTable.AllCategories` and `Describe`, and the Rule Editor's
  document model (`DlgDocument` and its parts). `DialogEngine.Commit` marks rules as played for you, and hot reload
  replaces databases for you.
- **`QueryPool` was removed.** Keep a `Query` of your own and call `Clear()` on it.
- **Engine-owned state is read-only**: `MatchTrace` (`Query`, `Winner`, `TieCount`, `Entries`) and
  `RuleRuntimeState` (`Disabled`, `TimesFired`, `LastFiredTime`). Use `RuleRuntimeState.Reset()` or
  `RuleSet.ResetState()` to clear rule state.
- **Fields that became properties** (`DialogEngine.Trace`, `DialogEngine.Recorder`,
  `DialogConductor.NamedFollowupsRequireEarshot`) compile from source, but can't be passed as `ref` or `out`.
- **Assemblies compiled against 0.6** (rather than from source) must be recompiled: the fields above became
  properties, and `Rule`'s constructor gained optional `events` and `formerNames` parameters.

## Spreadsheet columns with new meanings

Any unknown spreadsheet column is a criterion on the fact it's named after. Two names are now known columns:

- **`Do`** holds [game events](dlg-reference.md#do). Before, it was a criterion on a fact called `do`.
- **`Renamed`** (also `Renamed From`, `Former Names`) holds [former rule names](save-load.md#renamed-rules).

If a sheet used either as a criterion, rename the column, or move the condition into the `If` column
(`do == value`).

## Behavior changes

- **Expired facts** are no longer removed from memory when a query is built. They are still left out of every query
  and read, and are removed by the director's per-frame check (or `DialogEngine.PurgeExpiredFacts()` if you use the
  engine without a director). `FactMemory.Count` may count them a little longer.
- **Loading a save** upgrades the `DialogSaveData` you pass in place. Pass a copy if you need the original unchanged.
  Saved rule states that match no rule are now kept and logged instead of dropped; see
  [Removed rules](save-load.md#removed-rules).
- **`DialogRuleDatabase.RuleSet`** is compiled fresh in each Play Mode session. With domain reload off, read it again
  rather than caching it across sessions.
- **The IMGUI overlays** moved to **Add Component > Dynamic Dialog > Debug**. Existing scenes are unaffected. For
  shipping, use the [speech bubbles or subtitle bar](presenters.md).
- **Line ends are called "ended" everywhere**, matching `DialogConductor.LineEnded`.

## Save games

0.6 saves load without changes. 1.0 writes save version 2, which has the same fields, so 0.6 can still read it. See
[Saving and loading](save-load.md#versions).

## New in 1.0

Worth a look after upgrading: [game events](scripting.md#game-events) (`do`), [fact listeners](components.md#dialog-fact-listener),
the [speech bubbles and subtitle bar](presenters.md), [`renamed`](save-load.md#renamed-rules), Play Mode without a
domain reload, zero-allocation lookups, and the First Bark, Courtyard Quests and Dialog UI samples. The CHANGELOG
lists everything.
