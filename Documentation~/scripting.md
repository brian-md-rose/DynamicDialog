# Scripting

Everything is in the `DynamicDialog` namespace. The Unity components are in the `DynamicDialog` assembly, and the
engine underneath them, which has no Unity dependency, is in `DynamicDialog.Core`. Every public type and member has
XML documentation, so your IDE shows it as you type.

## Asking for a line

Call `Speak` on a speaker with a concept, and optionally facts about this event:

```csharp
public class Hurtable : MonoBehaviour
{
    public DialogSpeaker speaker;

    public void TakeDamage(float damage)
    {
        speaker.Speak("OnHurt", q => q.Set("damage", damage), interrupt: true);
    }
}
```

- Event facts are added to this one query only, after memory and fact providers, so they override them.
- `Speak` returns the `DialogLine` that started, or `null` if no rule matched or the speaker was busy.
- A speaker that is already talking ignores the request, unless `interrupt` is true. Then a matching line stops the
  current one, which drops its followups and `at end` events. If nothing matches, the current line carries on.
- For a UnityEvent (a button, a timeline signal), use `SpeakConcept(string)`, which takes just the concept.
- `StopLine()` stops the current line, as an interruption.

## Game events

Rules raise events with `do` (see [the language](dlg-reference.md#do)). Subscribe to an event by name with
`DialogEvents.On`, which returns an `IDisposable`:

```csharp
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
```

- Names ignore case. `e.Args` has every argument as text; `Arg`, `FloatArg` and `IntArg` read one with a fallback.
- Events without `at end` arrive after the rule's facts are written and before the line starts, so presenters and
  `AnyLineStarted` haven't heard about the line yet. `at end` events arrive when the line has played to the end.
- A handler that throws is logged, and the other handlers still run.
- `DialogEvents.Any` receives every event with its line. `DialogEvents.Raise(speaker, new DialogEvent("GiveItem",
  new[] { "glasses" }))` delivers an event as if a rule had raised it, which is handy for testing handlers.
- Subscriptions are cleared each time Play Mode starts, so subscribe in `OnEnable` or `Start`.

The [Dialog Event Listener](components.md#dialog-event-listener) component does the same with UnityEvents.

## Your own facts

Give rules facts from your game by implementing `IFactProvider` on a component. On a speaker's GameObject, the facts
describe that character. On the Dialog Director's GameObject, they are world facts for everyone.

```csharp
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
```

`AddFacts` runs on every lookup, so keep it cheap. Caching keys as `Symbol`s, as above, avoids looking names up each
time; `query.Set("health", health)` works too. A speaker collects its providers when it registers with the director
(when it is enabled, or at `Start` if the director wasn't ready), so add them in the scene or in `Awake`, not later. Computed facts aren't saved: a provider describes
the game as it is right now.

Rather than writing code, [Static Facts](components.md#static-facts) covers facts that don't change, and memory
covers facts rules set.

## Memory

Read and write memory from code through the director's engine. Times are the engine's clock (`Engine.Clock.Now`):

```csharp
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
```

`Add(key, amount, now)` adds to a number, like `remember x += 1`.

### Fact changes

`Engine.FactChanged` reports every change to world and character memory, from rules or code, including facts that
expire. Setting a fact to the value it already has reports nothing. Loading a save, or clearing memory, raises
`Engine.FactsReset` once instead of a change per fact.

```csharp
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
```

`FactChange` has the `Key`, the `Kind` (`Set`, `Removed` or `Expired`), whose memory (`IsWorld`, `Character`), and the
old and new values. The [Dialog Fact Listener](components.md#dialog-fact-listener) does this without code.

## Reacting to lines

- **Presenters** (`IDialogPresenter`) are the way to show lines in UI. See [Presenters](presenters.md).
- **A component next to a speaker** that implements `IDialogLineHandler` is told when that speaker's lines start and
  end, for lip sync, gestures and the like:

  ```csharp
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
  ```

- **Every speaker:** the static events `DialogSpeaker.AnyLineStarted` and `DialogSpeaker.AnyLineEnded`. Like
  `DialogEvents`, they are cleared when Play Mode starts.
- **One speaker in the inspector:** its **On Subtitle** and **On Line Ended** UnityEvents.

`DialogLine` has the `Rule` that won, the `Concept`, the `Speaker` and the `StartTime`. Prefer game events (`do`) over
checking `line.Rule.Name`: rule names change as writers work, and events don't.

## Loading rules from code

The director loads its **Databases** list. To load and unload others while playing:

```csharp
public class ChapterRules : MonoBehaviour
{
    public DialogRuleDatabase chapterTwo;

    void OnEnable() => DialogDirector.Instance.AddDatabase(chapterTwo);

    void OnDisable()
    {
        if (DialogDirector.Instance != null) DialogDirector.Instance.RemoveDatabase(chapterTwo);
    }
}
```

Loads are counted, so two callers can share a database; it unloads when every `AddDatabase` has been matched by a
`RemoveDatabase`. [Dialog Region](regions.md) does this for you.

## The engine without Unity

`DynamicDialog.Core` runs anywhere .NET Standard 2.1 does: servers, tools, unit tests. It has rules, memory and the
matcher, but no speakers, audio or followup timing.

```csharp
var clock = new ManualClock();
var engine = new DialogEngine(clock);

var source = DialogScriptParser.Parse(@"
rule SeeBarrel
  concept OnSee
  if object == barrel
  say ""A barrel.""
  remember barrels_seen += 1
");
engine.Matcher.Add(new RuleSet("barrels", source.Rules.Select(r => r.ToRule())));

var bot = engine.GetOrCreateCharacter("RedBot");
var match = engine.Ask(bot, "OnSee", q => q.Set("object", "barrel"));
if (match.Matched)
{
    engine.Commit(match);   // when the line starts: writes facts, records once and cooldown
    Console.WriteLine(match.Rule.Name);
}
```

Or build rules in code with `RuleBuilder`:

```csharp
var rule = new RuleBuilder("SecondBarrel")
    .Concept("OnSee")
    .Where(Criterion.Eq("object", "barrel"))
    .Where(Criterion.Ge("barrels_seen", 1))
    .Increment("barrels_seen")
    .ThenAny("Groan", delay: 1f)
    .Build();
```

`DialogConductor` adds speakers, line timing and followups on top of the engine; implement `IDialogSpeaker` to use it
with your own characters. The Unity components are built from these same pieces.
