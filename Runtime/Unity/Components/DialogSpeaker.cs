using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DynamicDialog
{
    /// <summary>
    /// Implement on a component next to a <see cref="DialogSpeaker"/> to react to its lines,
    /// e.g. lip sync, gestures, or a custom subtitle UI.
    /// </summary>
    public interface IDialogLineHandler
    {
        /// <param name="variation">The chosen variation, or null when the rule's payload isn't a <see cref="DialogResponse"/>.</param>
        void OnLineStarted(DialogSpeaker speaker, DialogLine line, DialogLineDefinition variation);
        void OnLineFinished(DialogSpeaker speaker, DialogLine line, bool completed);
    }

    [Serializable] public class DialogTextEvent : UnityEvent<string> { }

    /// <summary>
    /// A character that can speak. It registers with the <see cref="DialogDirector"/>, gathers
    /// facts from fact-provider components on its GameObject, plays the chosen variation, and
    /// reports when the line ends so followups can run. It can also poll for idle lines (talk slide 67).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dynamic Dialog/Dialog Speaker")]
    public class DialogSpeaker : MonoBehaviour, IDialogSpeaker
    {
        [Tooltip("The character's name in rules (the 'who' fact). Speakers with the same name share memory.")]
        [SerializeField] string who;

        [Tooltip("How far away this speaker can hear others, for 'then any' and 'then all' followups.")]
        [SerializeField, Min(0f)] float earshotRadius = 15f;

        [Tooltip("Set to false while the character can't talk (dead, gagged, in a cutscene).")]
        public bool canSpeak = true;

        [Header("Delivery")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] Animator animator;

        [Tooltip("For lines with no audio and no set duration: seconds per character of subtitle text.")]
        [SerializeField, Min(0f)] float secondsPerCharacter = 0.06f;

        [Tooltip("Shortest time a line without audio stays up.")]
        [SerializeField, Min(0f)] float minimumDuration = 1.5f;

        [Header("Idle chatter")]
        [SerializeField] bool idleTalk = true;
        [SerializeField] string idleConcept = "TalkIdle";
        [SerializeField, Min(0.1f)] float idleInterval = 5f;
        [Tooltip("Random extra time added to each idle interval, so speakers don't poll in sync.")]
        [SerializeField, Min(0f)] float idleJitter = 2f;

        [Header("Events")]
        [Tooltip("Raised with the subtitle text when a line starts.")]
        public DialogTextEvent onSubtitle = new DialogTextEvent();
        [Tooltip("Raised when a line ends, whether it completed or was stopped.")]
        public UnityEvent onLineFinished = new UnityEvent();

        /// <summary>Raised for every speaker. Handy for a single subtitle UI.</summary>
        public static event Action<DialogSpeaker, DialogLine, DialogLineDefinition> AnyLineStarted;
        public static event Action<DialogSpeaker, DialogLine, bool> AnyLineFinished;

        readonly List<IFactProvider> providers = new List<IFactProvider>();
        readonly List<IDialogLineHandler> handlers = new List<IDialogLineHandler>();

        DialogDirector director;
        DialogCharacter character;
        DialogLine current;
        DialogLineDefinition currentVariation;
        double endTime;
        double nextIdleTime;
        Symbol idleConceptSymbol;

        /// <summary>The character name: <c>who</c>, or the GameObject name when that is empty.</summary>
        public Symbol Who => character != null ? character.Who : Symbol.Of(string.IsNullOrWhiteSpace(who) ? gameObject.name : who.Trim());
        public DialogCharacter Character => character;
        public DialogDirector Director => director;
        public float EarshotRadius { get => earshotRadius; set => earshotRadius = Mathf.Max(0f, value); }
        public bool IsSpeaking => current != null;
        public DialogLine CurrentLine => current;
        public DialogLineDefinition CurrentVariation => currentVariation;

        /// <summary>This speaker's persistent memory (shared by speakers with the same name).</summary>
        public FactMemory Memory => character?.Memory;

        public bool IsAvailable => isActiveAndEnabled && canSpeak && current == null && character != null;

        // ---- Speaking -------------------------------------------------------------------------

        /// <summary>Asks for a line about <paramref name="concept"/> and plays it if a rule matches.</summary>
        /// <param name="eventFacts">Facts about this event, e.g. <c>q => q.Set("object", "barrel")</c>.</param>
        /// <param name="interrupt">Stop the current line (dropping its followups) if a new line matches.</param>
        /// <returns>The line that started, or null.</returns>
        public DialogLine Speak(Symbol concept, Action<Query> eventFacts = null, bool interrupt = false)
        {
            if (!EnsureRegistered()) return null;
            return director.Conductor.TrySpeak(this, concept, eventFacts, interrupt);
        }

        public DialogLine Speak(string concept, Action<Query> eventFacts = null, bool interrupt = false) =>
            Speak(Symbol.Of(concept), eventFacts, interrupt);

        /// <summary>For UnityEvents: speak a concept with no extra facts.</summary>
        public void SpeakConcept(string concept) => Speak(concept);

        /// <summary>Stops the current line. Its followups are dropped.</summary>
        public void StopLine()
        {
            if (current == null) return;
            if (audioSource != null && currentVariation != null && audioSource.clip == currentVariation.clip) audioSource.Stop();
            Finish(completed: false);
        }

        // ---- IDialogSpeaker -------------------------------------------------------------------

        public bool CanHear(IDialogSpeaker source)
        {
            if (!(source is Component c) || c == null) return false;
            return (c.transform.position - transform.position).sqrMagnitude <= earshotRadius * earshotRadius;
        }

        void IDialogSpeaker.BeginLine(DialogLine line)
        {
            current = line;
            var response = line.Response as DialogResponse;
            currentVariation = response?.Pick(director.Random);

            float duration = 0f;
            if (currentVariation != null)
            {
                var v = currentVariation;
                if (v.clip != null && audioSource != null)
                {
                    audioSource.clip = v.clip;
                    audioSource.Play();
                }
                if (animator != null && !string.IsNullOrEmpty(v.animatorTrigger)) animator.SetTrigger(v.animatorTrigger);
                duration = EstimateDuration(v);
                if (!string.IsNullOrEmpty(v.text)) onSubtitle.Invoke(v.text);
            }
            endTime = Time.timeAsDouble + duration;

            foreach (var h in handlers) h.OnLineStarted(this, line, currentVariation);
            AnyLineStarted?.Invoke(this, line, currentVariation);
        }

        /// <summary>How long a variation lasts: its set duration, else the clip's length, else an estimate from the text.</summary>
        public float EstimateDuration(DialogLineDefinition v)
        {
            if (v == null) return 0f;
            if (v.duration > 0f) return v.duration;
            if (v.clip != null)
            {
                float pitch = audioSource != null ? Mathf.Max(0.01f, Mathf.Abs(audioSource.pitch)) : 1f;
                return v.clip.length / pitch;
            }
            if (!string.IsNullOrEmpty(v.text)) return Mathf.Max(minimumDuration, v.text.Length * secondsPerCharacter);
            return 0f;
        }

        void Finish(bool completed)
        {
            var line = current;
            current = null;
            currentVariation = null;
            nextIdleTime = Math.Max(nextIdleTime, Time.timeAsDouble + idleInterval);

            foreach (var h in handlers) h.OnLineFinished(this, line, completed);
            onLineFinished.Invoke();
            AnyLineFinished?.Invoke(this, line, completed);
            director?.Conductor?.LineFinished(line, completed);
        }

        // ---- Lifecycle ------------------------------------------------------------------------

        void OnEnable()
        {
            idleConceptSymbol = Symbol.Of(idleConcept);
            ScheduleIdle();
            EnsureRegistered();
        }

        void Start() => EnsureRegistered();

        void OnDisable()
        {
            StopLine();
            if (director != null && director.Conductor != null) director.Conductor.Unregister(this);
            if (character != null)
                foreach (var p in providers) character.Providers.Remove(p);
            character = null;
            director = null;
        }

        void Update()
        {
            if (current != null)
            {
                if (Time.timeAsDouble >= endTime) Finish(completed: true);
                return;
            }
            if (idleTalk && Time.timeAsDouble >= nextIdleTime)
            {
                ScheduleIdle();
                if (!idleConceptSymbol.IsNone) Speak(idleConceptSymbol);
            }
        }

        bool EnsureRegistered()
        {
            if (director != null && character != null) return true;
            director = DialogDirector.Instance;
            if (director == null || director.Engine == null) return false;

            character = director.Engine.GetOrCreateCharacter(Who);
            providers.Clear();
            GetComponents(providers);
            foreach (var p in providers)
                if (!character.Providers.Contains(p)) character.Providers.Add(p);
            handlers.Clear();
            GetComponents(handlers);
            director.Conductor.Register(this);
            return true;
        }

        void ScheduleIdle()
        {
            nextIdleTime = Time.timeAsDouble + idleInterval + UnityEngine.Random.value * idleJitter;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, earshotRadius);
        }

        void OnValidate()
        {
            idleConceptSymbol = Symbol.Of(idleConcept);
        }
    }
}
