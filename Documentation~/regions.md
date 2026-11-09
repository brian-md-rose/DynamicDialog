# Regions

A big game doesn't need every line loaded all the time. A **region** loads a rule database when a level (or part of
one) becomes active and unloads it when it goes away. Its rules are added on top of the director's always-loaded
rules, and a lookup searches every loaded database together, so a region's specific rules can beat the global ones.

## Dialog Region

1. Put the **Dialog Director** in a scene that stays loaded (a bootstrap or persistent scene), with your global rules
   in its **Databases** list.
2. In each region's scene (usually loaded additively), add an object with **Dynamic Dialog > Dialog Region** and set
   its **Database** to that region's rules.
3. Optionally fill in **Region Facts**: **Region Name** sets the world fact `region` while the region is enabled, and
   the extra facts are world facts that exist only then.

The region loads its database when it is enabled and unloads it when it is disabled or its scene unloads.

```dlg
rule NickSwampHello
  who Nick
  concept OnTalk
  if region == swamp
  say "Welcome to the swamp. Mind the gators."

rule NickHello
  who Nick
  concept OnTalk
  say "Hey."
```

With `NickSwampHello` in the swamp region's database and `NickHello` in the global one, Nick uses the swamp line only
while the swamp region is loaded. The `if region == swamp` condition also makes it more specific, so it wins there.

## Dialog Addressable Region

**Dynamic Dialog > Dialog Addressable Region** does the same, but its **Database** is an Addressables reference. The
rules, and the audio clips they use, stay on disk until the region is enabled, and are released when it's disabled,
including when it's disabled before loading finished. It needs the Addressables package
(`com.unity.addressables`); without it the component doesn't exist and nothing else changes.

1. Mark the `.dlg`, spreadsheet or Rule Database asset as Addressable.
2. Assign it to the component's **Database**.

Loading takes a moment, so the rules aren't available on the very first frame. `IsLoading`, `IsLoaded`, the `Loaded`
event and `LoadedDatabase` report progress. `SetDatabase(reference)` changes the database from code; it takes effect
the next time the component is enabled.

## Sharing and state

- Loads are counted. Two regions that load the same database are fine: it unloads when both are gone.
- When a region's database unloads, its rules' `once` and cooldown state is kept, and applied again when it loads.
  [Saves](save-load.md) include that state, so a region the player isn't in still remembers its rules.
- Memory is never tied to a region: facts its rules wrote stay in world or character memory.
- The [Debugger](debugging.md)'s why-not answer says *not loaded* for a rule whose database wasn't loaded at the time.

## From code

`DialogDirector.AddDatabase(database)` and `RemoveDatabase(database)` are what the region components call; use them
to load rules for chapters, quests or cutscenes. See [Scripting](scripting.md#loading-rules-from-code).

## Why it's fast

Rules are bucketed by concept and speaker, so adding a region adds a few small buckets to search, not a long list.
Measured with 10,000 rules, a lookup takes under a microsecond in .NET and allocates nothing.
