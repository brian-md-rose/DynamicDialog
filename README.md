# Dynamic Dialog (Unity package)

Contextual dialog driven by a rule database, following Elan Ruskin's GDC 2012 talk
*"Rule Databases for Contextual Dialog and Game Logic"*.

1. Every lookup builds a flat **query** of facts: world memory, world providers, speaker memory,
   speaker providers, event facts, then `concept`, `who` and `rand`. Later sources override earlier ones.
2. A **rule** is a list of criteria that must all pass. A criterion that names a missing fact fails,
   unless it is built with `WhenMissing`. The rule with the most criteria wins, and ties are broken at random.
3. A winning rule can **write facts** back into speaker, world or character memory. Facts can expire.
4. A winning rule can **follow up** by sending a concept to a named character, to itself, or to
   anyone or everyone in earshot. The followup is evaluated when the line ends.

## Status

| Milestone | State |
|---|---|
| M0 package skeleton | done |
| M1 core matching (`Runtime/Core`, no UnityEngine dependency) | done |
| M2 memory, write-back, once/cooldown | done |
| M3 Unity runtime (director, speakers, audio, followups, earshot, vision, regions) | done |
| M4 authoring (`.dlg` and spreadsheet importers, hot reload, validator, exports) | done |
| M5 debugging (lookup recorder, Debugger window, why-not, coverage, test query, overlay) | done |
| M6 region streaming and benchmarks | done |
| M7 localization, save/load, sample | done |

## Install

In Unity 2021.3 or later, open Package Manager, choose **Add package from disk…**, and select this folder's `package.json`.

## Scene setup

1. Write rules in one of three ways (see **Writing rules** below): a `.dlg` text file, a spreadsheet saved as `.dlgcsv`,
   or a **Rule Database** asset edited in the inspector. All three produce the same `DialogRuleDatabase`.
2. Add a **Dialog Director** to the scene and drag the database into its list.
3. Add a **Dialog Speaker** to each character. `who` defaults to the GameObject name. Give it an AudioSource for voiced lines.
4. Optional components:

| Component | Goes on | What it does |
|---|---|---|
| Dialog Vision | speaker | Every few seconds, sends `OnSee` with `object` = the nearest visible **Dialog Tag** |
| Dialog Tag | any object | Makes the object something speakers can remark on |
| Static Facts | speaker or director | Fixed facts set in the inspector (`faction = penguin`) |
| Nearby Speaker Facts | speaker | Adds `nearby_speakers` and `near_<who>` |
| Dialog Area | trigger volume | Sets `area` in the memory of speakers inside it |
| Dialog Region | region scene | Loads a database, and region-only world facts (`region` = name, plus any others), while enabled |
| Dialog Addressable Region | region scene | Same, but streams the database through Addressables (only when the Addressables package is installed) |
| Subtitle Overlay (Debug) | anywhere | IMGUI subtitles, optionally with rule names |
| Debug Overlay | anywhere | Labels over each speaker: current line and latest lookup (green played, yellow matched but not played, red no match) |

Your own facts: implement `IFactProvider` on a component next to a speaker (character facts) or the director (world facts).
React to lines: implement `IDialogLineHandler`, use a speaker's `onSubtitle` event, or the static `DialogSpeaker.AnyLineStarted`.

Code can trigger lines with `speaker.Speak("OnHurt", q => q.Set("damage", 30))`. Pass `interrupt: true` to cut off the current line,
which also drops its followups. Rule edits made in the inspector during Play Mode take effect immediately.

Built-in facts: `concept`, `who`, `rand` (0-100), `map` (scene name), `speakers_talking`, `from` (who sent a followup),
`object` and `object_distance` (vision), `area` (areas).

## Writing rules

The talk's main advice is to let writers work in the tool they like. All of these formats import into the same asset,
and saving a file during Play Mode swaps the new rules in without restarting.

**`.dlg` text files** (**Assets > Create > Dynamic Dialog > Dialog Script**):

```
@who Nick                              # default speaker for the rules below

rule NickSwampIntro
  concept TalkIdle
  if map == swamp and survivors >= 3
  if said_intro missing                 # 0 or not set yet
  if health in 30..60
  say "Man, this swamp." audio nick_swamp_01 duration 3s
  say "Cesspool of disease, is what this is."
  remember said_intro = 1
  remember speaking = 1 for 4s
  then Rochelle SwampReply after 1s     # or: then self|any|all Concept
  once
```

The full grammar, including `select`, `forget`, `cooldown`, `weight`, `bonus`, `default`, `@concept` and `@partition`,
is documented at the top of `Runtime/Core/Authoring/DialogScriptParser.cs`. Errors show in the Console with file and line,
and the rest of the file still imports.

**Spreadsheets** (**Assets > Create > Dynamic Dialog > Dialog Spreadsheet**): save as CSV or TSV and name the file
`.dlgcsv` or `.dlgtsv`. There is one row per recorded line; a row with an empty `Rule` cell adds a variation to the rule above.
Known columns are `Rule, Who, Concept, If, Text, Audio, Duration, Anim, Remember, Then, Once, Cooldown, Weight, Bonus, Select`.
`Notes`, `Comment`, `Actor`, `Status` and columns starting with `#` are ignored. **Any other column is a criterion**
named after its header: a `Map` column holding `swamp` means `Map == swamp`, and cells can also hold `< 30`, `>= 3`,
`!= nick`, `30..60`, `exists` or `missing`.

**Audio** is looked up by name or path: first in the importer's audio folder (by default, the file's own folder),
then anywhere in the project. Missing clips are a warning, and the line still shows its text.

**Checking rules:** **Window > Dynamic Dialog > Rule Validator** checks every database together. It finds followups no
rule answers, conditions that contradict each other, likely typos in fact names, duplicate names, and rules that do nothing.
List the facts your own code provides in its *Facts from code* field. Each database's inspector also has **Check Rules**,
**Export .dlg** (to move inspector-made rules into text) and **Export Recording Script** (a CSV for voice sessions).

## Localization

Each line has a **key**: its `key` (`say "Hi" key GREET_1` in .dlg, a `Key` column in spreadsheets, or the inspector),
else its audio clip's name, else `<rule>_<n>`. A localizer on the Dialog Director turns keys into translated subtitles and clips:

- With the Unity Localization package installed, add **Unity Localization Localizer** to the director and name your string
  table collection (and optionally an asset table for voice clips). Lines with no entry fall back to the authored text and clip.
- Or implement `IDialogLocalizer` yourself and set `DialogDirector.Localizer`.

The rule database inspector's **Export String Table CSV** writes every line's key and source text, plus who says it, for translators.

## Saving

`DialogDirector.SaveStateJson()` and `LoadStateJson(json)` save and restore world memory, every character's memory,
and which rules have played (so `once` rules stay used and cooldowns carry over). Names are saved as text, so a save
works across sessions. Region databases that aren't loaded keep their state too, and it is reapplied when they load.
Lines in progress and pending followups aren't saved. `SaveState()` returns a `DialogSaveData` if you'd rather use your own serializer.

## Sample

In Package Manager, select Dynamic Dialog and import **Robot Banter**. Open `RobotBanter.unity` and press Play:
two robots wander a room and talk about what they see, remember, reply to each other, and get interrupted. Every line
is in `RobotBanter.dlg`; edit it while playing.

## Debugging

**Window > Dynamic Dialog > Debugger** has three tabs:

- **Lookups** (Play Mode): every query, newest first. Select one to see each fact with where it came from (world memory,
  a speaker's memory, a fact provider, the event, or the engine), every rule the matcher tested with the condition that
  failed and the value it saw, and the winner. **Why didn't this rule play?** explains any loaded rule against that query:
  a condition failed, it was outscored by a more specific rule, it lost a tie, it's on cooldown or already played once,
  or its rule set wasn't loaded. **Copy into Test Query** carries the facts over so you can tweak them.
- **Coverage** (Play Mode): how often each loaded rule has played, and which never have.
- **Test Query** (any time): type `who`, `concept` and facts (`map = swamp`, one per line) and see every rule ranked
  with its verdict. It compiles rules fresh from the project, so it needs no running game.

Recording is controlled by **Record Lookups** on the Dialog Director. It is on by default, and it only runs in the
editor and development builds. From code, use `DialogDirector.Recorder` and `RuleDiagnostics.Explain`.

## Performance

Measured on a desktop PC (.NET 9, Release) with 10,000 rules in 200 buckets of 50 and about 8 criteria per rule,
the scale the talk describes:

| Operation | Time | Garbage |
|---|---|---|
| `RuleMatcher.FindBest` | 0.84 µs | 0 B |
| `DialogEngine.Ask` (build a ~40-fact query from memory and providers, then match) | 0.71 µs | 0 B |
| The same with lookup recording on | 2.1 µs | about 7 KB |
| One 10,000-rule bucket (no partitioning, for comparison) | 45 µs | 0 B |
| `RuleMatcher.FindBest` inside the Unity 6.5 editor (Mono, editor code optimization) | 4.0 µs | 0 B |

The matcher loads each query into a table indexed by symbol, so each condition costs one array read.
The Play Mode tests check for zero garbage under Unity's own runtime and log the time per lookup.
To rerun the numbers:

```bash
dotnet run -c Release --project "Tests~/DotNet/Bench"
```

## Core API

```csharp
var engine = new DialogEngine(clock);
engine.Matcher.Add(new RuleSet("global", new[]
{
    new RuleBuilder("firstBarrel").Concept("OnSee").Where(Criterion.Eq("object", "barrel"))
        .Where(Criterion.ZeroOrMissing("seen_barrels")).Increment("seen_barrels").Build(),
    new RuleBuilder("secondBarrel").Concept("OnSee").Where(Criterion.Eq("object", "barrel"))
        .Where(Criterion.Eq("seen_barrels", 1)).Build(),
}));

var bot = engine.GetOrCreateCharacter("RedBot");
var line = engine.Ask(bot, "OnSee", q => q.Set("object", "barrel"));
engine.Commit(line);   // call when the line starts playing
// later, when the line ends: dispatch line.Followups
```

## Tests

`Tests/Editor` holds the core NUnit tests (EditMode), `Tests/EditorIntegration` tests the importers and editor tools,
and `Tests/Runtime` holds Play Mode tests for the components.
To run them in Unity, add `"testables": ["com.dynamicdialog"]` to your project's `Packages/manifest.json`.
The core tests also run outside Unity:

```bash
dotnet test "Tests~/DotNet/Tests/DynamicDialog.Core.Tests.csproj"
```

`Tests~/DotNet/Core` builds `Runtime/Core` against `netstandard2.1` with warnings treated as errors.
That catches any API Unity doesn't provide. `Tests~/DotNet/Unity` compile-checks `Runtime/Unity` against an installed editor's DLLs
(set `-p:UnityManaged=...` if yours isn't 6000.5.5f1). Unity ignores folders whose names end in `~`.
