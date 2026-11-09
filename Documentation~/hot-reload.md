# Hot reload

Writers write better when they hear their lines straight away. Dynamic Dialog swaps edited rules into the running
game without stopping Play Mode.

## How to use it

1. Press **Play**.
2. Edit a `.dlg`, `.dlgcsv` or `.dlgtsv` file in any editor, or in the [Rule Editor](rule-editor.md), and save it.
3. Unity reimports the file, and the director switches to the new rules. The next lookup uses them.

Edits to a **Rule Database** asset in the inspector also apply at once.

With an external text editor, Unity reimports when it notices the change, which is usually when the Unity window
gets focus again. If nothing happens, check that **Auto Refresh** is on (**Edit > Preferences > Asset Pipeline**), or
press **Ctrl+R** (**Cmd+R** on macOS) in Unity.

Turn on **Log Lines** on the Dialog Director to get a Console message each time rules are reloaded.

## What changes and what doesn't

| Kept | Reset or replaced |
|---|---|
| All memory: world, character and expiring facts | The reloaded file's rules |
| Lines already playing, and scheduled followups | The reloaded file's `once`, cooldown and variation order: a `once` rule from that file can play again |
| Other loaded databases, untouched | |
| [Saved rule states](save-load.md) waiting for a rule, which are applied again, so adding a `renamed` line picks up old state |  |

To hear a `once` rule again without editing it, stop and start Play Mode, or clear the memory facts it checks.

## Mistakes while reloading

A statement with an error is skipped, and the Console names the file and line. The rest of the file still loads,
so a typo doesn't take the whole file's dialog away mid-session. Fix it and save again.

## Where it works

Hot reload is an editor feature: it runs in Play Mode in the Unity editor, for any database the director has loaded,
from its **Databases** list or from a region. Player builds use the rules they were built with.
