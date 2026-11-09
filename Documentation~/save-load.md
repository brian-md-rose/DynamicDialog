# Saving and loading

The Dialog Director can save everything the dialog remembers and restore it later, so `once` rules stay used,
cooldowns carry over, and characters remember what they were told.

## Saving to a file

```csharp
public class DialogSaves : MonoBehaviour
{
    string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "dialog.json");

    public void Save() => System.IO.File.WriteAllText(SavePath, DialogDirector.Instance.SaveStateJson());

    public void Load()
    {
        if (System.IO.File.Exists(SavePath))
            DialogDirector.Instance.LoadStateJson(System.IO.File.ReadAllText(SavePath));
    }
}
```

`SaveState()` returns a serializable `DialogSaveData` instead, if you'd rather put it inside your own save file or
use your own serializer; `LoadState(data)` restores it.

## What is saved

| Saved | Not saved |
|---|---|
| World memory, and every character's memory, including how long expiring facts have left | Lines in progress and scheduled followups (loading stops them) |
| Each rule's state: whether a `once` rule has played, how often it played, and how long ago (for cooldowns) | Facts from fact providers and Static Facts, which describe the game as it is |
| States for region databases that aren't loaded right now; they are applied when the region loads | Variation order (`shuffle`, `sequential`) |

Names are saved as text, so a save works across sessions and builds. After a load, every
[Dialog Fact Listener](components.md#dialog-fact-listener) sends its fact's current value again, and
`Engine.FactsReset` is raised once.

## Saves that keep working as the game changes

### Versions

Each save records its format version (`DialogSaveData.CurrentVersion`, currently 2). Older saves, including 0.6 saves
(version 1, or 0 if no version was written), are upgraded when they load. A save from a newer version of the package
loads as far as this version understands it, with a warning. Loading never throws over an old or half-filled save.

### Renamed rules

Rule state is saved under the rule's name. When you rename a rule, list its old names, and saved games keep its
`once` and cooldown state:

```dlg
rule NurseThanksForGlasses
  renamed NurseThanks
  who Nurse
  concept OnGiveGlasses
  say "Thank you so much."
  once
```

Spreadsheets have a **Renamed** column, inspector rules a **Former Names** list, and the Rule Editor a **Renamed from**
field (and a **Keep Saves** button after you rename a `once` or cooldown rule). The Rule Validator warns when a former
name is still another rule's name, or when two rules claim the same one.

### Removed rules

A saved state that matches no rule (the rule was removed, or renamed without `renamed`) isn't applied, and isn't
dropped either. It is kept and written into later saves, so it applies again if the rule comes back or gets a
`renamed` line. The director logs a warning naming each one. `DialogDirector.DiscardUnmatchedRuleStates()` drops them
once you're sure they're gone.

Memory is stored by character and fact name, not by rule, so renaming or removing rules never affects it.

### Renaming a database

Saves find rules by set name (the database asset's name) and rule name. Renaming a `.dlg` or spreadsheet file
changes its set name, and its rules' saved states then wait, unapplied, for a database with the old name. Keep
database asset names stable once a game has shipped.

## Load reports

`DialogDirector.LastLoadReport` says what the last load did:

```csharp
var director = DialogDirector.Instance;
director.LoadStateJson(json);
DialogLoadReport report = director.LastLoadReport;
if (report.UnmatchedRules.Count > 0)
    Debug.Log("Saved rule states with no rule: " + string.Join(", ", report.UnmatchedRules));
```

It has `SavedVersion`, `Upgraded`, `FromNewerVersion`, `RenamedRules`, `UnmatchedRules`, `WaitingRules` (states for
databases that aren't loaded yet; updated as regions load), `HasWarnings`, and a readable `ToString()`. Without the
director, `engine.LoadState(data, out DialogLoadReport report)` does the same.
