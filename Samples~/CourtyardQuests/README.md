# Courtyard Quests

A small, playable slice of a game: the courtyard of Willow Clinic, with five characters to talk to, three fetch
quests, and pigeons. Open `CourtyardQuests.unity` and press Play.

**Controls:** WASD or arrow keys to walk, **E** to talk to whoever is nearest, **F** to pick up or drop small things,
**Tab** for the debug overlays. With a gamepad: left stick, **A** to talk, **X** to pick up, **View** for the overlays.

## Who's here

- **Rosa**, the duty nurse, walks her rounds and reminds Hale about his medication.
- **Hale**, a retired sailor in a wheelchair, has opinions about everything, including the pigeons.
- **Jun** has lost his crutch. Bring it back and he starts his physio laps around the courtyard.
- **Mei**, the gardener, has lost her reading glasses and can't tell people from shrubs.
- **Otto**, on the oak bench, has lost his hearing aid and mishears everything.

## What it shows

All of the dialog is in `CourtyardQuests.dlg`. Edit it while playing to change the scene straight away.

| Feature | Where to look |
|---|---|
| **Talk:** pressing E asks the nearest character for the `OnTalk` concept. The most specific matching rule wins: first meetings, chat, and replies to what you're carrying. | `CourtyardPlayer.cs`, rules `RosaFirstMeet`, `RosaChat`, `RosaSeesDefib` |
| **See:** Dialog Vision raises `OnSee` with `object = <tag>` when a character spots a Dialog Tag (props, the player, the pigeons). | each character's Dialog Vision; rules `HaleSeesPlayer`, `PlayerSeesCrutch` |
| **Areas:** Dialog Areas raise `OnEnterArea` and set the `area` fact (garden, fountain, oak, nurse station, clinic door). | the **Areas** objects; rules `PlayerEntersGarden`, `JunInGarden` |
| **Pick-ups:** picking something up raises `OnPickUp` with an event fact, `item`. | `PlayerCarry.cs`; rules `PlayerPicksUpCrutch` |
| **Facts from code:** `player_holding` is added to every query by a fact provider. | `CourtyardWorldFacts.cs` |
| **Memory:** quests are world facts (`crutch_quest`, `crutch_returned`) written with `remember world`. Characters count chats in their own memory. | rules `JunAsks`, `JunGetsCrutch`, `RosaChatty` |
| **Game events:** `do GiveItem crutch` hands the item over, and `do StartWalking at end` sets Jun walking once he has finished his line. Code reacts to event names, never rule names, so rules can be renamed freely. | `QuestHooks.cs`, rule `JunGetsCrutch` |
| **Fact changes:** quest markers watch for `crutch_returned` and friends with `DialogEngine.FactChanged`. | `QuestMarker.cs` |
| **Characters reacting to each other:** idle chatter checks who is nearby (`near_Hale`), and followups pass the conversation along (`then Hale HaleAyeAye`). | rules `RosaRemindsHale`, `OttoShoutsAtHale` |
| **Speech bubbles:** the package's Dialog Speech Bubbles prefab, pointing at each speaker's head. | the **Dialog Speech Bubbles** object |

**Tab** turns on the package's debug overlays: subtitles with rule names, and the lookups over each character's head.
**Window > Dynamic Dialog > Debugger** shows every lookup and explains why a rule did or didn't play.

## How it's built

The objects the rules care about are ordinary scene objects you can select and edit: the characters (Dialog Speaker,
Dialog Vision, Nearby Speaker Facts), the props (Dialog Tag), the areas (Dialog Area), and the Dialog Director.
The scenery (lawn, walls, trees, fountain, clinic building) is built from primitives by `CourtyardScenery`,
with the render pipeline's default material, so the sample looks right in Built-in, URP and HDRP projects. In the
editor you see a preview of it that isn't saved with the scene.

Input uses the Input System package when it's installed, and the old Input Manager otherwise (`CourtyardInput.cs`).
Button prompts, the talk icon and the talk blips are in `CourtyardHud.cs`.

## Requirements

Speech bubbles use uGUI and TextMeshPro (`com.unity.ugui`, plus `com.unity.textmeshpro` before Unity 6). If TMP
Essential Resources aren't in the project yet, importing this sample adds them.

## Credits

Characters, props, button prompts and sounds are by Kenney (www.kenney.nl), released under Creative Commons Zero
(CC0). Each pack's `License.txt` is in `Art/Kenney`. Only the files this sample uses are included.
