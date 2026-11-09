# Concepts

This page explains how Dynamic Dialog decides what a character says. The examples are in the
[`.dlg` language](dlg-reference.md), but everything here applies to every way of writing rules.

## The lookup

Something happens in the game: a character sees a barrel, gets hurt, or has been quiet for a while. The game asks
that character's [Dialog Speaker](components.md#dialog-speaker) to speak a **concept**, a name for what happened
(`OnSee`, `OnHurt`, `TalkIdle`). Then:

1. The engine builds a **query**: a flat list of facts describing this moment.
2. It finds every loaded rule whose **criteria** (conditions) all pass against the query.
3. The rule with the highest **score** wins. Ties are broken at random.
4. The winner plays: the speaker says one of its lines, its fact writes are applied, and its game events are raised.
5. When the line ends, the winner's **followups** are sent, which starts the next lookup in a conversation.

If no rule matches, nothing is said. That's normal: most lookups, like idle chatter or things seen in passing,
are allowed to find nothing.

## Facts

A fact is a name and a value, such as `health = 30` or `map = swamp`. Every value is a number underneath:

- Numbers are numbers: `30`, `-2`, `0.5`.
- `true` and `false` are 1 and 0.
- Anything else is a **name** (`swamp`, `Nick`, `glasses`). Names can be compared with `==` and `!=`, but `<` or `>`
  on names means nothing useful.

Names ignore case: `Swamp` and `swamp` are the same name, and so are the facts `Health` and `health`. Tools
show a name with the capitalization it was first written with.

### Where the query's facts come from

Each lookup builds a fresh query from these sources, in this order. When two sources set the same fact, the later
one wins.

| Order | Source | Examples |
|---|---|---|
| 1 | **World memory**: facts rules (or your code) wrote with `remember world …` | `gossip = 2` |
| 2 | **World fact providers**: components on the Dialog Director, and active regions | `map`, `speakers_talking`, `region`, Static Facts on the director |
| 3 | **The speaker's memory**: facts written with `remember …` | `said_intro = 1`, `area = garden` |
| 4 | **The speaker's fact providers**: components on the speaker's GameObject | Static Facts, Nearby Speaker Facts, your own `IFactProvider` |
| 5 | **Event facts**: passed with this one lookup | `object = barrel` from Dialog Vision, `from` on a followup |
| 6 | **The engine** | `concept`, `who`, `rand` |

### Built-in facts

| Fact | Value | Set by |
|---|---|---|
| `concept` | The concept being asked for | The engine, on every query |
| `who` | The speaker's name | The engine |
| `rand` | A new random number from 0 to 100 on every query | The engine |
| `map` | The active scene's name | Dialog Director (**Add Built In World Facts**) |
| `speakers_talking` | How many lines are playing | Dialog Director (**Add Built In World Facts**) |
| `from` | Who sent this followup | The engine, on followup queries only |
| `object`, `object_distance` | What the speaker saw, and how far away it is | Dialog Vision, on `OnSee` queries |
| `area` | The Dialog Area the speaker is standing in | Dialog Area, in the speaker's memory |
| `nearby_speakers`, `near_<who>` | How many other speakers are in earshot, and 1 for each of them | Nearby Speaker Facts |
| `region` | The active region's name | Dialog Region, Dialog Addressable Region |

`WellKnownFacts` has each of these names as a constant for code.

## Memory and scopes

Memory is where rules keep what has happened. There are two kinds:

- **World memory**, shared by everyone. Facts in it appear in every speaker's queries.
- **Character memory**, one per character name. Facts in it only appear in that character's own queries. Two
  speakers with the same **Who** share one memory.

A rule writes to the speaker's memory by default. It can also write to world memory or to another character's
memory:

```dlg
rule NurseThanks
  who Nurse
  concept OnGiveGlasses
  say "My glasses! Thank you."
  remember thanked_player = 1          # the Nurse's own memory
  remember world glasses_found = 1     # everyone's
  remember Doctor nurse_happy = 1      # the Doctor's memory
  remember speaking = 1 for 4s         # forgotten again after 4 seconds
```

`remember fact += 1` adds to a number (a missing fact counts as 0). `forget fact` removes a fact. A fact written
`for` a time **expires**: it disappears from queries when its time is up.

Memory lasts until Play Mode stops, or until you [save and load it](save-load.md). Your code can read and write
memory too; see [Scripting](scripting.md#memory).

## Criteria and missing facts

A criterion tests one fact: `health < 30`, `map == swamp`, `health in 30..60`, `object exists`. A rule's
`who` and `concept` are criteria too (`who == Nick`, `concept == TalkIdle`).

**A criterion on a fact that isn't in the query fails.** So `if health < 30` never matches a character with no
`health` fact. When you mean "zero or never set", which is the usual way to say "this hasn't happened yet", use
`missing`:

```dlg
rule NickSwampIntro
  who Nick
  concept TalkIdle
  if map == swamp
  if said_swamp_intro missing     # 0, or never set
  say "Man, this swamp."
  remember said_swamp_intro = 1
```

Or give a criterion a value to test when the fact is missing: `if zombies > 2 default 0`.

## Scoring: the most specific rule wins

A rule's **score** is the number of its criteria, plus its `bonus` if it has one. Every criterion counts the same,
including `who` and `concept`. Of the rules that match, the one with the highest score wins.

```dlg
rule SeeAnything
  concept OnSee
  say "Huh, look at that."

rule SeeBarrel
  concept OnSee
  if object == barrel
  say "A barrel."

rule SeeBarrelAgain
  concept OnSee
  if object == barrel
  if barrels_seen >= 1
  say "Another barrel. Of course."
```

Seeing a barrel for the first time, SeeAnything (score 1) and SeeBarrel (score 2) both match, and SeeBarrel wins.
Seeing a crate, only SeeAnything matches. This is how you build up dialog: start with general rules, then add
specific ones wherever a character should notice more. You never edit the general rule to make room.

When several matching rules share the best score, one is picked at random. A rule's `weight` (1 by default)
sets its relative chance: a rule with `weight 3` is picked three times as often as one with `weight 1`.

Use `bonus` sparingly, when a rule should win over rules that have more criteria. A rule with `bonus 2` and two
criteria scores 4.

### Variations

A rule can have several `say` lines. Each time the rule plays, one is chosen by its `select` mode:

| Mode | Picks |
|---|---|
| `norepeat` (default) | At random, but never the same line twice in a row |
| `random` | At random |
| `shuffle` | Every line once, in random order, before any repeats |
| `sequential` | In order, looping back to the first |

## Followups and conversations

A rule's `then` statement sends a concept to someone when its line **ends**. That's how conversations happen:

```dlg
rule NickSwampComplaint
  who Nick
  concept TalkIdle
  if map == swamp
  say "Cesspool of disease, is what this is."
  then Rochelle SwampReply after 1s

rule RochelleSwampReply
  who Rochelle
  concept SwampReply
  say "Could be worse. Could be raining."
```

The receiver builds its own query at the moment the followup arrives, not when the first line started. If things
have changed (Rochelle walked away, or is busy), the conversation ends by itself, or takes another turn: Rochelle's
reply is a normal lookup, so you can give her several `SwampReply` rules for different situations. The followup's
query also has a `from` fact naming who sent it.

The receiver can be:

| `then …` | Who is asked |
|---|---|
| `then Rochelle Concept` | The speaker named Rochelle. If **Named Followups Require Earshot** is on in the director, only when she can hear the line. |
| `then self Concept` | The same speaker, to split a long speech into parts |
| `then any Concept` | Everyone in earshot is asked; only the best-scoring reply plays |
| `then all Concept` | Everyone in earshot is asked; everyone with a match replies |

"In earshot" means within the listener's **Earshot Radius** (on its Dialog Speaker) of the speaker.
`after 1s` adds a pause after the line ends.

A line that is **interrupted** (cut off by another line, stopped, or its speaker disabled) sends no followups and
raises no `at end` events.

## Once and cooldown

- `once` makes a rule play at most one time. After that it never matches again.
- `cooldown 30s` stops a rule from matching again until 30 seconds after it last played.

This state is kept per rule, in game time (Unity's scaled time, so it stops while `Time.timeScale` is 0). It is
included in [save data](save-load.md). Use memory facts instead of `once` when other rules need to know that
something was said.

## Interruptions and busy speakers

A speaker says one line at a time. A speaker that is already talking ignores new requests, unless the request
interrupts: `speaker.Speak("OnHurt", interrupt: true)` stops the current line (dropping its followups) and plays the
new one, but only if a rule matches. If none does, the current line carries on.

A speaker with **Can Speak** off, or that is disabled, isn't asked at all.

## How long a line lasts

A line lasts for its `duration` if it has one. Otherwise it lasts for its audio clip's length, or, with no audio,
for its text's length times the speaker's **Seconds Per Character**, but at least **Minimum Duration**.

## Game events

A rule can raise named events for your code when it plays, with `do`:

```dlg
rule NurseTakesGlasses
  who Nurse
  concept OnTalk
  if player_has == glasses
  say "Are those my glasses? Thank you!"
  do GiveItem glasses
  do StartWalking at end
```

Events without `at end` are raised when the line starts, after the rule's facts are written. Events with `at end`
are raised when the line has played to the end. Your code reacts with `DialogEvents.On` or a
[Dialog Event Listener](components.md#dialog-event-listener). This keeps game code free of rule names: renaming or
rewriting rules never breaks a quest. See [Scripting](scripting.md#game-events).

## Rule sets and partitioning

Each rule database is a **rule set**. Loaded sets are searched together, so a level's set can add rules on top of
the global ones; see [Regions](regions.md).

Inside a set, rules are grouped into buckets by their exact `concept` and `who` values, so a lookup only tests the
few rules that could answer it. This is why a lookup stays fast with thousands of rules. Rules with no `who` go into a
bucket that every speaker checks. The `@partition` directive changes the keys; you rarely need it.
