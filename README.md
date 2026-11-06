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
| Dialog Event Listener | anywhere | UnityEvents for the game events rules raise with `do` |
| Dialog Fact Listener | anywhere | UnityEvents when a fact in world or character memory changes |
| Dialog Speech Bubbles | screen-space Canvas | Speech bubbles over speakers' heads (see **Showing lines**) |
| Dialog Subtitle Bar | screen-space Canvas | Subtitles along the bottom of the screen (see **Showing lines**) |
| Dialog Bubble Anchor | speaker | Where that speaker's bubble points, e.g. the head bone |
| Subtitle Overlay *(debug tool)* | anywhere | IMGUI subtitles, optionally with rule names. For prototyping; not meant to ship |
| Debug Overlay *(debug tool)* | anywhere | IMGUI labels over each speaker: current line and latest lookup (green played, yellow matched but not played, red no match). Not meant to ship |

Your own facts: implement `IFactProvider` on a component next to a speaker (character facts) or the director (world facts).
React to lines: write an `IDialogPresenter` (see **Showing lines**), implement `IDialogLineHandler` on a speaker, use a speaker's `onSubtitle` event, or the static `DialogSpeaker.AnyLineStarted`.
To trigger game code from a rule, use a `do` event (see **Writing rules**) rather than checking rule names.

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
  do PlayMusic swamp_theme              # a game event for your code
  then Rochelle SwampReply after 1s     # or: then self|any|all Concept
  once
```

The full grammar, including `select`, `forget`, `cooldown`, `weight`, `bonus`, `default`, `@concept` and `@partition`,
is documented at the top of `Runtime/Core/Authoring/DialogScriptParser.cs`.

**Game events (`do`)** let rules trigger game code without the code knowing rule names. `do GiveItem glasses`
raises the event `GiveItem` with the argument `glasses` when the line starts, right after the rule's `remember` facts
are written. Add `at end` (`do StartWalking at end`) to raise it when the line has played to the end instead; like
followups, it is dropped if the line is interrupted. Arguments are words or quoted text. React to them in code:

```csharp
IDisposable sub = DialogEvents.On("GiveItem", (speaker, e) => inventory.Give(speaker, e.Arg(0)));
// later: sub.Dispose();
```

or with a **Dialog Event Listener** component (event name, optional speaker, UnityEvents). To react to facts instead,
for quest markers or UI, use a **Dialog Fact Listener** component or `DialogDirector.Instance.Engine.FactChanged`,
which reports every change to world and character memory, including expiry. Errors show in the Console with file and line,
and the rest of the file still imports.

**Rule Editor** (**Window > Dynamic Dialog > Rule Editor**, or **Open in Rule Editor** on a `.dlg` file's inspector) edits
`.dlg` files without typing the syntax. Names come from a **Rule Editor String Table** asset through searchable drop-downs
(type to filter, or add a new name). Problems are re-checked as you edit, each followup shows the rules that answer it
(with **Create Answer** when none do), and a *Links* section shows the rules that lead to a rule and the rules that set or
check its facts. Drag rules to reorder them, group them by speaker, and copy or paste them as `.dlg` text. Saving keeps the
file's comments: comments above a rule travel with it, and comments inside a rule move just above it.

**Spreadsheets** (**Assets > Create > Dynamic Dialog > Dialog Spreadsheet**): save as CSV or TSV and name the file
`.dlgcsv` or `.dlgtsv`. There is one row per recorded line; a row with an empty `Rule` cell adds a variation to the rule above.
Known columns are `Rule, Who, Concept, If, Text, Audio, Duration, Anim, Remember, Then, Do, Once, Cooldown, Weight, Bonus, Select`
(`Do` holds game events: `GiveItem glasses; StartWalking at end`).
`Notes`, `Comment`, `Actor`, `Status` and columns starting with `#` are ignored. **Any other column is a criterion**
named after its header: a `Map` column holding `swamp` means `Map == swamp`, and cells can also hold `< 30`, `>= 3`,
`!= nick`, `30..60`, `exists` or `missing`.

**Audio** is looked up by name or path: first in the importer's audio folder (by default, the file's own folder),
then anywhere in the project. Missing clips are a warning, and the line still shows its text.

**Checking rules:** **Window > Dynamic Dialog > Rule Validator** checks every database together. It finds followups no
rule answers, conditions that contradict each other, likely typos in fact names, duplicate names, and rules that do nothing.
List the facts your own code provides in its *Facts from code* field. Each database's inspector also has **Check Rules**,
**Export .dlg** (to move inspector-made rules into text) and **Export Recording Script** (a CSV for voice sessions).

## Showing lines

Two ready-made presenters ship as prefabs in `Runtime/Presentation/Prefabs`. Add them with **GameObject > Dynamic Dialog >
Speech Bubbles** or **Subtitle Bar**. Both are uGUI with TextMeshPro, so they need the uGUI package and, on Unity 2021.3 and
2022, the TextMeshPro package. Both are included in Unity's default templates. Without them, the presentation assembly doesn't
compile, and the rest of the package works as before. Import TMP Essential Resources the first time Unity asks you to.

- **Dialog Speech Bubbles:** a bubble over each speaker's head (or at its **Dialog Bubble Anchor**). Bubbles fade in and out and
  size themselves to their text. A tail points at the speaker. Settings:
  - **Overlap:** *Stack* (the default) keeps a bubble per speaker and moves overlapping bubbles apart. Newer bubbles go above,
    or below when there is no room. *Replace* shows only the newest line; when it ends, the next newest still playing comes back.
    *Max Bubbles* caps a stack.
  - **Screen edges:** with *Keep On Screen* (the default), a speaker past the edge of the screen keeps its bubble at the
    nearest edge, and a speaker behind the camera gets its bubble on the edge on its side. The tail points their way. *Hide*
    hides those bubbles instead. *Max Distance* hides far-away speakers.
- **Dialog Subtitle Bar:** a row per line along the bottom of the screen, with an optional colored speaker name. Overlap is
  *Stack* (up to *Max Rows*, oldest dropped) or *Replace*.
- **Typewriter** (both): text appears at *Characters Per Second*, and speeds up when needed, so the whole line is visible by
  *Finish By* of the line's duration. Turn it off to show lines at once. Rich-text tags aren't counted.

**Restyling:** make a prefab variant of *Dialog Speech Bubble* (the single bubble) or *Dialog Subtitle Bar*, then change its sprites,
colors, TextMeshPro fonts, padding, maximum width and fade times. Assign your bubble variant to *Bubble Prefab* on Dialog Speech
Bubbles. Override `DialogSpeechBubble.Layout` to arrange a bubble differently.

**Your own UI:** implement `IDialogPresenter` and register it with `DialogPresenters.Register`, or derive a component from
`DialogPresenterBehaviour`, which registers itself while enabled. Every line any speaker plays reaches every registered presenter:
`OnLineStarted(line)`, then either `OnLineEnded` (it played to the end) or `OnLineInterrupted` (it was cut off, stopped, or its
speaker was disabled). `DialogPresentedLine` carries the speaker, the line, its localized `Text`, `StartTime` and `Duration`.
A presenter registered mid-line is told about lines already playing. `DialogTypewriter`, `DialogOverlap` and `DialogScreenPlacement`
are the built-in components' logic, for reuse in your own UI.

```csharp
public class MyCaptions : DialogPresenterBehaviour
{
    public TMPro.TMP_Text label;
    public override void OnLineStarted(DialogPresentedLine line) => label.text = line.SpeakerName + ": " + line.Text;
    public override void OnLineEnded(DialogPresentedLine line) => label.text = "";
}
```

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
and `Tests/Runtime` holds Play Mode tests for the components. `Tests/Presentation` holds Play Mode tests for the speech bubble
and subtitle bar prefabs (they need TMP Essential Resources imported).
To run them in Unity, add `"testables": ["com.dynamicdialog"]` to your project's `Packages/manifest.json`.
The core tests also run outside Unity:

```bash
dotnet test "Tests~/DotNet/Tests/DynamicDialog.Core.Tests.csproj"
```

`Tests~/DotNet/Core` builds `Runtime/Core` against `netstandard2.1` with warnings treated as errors.
That catches any API Unity doesn't provide. `Tests~/DotNet/Unity` compile-checks `Runtime/Unity` against an installed editor's DLLs
(set `-p:UnityManaged=...` if yours isn't 6000.5.5f1). `Tests~/DotNet/Presentation` does the same for `Runtime/Presentation`;
uGUI and TextMeshPro are packages, so it also needs a Unity project's compiled `Library/ScriptAssemblies`
(`-p:UnityScriptAssemblies=...`). Unity ignores folders whose names end in `~`.
