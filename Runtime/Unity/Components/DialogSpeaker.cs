using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace DynamicDialog
{
    /// <summary>
    /// Implement on a component next to a <see cref="DialogSpeaker"/> to react to its lines,
    /// e.g. lip sync, gestures, or a custom subtitle UI.
    /// </summary>
    public interface IDialogLineHandler
    {
        /// <summary>A line started on <paramref name="speaker"/>.</summary>
        /// <param name="speaker">The speaker.</param>
        /// <param name="line">The line.</param>
        /// <param name="variation">The chosen variation, or null when the rule's payload isn't a <see cref="DialogResponse"/>.</param>
        void OnLineStarted(DialogSpeaker speaker, DialogLine line, DialogLineDefinition variation);
        /// <summary>The speaker's line ended.</summary>
        /// <param name="speaker">The speaker.</param>
        /// <param name="line">The line.</param>
        /// <param name="completed">False when it was stopped early (interrupted, disabled, or <see cref="DialogSpeaker.StopLine"/>).</param>
        void OnLineEnded(DialogSpeaker speaker, DialogLine line, bool completed);
    }

    /// <summary>A UnityEvent that passes a line's subtitle text.</summary>
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

        /// <summary>Set to false while the character can't talk (dead, gagged, in a cutscene).</summary>
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

        /// <summary>Raised with the subtitle text when a line starts.</summary>
        [Header("Events")]
        [Tooltip("Raised with the subtitle text when a line starts.")]
        public DialogTextEvent onSubtitle = new DialogTextEvent();
        /// <summary>Raised when a line ends, whether it completed or was stopped.</summary>
        [Tooltip("Raised when a line ends, whether it completed or was stopped.")]
        [FormerlySerializedAs("onLineFinished")]
        public UnityEvent onLineEnded = new UnityEvent();

        /// <summary>Old name of <see cref="onLineEnded"/>.</summary>
        [Obsolete("Use onLineEnded.")]
        public UnityEvent onLineFinished => onLineEnded;

        /// <summary>Raised when any speaker's line starts. Handy for a single subtitle UI.</summary>
        /// <remarks>The variation is null when the rule's payload isn't a <see cref="DialogResponse"/>.</remarks>
        public static event Action<DialogSpeaker, DialogLine, DialogLineDefinition> AnyLineStarted;
        /// <summary>Raised when any speaker's line ends. The bool is false if it was stopped early.</summary>
        public static event Action<DialogSpeaker, DialogLine, bool> AnyLineEnded;

        /// <summary>Old name of <see cref="AnyLineEnded"/>.</summary>
        [Obsolete("Use AnyLineEnded.")]
        public static event Action<DialogSpeaker, DialogLine, bool> AnyLineFinished
        {
            add => AnyLineEnded += value;
            remove => AnyLineEnded -= value;
        }

        // Play Mode without a domain reload: drop the previous session's subscribers.
        internal static void ResetStatics()
        {
            AnyLineStarted = null;
            AnyLineEnded = null;
        }

        readonly List<IFactProvider> providers = new List<IFactProvider>();
        readonly List<IDialogLineHandler> handlers = new List<IDialogLineHandler>();

        DialogDirector director;
        DialogCharacter character;
        DialogLine current;
        DialogLineDefinition currentVariation;
        string currentKey, currentText;
        AudioClip currentClip;
        double endTime;
        double startTime;
        float lineDuration;
        double nextIdleTime;
        Symbol idleConceptSymbol;

        /// <summary>The character name: <c>who</c>, or the GameObject name when that is empty.</summary>
        public Symbol Who => character != null ? character.Who : Symbol.Of(string.IsNullOrWhiteSpace(who) ? gameObject.name : who.Trim());
        /// <summary>The engine character, or null while the speaker isn't registered with a director.</summary>
        public DialogCharacter Character => character;
        /// <summary>The director this speaker is registered with, or null.</summary>
        public DialogDirector Director => director;
        /// <summary>How far away this speaker can hear others, for "then any" and "then all".</summary>
        public float EarshotRadius { get => earshotRadius; set => earshotRadius = Mathf.Max(0f, value); }
        /// <summary>Whether a line is playing.</summary>
        public bool IsSpeaking => current != null;
        /// <summary>The line playing, or null.</summary>
        public DialogLine CurrentLine => current;
        /// <summary>The variation playing, or null.</summary>
        public DialogLineDefinition CurrentVariation => currentVariation;

        /// <summary>The current line's localization key.</summary>
        public string CurrentKey => currentKey;
        /// <summary>The current line's subtitle, after localization.</summary>
        public string CurrentText => currentText;
        /// <summary>The current line's clip, after localization.</summary>
        public AudioClip CurrentClip => currentClip;
        /// <summary>When the current line started, in <see cref="Time.timeAsDouble"/> seconds. 0 when not speaking.</summary>
        public double CurrentStartTime => current != null ? startTime : 0d;
        /// <summary>How long the current line lasts, in seconds. 0 when not speaking.</summary>
        public float CurrentDuration => current != null ? lineDuration : 0f;

        /// <summary>This speaker's persistent memory (shared by speakers with the same name).</summary>
        public FactMemory Memory => character?.Memory;

        /// <summary>Whether the speaker can take a new line now: enabled, <see cref="canSpeak"/>, registered and not speaking.</summary>
        public bool IsAvailable => isActiveAndEnabled && canSpeak && current == null && character != null;

        // ---- Speaking -------------------------------------------------------------------------

        /// <summary>Asks for a line about <paramref name="concept"/> and plays it if a rule matches.</summary>
        /// <param name="concept">What happened, e.g. "OnHurt".</param>
        /// <param name="eventFacts">Facts about this event, e.g. <c>q => q.Set("object", "barrel")</c>.</param>
        /// <param name="interrupt">Stop the current line (dropping its followups) if a new line matches.</param>
        /// <returns>The line that started, or null.</returns>
        public DialogLine Speak(Symbol concept, Action<Query> eventFacts = null, bool interrupt = false)
        {
            if (!EnsureRegistered()) return null;
            return director.Conductor.TrySpeak(this, concept, eventFacts, interrupt);
        }

        /// <summary>Asks for a line about <paramref name="concept"/> and plays it if a rule matches.</summary>
        public DialogLine Speak(string concept, Action<Query> eventFacts = null, bool interrupt = false) =>
            Speak(Symbol.Of(concept), eventFacts, interrupt);

        /// <summary>For UnityEvents: speak a concept with no extra facts.</summary>
        public void SpeakConcept(string concept) => Speak(concept);

        /// <summary>Stops the current line. Its followups are dropped.</summary>
        public void StopLine()
        {
            if (current == null) return;
            if (audioSource != null && currentClip != null && audioSource.clip == currentClip) audioSource.Stop();
            Finish(completed: false);
        }

        // ---- IDialogSpeaker -------------------------------------------------------------------

        /// <summary>Whether <paramref name="source"/> is within <see cref="EarshotRadius"/>.</summary>
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
            currentKey = null;
            currentText = null;
            currentClip = null;

            float duration = 0f;
            if (currentVariation != null)
            {
                var v = currentVariation;
                currentKey = LineKeys.For(line.Rule.Name, response.lines.IndexOf(v), v.key, v.clip != null ? v.clip.name : null);
                var localizer = director.Localizer;
                currentText = localizer?.GetText(currentKey, v) ?? v.text;
                currentClip = localizer?.GetClip(currentKey, v) ?? v.clip;

                if (currentClip != null && audioSource != null)
                {
                    audioSource.clip = currentClip;
                    audioSource.Play();
                }
                if (animator != null && !string.IsNullOrEmpty(v.animatorTrigger)) animator.SetTrigger(v.animatorTrigger);
                duration = EstimateDuration(v.duration, currentClip, currentText);
                if (!string.IsNullOrEmpty(currentText)) onSubtitle.Invoke(currentText);
            }
            startTime = Time.timeAsDouble;
            lineDuration = duration;
            endTime = startTime + duration;

            foreach (var h in handlers) h.OnLineStarted(this, line, currentVariation);
            AnyLineStarted?.Invoke(this, line, currentVariation);
            DialogPresenters.LineStarted(this, line, currentVariation, currentText, startTime, duration);
        }

        /// <summary>How long a variation lasts: its set duration, else the clip's length, else an estimate from the text.</summary>
        public float EstimateDuration(DialogLineDefinition v) =>
            v == null ? 0f : EstimateDuration(v.duration, v.clip, v.text);

        float EstimateDuration(float setDuration, AudioClip clip, string text)
        {
            if (setDuration > 0f) return setDuration;
            if (clip != null)
            {
                float pitch = audioSource != null ? Mathf.Max(0.01f, Mathf.Abs(audioSource.pitch)) : 1f;
                return clip.length / pitch;
            }
            if (!string.IsNullOrEmpty(text)) return Mathf.Max(minimumDuration, text.Length * secondsPerCharacter);
            return 0f;
        }

        void Finish(bool completed)
        {
            var line = current;
            current = null;
            currentVariation = null;
            currentKey = currentText = null;
            currentClip = null;
            nextIdleTime = Math.Max(nextIdleTime, Time.timeAsDouble + idleInterval);

            foreach (var h in handlers) h.OnLineEnded(this, line, completed);
            onLineEnded.Invoke();
            AnyLineEnded?.Invoke(this, line, completed);
            DialogPresenters.LineEnded(line, completed);
            director?.Conductor?.EndLine(line, completed);
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
