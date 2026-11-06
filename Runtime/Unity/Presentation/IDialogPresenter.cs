namespace DynamicDialog
{
    /// <summary>
    /// Shows dialog lines to the player: a speech bubble, a subtitle bar, or your own UI.
    /// Register one with <see cref="DialogPresenters.Register"/>, or derive from
    /// <see cref="DialogPresenterBehaviour"/>, which registers itself while enabled.
    /// </summary>
    /// <remarks>
    /// Every line any <see cref="DialogSpeaker"/> plays reaches every registered presenter: first
    /// <see cref="OnLineStarted"/>, then exactly one of <see cref="OnLineEnded"/> (the line played to the end)
    /// or <see cref="OnLineInterrupted"/> (it was stopped early). Lines with no text (audio only) are passed
    /// on too; check <see cref="DialogPresentedLine.HasText"/>. A presenter that registers while lines are
    /// already playing gets <see cref="OnLineStarted"/> for each of them straight away.
    /// </remarks>
    public interface IDialogPresenter
    {
        /// <summary>A line started. Show it.</summary>
        /// <param name="line">The line, its speaker and its localized text.</param>
        void OnLineStarted(DialogPresentedLine line);

        /// <summary>A line played to the end. Hide it.</summary>
        /// <param name="line">The same value <see cref="OnLineStarted"/> received.</param>
        void OnLineEnded(DialogPresentedLine line);

        /// <summary>
        /// A line was stopped early: interrupted by another line, <see cref="DialogSpeaker.StopLine"/>,
        /// or its speaker was disabled or destroyed. Hide it, usually faster than <see cref="OnLineEnded"/>.
        /// </summary>
        /// <param name="line">The same value <see cref="OnLineStarted"/> received.</param>
        void OnLineInterrupted(DialogPresentedLine line);
    }

    /// <summary>A line as presenters see it: who says it, what they say, and when.</summary>
    public readonly struct DialogPresentedLine
    {
        /// <summary>Creates a presented line. The package makes these for you; tests and custom speakers can make their own.</summary>
        /// <param name="speaker">Who says it.</param>
        /// <param name="line">The engine's line.</param>
        /// <param name="variation">The chosen variation, or null.</param>
        /// <param name="text">The subtitle text, after localization. May be null.</param>
        /// <param name="startTime">When it started, in <see cref="UnityEngine.Time.timeAsDouble"/> seconds.</param>
        /// <param name="duration">How long it lasts, in seconds.</param>
        public DialogPresentedLine(DialogSpeaker speaker, DialogLine line, DialogLineDefinition variation,
                                   string text, double startTime, float duration)
        {
            Speaker = speaker;
            Line = line;
            Variation = variation;
            Text = text;
            StartTime = startTime;
            Duration = duration;
        }

        /// <summary>Who says it.</summary>
        public DialogSpeaker Speaker { get; }
        /// <summary>The engine's line: its rule, concept and followups.</summary>
        public DialogLine Line { get; }
        /// <summary>The chosen variation, or null when the rule's payload isn't a <see cref="DialogResponse"/>.</summary>
        public DialogLineDefinition Variation { get; }
        /// <summary>The subtitle text, after localization. Null or empty for audio-only lines.</summary>
        public string Text { get; }
        /// <summary>When the line started, in <see cref="UnityEngine.Time.timeAsDouble"/> seconds.</summary>
        public double StartTime { get; }
        /// <summary>How long the line lasts, in seconds (its set duration, its clip's length, or an estimate from the text).</summary>
        public float Duration { get; }

        /// <summary>Whether there is subtitle text to show.</summary>
        public bool HasText => !string.IsNullOrEmpty(Text);

        /// <summary>The speaker's name in rules (its <c>who</c>), for name labels. Empty if the speaker is gone.</summary>
        public string SpeakerName => Speaker != null ? Speaker.Who.Name : string.Empty;

        /// <summary>Seconds since the line started, at <paramref name="now"/> (<see cref="UnityEngine.Time.timeAsDouble"/>).</summary>
        public float Elapsed(double now) => (float)(now - StartTime);
    }
}
