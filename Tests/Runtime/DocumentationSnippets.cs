// The manual's Unity C# examples (Documentation~), compiled with the Play Mode tests.
// DocumentationTests (Tests/Editor) checks that every C# block in the manual still matches code in a
// DocumentationSnippets.cs file, ignoring whitespace. Edit both together.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicDialog.Tests.Docs
{
    // ---- scripting.md -------------------------------------------------------------------------

    public class Hurtable : MonoBehaviour
    {
        public DialogSpeaker speaker;

        public void TakeDamage(float damage)
        {
            speaker.Speak("OnHurt", q => q.Set("damage", damage), interrupt: true);
        }
    }

    public class Inventory : MonoBehaviour
    {
        IDisposable giveItem;

        void OnEnable() => giveItem = DialogEvents.On("GiveItem", OnGiveItem);

        void OnDisable() => giveItem?.Dispose();

        void OnGiveItem(DialogSpeaker speaker, DialogEvent e)
        {
            string item = e.Arg(0);       // "glasses" in: do GiveItem glasses
            int count = e.IntArg(1, 1);   // the second argument as a number, or 1
            Debug.Log($"{speaker.name} gets {count} {item}");
        }
    }

    public class HealthFacts : MonoBehaviour, IFactProvider
    {
        static readonly Symbol Health = Symbol.Of("health");
        static readonly Symbol Weapon = Symbol.Of("weapon");
        static readonly Symbol Armed = Symbol.Of("armed");

        public float health = 100f;
        public string weapon = "pistol";

        public void AddFacts(Query query)
        {
            query.Set(Health, health);
            query.Set(Weapon, Symbol.Of(weapon));   // a name: rules test "if weapon == pistol"
            query.Set(Armed, !string.IsNullOrEmpty(weapon));
        }
    }

    public static class MemoryExample
    {
        public static void Run()
        {
            var engine = DialogDirector.Instance.Engine;
            double now = engine.Clock.Now;

            // World memory, seen by everyone.
            engine.World.Set("alarm", 1f, now);
            engine.World.Set(Symbol.Of("weather"), Symbol.Of("rain"), now, duration: 60f);

            // One character's memory. A speaker's own memory is also speaker.Memory.
            var nurse = engine.GetOrCreateCharacter("Nurse");
            nurse.Memory.Set("trusts_player", 1f, now);
            if (nurse.Memory.TryGet("trusts_player", now, out float trust))
                Debug.Log("Nurse trust: " + trust);
            nurse.Memory.Remove(Symbol.Of("trusts_player"));
        }
    }

    public class QuestLog : MonoBehaviour
    {
        void OnEnable()
        {
            var engine = DialogDirector.Instance.Engine;
            engine.FactChanged += OnFactChanged;
            engine.FactsReset += OnFactsReset;
        }

        void OnDisable()
        {
            var director = DialogDirector.Instance;
            if (director == null || director.Engine == null) return;
            director.Engine.FactChanged -= OnFactChanged;
            director.Engine.FactsReset -= OnFactsReset;
        }

        void OnFactChanged(FactChange change)
        {
            if (change.IsWorld && change.Key == Symbol.Of("glasses_returned") && change.HasNewValue)
                Debug.Log("Quest done: the nurse has her glasses.");
        }

        void OnFactsReset()
        {
            // A save was loaded: read the facts you care about again.
        }
    }

    public class Gestures : MonoBehaviour, IDialogLineHandler
    {
        public Animator animator;

        public void OnLineStarted(DialogSpeaker speaker, DialogLine line, DialogLineDefinition variation)
        {
            animator.SetBool("Talking", true);
        }

        public void OnLineEnded(DialogSpeaker speaker, DialogLine line, bool completed)
        {
            animator.SetBool("Talking", false);
        }
    }

    public class ChapterRules : MonoBehaviour
    {
        public DialogRuleDatabase chapterTwo;

        void OnEnable() => DialogDirector.Instance.AddDatabase(chapterTwo);

        void OnDisable()
        {
            if (DialogDirector.Instance != null) DialogDirector.Instance.RemoveDatabase(chapterTwo);
        }
    }

    // ---- presenters.md (the TextMeshPro examples are in Tests/Presentation) -------------------

    public class ConsoleLog : IDialogPresenter
    {
        public void OnLineStarted(DialogPresentedLine line) => Debug.Log(line.SpeakerName + ": " + line.Text);

        public void OnLineEnded(DialogPresentedLine line) { }

        public void OnLineInterrupted(DialogPresentedLine line) => Debug.Log(line.SpeakerName + " was cut off.");
    }

    public static class ConsoleLogExample
    {
        public static void Run()
        {
            var log = new ConsoleLog();
            DialogPresenters.Register(log);
            // ...
            DialogPresenters.Unregister(log);
        }
    }

    // ---- localization.md ----------------------------------------------------------------------

    public class MyLocalizer : MonoBehaviour, IDialogLocalizer
    {
        readonly Dictionary<string, string> texts = new Dictionary<string, string>();

        public void SetText(string key, string text) => texts[key] = text;

        public string GetText(string key, DialogLineDefinition line) =>
            texts.TryGetValue(key, out var text) ? text : null;

        public AudioClip GetClip(string key, DialogLineDefinition line) => null;   // keep the authored clip
    }

    // ---- save-load.md -------------------------------------------------------------------------

    public class DialogSaves : MonoBehaviour
    {
        string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "dialog.json");

        public void Save() => System.IO.File.WriteAllText(SavePath, DialogDirector.Instance.SaveStateJson());

        public void Load()
        {
            if (System.IO.File.Exists(SavePath))
                DialogDirector.Instance.LoadStateJson(System.IO.File.ReadAllText(SavePath));
        }
    }

    public static class LoadReportExample
    {
        public static void Run(string json)
        {
            var director = DialogDirector.Instance;
            director.LoadStateJson(json);
            DialogLoadReport report = director.LastLoadReport;
            if (report.UnmatchedRules.Count > 0)
                Debug.Log("Saved rule states with no rule: " + string.Join(", ", report.UnmatchedRules));
        }
    }
}
