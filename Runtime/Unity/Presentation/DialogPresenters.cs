using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// The registered <see cref="IDialogPresenter"/>s. Every <see cref="DialogSpeaker"/> line is passed to each of them.
    /// <code>
    /// DialogPresenters.Register(myPresenter);   // starts receiving lines
    /// DialogPresenters.Unregister(myPresenter);
    /// </code>
    /// </summary>
    /// <remarks>
    /// A presenter that throws is logged and doesn't stop the others. Presenters are called after the
    /// speaker's <see cref="IDialogLineHandler"/>s and <see cref="DialogSpeaker.AnyLineStarted"/> /
    /// <see cref="DialogSpeaker.AnyLineEnded"/>, and before the line's followups start.
    /// Registrations are cleared when Play Mode starts.
    /// </remarks>
    public static class DialogPresenters
    {
        static readonly List<IDialogPresenter> presenters = new List<IDialogPresenter>();
        static readonly Dictionary<DialogLine, DialogPresentedLine> playing = new Dictionary<DialogLine, DialogPresentedLine>();
        static readonly List<DialogPresentedLine> playingInOrder = new List<DialogPresentedLine>();

        /// <summary>The registered presenters, in registration order.</summary>
        public static IReadOnlyList<IDialogPresenter> All => presenters;

        /// <summary>The lines playing now, oldest first.</summary>
        public static IReadOnlyList<DialogPresentedLine> Playing => playingInOrder;

        /// <summary>
        /// Starts passing lines to <paramref name="presenter"/>. It is told about lines already playing at once.
        /// Registering twice does nothing.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="presenter"/> is null.</exception>
        public static void Register(IDialogPresenter presenter)
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (presenters.Contains(presenter)) return;
            presenters.Add(presenter);
            foreach (var line in playingInOrder.ToArray()) Call(presenter, line, Kind.Started);
        }

        /// <summary>Stops passing lines to <paramref name="presenter"/>. Returns false if it wasn't registered.</summary>
        public static bool Unregister(IDialogPresenter presenter) => presenter != null && presenters.Remove(presenter);

        /// <summary>Whether <paramref name="presenter"/> is registered.</summary>
        public static bool IsRegistered(IDialogPresenter presenter) => presenter != null && presenters.Contains(presenter);

        // ---- Called by DialogSpeaker -----------------------------------------------------------

        internal static void LineStarted(DialogSpeaker speaker, DialogLine line, DialogLineDefinition variation,
                                         string text, double startTime, float duration)
        {
            if (line == null) return;
            var presented = new DialogPresentedLine(speaker, line, variation, text, startTime, duration);
            if (playing.ContainsKey(line)) playingInOrder.RemoveAll(p => p.Line == line);
            playing[line] = presented;
            playingInOrder.Add(presented);
            foreach (var p in presenters.ToArray()) Call(p, presented, Kind.Started);
        }

        internal static void LineEnded(DialogLine line, bool completed)
        {
            if (line == null || !playing.TryGetValue(line, out var presented)) return;
            playing.Remove(line);
            playingInOrder.RemoveAll(p => p.Line == line);
            var kind = completed ? Kind.Ended : Kind.Interrupted;
            foreach (var p in presenters.ToArray()) Call(p, presented, kind);
        }

        enum Kind { Started, Ended, Interrupted }

        static void Call(IDialogPresenter presenter, DialogPresentedLine line, Kind kind)
        {
            try
            {
                switch (kind)
                {
                    case Kind.Started: presenter.OnLineStarted(line); break;
                    case Kind.Ended: presenter.OnLineEnded(line); break;
                    default: presenter.OnLineInterrupted(line); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, presenter as UnityEngine.Object);
            }
        }

        /// <summary>Clears everything, for Play Mode without a domain reload (see <see cref="DialogStaticState"/>) and for tests.</summary>
        internal static void Reset()
        {
            presenters.Clear();
            playing.Clear();
            playingInOrder.Clear();
        }
    }
}
