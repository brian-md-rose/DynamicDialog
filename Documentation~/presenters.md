# Showing lines and custom presenters

A **presenter** shows lines to the player. The package has two, the speech bubbles and the subtitle bar, and you can
write your own. Every line any [Dialog Speaker](components.md#dialog-speaker) plays goes to every enabled presenter.

## The built-in presenters

Add them from the **GameObject > Dynamic Dialog** menu:

- **Speech Bubbles**: a bubble over each speaker's head (or at its [Dialog Bubble Anchor](components.md#dialog-bubble-anchor)),
  with a tail pointing at the speaker. Bubbles fade in and out, size themselves to the text, move apart when speakers
  overlap, and stay on screen when a speaker walks past the edge.
- **Subtitle Bar**: a row per line along the bottom of the screen, with the speaker's colored name.

Both are prefabs with their own screen-space canvas, built with uGUI and TextMeshPro. Both reveal text with a
typewriter that speeds up when needed so the whole line is visible before it ends. Their settings are in
[Components](components.md#dialog-speech-bubbles).

Each presenter shows every line, so a scene with both shows each line twice. Use one, or turn one on and off for
different situations (the **Dialog UI** sample switches between them).

### Restyling

Don't edit the package's prefabs: changes there are lost when the package updates. Instead:

1. In the Project window, find `Packages/Dynamic Dialog/Runtime/Presentation/Prefabs`.
2. Right-click **Dialog Speech Bubble** (the single bubble) and choose **Create > Prefab Variant**. Move the variant
   into your `Assets` folder.
3. Change the variant: its sprites, colors, TextMeshPro font and size, **Padding**, **Min Width** and **Max Width**,
   and fade times.
4. Drag the variant into the **Bubble Prefab** field of your **Dialog Speech Bubbles**.

The subtitle bar is restyled the same way: make a variant of **Dialog Subtitle Bar** and use it instead of the
original. The **Dialog UI** sample includes a restyled bubble.

For a bubble that is arranged differently, derive from `DialogSpeechBubble` and override `Layout()` (size and place
the parts) or `PointTailAt(target)`. `Show`, `Hide` and `Tick` are virtual too.

## Writing a presenter

A presenter implements `IDialogPresenter`, which has three methods. Each line produces `OnLineStarted`, then exactly
one of `OnLineEnded` (it played to the end) or `OnLineInterrupted` (it was cut off by another line, stopped with
`StopLine`, or its speaker was disabled or destroyed).

The easy way is to derive a component from `DialogPresenterBehaviour`, which registers itself while it is enabled.
`OnLineInterrupted` calls `OnLineEnded` unless you override it.

```csharp
public class Captions : DialogPresenterBehaviour
{
    public TMP_Text label;
    DialogSpeaker showing;

    public override void OnLineStarted(DialogPresentedLine line)
    {
        if (!line.HasText) return;   // an audio-only line
        showing = line.Speaker;
        label.text = line.SpeakerName + ": " + line.Text;
    }

    public override void OnLineEnded(DialogPresentedLine line)
    {
        if (line.Speaker != showing) return;   // someone else's newer line is showing
        showing = null;
        label.text = "";
    }
}
```

(This needs `using TMPro;` and `using DynamicDialog;`.) If you override `OnEnable` or `OnDisable`, call the base
method, which registers and unregisters the presenter.

### What a presenter is told

`DialogPresentedLine` is a small struct, passed to all three methods with the same values:

| Member | What it is |
|---|---|
| `Speaker` | The `DialogSpeaker` saying the line. |
| `SpeakerName` | The speaker's **Who** name. |
| `Text` | The line's subtitle, already [localized](localization.md). Empty for audio-only lines. |
| `HasText` | Whether there is text to show. |
| `StartTime` | When the line started, in `Time.timeAsDouble` seconds. |
| `Duration` | How long the line lasts, in seconds. |
| `Elapsed(now)` | Seconds since the line started. |
| `Line` | The `DialogLine`: the rule that won, the concept. |
| `Variation` | The `DialogLineDefinition` that was picked: its clip, animator trigger and key. |

A speaker says one line at a time, so `Speaker` together with `StartTime` identifies a line.

### Presenters that aren't components

Any class can implement `IDialogPresenter`. Register it with `DialogPresenters.Register` and remove it with
`DialogPresenters.Unregister`:

```csharp
public class ConsoleLog : IDialogPresenter
{
    public void OnLineStarted(DialogPresentedLine line) => Debug.Log(line.SpeakerName + ": " + line.Text);

    public void OnLineEnded(DialogPresentedLine line) { }

    public void OnLineInterrupted(DialogPresentedLine line) => Debug.Log(line.SpeakerName + " was cut off.");
}
```

```csharp
var log = new ConsoleLog();
DialogPresenters.Register(log);
// ...
DialogPresenters.Unregister(log);
```

Registered presenters are cleared when Play Mode starts, so register them from a component's `OnEnable` or `Start`.

### Rules presenters can rely on

- A presenter that registers while lines are playing gets `OnLineStarted` for each of them straight away, so it
  never misses a line that's already showing.
- A presenter that throws an exception is logged, and the others still get the line.
- `DialogPresenters.Playing` lists the lines playing now, oldest first. `DialogPresenters.All` lists the presenters.
- Game events (`do`) that start with the line are raised just before presenters hear about it.

### Reusing the built-in logic

The built-in presenters' logic is public, so your UI can behave the same way:

- **`DialogTypewriter`**: how many characters to show at a given time, speeding up so the line finishes by
  `finishBy` of its duration. Serializable, so it shows in the inspector.
- **`DialogOverlap.SelectVisible`** picks which lines to show under *Stack* or *Replace*, and
  **`DialogOverlap.SeparateVertically`** moves overlapping rectangles apart.
- **`DialogScreenPlacement.Place`** keeps a bubble on screen and says where its tail should point, including for a
  speaker behind the camera. `IsOnScreen` and `EdgePoint` help with the same.

A typewriter caption:

```csharp
public class TypedCaption : DialogPresenterBehaviour
{
    public TMP_Text label;
    public DialogTypewriter typewriter = new DialogTypewriter();
    DialogPresentedLine current;
    bool showing;

    public override void OnLineStarted(DialogPresentedLine line)
    {
        current = line;
        showing = line.HasText;
        label.text = line.Text;
        label.maxVisibleCharacters = 0;
    }

    public override void OnLineEnded(DialogPresentedLine line)
    {
        if (line.Speaker != current.Speaker || line.StartTime != current.StartTime) return;
        showing = false;
        label.text = "";
    }

    void Update()
    {
        if (!showing) return;
        int characters = label.textInfo.characterCount;   // without rich-text tags
        label.maxVisibleCharacters = typewriter.VisibleCharacters(characters, current.Elapsed(Time.timeAsDouble), current.Duration);
    }
}
```

The **Dialog UI** sample's `ChatLogPresenter.cs` is a complete presenter: a chat log that strikes out interrupted lines.

## Other ways to react to lines

Presenters are for showing lines. For other reactions, such as lip sync or gestures on one character, use an
`IDialogLineHandler` component next to the speaker, the speaker's **On Subtitle** and **On Line Ended** UnityEvents,
or the static `DialogSpeaker.AnyLineStarted` and `AnyLineEnded` events. See [Scripting](scripting.md#reacting-to-lines).
