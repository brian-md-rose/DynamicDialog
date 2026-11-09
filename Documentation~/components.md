# Components

Every component is under **Add Component > Dynamic Dialog**. A scene needs one **Dialog Director** and a **Dialog
Speaker** on each character; everything else is optional.

| Component | Goes on | What it does |
|---|---|---|
| [Dialog Director](#dialog-director) | One object per scene (or a bootstrap scene) | Loads rule databases, holds world memory, plays followups |
| [Dialog Speaker](#dialog-speaker) | Each character | Asks for lines and delivers them: audio, animation, subtitles, idle chatter |
| [Dialog Vision](#dialog-vision) | A speaker | Remarks on Dialog Tags it can see |
| [Dialog Tag](#dialog-tag) | Anything worth remarking on | Makes the object visible to Dialog Vision |
| [Static Facts](#static-facts) | A speaker or the director | Fixed facts typed in the inspector |
| [Nearby Speaker Facts](#nearby-speaker-facts) | A speaker | Facts about who is in earshot |
| [Dialog Area](#dialog-area) | A trigger volume | Sets `area` in the memory of speakers inside it |
| [Dialog Region](#dialog-region) | A region's scene | Loads a rule database while enabled |
| [Dialog Addressable Region](#dialog-addressable-region) | A region's scene | The same, streamed through Addressables |
| [Dialog Event Listener](#dialog-event-listener) | Anywhere | UnityEvents for the game events rules raise with `do` |
| [Dialog Fact Listener](#dialog-fact-listener) | Anywhere | UnityEvents when a fact in memory changes |
| [Dialog Speech Bubbles](#dialog-speech-bubbles) | Its own canvas (prefab) | Speech bubbles over speakers |
| [Dialog Speech Bubble](#dialog-speech-bubble) | The bubble prefab | One bubble's look and layout |
| [Dialog Subtitle Bar](#dialog-subtitle-bar) | Its own canvas (prefab) | Subtitles along the bottom of the screen |
| [Dialog Bubble Anchor](#dialog-bubble-anchor) | A speaker | Where that speaker's bubble points |
| [Unity Localization Localizer](#unity-localization-localizer) | The director | Translated text and voice from Unity Localization tables |
| [Subtitle Overlay](#subtitle-overlay-debug) *(debug)* | Anywhere | Quick IMGUI subtitles for prototypes |
| [Debug Overlay](#debug-overlay-debug) *(debug)* | Anywhere | IMGUI labels over each speaker with its latest lookup |

The assets are the **Dialog Rule Database** (made by importing a `.dlg` or spreadsheet, or with
**Assets > Create > Dynamic Dialog > Rule Database**; see [Spreadsheets and the inspector](authoring-formats.md)) and
the **Rule Editor String Table** (see [Rule Editor](rule-editor.md)).

## Dialog Director

The scene's dialog hub. It owns the rules, world memory, and the followup schedule. Speakers find it by
themselves (`DialogDirector.Instance`), so you don't need to link them. Any [fact provider](scripting.md#your-own-facts)
component on the same GameObject, such as Static Facts, adds world facts to every query.

| Setting | Default | What it does |
|---|---|---|
| **Databases** | | Rule databases that are always loaded. Regions can load more. |
| **Random Seed** | 0 | 0 picks a new seed each run. Any other value makes tie-breaks, `rand` and variation picks repeat run to run. |
| **Add Built In World Facts** | on | Adds `map` (the active scene's name) and `speakers_talking` (lines playing) to every query. |
| **Named Followups Require Earshot** | off | When on, `then Name Concept` only reaches that character if they can hear the speaker. `any` and `all` always need earshot. |
| **Log Lines** | off | Logs each line, game event and hot reload to the Console. |
| **Log No Match** | off | Logs every lookup that found no rule. Noisy, but useful when a line never plays. |
| **History Size** | 64 | How many recent lines `History` keeps. |
| **Record Lookups** | on | Records each lookup for the [Debugger](debugging.md) and Debug Overlay. Only works in the editor and development builds. |
| **Record Capacity** | 256 | How many lookups to keep. |

From code: `Engine` (rules, characters, memory), `Conductor` (playing lines and followups), `AddDatabase` and
`RemoveDatabase`, `SaveStateJson` and `LoadStateJson` ([Saving](save-load.md)), `Localizer`, `Recorder` and `History`.

## Dialog Speaker

Makes a GameObject a character. It asks for lines, plays them, and tells presenters, handlers and events about them.

| Setting | Default | What it does |
|---|---|---|
| **Who** | (empty) | The character's name in rules (the `who` fact). Empty uses the GameObject's name. Speakers with the same name share one memory. |
| **Earshot Radius** | 15 | How far away this speaker hears others, for `then any` and `then all`. Shown as a gizmo when selected. |
| **Can Speak** | on | Turn off while the character can't talk (dead, gagged, in a cutscene). |
| **Audio Source** | | Plays each line's clip. Without one, voiced lines still show their text for the clip's length. |
| **Animator** | | Gets each line's `anim` trigger. |
| **Seconds Per Character** | 0.06 | For lines with no clip and no `duration`: how long each character of text keeps the line up. |
| **Minimum Duration** | 1.5 | The shortest time a line without audio stays up. |
| **Idle Talk** | on | Every **Idle Interval** seconds (plus up to **Idle Jitter** more), when not talking, ask for **Idle Concept**. |
| **Idle Concept** | `TalkIdle` | The concept idle chatter asks for. |
| **Idle Interval**, **Idle Jitter** | 5, 2 | Seconds between idle requests, and the random extra so speakers don't talk in sync. |
| **On Subtitle** | | UnityEvent with the line's (localized) text when a line starts. |
| **On Line Ended** | | UnityEvent when a line ends, whether it played to the end or was stopped. |

From code: `Speak(concept, eventFacts, interrupt)`, `StopLine()`, `IsSpeaking`, `CurrentLine`, `CurrentText`,
`CurrentClip`, `Memory`, and the static events `AnyLineStarted` and `AnyLineEnded`. Components on the same GameObject
that implement `IFactProvider` add facts to the speaker's queries, and ones that implement `IDialogLineHandler` are
told when its lines start and end. See [Scripting](scripting.md).

## Dialog Vision

Every **Poll Interval** seconds, finds the [Dialog Tags](#dialog-tag) the speaker can see, nearest first, and asks the
speaker for **Concept** (`OnSee`) about each one until a line plays. The query has `object` (the tag's name) and
`object_distance`. Each tag it asks about then rests for **Tag Cooldown** seconds. Writers use memory to make the
second sighting different from the first.

| Setting | Default | What it does |
|---|---|---|
| **Concept** | `OnSee` | The concept to ask for. |
| **Poll Interval** | 2 | Seconds between looks. |
| **Range** | 10 | How far the speaker can see. |
| **Field Of View** | 120 | Degrees, centred on **Eyes**' forward direction. |
| **Tag Cooldown** | 20 | Seconds before the same tag is considered again. |
| **Eyes** | | Where the speaker looks from. Empty uses this transform. |
| **Require Line Of Sight** | on | Raycasts against **Occluders**; the speaker's own colliders and the tag's are ignored. |

## Dialog Tag

Marks an object that speakers can remark on. **Tag Name** is the `object` value rules test (`if object == barrel`);
empty uses the GameObject's name. **Focus** is the point speakers aim at for line of sight; empty uses the transform.

## Static Facts

Fixed facts typed in the inspector, such as `faction = penguin` or `is_doctor = true`. Values are numbers,
true/false, or names. On a speaker they describe that character; on the director they are world facts.
`SetFact(key, value)` changes one at runtime.

## Nearby Speaker Facts

Adds two kinds of facts to the speaker's queries: `nearby_speakers`, how many other speakers are in this speaker's
earshot, and `near_<who>` = 1 for each of them (`near_Rochelle`). Rules can then say "if Rochelle is here".

```dlg
rule NickMissesRochelle
  who Nick
  concept TalkIdle
  if near_Rochelle missing
  if nearby_speakers >= 1
  say "Anyone seen Rochelle?"
```

## Dialog Area

A trigger volume that writes `area` = **Area Name** (or the GameObject's name) into the memory of each speaker that
enters, and removes it when they leave (unless they've already entered another area). **Enter Concept**, if set, is
asked for when a speaker enters. The area needs a trigger collider, and the area or the speaker needs a Rigidbody, as
usual for Unity triggers.

## Dialog Region

Loads **Database** while enabled and unloads it when disabled, so a level's rules come and go with the level. The
**Region Facts** are world facts that exist only while the region is enabled: **Region Name** sets `region`, plus any
extra facts. See [Regions](regions.md).

## Dialog Addressable Region

Like Dialog Region, but **Database** is an Addressables reference: the rules and their audio stay on disk until the
region is enabled, and are released when it's disabled, even mid-load. Exists only when the Addressables package is
installed. `Loaded`, `IsLoaded`, `IsLoading` and `LoadedDatabase` report its progress. See [Regions](regions.md).

## Dialog Event Listener

Connects the game events rules raise with `do` to UnityEvents, for designers who'd rather wire things in the inspector
than write code. Each entry in **Events** has:

| Field | What it does |
|---|---|
| **Event Name** | The name after `do` in a rule (ignores case). |
| **Only From** | Only react when this speaker's line raised the event. Empty reacts to anyone. Adding the component to a speaker fills this in. |
| **On Event** | Raised when the event happens. |
| **On Event With Argument** | Raised with the event's first argument as text (empty if it has none). |

Code on the same object can use the `Received` C# event. For code elsewhere, `DialogEvents.On` is simpler; see
[Game events](scripting.md#game-events).

## Dialog Fact Listener

Watches one fact in memory and raises UnityEvents when it changes, whether a rule or code changed it, and when it
expires. Use it for quest markers, counters and doors that open when the dialog says so.

| Setting | What it does |
|---|---|
| **Scope** | Whose memory: **World**, **Speaker** (the Dialog Speaker on this GameObject) or **Character**. |
| **Character** | The character's name, for the Character scope. |
| **Fact** | The fact to watch, such as `glasses_returned`. |
| **Notify Current Value** | Raise the events with the current value when enabled, and after a save is loaded. On by default. |
| **On Value Changed** | The new value as a number; 0 when the fact is removed or expires. |
| **On Text Changed** | The new value as text: a name such as `garden`, or a number. Empty when removed. |
| **On Set** | The fact appeared (it was missing before). |
| **On Removed** | The fact was removed or expired. |

`Scope`, `Character` and `Fact` can be changed from code, and `TryGetValue` reads the current value.

## Dialog Speech Bubbles

Shows each line in a bubble over its speaker, with a tail pointing at them. Add it with **GameObject > Dynamic Dialog >
Speech Bubbles**, which adds the prefab with its own screen-space canvas. It needs uGUI and TextMeshPro.

| Setting | Default | What it does |
|---|---|---|
| **Bubble Prefab** | Dialog Speech Bubble | The bubble to show. Assign a restyled variant here. |
| **View Camera** | | The camera speakers are seen through. Empty uses `Camera.main`. |
| **Height Offset** | 2 | How far above a speaker's pivot its bubble points, unless it has a Dialog Bubble Anchor. |
| **Max Distance** | 0 | Hide bubbles of speakers farther than this from the camera. 0 means no limit. |
| **Overlap** | Stack | *Stack*: a bubble per speaker, moved apart when they overlap (newer above, or below near the top of the screen). *Replace*: only the newest line shows; when it ends, the next newest still playing comes back. |
| **Max Bubbles** | 0 | With Stack, the most bubbles at once (newest kept). 0 means no limit. |
| **Overlap Spacing** | 6 | The gap between bubbles moved apart. |
| **Offscreen** | Keep On Screen | *Keep On Screen*: a speaker past a screen edge, or behind the camera, keeps its bubble at the nearest edge, with the tail pointing their way. *Hide*: hide those bubbles. |
| **Screen Margin** | 16 | The closest a bubble comes to a screen edge. |
| **Tail Gap** | 18 | Space between the speaker's anchor and the bubble, for the tail. |
| **Typewriter** | on, 45/s, 0.7 | Reveal text a character at a time at **Characters Per Second**, speeding up so the whole line shows by **Finish By** (a fraction of the line's duration). Turn off to show lines at once. |

`BubbleFor(speaker)` returns a speaker's current bubble. Distances are in canvas units.

## Dialog Speech Bubble

The single bubble inside the bubble prefab. Change these on a [prefab variant](presenters.md#restyling) to restyle bubbles.

| Setting | What it does |
|---|---|
| **Body** | The TextMeshPro text for the line. |
| **Name Label** | Optional text for the speaker's name. |
| **Tail** | Optional tail, drawn pointing down, with its pivot where it joins the bubble. |
| **Padding**, **Name Spacing**, **Min Width**, **Max Width** | Layout, in canvas units. The bubble sizes itself to its text between the two widths. |
| **Fade In Seconds**, **Fade Out Seconds**, **Interrupted Fade Seconds** | Fade times. Interrupted lines fade faster. |

Derive from it and override `Show`, `Hide`, `Tick`, `Layout` or `PointTailAt` for a different behavior.

## Dialog Subtitle Bar

A row of text per line along the bottom of the screen. Add it with **GameObject > Dynamic Dialog > Subtitle Bar**.
It needs uGUI and TextMeshPro.

| Setting | Default | What it does |
|---|---|---|
| **Row Container**, **Row Template** | | Where rows go, and an inactive row cloned for each line. Its first TextMeshPro child shows the text. |
| **Show Speaker Name** | on | Put the speaker's name before the line. |
| **Name Format** | `<b>{0}:</b> ` | How the name is written; `{0}` is the name. Rich text works. |
| **Name Color** | gold | The name's color. |
| **Overlap** | Stack | *Stack*: a row per line, up to **Max Rows** (oldest dropped). *Replace*: only the newest line. |
| **Max Rows** | 3 | With Stack, the most rows at once. 0 means no limit. |
| **Fade In/Out/Interrupted Seconds** | | Fade times. |
| **Typewriter** | | As for the speech bubbles. |

`Rows` lists the rows on screen.

## Dialog Bubble Anchor

Says where a speaker's bubble points. **Point** is a transform, such as the head bone (empty uses this GameObject),
and **Offset** is added in world space (0.3 up by default). Without an anchor, bubbles point at the speaker's position
plus the Speech Bubbles' **Height Offset**.

## Unity Localization Localizer

Put it on the Dialog Director to show translated subtitles and play translated voice clips from Unity Localization
tables. **String Table** (`Dialog` by default) is the string table collection; **Audio Table** (optional) is an asset
table collection of clips. Lines with no entry fall back to the authored text and clip. Exists only when the
Localization package is installed. See [Localization](localization.md).

## Subtitle Overlay (debug)

**Add Component > Dynamic Dialog > Debug > Subtitle Overlay.** IMGUI subtitles with no setup, for prototypes. **Show
Rule Names** adds each line's rule and concept. It isn't meant to ship; use the Subtitle Bar instead.

## Debug Overlay (debug)

**Add Component > Dynamic Dialog > Debug > Debug Overlay.** Draws a label over each speaker in the Game view: its name,
its current line, and its latest lookup (green: played, yellow: matched but not played, red: no match). It needs
**Record Lookups** on the director. Settings: **Visible** (toggle it from your own debug key), **View Camera**,
**Height Offset**, **Show Seconds**, **Only Misses** (show only lookups that found nothing) and **Font Size**.

## Optional packages

Some components live in assemblies that only compile when their packages are installed. Without those packages, the
rest of Dynamic Dialog compiles and works, with no errors or warnings.

| Needs | Components |
|---|---|
| uGUI and TextMeshPro (`com.unity.ugui` 2.x on Unity 6; `com.unity.ugui` and `com.unity.textmeshpro` before) | Dialog Speech Bubbles, Dialog Speech Bubble, Dialog Subtitle Bar |
| Addressables (`com.unity.addressables`) | Dialog Addressable Region |
| Localization (`com.unity.localization`) | Unity Localization Localizer |
