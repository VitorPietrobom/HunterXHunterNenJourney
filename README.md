# Hunter: Awakening

A *Hunter x Hunter* fan game prototype built in Unity. You play Gon. You train your body, awaken your aura, learn
Nen from Kite, and take on the Chimera Ants, ending in a fight with Meruem, the King, in his castle.

There are no XP grinds for stats. **You get stronger by training**: every drill raises real attributes, and those
attributes change how fast you run, how hard you hit and how much aura you hold. Fights teach you too, and the game
tells you *why* you lost and what to train next.

> Unofficial, non-commercial fan project. *Hunter x Hunter* belongs to Yoshihiro Togashi and its rights holders.

---

## What's in the game

**Training**
- Physical drills: **Lift Rack** (Strength), **Sprint Course** (Speed, Endurance) and **Precision Pads**
  (Coordination, Mobility). Each has an adjustable difficulty, and a drill that's too easy stops teaching you.
- **Fatigue and strain**: training tires you, and overtraining has a cost. Rest at the bench to recover.
- A **progression dashboard** (Tab) shows every attribute, what it does in play, mastery and milestones, and a
  breakdown of your last session and last fight.

**Nen**
- **Aura awakening** at the Meditation Stone, once your body is conditioned.
- **Ten, Zetsu, Ren and Gyo**, then **Ken** and **Ko**. Aura drills: the Aura Mat, the Ren Circle and the Gyo Stones.
- **Water Divination**: the classic glass-and-leaf test reveals that you're an **Enhancer**, with bonuses to match.
- **Jajanken**, Gon's Hatsu: chant, then throw **Rock**, **Paper** (a ranged aura blast) or **Scissors** (an aura
  blade that sweeps through a crowd).
- **Aura is a daily budget**: a large pool with no passive regeneration. It refills at the rest bench, or trickles
  back while you stand still in Zetsu.

**Combat**
- Light and heavy attacks, **combos**, **lock-on**, blocking, **parries** and **perfect dodges**. Opponents
  telegraph their attacks (red wind-up, yellow opening, blue guard) and start guarding attacks you repeat.
- **Arena**: four sparring tiers, from Rookie up to Killua (Godspeed).
- **The hunt**: four **Chimera Ant camps** in the woods, each tougher than the last. Pull ants one at a time,
  ambush them from Zetsu, and earn **Hunter XP** and levels. Clearing every camp opens **the King's castle**.

**Story and presentation**
- **Kite** is your mentor. His story beats unlock with your progress, and the game has an ending after Meruem.
- The **Hunter's Guide**, a notice board and a dashboard page, explains everything you've learned so far.
- Anime toon shading, aura effects that grow with your Ren, hit feedback, damage numbers, a title screen, a pause
  menu with settings, and a soundtrack that follows the moment (see [Soundtrack](#soundtrack)).

## Controls

| Action | Key |
|---|---|
| Move / look / sprint / jump / dodge | WASD · mouse · Shift · Space · Ctrl |
| Light / heavy attack | LMB / RMB |
| Block (tap just before a hit to parry) | Q |
| Lock on | Middle mouse or X |
| Interact · drill difficulty | E · [ / ] |
| Ten · Ren · Zetsu · Gyo | T · hold R · Z · hold G |
| Ko (with Ren up) | F |
| Jajanken: chant, then Rock / Paper / Scissors | hold V, then release / RMB / LMB |
| Dashboard and Hunter's Guide · pause · controls | Tab · Esc · F1 |
| Save · reload | F5 · F9 |

## Getting started

### Requirements
- **Unity 6000.6.0f1** (Unity 6), with the Universal Render Pipeline and the Input System, both set up in this
  project.
- **Git LFS**: the models and textures are stored with LFS. Run `git lfs install` before cloning.

### Open and play
1. Clone the repository and open the folder in Unity Hub with the Unity version above.
2. Open `Assets/_Project/Scenes/TrainingSandbox.unity` and press **Play**.
3. If the scene is missing or broken, rebuild it from the menu: **Hunter Awakening → Build Prototype Scene**. The
   scene is generated from code (`Assets/_Project/Editor/PrototypeSceneBuilder*.cs`), so this always gives you a
   clean, current version.

### Saves
Progress is saved as JSON in Unity's persistent data folder. On Windows that's
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\HunterXHunterNenJourney\hunter_awakening_save.json`. To start over,
use **New game** on the title screen, **Reset progress** in the pause menu, or **Delete save** in the dashboard.

### Soundtrack
The music is the *Hunter x Hunter (2011)* original soundtrack, which is copyrighted, so **it isn't in this
repository**. The game runs silently without it. To add it locally:

1. Get the OST compilation as a single MP3 (80 minutes, 42 tracks).
2. Install [ffmpeg](https://ffmpeg.org/) and Python 3.
3. Run:
   ```bash
   python "Assets/_Project/Audio/split_soundtrack.py.txt" "path/to/Hunter X Hunter 2011 - OST Compilation.mp3"
   ```
   This cuts the compilation by its tracklist into `Assets/_Project/Resources/Music/<Mood>/`, a git-ignored folder.
   The moods are Title, Calm, Castle, Fight and Boss. The game picks the music up automatically, and builds include
   it.

## Project layout

```
Assets/_Project/
  Scripts/        Gameplay code (one assembly, HunterAwakening)
    Core/           Progression, stats, fatigue, saves, game session, music
    Player/         Input, movement, camera, animation
    Combat/         Player and enemy combat, encounters, effects, the arena
    Aura/           Nen techniques, aura visuals, Water Divination
    Training/       The drills
    World/          Interactables: rest bench, castle gate, Kite, guide board
    UI/             HUD, dashboard, Hunter's Guide, dialogue, title, pause and ending screens
  Editor/         Scene builder, model and material import, toon materials
  Art/            Characters (rigged models, Blender prep scripts), shaders, textures
  Audio/          The soundtrack split script
  Data/           Exercise definitions and progression config
  Tests/EditMode/ Unit tests for progression and combat math
```

Characters are rigged in Blender by the scripts in `Assets/_Project/Art/Characters/` (`prepare_characters`,
`autorig`, `prepare_kite`, `prepare_ants`), then imported as Unity Humanoids so they all share one set of animations.

## Tests
Open **Window → General → Test Runner** and run the **EditMode** tests. They cover progression, fatigue, stat
effects, combat math, the aura budget and Hunter levels.

## Roadmap
What's done, what needs playtesting and what's next: see [ROADMAP.md](ROADMAP.md).

## Credits
- *Hunter x Hunter*: created by Yoshihiro Togashi. The 2011 anime is by Madhouse, with music by Yoshihisa Hirano.
- Character models: community creators on Sketchfab and VRChat, adapted for this project.
- Animations: Mixamo.
- Built with Unity.
