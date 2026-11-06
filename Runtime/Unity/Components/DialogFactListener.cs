using System;
using UnityEngine;
using UnityEngine.Events;

namespace DynamicDialog
{
    /// <summary>A UnityEvent that passes a number.</summary>
    [Serializable] public class DialogFloatEvent : UnityEvent<float> { }

    /// <summary>
    /// Watches one fact in dialog memory and raises UnityEvents when it changes, whether a rule
    /// ("remember", "forget") or code changed it. Use it for quest markers, UI counters and the like.
    /// For code, subscribe to <see cref="DialogEngine.FactChanged"/> instead.
    /// </summary>
    [AddComponentMenu("Dynamic Dialog/Dialog Fact Listener")]
    public class DialogFactListener : MonoBehaviour
    {
        [Tooltip("Whose memory to watch. Speaker means the Dialog Speaker on this GameObject.")]
        [SerializeField] FactScope scope = FactScope.World;

        [Tooltip("Only for the Character scope: the character's name.")]
        [SerializeField] string character;

        [Tooltip("The fact to watch, e.g. crutch_returned.")]
        [SerializeField] string fact;

        [Tooltip("Raise the events with the current value when this component is enabled, and after a save is loaded.")]
        [SerializeField] bool notifyCurrentValue = true;

        /// <summary>Raised with the fact's new value (0 when it is removed or expires).</summary>
        [Header("Events")]
        [Tooltip("Raised with the new value; 0 when the fact is removed or expires.")]
        public DialogFloatEvent onValueChanged = new DialogFloatEvent();

        /// <summary>Raised with the new value as text: a name such as "garden", or a number. Empty when removed.</summary>
        [Tooltip("Raised with the new value as text: a name such as 'garden', or a number. Empty when the fact is removed.")]
        public DialogTextEvent onTextChanged = new DialogTextEvent();

        /// <summary>Raised when the fact gets a value it didn't have (it was missing before).</summary>
        [Tooltip("Raised when the fact appears (it was missing before).")]
        public UnityEvent onSet = new UnityEvent();

        /// <summary>Raised when the fact is removed or expires.</summary>
        [Tooltip("Raised when the fact is removed or expires.")]
        public UnityEvent onRemoved = new UnityEvent();

        DialogEngine engine;
        Symbol key;

        /// <summary>Whose memory is watched.</summary>
        public FactScope Scope { get => scope; set { scope = value; Resubscribe(); } }

        /// <summary>The character watched, for <see cref="FactScope.Character"/>.</summary>
        public string Character { get => character; set { character = value; Resubscribe(); } }

        /// <summary>The fact watched.</summary>
        public string Fact { get => fact; set { fact = value; Resubscribe(); } }

        /// <summary>The fact's current value. Returns false (and 0) if it is missing or nothing is watched yet.</summary>
        public bool TryGetValue(out float value)
        {
            value = 0f;
            var memory = Memory();
            return memory != null && memory.TryGet(key, engine.Clock.Now, out value);
        }

        void OnEnable() => Subscribe();

        void Start()
        {
            // The director may not have existed yet in OnEnable.
            if (engine == null) Subscribe();
        }

        void OnDisable() => Unsubscribe();

        void Resubscribe()
        {
            if (!isActiveAndEnabled) return;
            Unsubscribe();
            Subscribe();
        }

        void Subscribe()
        {
            var director = DialogDirector.Instance;
            if (director == null || director.Engine == null || string.IsNullOrWhiteSpace(fact)) return;
            engine = director.Engine;
            key = Symbol.Of(fact.Trim());
            engine.FactChanged += OnFactChanged;
            engine.FactsReset += OnFactsReset;
            if (notifyCurrentValue) NotifyCurrent();
        }

        void Unsubscribe()
        {
            if (engine == null) return;
            engine.FactChanged -= OnFactChanged;
            engine.FactsReset -= OnFactsReset;
            engine = null;
        }

        /// <summary>The name of the character watched, or <see cref="Symbol.None"/> for world memory.</summary>
        Symbol Owner()
        {
            switch (scope)
            {
                case FactScope.World: return Symbol.None;
                case FactScope.Character: return string.IsNullOrWhiteSpace(character) ? Symbol.None : Symbol.Of(character.Trim());
                default:
                    var speaker = GetComponent<DialogSpeaker>();
                    return speaker != null ? speaker.Who : Symbol.Of(gameObject.name);
            }
        }

        FactMemory Memory()
        {
            if (engine == null) return null;
            if (scope == FactScope.World) return engine.World;
            var owner = Owner();
            return owner.IsNone ? null : engine.GetOrCreateCharacter(owner).Memory;
        }

        void OnFactChanged(FactChange change)
        {
            if (change.Key != key) return;
            if (scope == FactScope.World ? change.Memory != engine.World : change.Character != Owner()) return;
            if (change.HasNewValue)
            {
                onValueChanged.Invoke(change.NewValue);
                onTextChanged.Invoke(FactValue.Format(change.Key, change.NewValue, change.NewValueIsSymbol));
                if (!change.HadOldValue) onSet.Invoke();
            }
            else
            {
                onValueChanged.Invoke(0f);
                onTextChanged.Invoke("");
                onRemoved.Invoke();
            }
        }

        void OnFactsReset()
        {
            if (notifyCurrentValue) NotifyCurrent();
        }

        void NotifyCurrent()
        {
            var memory = Memory();
            if (memory == null) return;
            if (memory.TryGet(key, engine.Clock.Now, out float value, out bool isSymbol))
            {
                onValueChanged.Invoke(value);
                onTextChanged.Invoke(FactValue.Format(key, value, isSymbol));
                onSet.Invoke();
            }
            else
            {
                onValueChanged.Invoke(0f);
                onTextChanged.Invoke("");
            }
        }
    }
}
