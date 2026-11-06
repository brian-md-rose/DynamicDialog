using System;
using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>
    /// Something in the game that can deliver lines: the Unity DialogSpeaker component, or a fake in tests.
    /// </summary>
    public interface IDialogSpeaker
    {
        /// <summary>The engine character this speaker talks as.</summary>
        DialogCharacter Character { get; }

        /// <summary>Whether the speaker can take a new line now (active, alive, not already speaking).</summary>
        bool IsAvailable { get; }

        /// <summary>Whether this speaker is close enough to hear <paramref name="source"/>, for "then any/all" followups.</summary>
        bool CanHear(IDialogSpeaker source);

        /// <summary>
        /// Start delivering a line. The speaker must later call
        /// <see cref="DialogConductor.EndLine"/> with this line, including when it is stopped.
        /// </summary>
        void BeginLine(DialogLine line);

        /// <summary>Stop the current line now. The speaker should report it as not completed.</summary>
        void StopLine();
    }

    /// <summary>One line being delivered.</summary>
    public sealed class DialogLine
    {
        /// <summary>Unique, increasing ID for this line.</summary>
        public readonly int Id;
        /// <summary>Who is saying it.</summary>
        public readonly IDialogSpeaker Speaker;
        /// <summary>The lookup that produced it.</summary>
        public readonly MatchResult Match;
        /// <summary>The concept the speaker was asked for.</summary>
        public readonly Symbol Concept;
        /// <summary>Game time it started.</summary>
        public readonly double StartTime;

        internal DialogLine(int id, IDialogSpeaker speaker, MatchResult match, Symbol concept, double startTime)
        {
            Id = id;
            Speaker = speaker;
            Match = match;
            Concept = concept;
            StartTime = startTime;
        }

        /// <summary>The winning rule.</summary>
        public Rule Rule => Match.Rule;
        /// <summary>The winning rule's payload.</summary>
        public object Response => Match.Response;

        /// <summary>"#id speaker: rule (concept)".</summary>
        public override string ToString() => $"#{Id} {Speaker.Character}: {Rule.Name} ({Concept})";
    }

    /// <summary>
    /// Runs conversations: asks speakers for lines, starts them, and when a line finishes,
    /// schedules its followups. Each followup is evaluated when it is due, never earlier
    /// (talk slides 69-79). A stopped line drops its followups, so interrupting a speaker
    /// ends the conversation. Needs no Unity; call <see cref="Update"/> once per frame.
    /// </summary>
    public sealed class DialogConductor
    {
        /// <summary>Old name of <see cref="WellKnownFacts.From"/>.</summary>
        [Obsolete("Use WellKnownFacts.From.")]
        public static readonly Symbol FromFact = WellKnownFacts.From;

        struct Pending
        {
            public double Due;
            public IDialogSpeaker Source;
            public Followup Followup;
        }

        readonly List<IDialogSpeaker> speakers = new List<IDialogSpeaker>();
        readonly Dictionary<IDialogSpeaker, DialogLine> active = new Dictionary<IDialogSpeaker, DialogLine>();
        readonly List<Pending> pending = new List<Pending>();
        readonly List<Pending> dueScratch = new List<Pending>();
        readonly List<IDialogSpeaker> candidateScratch = new List<IDialogSpeaker>();
        readonly List<DialogCharacter> characterScratch = new List<DialogCharacter>();
        int nextLineId = 1;

        /// <summary>The engine lines are looked up in.</summary>
        public readonly DialogEngine Engine;

        /// <summary>If true, a followup sent to a named character needs that character to be in earshot too.</summary>
        public bool NamedFollowupsRequireEarshot { get; set; }

        /// <summary>Raised after a line's facts are committed and the speaker has been told to start.</summary>
        public event Action<DialogLine> LineStarted;

        /// <summary>Raised when a line ends. The bool is false if it was stopped early.</summary>
        public event Action<DialogLine, bool> LineEnded;

        /// <summary>Raised when a speaker was asked for a concept and nothing matched. Useful for debug logs.</summary>
        public event Action<IDialogSpeaker, Symbol> NoMatch;

        /// <summary>Creates a conductor for <paramref name="engine"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        public DialogConductor(DialogEngine engine)
        {
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        /// <summary>Every registered speaker.</summary>
        public IReadOnlyList<IDialogSpeaker> Speakers => speakers;
        /// <summary>How many followups are scheduled and not yet sent.</summary>
        public int PendingFollowupCount => pending.Count;
        /// <summary>Lines playing now.</summary>
        public IEnumerable<DialogLine> ActiveLines => active.Values;

        /// <summary>Adds a speaker, so followups can reach it. Registering twice has no effect.</summary>
        public void Register(IDialogSpeaker speaker)
        {
            if (speaker == null) throw new ArgumentNullException(nameof(speaker));
            if (!speakers.Contains(speaker)) speakers.Add(speaker);
        }

        /// <summary>Removes a speaker: stops its line and drops followups it sent.</summary>
        public void Unregister(IDialogSpeaker speaker)
        {
            if (active.ContainsKey(speaker)) speaker.StopLine();
            active.Remove(speaker);
            speakers.Remove(speaker);
            pending.RemoveAll(p => p.Source == speaker);
        }

        /// <summary>The first registered speaker with this name, or null.</summary>
        public IDialogSpeaker FindSpeaker(Symbol who)
        {
            foreach (var s in speakers)
                if (s.Character.Who == who) return s;
            return null;
        }

        /// <summary>Whether the speaker has a line playing.</summary>
        public bool IsSpeaking(IDialogSpeaker speaker) => active.ContainsKey(speaker);

        /// <summary>The speaker's current line, or null.</summary>
        public DialogLine GetActiveLine(IDialogSpeaker speaker) => active.TryGetValue(speaker, out var l) ? l : null;

        /// <summary>
        /// Asks <paramref name="speaker"/> for a line about <paramref name="concept"/> and starts it if one matches.
        /// A busy speaker is skipped unless <paramref name="interrupt"/> is true, which stops its
        /// current line and drops that line's followups.
        /// </summary>
        /// <param name="speaker">Who to ask.</param>
        /// <param name="concept">What happened.</param>
        /// <param name="eventFacts">Sets facts about this event. May be null.</param>
        /// <param name="interrupt">Stop the speaker's current line if a new one matches.</param>
        /// <returns>The line that started, or null.</returns>
        public DialogLine TrySpeak(IDialogSpeaker speaker, Symbol concept, Action<Query> eventFacts = null, bool interrupt = false)
        {
            if (speaker == null) return null;
            bool speaking = active.ContainsKey(speaker);
            if (speaking && !interrupt) return null;
            if (!speaking && !speaker.IsAvailable) return null;

            var match = Engine.Ask(speaker.Character, concept, eventFacts);
            if (!match.Matched)
            {
                NoMatch?.Invoke(speaker, concept);
                return null;
            }
            if (speaking) speaker.StopLine();
            return Start(speaker, match, concept);
        }

        /// <summary>Asks a speaker for a line and starts it if one matches. See <see cref="TrySpeak(IDialogSpeaker, Symbol, Action{Query}, bool)"/>.</summary>
        public DialogLine TrySpeak(IDialogSpeaker speaker, string concept, Action<Query> eventFacts = null, bool interrupt = false) =>
            TrySpeak(speaker, Symbol.Of(concept), eventFacts, interrupt);

        /// <summary>Asks every available candidate and starts only the best reply.</summary>
        public DialogLine TrySpeakBest(IEnumerable<IDialogSpeaker> candidates, Symbol concept, Action<Query> eventFacts = null)
        {
            characterScratch.Clear();
            candidateScratch.Clear();
            foreach (var c in candidates)
            {
                if (c == null || active.ContainsKey(c) || !c.IsAvailable) continue;
                candidateScratch.Add(c);
                characterScratch.Add(c.Character);
            }
            var match = Engine.AskBest(characterScratch, concept, eventFacts);
            if (!match.Matched) return null;
            var speaker = candidateScratch[characterScratch.IndexOf(match.Speaker)];
            return Start(speaker, match, concept);
        }

        /// <summary>
        /// The speaker reports that a line has ended, whether it played to the end or was stopped.
        /// Followups are scheduled only when <paramref name="completed"/> is true. Stale or repeated reports are ignored.
        /// </summary>
        /// <param name="line">The line passed to <see cref="IDialogSpeaker.BeginLine"/>.</param>
        /// <param name="completed">False when the line was stopped early.</param>
        public void EndLine(DialogLine line, bool completed)
        {
            if (line == null) return;
            if (!active.TryGetValue(line.Speaker, out var current) || current != line) return; // stale or duplicate
            active.Remove(line.Speaker);

            if (completed)
            {
                double now = Engine.Clock.Now;
                foreach (var f in line.Rule.Followups)
                    pending.Add(new Pending { Due = now + Math.Max(0f, f.Delay), Source = line.Speaker, Followup = f });
            }
            LineEnded?.Invoke(line, completed);
        }

        /// <summary>Old name of <see cref="EndLine"/>.</summary>
        [Obsolete("Use EndLine.")]
        public void LineFinished(DialogLine line, bool completed) => EndLine(line, completed);

        /// <summary>Sends every followup that is due. Call once per frame.</summary>
        public void Update()
        {
            if (pending.Count == 0) return;
            double now = Engine.Clock.Now;

            dueScratch.Clear();
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].Due <= now)
                {
                    dueScratch.Add(pending[i]);
                    pending.RemoveAt(i);
                }
            }
            // Oldest first, so followups from the same line keep their authored order.
            for (int i = dueScratch.Count - 1; i >= 0; i--) Dispatch(dueScratch[i]);
        }

        /// <summary>Drops every scheduled followup, e.g. on a cutscene or level change.</summary>
        public void ClearPendingFollowups() => pending.Clear();

        /// <summary>Stops every line that is playing.</summary>
        public void StopAll()
        {
            foreach (var s in new List<IDialogSpeaker>(active.Keys)) s.StopLine();
        }

        void Dispatch(in Pending p)
        {
            var source = p.Source;
            var f = p.Followup;
            Action<Query> facts = q => q.Set(WellKnownFacts.From, source.Character.Who);

            switch (f.Target)
            {
                case FollowupTarget.Self:
                    TrySpeak(source, f.Concept, facts);
                    break;

                case FollowupTarget.Character:
                    var target = FindSpeaker(f.Character);
                    if (target == null) break;
                    if (NamedFollowupsRequireEarshot && target != source && !target.CanHear(source)) break;
                    TrySpeak(target, f.Concept, facts);
                    break;

                case FollowupTarget.Any:
                    TrySpeakBest(Listeners(source), f.Concept, facts);
                    break;

                case FollowupTarget.All:
                    foreach (var listener in Listeners(source)) TrySpeak(listener, f.Concept, facts);
                    break;
            }
        }

        /// <summary>Speakers other than <paramref name="source"/> that can hear it. Returns a new list.</summary>
        public List<IDialogSpeaker> Listeners(IDialogSpeaker source)
        {
            var result = new List<IDialogSpeaker>();
            foreach (var s in speakers)
                if (s != source && s.CanHear(source)) result.Add(s);
            return result;
        }

        DialogLine Start(IDialogSpeaker speaker, MatchResult match, Symbol concept)
        {
            Engine.Commit(match);
            var line = new DialogLine(nextLineId++, speaker, match, concept, Engine.Clock.Now);
            active[speaker] = line;
            // Raise before BeginLine: a zero-length line may finish inside BeginLine,
            // and listeners should always see the start before the finish.
            LineStarted?.Invoke(line);
            speaker.BeginLine(line);
            return line;
        }
    }
}
