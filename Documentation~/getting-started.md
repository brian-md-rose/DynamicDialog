# Getting started

In about ten minutes you will install the package, make a character who talks, and write three rules that show
the main idea: **the most specific rule that matches wins.** The result is the same scene as the **First Bark**
sample, which you can import at the end to compare.

## 1. Install (1 minute)

Dynamic Dialog needs Unity 2021.3 or later. Install it with Package Manager (**Window > Package Manager**):

- From a folder: **+ > Add package from disk…**, then pick the package's `package.json`.
- From a Git repository: **+ > Add package from git URL…**, then paste the repository URL.

The speech bubbles used below need uGUI and TextMeshPro. Unity's project templates already include them. In Unity
2021.3 and 2022, if Unity asks to import **TMP Essential Resources**, click **Import TMP Essentials**.

## 2. Write a rule (2 minutes)

In the Project window, right-click a folder and choose **Create > Dynamic Dialog > Dialog Script (.dlg)**. Name the
file `Pip`. Open it in any text editor and replace its contents with one rule:

```dlg
rule Chatter
  concept TalkIdle
  say "Nice weather for standing around."
  say "Did you know I'm made of one rule file?"
```

A rule answers a **concept**: the kind of thing that just happened. `TalkIdle` is what a character asks for when
it has nothing else to do. The rule has two `say` lines, and one of them is picked each time it plays.

Save the file. Unity imports it as a **Dialog Rule Database** asset. If there's a mistake in it, the Console says
which line.

## 3. Set up the scene (3 minutes)

Start from an empty 3D scene with its Main Camera.

1. **The director.** Choose **GameObject > Create Empty** and name it `Dialog Director`. Click **Add Component** and
   add **Dynamic Dialog > Dialog Director**. Drag `Pip.dlg` from the Project window into its **Databases** list. The
   director answers every "what should I say?" question in the scene.
2. **The character.** Choose **GameObject > 3D Object > Capsule** and name it `Pip`. Add **Dynamic Dialog > Dialog
   Speaker**. Leave **Who** empty, so the speaker uses the GameObject's name, `Pip`. **Idle Talk** is on by default,
   so every few seconds the speaker asks for a `TalkIdle` line.
3. **The speech bubbles.** Choose **GameObject > Dynamic Dialog > Speech Bubbles**. This adds a canvas that shows
   each line over its speaker's head.

Press **Play**. Every five to seven seconds, Pip says one of the two lines in a speech bubble.

## 4. Add a more specific rule (2 minutes)

Leave the game playing. Add a second rule to `Pip.dlg`, below the first, and save the file:

```dlg
rule Chatter
  concept TalkIdle
  say "Nice weather for standing around."
  say "Did you know I'm made of one rule file?"
  remember barks += 1

rule Hello
  concept TalkIdle
  if barks missing
  say "Oh! Hello there. I'm Pip."
  remember barks = 1
```

Unity reimports the file and the running game switches to the new rules straight away. This is
[hot reload](hot-reload.md), and it works for every rule file.

What happens now:

- Both rules answer `TalkIdle`. **Hello** has one more condition, `if barks missing` (the fact `barks` is 0 or has
  never been set), so while it matches it beats **Chatter**.
- When Hello plays, `remember barks = 1` writes a fact into **Pip's memory**. Now Hello no longer matches, so Chatter
  plays instead, and its `remember barks += 1` counts every line.

Stop and start Play Mode to hear Hello again: memory starts empty each session (unless you [save it](save-load.md)).

## 5. Use the memory (1 minute)

Add a third rule that only matches after a while:

```dlg
rule Tired
  concept TalkIdle
  if barks >= 5
  say "That's everything I know. Write me some more rules!"
  remember barks = 1
```

After a few lines, `barks` reaches 5. **Tired** and **Chatter** both match then, but Tired has two conditions
(`concept` and `barks`) against Chatter's one, so Tired wins and starts the count again.

That's the whole system: describe a situation with conditions, and the most specific description wins. General
rules are fallbacks, and you add specific rules on top whenever you want a character to notice more.

## 6. See why (1 minute)

With the game playing, open **Window > Dynamic Dialog > Debugger**. The **Lookups** tab lists every question a
speaker asked, newest first. Select one to see every fact in the question, every rule that was tested and the
condition that failed, and the winner. Pick a rule under **Why didn't this rule play?** for a one-line answer.
[Debugging](debugging.md) covers the rest of the window.

## 7. Compare with First Bark

In Package Manager, select Dynamic Dialog, open **Samples** and import **First Bark**. Open `FirstBark.unity`: it is
the scene you just built, with a nicer-looking Pip and comments in `FirstBark.dlg` explaining each rule.

## Where next

- [Concepts](concepts.md) explains facts, memory, scoring and followups properly.
- [The `.dlg` language](dlg-reference.md) lists every statement.
- **Robot Banter** and **Courtyard Quests** show conversations between characters, things characters see, areas,
  and rules that trigger game code.
