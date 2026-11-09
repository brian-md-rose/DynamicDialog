# Dialog UI

The package's two built-in presenters, a restyled speech bubble, and a custom presenter of your own, all showing the
same conversation. Open `DialogUI.unity` and press Play. Ada, Bo and Cy chat, and sometimes talk over each other.

| Key | Shows |
|---|---|
| **1** | **Dialog Speech Bubbles**: bubbles that follow each speaker, with a typewriter reveal. Overlapping bubbles are moved apart. |
| **2** | **Dialog Subtitle Bar**: a bar at the bottom of the screen with the speaker's name, up to three rows. |
| **3** | **Chat log**: `ChatLogPresenter.cs`, a custom presenter written for this sample. |
| **4** | All three at once. Each presenter shows every line. |
| **Space** | Cy shouts, interrupting Cy's current line. Watch the bubble fade quickly and the chat log strike the line out. |

## Restyling the built-in UI

The bubbles here use `Restyled Speech Bubble.prefab`, a prefab variant of the package's **Dialog Speech Bubble**
with different colors and fonts. To restyle your own, create a variant (or a copy) of
`Packages/Dynamic Dialog/Runtime/Presentation/Prefabs/Dialog Speech Bubble.prefab`, change it, and drag it into the
**Bubble Prefab** field of **Dialog Speech Bubbles**. The **Dialog Subtitle Bar** prefab is restyled the same way.
Both components have settings for the typewriter speed, what happens when speakers overlap, and fade times.

## Writing your own presenter

A presenter is anything that implements `IDialogPresenter`: it is told when each line starts, ends or is
interrupted, with the speaker, the localized text, the start time and the duration (`DialogPresentedLine`).
The easiest way is to derive from `DialogPresenterBehaviour`, which registers itself while enabled:

```csharp
public class MyCaptions : DialogPresenterBehaviour
{
    public override void OnLineStarted(DialogPresentedLine line) { /* show line.SpeakerName and line.Text */ }
    public override void OnLineEnded(DialogPresentedLine line) { /* hide it */ }
    public override void OnLineInterrupted(DialogPresentedLine line) { /* hide it faster */ }
}
```

`ChatLogPresenter.cs` is a complete example. A plain C# class can implement `IDialogPresenter` and call
`DialogPresenters.Register(presenter)` instead. `PresenterShowcase.cs` switches presenters by enabling and
disabling their components.

## Requirements

uGUI and TextMeshPro (`com.unity.ugui`, plus `com.unity.textmeshpro` before Unity 6). If TMP Essential Resources
aren't in the project yet, importing this sample adds them. The number keys use the Input System package when it's
installed and the old Input Manager otherwise. Works with any render pipeline.
