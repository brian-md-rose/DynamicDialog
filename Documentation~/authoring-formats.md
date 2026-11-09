# Spreadsheets and the inspector

There are three ways to write rules, and they all make the same **Dialog Rule Database** asset, so the rest of the
package doesn't care which you use. Mix them freely: a director can load a `.dlg` file, a spreadsheet and an
inspector-made database at once.

| Format | Best for |
|---|---|
| [`.dlg` text](dlg-reference.md) | Writers who like a text editor, version control and diffs. Also edited by the [Rule Editor](rule-editor.md). |
| Spreadsheet (`.dlgcsv`, `.dlgtsv`) | Large amounts of dialog with many variations, voice recording sessions, writers who already work in a spreadsheet. |
| Rule Database in the inspector | A few rules, or rules a designer tweaks in Unity. |

Saving a `.dlg` or spreadsheet file during Play Mode swaps the new rules into the game ([hot reload](hot-reload.md)).
Inspector edits during Play Mode take effect immediately too.

## Spreadsheets

Create one with **Assets > Create > Dynamic Dialog > Dialog Spreadsheet (.dlgcsv)**, or save a sheet from Excel,
Google Sheets or LibreOffice as CSV (comma-separated) or TSV (tab-separated). Give the file the extension `.dlgcsv`
or `.dlgtsv` so Unity imports it as rules. The separator is detected from the header row.

**One row per line.** The first row is the header. Each row after it is one line a character can say. A row with
an empty **Rule** cell adds another variation to the rule above it, and so does a row that repeats a rule's name.

```csv
Rule,Who,Concept,If,Text,Audio,Remember,Then,Do,Once,Notes
NickSwampIntro,Nick,TalkIdle,map == swamp; said_intro missing,"Man, this swamp.",nick_swamp_01,said_intro = 1,Rochelle SwampReply after 1s,,yes,The opening line
,,,,Cesspool of disease.,nick_swamp_02,,,,,
RochelleSwampReply,Rochelle,SwampReply,,Could be worse.,,,,,,
NurseGetsGlasses,Nurse,OnTalk,player_has == glasses,My glasses!,,world glasses_returned = 1,,GiveItem glasses; StartWalking at end,,
```

### Columns

Header names ignore case, spaces and underscores (`Rule Name` and `rule_name` are the same). Rule cells (everything
except the line columns) only count on a rule's first row.

| Column (other accepted names) | Holds | Like `.dlg` |
|---|---|---|
| **Rule** (`Name`, `ID`, `Rule Name`) | The rule's name. Required. | `rule` |
| **Who** (`Speaker`, `Character`) | The speaker | `who` |
| **Concept** (`Event`) | The concept | `concept` |
| **If** (`Criteria`, `Conditions`, `Condition`) | Criteria in `.dlg` syntax, separated by `;`, `,` or `and` | `if` |
| **Text** (`Line`, `Say`, `Dialog`, `Dialogue`, `Subtitle`) | The line's text, without quotes | `say "…"` |
| **Audio** (`File`, `Clip`, `VO`, `Audio File`) | The line's clip | `say … audio` |
| **Duration** | The line's duration | `say … duration` |
| **Anim** (`Animation`) | Animator trigger | `say … anim` |
| **Key** (`Line Key`, `String Key`, `Loc`, `Loc Key`) | Localization key | `say … key` |
| **Remember** (`Sets`, `Write`, `Writes`) | Fact writes separated by `;`, e.g. `said_x = 1; world gossip += 1 for 10s; forget mood` | `remember`, `forget` |
| **Then** (`Followup`, `Followups`, `Next`) | Followups separated by `;`, e.g. `Rochelle Reply after 1s; any Cheer` | `then` |
| **Do** | Game events separated by `;`, e.g. `GiveItem glasses; StartWalking at end` | `do` |
| **Renamed** (`Renamed From`, `Former Names`) | Former rule names, separated by spaces, commas or semicolons | `renamed` |
| **Once** | `x`, `y`, `yes`, `true`, `1` or `once` to make it a once rule | `once` |
| **Cooldown** | Seconds | `cooldown` |
| **Weight** | A number | `weight` |
| **Bonus** (`Score`, `Score Bonus`) | A whole number | `bonus` |
| **Select** (`Selection`) | `random`, `norepeat`, `shuffle` or `sequential` | `select` |

Columns named **Notes**, **Note**, **Comment**, **Comments**, **Description**, **Status** or **Actor**, and columns whose
header starts with `#`, are ignored. Use them for anything the game doesn't need.

### Any other column is a criterion

A column with any other header is a criterion on the fact it is named after. A cell of `swamp` under a `Map` column
means `map == swamp`. A cell can also hold `< 30`, `>= 3`, `!= nick`, `30..60`, `exists` or `missing`. An empty cell
adds no criterion.

```csv
Rule,Concept,Map,Health,Text
SwampHurt,OnHurt,swamp,< 30,I'm bleeding into the swamp water!
AnyHurt,OnHurt,,,Ow!
```

> **Careful:** because unknown columns are criteria, a misspelled column header (`Txet`) becomes a criterion that
> never passes. The [Rule Validator](debugging.md#rule-validator) points these out: a fact that nothing else uses and
> looks like another is a warning ("Did you mean…?"), and a fact that no rule writes is listed under its *Info* messages.

## Rule Database assets (the inspector)

Create one with **Assets > Create > Dynamic Dialog > Rule Database**. Select it to edit its rules in the inspector.
Each rule has the same parts as a `.dlg` rule:

| Field | Like `.dlg` |
|---|---|
| **Name** | `rule` |
| **Who**, **Concept** | `who`, `concept` |
| **Criteria**: key, operator, value (and an upper bound for *Between*), optional default when missing | `if` |
| **Response**: selection mode and lines (text, clip, duration, animator trigger, key) | `select`, `say` |
| **Remember**: scope, character, key, operation (*Set*, *Add*, *Remove*), value, duration | `remember`, `forget` |
| **Then**: target, character, concept, delay | `then` |
| **Events**: name, arguments, timing (*Line Started* or *Line Ended*) | `do` |
| **Once**, **Cooldown**, **Weight**, **Score Bonus** | `once`, `cooldown`, `weight`, `bonus` |
| **Former Names** | `renamed` |
| **Partition Keys** (on the database) | `@partition` |

The database inspector also has buttons for every format:

- **Check Rules**: the same checks as the [Rule Validator](debugging.md#rule-validator), for this database.
- **Export .dlg…**: writes the rules as a `.dlg` file, for moving inspector rules into text.
- **Export Recording Script…**: a CSV of every line sorted by character (Character, Rule, Concept, Take, Text,
  Audio File), for voice recording sessions.
- **Export String Table CSV…**: every line's key and text, for translators; see [Localization](localization.md).
- **Open Source File** (imported databases only): opens the `.dlg` or spreadsheet the asset came from.

Databases imported from a `.dlg` or spreadsheet are read-only in the inspector: edit the file instead.
