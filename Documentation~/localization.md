# Localization

Rules are written once, in one language. Translation happens per **line**: each line has a key, and a **localizer**
on the Dialog Director turns keys into translated subtitles and voice clips while the game plays. Rules, conditions
and memory don't change between languages.

## Line keys

Every line's key is, in order of preference:

1. Its explicit key: `say "Hello." key GREET_1` in `.dlg`, a **Key** column in spreadsheets, or the line's **Key**
   field in the inspector.
2. Otherwise, its audio clip's name: `say "Hello." audio nick_hello_01` has the key `nick_hello_01`. Voice files are
   usually named after their line, so this is often all you need.
3. Otherwise, `<rule>_<n>`, the rule's name and the line's position counting from 1: the second line of rule
   `NickGreets` is `NickGreets_2`.

```dlg
rule NickGreets
  who Nick
  concept OnTalk
  say "Hey there." key NICK_HEY
  say "Yo." audio nick_yo_01
  say "Hi."
```

Here the keys are `NICK_HEY`, `nick_yo_01` and `NickGreets_3`.

> **Give shipped lines stable keys.** `<rule>_<n>` keys change when you rename a rule or reorder its lines, which
> disconnects them from their translations. Before you send lines to translators, give them explicit keys or audio
> names.

## Exporting text for translators

Select a rule database and click **Export String Table CSV…** in its inspector. The CSV has one row per line of text,
with columns **Key**, **Source** (the authored text), **Character**, **Rule** and **Concept**. The last three give
translators context. Import the file into your translation tool, or into a Unity Localization string table, keyed by
the **Key** column.

## With Unity Localization

If the **Localization** package (`com.unity.localization`) is installed:

1. Set up Localization as usual (**Edit > Project Settings > Localization**), with your locales.
2. Create a **String Table Collection** for subtitles, named `Dialog` (or anything else), with an entry per line key.
3. Optionally, create an **Asset Table Collection** of translated voice clips, with the same keys.
4. Add **Dynamic Dialog > Unity Localization Localizer** to the Dialog Director's GameObject. Set **String Table** to
   the string table collection's name, and **Audio Table** to the asset table's name (leave it empty to keep the
   authored clips).

Speakers now show the current locale's text and play its clips. A line with no entry, or an empty one, falls back to
its authored text and clip, so you can translate as you go. The localizer looks entries up when each line starts, so
switching the locale affects the next line. It reads tables synchronously; set the tables to preload to avoid a pause
on the first line.

## Your own localizer

For any other translation system, implement `IDialogLocalizer`. Return `null` to keep the authored text or clip.

```csharp
public class MyLocalizer : MonoBehaviour, IDialogLocalizer
{
    readonly Dictionary<string, string> texts = new Dictionary<string, string>();

    public void SetText(string key, string text) => texts[key] = text;

    public string GetText(string key, DialogLineDefinition line) =>
        texts.TryGetValue(key, out var text) ? text : null;

    public AudioClip GetClip(string key, DialogLineDefinition line) => null;   // keep the authored clip
}
```

Put the component on the Dialog Director's GameObject, where the director finds it when it starts, or assign one from
code with `DialogDirector.Instance.Localizer = myLocalizer;`.

## What is localized

The localizer changes what speakers show and play: the speaker's `CurrentText` and `CurrentClip`, its **On
Subtitle** event, and the `Text` every [presenter](presenters.md) receives. A line's duration follows the translated
clip, or the translated text's length when there's no clip and no set duration.

Rule names, concepts, facts and game event arguments are never translated: they are identifiers, not text.
