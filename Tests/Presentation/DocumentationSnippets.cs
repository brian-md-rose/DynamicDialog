// The manual's TextMeshPro presenter examples (Documentation~/presenters.md), compiled with the presentation tests.
// DocumentationTests (Tests/Editor) checks that the manual's C# blocks still match this code, ignoring whitespace.
using TMPro;
using UnityEngine;

namespace DynamicDialog.Presentation.Tests.Docs
{
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
}
