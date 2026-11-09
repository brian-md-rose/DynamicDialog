# Third Party Notices

This package contains third-party material, listed below. Everything else in the package (code, rule files, scenes,
prefabs, and the bubble sprites in `Runtime/Presentation/Sprites`, which the package's own editor code draws) is
original work covered by [LICENSE.md](LICENSE.md).

## Kenney asset packs (Courtyard Quests sample)

The **Courtyard Quests** sample includes files from three asset packs by Kenney (<https://www.kenney.nl>). Only the files
the sample uses are included. Each pack's original `License.txt` is kept next to its files.

| Component | Version | Files in this package | License |
|---|---|---|---|
| Mini Characters | 1.0 | `Samples~/CourtyardQuests/Art/Kenney/MiniCharacters` (character and mobility-aid models, `Textures/colormap.png`) | CC0 1.0 |
| Input Prompts | 1.5A | `Samples~/CourtyardQuests/Art/Kenney/InputPrompts` (keyboard and gamepad prompt icons) | CC0 1.0 |
| Interface Sounds | 1.0 | `Samples~/CourtyardQuests/Art/Kenney/InterfaceSounds` (`pluck_001.ogg`, `pluck_002.ogg`) | CC0 1.0 |

**License:** Creative Commons Zero v1.0 Universal (CC0 1.0), <https://creativecommons.org/publicdomain/zero/1.0/>.
The author has dedicated these works to the public domain. They may be used for any purpose, including commercially,
without attribution. Credit is appreciated by the author but not required: *Kenney (www.kenney.nl)*.
Some Input Prompts icons are styled after a game controller's buttons. CC0 covers the artwork only; Xbox is a trademark
of Microsoft Corporation, which is not affiliated with this package.

## Material used but not included

These are referenced by the package but are not part of it. They come with Unity, or with packages you install, under
their own terms:

- **TextMeshPro** and its **Essential Resources** (including the *Liberation Sans* font asset that the speech bubble and
  subtitle bar prefabs use). Importing a sample into a project that lacks the Essential Resources imports them from the
  TextMeshPro package installed in that project; they are not shipped in Dynamic Dialog.
- Unity's built-in fonts (`LegacyRuntime.ttf` or `Arial.ttf`), used by the Courtyard Quests sign and the debug overlays.
- Optional Unity packages the package can integrate with: uGUI, Addressables, Localization and the Input System.

Development-only tools in this repository, such as the NUnit test packages used by `Tests~/DotNet` and the GameCI
GitHub Actions used by `.github/workflows`, are downloaded when those tools run and are not redistributed.

## Acknowledgement

The design follows Elan Ruskin's GDC 2012 talk *"Rule Databases for Contextual Dialog and Game Logic"*. Dynamic Dialog is
an independent implementation and contains no code, text or other material from the talk.
