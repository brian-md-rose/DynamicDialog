using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// Game code's way to react to the game events rules raise with <c>do</c>, without matching rule names.
    /// <code>
    /// // In Clinic.dlg:   do GiveItem glasses
    /// var subscription = DialogEvents.On("GiveItem", (speaker, e) => inventory.Give(speaker, e.Arg(0)));
    /// // later:
    /// subscription.Dispose();
    /// </code>
    /// </summary>
    /// <remarks>
    /// Events raised by the active <see cref="DialogDirector"/>'s rules arrive here. Names match ignoring case.
    /// A handler that throws is logged and doesn't stop the others. Events timed
    /// <see cref="DialogEventTiming.LineStarted"/> arrive after the rule's facts are written and before the
    /// speaker starts the line; <see cref="DialogEventTiming.LineEnded"/> events arrive when the line ends,
    /// and never for a line that was stopped early. Subscriptions are cleared when Play Mode starts.
    /// </remarks>
    public static class DialogEvents
    {
        static readonly Dictionary<string, List<Action<DialogSpeaker, DialogEvent>>> handlers =
            new Dictionary<string, List<Action<DialogSpeaker, DialogEvent>>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Raised for every event, with the line that raised it. The speaker is null when the line's
        /// speaker isn't a <see cref="DialogSpeaker"/> (a custom <see cref="IDialogSpeaker"/>).
        /// </summary>
        public static event Action<DialogSpeaker, DialogEvent, DialogLine> Any;

        /// <summary>Calls <paramref name="handler"/> whenever a rule raises the event <paramref name="name"/>.</summary>
        /// <param name="name">The event's name, as written after <c>do</c>.</param>
        /// <param name="handler">Gets the speaker whose line raised it (null for a custom <see cref="IDialogSpeaker"/>) and the event, with its arguments.</param>
        /// <returns>Dispose it to stop listening. Or call <see cref="Off"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is null.</exception>
        public static IDisposable On(string name, Action<DialogSpeaker, DialogEvent> handler)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An event needs a name.", nameof(name));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            name = name.Trim();
            if (!handlers.TryGetValue(name, out var list)) handlers[name] = list = new List<Action<DialogSpeaker, DialogEvent>>();
            list.Add(handler);
            return new Subscription(name, handler);
        }

        /// <summary>Stops calling a handler added with <see cref="On"/>. Returns false if it wasn't subscribed.</summary>
        public static bool Off(string name, Action<DialogSpeaker, DialogEvent> handler)
        {
            if (string.IsNullOrWhiteSpace(name) || handler == null) return false;
            name = name.Trim();
            if (!handlers.TryGetValue(name, out var list) || !list.Remove(handler)) return false;
            if (list.Count == 0) handlers.Remove(name);
            return true;
        }

        /// <summary>Whether anything is listening for <paramref name="name"/> through <see cref="On"/>.</summary>
        public static bool HasHandlers(string name) =>
            !string.IsNullOrWhiteSpace(name) && handlers.ContainsKey(name.Trim());

        /// <summary>
        /// Delivers an event to its handlers as if a rule had raised it. The director calls this for you;
        /// call it yourself to test handlers, or to raise rule-style events from code.
        /// </summary>
        /// <param name="speaker">The speaker it comes from. May be null.</param>
        /// <param name="dialogEvent">The event.</param>
        /// <param name="line">The line that raised it. May be null.</param>
        public static void Raise(DialogSpeaker speaker, DialogEvent dialogEvent, DialogLine line = null)
        {
            if (string.IsNullOrEmpty(dialogEvent.Name)) return;
            if (handlers.TryGetValue(dialogEvent.Name, out var list))
            {
                // Copy, so a handler can subscribe or unsubscribe while the event is delivered.
                var snapshot = list.ToArray();
                foreach (var h in snapshot)
                {
                    try { h(speaker, dialogEvent); }
                    catch (Exception e) { Debug.LogException(e, speaker); }
                }
            }
            var any = Any;
            if (any == null) return;
            foreach (Action<DialogSpeaker, DialogEvent, DialogLine> h in any.GetInvocationList())
            {
                try { h(speaker, dialogEvent, line); }
                catch (Exception e) { Debug.LogException(e, speaker); }
            }
        }

        /// <summary>Removes every handler, including <see cref="Any"/>.</summary>
        public static void Clear()
        {
            handlers.Clear();
            Any = null;
        }

        internal static void OnConductorEvent(DialogLine line, DialogEvent e) => Raise(line?.Speaker as DialogSpeaker, e, line);

        sealed class Subscription : IDisposable
        {
            string name;
            Action<DialogSpeaker, DialogEvent> handler;

            public Subscription(string name, Action<DialogSpeaker, DialogEvent> handler)
            {
                this.name = name;
                this.handler = handler;
            }

            public void Dispose()
            {
                if (handler == null) return;
                Off(name, handler);
                handler = null;
                name = null;
            }
        }
    }
}
