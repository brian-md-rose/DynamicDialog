using System;
using System.Collections.Generic;

namespace DynamicDialog
{
    /// <summary>
    /// Something in the game that can deliver lines: the Unity DialogSpeaker component, or a fake in tests.
    /// </summary>
    public interface IDialogSpeaker
    {
        DialogCharacter Character { get; }

        /// <summary>Whether the speaker can take a new line now (active, alive, not already speaking).</summary>
        bool IsAvailable { get; }

        /// <summary>Whether this speaker is close enough to hear <paramref name="source"/>, for "then any/all" followups.</summary>
        bool CanHear(IDialogSpeaker source);

        /// <summary>
        /// Start delivering a line. The speaker must later call
        /// <see cref="DialogConductor.LineFinished"/> with this line, including when it is stopped.
        /// </summary>
        void BeginLine(DialogLine line);

        /// <summary>Stop the current line now. The speaker should report it as not completed.</summary>
        void StopLine();
    }

    /// <summary>One line being delivered.</summary>
    public sealed class DialogLine
    {
        public readonly int Id;
        public readonly IDialogSpeaker Speaker;
        public readonly MatchResult Match;
        public readonly Symbol Concept;
        public readonly double StartTime;

        internal DialogLine(int id, IDialogSpeaker speaker, MatchResult match, Symbol concept, double startTime)
        {
            Id = id;
            Speaker = speaker;
            Match = match;
            Concept = concept;
            StartTime = startTime;
        }

        public Rule Rule => Match.Rule;
        public object Response => Match.Response;

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
        /// <summary>Fact a followup's receiver gets: who triggered it.</summary>
        public static readonly Symbol FromFact = Symbol.Of("from");

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

        public readonly DialogEngine Engine;

        /// <summary>If true, a followup sent to a named character needs that character to be in earshot too.</summary>
        public bool NamedFollowupsRequireEarshot;

        /// <summary>Raised after a line's facts are committed and the speaker has been told to start.</summary>
        public event Action<DialogLine> LineStarted;

        /// <summary>Raised when a line ends. The bool is false if it was stopped early.</summary>
        public event Action<DialogLine, bool> LineEnded;

        /// <summary>Raised when a speaker was asked for a concept and nothing matched. Useful for debug logs.</summary>
        public event Action<IDialogSpeaker, Symbol> NoMatch;

        public DialogConductor(DialogEngine engine)
        {
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public IReadOnlyList<IDialogSpeaker> Speakers => speakers;
        public int PendingFollowupCount => pending.Count;
        public IEnumerable<DialogLine> ActiveLines => active.Values;

        public void Register(IDialogSpeaker speaker)
        {
            if (speaker == null) throw new ArgumentNullException(nameof(speaker));
            if (!speakers.Contains(speaker)) speakers.Add(speaker);
        }

        public void Unregister(IDialogSpeaker speaker)
        {
            if (active.ContainsKey(speaker)) speaker.StopLine();
            active.Remove(speaker);
            speakers.Remove(speaker);
            pending.RemoveAll(p => p.Source == speaker);
        }

        public IDialogSpeaker FindSpeaker(Symbol who)
        {
            foreach (var s in speakers)
                if (s.Character.Who == who) return s;
            return null;
        }

        public bool IsSpeaking(IDialogSpeaker speaker) => active.ContainsKey(speaker);

        public DialogLine GetActiveLine(IDialogSpeaker speaker) => active.TryGetValue(speaker, out var l) ? l : null;

        /// <summary>
        /// Asks <paramref name="speaker"/> for a line about <paramref name="concept"/> and starts it if one matches.
        /// A busy speaker is skipped unless <paramref name="interrupt"/> is true, which stops its
        /// current line and drops that line's followups.
        /// </summary>
        public DialogLine TrySpeak(IDialogSpeaker speaker, Symbol concept, Action<Query> eventFacts = null, bool interrupt = false)
        {
            if (speaker == null) return null;
            bool speaking = active.ContainsKey(speaker);
            if (speaking && !interrupt) return null;
            if (!speaking && !speaker.IsAvailable) return null;

            var match = Engine.Ask(speaker.Character, concept, eventFacts);
            if (!match.Success)
            {
                NoMatch?.Invoke(speaker, concept);
                return null;
            }
            if (speaking) speaker.StopLine();
            return Start(speaker, match, concept);
        }

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
            if (!match.Success) return null;
            var speaker = candidateScratch[characterScratch.IndexOf(match.Speaker)];
            return Start(speaker, match, concept);
        }

        /// <summary>The speaker reports that a line has ended. <paramref name="completed"/> is false when it was stopped early.</summary>
        public void LineFinished(DialogLine line, bool completed)
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
            Action<Query> facts = q => q.Set(FromFact, source.Character.Who);

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
