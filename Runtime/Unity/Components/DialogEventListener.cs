using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DynamicDialog
{
    /// <summary>
    /// Wires the game events rules raise with <c>do</c> to UnityEvents in the inspector, e.g. play a
    /// particle effect on <c>do Celebrate</c>, or open a door on <c>do OpenGate north</c>.
    /// </summary>
    [AddComponentMenu("Dynamic Dialog/Dialog Event Listener")]
    public class DialogEventListener : MonoBehaviour
    {
        /// <summary>One event to listen for.</summary>
        [Serializable]
        public class Entry
        {
            /// <summary>The event's name, as written after <c>do</c>. Matches ignoring case.</summary>
            [Tooltip("The event's name, as written after 'do' in a rule.")]
            public string eventName;

            /// <summary>Only react when this speaker's line raised the event. Empty reacts to any speaker.</summary>
            [Tooltip("Only react when this speaker's line raised the event. Leave empty to react to anyone.")]
            public DialogSpeaker onlyFrom;

            /// <summary>Raised when the event arrives.</summary>
            public UnityEvent onEvent = new UnityEvent();

            /// <summary>Raised with the event's first argument (empty if it has none).</summary>
            [Tooltip("Raised with the event's first argument, or an empty string if it has none.")]
            public DialogTextEvent onEventWithArgument = new DialogTextEvent();
        }

        /// <summary>The events this component listens for.</summary>
        public List<Entry> events = new List<Entry>();

        /// <summary>Raised for every listened-for event, with the speaker and the full event, for code on this object.</summary>
        public event Action<DialogSpeaker, DialogEvent> Received;

        void OnEnable() => DialogEvents.Any += OnAny;

        void OnDisable() => DialogEvents.Any -= OnAny;

        void OnAny(DialogSpeaker speaker, DialogEvent e, DialogLine line)
        {
            bool any = false;
            foreach (var entry in events)
            {
                if (entry == null || !e.Is(entry.eventName)) continue;
                if (entry.onlyFrom != null && entry.onlyFrom != speaker) continue;
                any = true;
                entry.onEvent?.Invoke();
                entry.onEventWithArgument?.Invoke(e.Arg(0, ""));
            }
            if (any) Received?.Invoke(speaker, e);
        }

        void Reset()
        {
            var speaker = GetComponent<DialogSpeaker>();
            if (speaker != null) events.Add(new Entry { onlyFrom = speaker });
        }
    }
}
