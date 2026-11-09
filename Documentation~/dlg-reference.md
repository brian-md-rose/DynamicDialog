# The `.dlg` language

A `.dlg` file is a plain text file of rules. Unity imports it as a **Dialog Rule Database** that you add to a
[Dialog Director](components.md#dialog-director) or a [region](regions.md). Create one with
**Assets > Create > Dynamic Dialog > Dialog Script (.dlg)**, or make a text file with the `.dlg` extension.

For what rules *mean* (scoring, memory, followups), read [Concepts](concepts.md) first. This page is the syntax.

## A complete example

```dlg
# The swamp conversation.
@who Nick                                  # rules below are Nick's, unless they say otherwise

rule NickSwampIntro
  concept TalkIdle
  if map == swamp and survivors >= 3
  if said_intro missing                    # 0, or never set
  if health in 30..60
  select shuffle
  say "Man, this swamp." audio nick_swamp_01 duration 3s anim Shrug
  say "Cesspool of disease, is what this is."
  remember said_intro = 1
  remember speaking = 1 for 4s
  remember world swamp_talk += 1
  do PlayMusic swamp_theme
  then Rochelle SwampReply after 1s
  once

rule RochelleSwampReply
  who Rochelle
  concept SwampReply
  say "Could be worse. Could be raining."
```

## The basics

- **One statement per line.** Indent however you like; indentation is only for reading.
- **A rule** starts with `rule Name` and runs until the next `rule` line or the end of the file. Every statement
  except the `@` directives belongs to the rule above it.
- **Comments** start with `#` or `//` and run to the end of the line. Inside quotes, `#` and `//` are just text.
- **Names** (rule names, facts, characters, concepts, values) are single words: letters, digits, `_`, `.` and the
  like, with no spaces. Quote a value that has spaces: `if title == "Head Nurse"`. Names ignore case.
- **Text** goes in double quotes. Inside quotes, write `\"` for a quote mark and `\n` for a new line. Curly quotes
  (`“…”`) work too, which helps when text is pasted from a word processor.
- **Numbers** use a dot for decimals (`0.5`), whatever the computer's language.
- **Times** are seconds: `4`, `4s`, `1.5s` or `500ms`.
- **Errors** are reported in the Console with the file and line. A statement with an error is skipped, and the rest
  of the file still loads.

## Directives

Directives start with `@` and apply to every rule after them in the file, until the next directive of the same kind.

### `@who`

`@who Name` makes `Name` the speaker of the rules that follow (as if each had `who Name`). `@who` on its own goes back
to rules for anyone.

```dlg
@who Nurse

rule NurseHello
  concept OnTalk
  say "Hello, dear."

@who

rule AnyoneHurt
  concept OnHurt
  say "Ow!"
```

### `@concept`

`@concept Name` gives the rules that follow a default concept. `@concept` on its own clears it.

```dlg
@concept OnSee

rule SeeBarrel
  if object == barrel
  say "A barrel."

rule SeeCrate
  if object == crate
  say "A crate."
```

### `@partition`

`@partition key ...` sets the fact keys the file's rules are grouped by for fast lookups (at most four). The
default is `concept who`, which suits almost every file. Change it only for a large file whose rules are all
told apart by some other exact-match fact, such as `@partition concept who map`.

```dlg
@partition concept who map

rule NickSwamp
  who Nick
  concept TalkIdle
  if map == swamp
  say "Swamp."
```

## Rule statements

### `rule`

`rule Name` starts a rule. Names must be unique within the file: the debugger shows rules by name, and
[save data](save-load.md) finds them by name. A second rule with the same name is a warning.

### `who`

`who Name` makes the rule only match for that speaker. It is a criterion (`who == Name`), so it counts toward the
score. `who any` (or `who *`) removes an `@who` default for this rule.

```dlg
@who Nick

rule NickHello
  concept OnTalk
  say "Hey."

rule AnyoneLaughs
  who any
  concept OnJoke
  say "Ha!"
```

### `concept`

`concept Name` makes the rule answer that concept. It is a criterion too. A rule with no concept answers *every*
concept, which is almost never what you want, so the importer warns about it. `concept any` removes an `@concept`
default.

### `if` and `and`

`if` adds criteria. Every criterion must pass for the rule to match. You can put several on one line, separated by
`and`, a comma or a semicolon, or write several `if` lines. A line starting with `and` works like `if`.

```dlg
rule LowHealthInSwamp
  concept TalkIdle
  if map == swamp and health < 30
  if zombies >= 3, ammo <= 10
  and rand < 50
  say "This is bad."
```

The comparisons:

| Write | Passes when |
|---|---|
| `fact == value` (also `=` or `is`) | the fact equals the value |
| `fact != value` (also `<>`) | the fact exists and isn't the value |
| `fact < 3`, `fact <= 3`, `fact > 3`, `fact >= 3` | the fact compares that way with the number |
| `fact in 30..60` | the fact is from 30 to 60, both included |
| `fact exists` | the fact is there, whatever its value |
| `fact missing` | the fact is 0 or isn't there |

A criterion on a fact that isn't in the query fails, including `!=`. Add `default N` to test `N` instead when the
fact is missing:

```dlg
rule ManyZombies
  concept TalkIdle
  if zombies > 2 default 0      # no zombies fact counts as 0
  if mood != angry
  say "There are a lot of them."
```

Values are numbers, `true`/`false` (1/0), or names. See [Facts](concepts.md#facts).

### `say`

`say "Text"` adds a line. A rule with several `say` lines has variations, and one is picked each time it plays
(see `select`). After the text, a line can have options, in any order:

| Option | Meaning |
|---|---|
| `audio name` | An audio clip, by file name (`nick_01`) or path (`Voice/Nick/nick_01.wav`) |
| `duration 3s` | How long the line lasts. Without it: the clip's length, or an estimate from the text |
| `anim Trigger` | An Animator trigger set on the speaker's Animator when the line starts |
| `key NAME` | The line's [localization key](localization.md#line-keys) |

```dlg
rule NickGreets
  who Nick
  concept OnTalk
  say "Hey there." audio nick_hey duration 2s anim Wave key NICK_HEY
  say "Yo."
  say audio nick_grunt
```

A line can be audio only (`say audio nick_grunt`), with no text. Audio clips are found when the file is imported:
first in the importer's **Audio Folder** (by default, the `.dlg` file's own folder), then, if **Search Whole Project**
is on, anywhere in the project. A missing clip is a warning, and the line still shows its text.

### `select`

`select mode` sets how a variation is picked:

| Mode | Picks |
|---|---|
| `norepeat` (the default) | At random, but never the same line twice in a row |
| `random` | At random |
| `shuffle` | Every line once, in random order, before any repeats |
| `sequential` | In order, looping |

```dlg
rule CountDown
  concept Countdown
  select sequential
  say "Three."
  say "Two."
  say "One."
```

### `remember`

`remember` writes a fact to memory when the rule plays.

```
remember [scope] fact = value [for time]
remember [scope] fact += amount [for time]
remember [scope] fact -= amount [for time]
```

The scope says whose memory: leave it out (or write `self` or `speaker`) for the speaker's own memory, `world` for
world memory, or a character's name for that character's memory. `for time` makes the fact expire.

```dlg
rule NurseThanks
  who Nurse
  concept OnGiveGlasses
  say "My glasses! Thank you."
  remember thanked = 1
  remember self times_thanked += 1
  remember world glasses_found = 1
  remember Doctor heard_thanks = 1
  remember mood = grateful
  remember just_spoke = 1 for 4s
  remember world tension -= 0.5
```

`+=` and `-=` treat a missing fact as 0.

### `forget`

`forget [scope] fact` removes a fact from memory.

```dlg
rule NurseCalmsDown
  who Nurse
  concept TalkIdle
  if mood == angry
  say "Fine. I forgive you."
  forget mood
  forget world argument_started
```

### `then`

`then target Concept [after time]` sends a concept to someone when the line ends, if it played to the end. The
target is a character's name, `self`, `any` (everyone in earshot; only the best reply plays) or `all` (everyone in
earshot who has a reply). See [Followups](concepts.md#followups-and-conversations).

```dlg
rule NickJoke
  who Nick
  concept TalkIdle
  say "What do you call a zombie with no legs?"
  then self JokePunchline after 1.5s
  then any GroanAtJoke after 3s

rule NickPunchline
  who Nick
  concept JokePunchline
  say "Anything you like. It can't chase you."

rule GroanAtNick
  concept GroanAtJoke
  if from == Nick
  say "Ugh."
```

### `do`

`do EventName [arguments ...] [at start | at end]` raises a game event for your code.

- Without timing, or with `at start`, the event is raised when the line starts, after the rule's facts are written.
- With `at end`, it is raised when the line ends, only if it played to the end. Like followups, it is dropped if
  the line is interrupted.
- Arguments are words or quoted text, separated by spaces. Quote an argument that contains a space, a comma, or the
  word `at` as the second-to-last word.

```dlg
rule NurseGetsGlasses
  who Nurse
  concept OnTalk
  if player_has == glasses
  say "My glasses! Now I can see the garden again."
  remember world glasses_returned = 1
  do GiveItem glasses
  do PlaySound "happy chime"
  do StartWalking garden at end
```

React to events in code with `DialogEvents.On("GiveItem", ...)`, or with a
[Dialog Event Listener](components.md#dialog-event-listener). See [Scripting](scripting.md#game-events).

### `renamed`

`renamed OldName [Older ...]` lists the names a rule used to have. When a player loads a save made before the rename,
the rule's `once` and cooldown state is found under its old name. Without it, renaming a `once` rule would make it
play again in old saves. See [Renamed rules](save-load.md#renamed-rules).

```dlg
rule NurseThanksForGlasses
  renamed NurseThanks NurseGlasses
  who Nurse
  concept OnGiveGlasses
  say "Thank you so much."
  once
```

Memory facts aren't tied to rules, so they need no `renamed`.

### `once`

`once` makes the rule play at most one time.

### `cooldown`

`cooldown time` stops the rule from matching again until that much game time after it last played.

```dlg
rule Cough
  concept TalkIdle
  say "*cough*"
  cooldown 30s

rule ShowTheWay
  concept OnTalk
  say "The ward is through there."
  once
```

### `weight`

`weight number` sets the rule's chance when it ties with other matching rules on score. The default is 1; a rule
with `weight 3` is picked three times as often as a rule with weight 1. `weight 0` only plays when every tied rule
also has weight 0.

### `bonus`

`bonus number` adds to the rule's score (normally the number of criteria), so it can beat more specific rules.

```dlg
rule RareGreeting
  concept OnTalk
  say "Well, well, well."
  weight 0.2

rule CommonGreeting
  concept OnTalk
  say "Hello."

rule AlarmOverridesEverything
  concept OnTalk
  if alarm == 1
  say "No time to talk!"
  bonus 10
```

## Statement summary

| Statement | Example |
|---|---|
| `@who` | `@who Nick` |
| `@concept` | `@concept OnSee` |
| `@partition` | `@partition concept who` |
| `rule` | `rule NickSwampIntro` |
| `who` | `who Nick`, `who any` |
| `concept` | `concept TalkIdle` |
| `if` / `and` | `if map == swamp and health < 30` |
| `say` | `say "Text" audio clip duration 3s anim Trigger key KEY` |
| `select` | `select shuffle` |
| `remember` | `remember world gossip += 1 for 10s` |
| `forget` | `forget Nick area` |
| `then` | `then Rochelle Reply after 1s` |
| `do` | `do GiveItem glasses`, `do StartWalking at end` |
| `renamed` | `renamed OldName` |
| `once` | `once` |
| `cooldown` | `cooldown 30s` |
| `weight` | `weight 2` |
| `bonus` | `bonus 1` |
