# First Bark

The smallest Dynamic Dialog scene: one character, one rule file. Open `FirstBark.unity` and press Play.
Every few seconds Pip says something in a speech bubble.

## What's in the scene

| Object | Components | What it does |
|---|---|---|
| **Dialog Director** | Dialog Director | Loads `FirstBark.dlg` (see its **Databases** list) and answers every "what should I say?" question. |
| **Pip** | Dialog Speaker, Simple Body | The character. **Idle Talk** is on, so the speaker asks for the `TalkIdle` concept every few seconds. Simple Body only draws the capsule. |
| **Dialog Speech Bubbles** | Dialog Speech Bubbles | The package's speech bubble prefab (**GameObject > Dynamic Dialog > Speech Bubbles**). It shows each line over its speaker. |
| Main Camera, Directional Light | | |

## What's in the rules

`FirstBark.dlg` has three rules for the `TalkIdle` concept:

- **Hello** checks `barks missing`, so it wins the first time and sets `barks = 1` in Pip's memory.
- **Chatter** has no conditions besides the concept. It plays when nothing more specific matches, picks one of its
  lines at random, and adds 1 to `barks`.
- **Tired** checks `barks >= 5`, so after a few lines it beats Chatter and resets the count.

The most specific matching rule always wins: that is the whole idea. Save `FirstBark.dlg` while the scene is
playing and Pip uses your edits straight away. Open **Window > Dynamic Dialog > Debugger** to see each lookup and
why every rule did or didn't play.

## Requirements

Speech bubbles use uGUI and TextMeshPro (`com.unity.ugui`, plus `com.unity.textmeshpro` before Unity 6). If TMP
Essential Resources aren't in the project yet, importing this sample adds them. Works with any render pipeline.
