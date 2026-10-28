# Hunter: Awakening — Roadmap

Source of truth for design: `Hunter_Awakening_Unity_GDD.md` (v0.2).
This file tracks what exists, what is missing, and what to do next.

**Legend:** 🔥 next up · ⭐ high priority · ◻️ later · ✅ done · ⚠️ done but needs verification

---

## Where we are

The first playable vertical slice exists: the `TrainingSandbox` scene (built by **Hunter Awakening → Build Prototype Scene**), three physical drills, aura awakening, Ten/Ren, an aura control drill, a fixed-stat sparring rival, fatigue, save/load and a progression dashboard. Killua is the animated player model (12 Mixamo clips).

### GDD §13 — MVP definition of done

| # | Criterion | Status |
|---|-----------|--------|
| 1 | Player trains and sees a measurable improvement | ✅ Dashboard shows before → after and the practical effect |
| 2 | Improvement changes movement or combat | ✅ Speed, stamina, jump, damage, HP all derive from attributes |
| 3 | Training too hard has a visible cost | ✅ Immediate + accumulated fatigue lower readiness, performance and gains |
| 4 | Technique execution matters independently of stats | ⚠️ Form/spacing/timing scored; needs playtesting to confirm it *feels* that way |
| 5 | Opponent reveals a weakness the player can train | ✅ Combat report names the weakness and the drill that fixes it |
| 6 | Returning after training produces a different encounter | ⚠️ Mechanically true; not yet playtested end-to-end |
| 7 | Feedback explains what happened and why | ✅ Causal notes (too easy, sloppy form, fatigue, diminishing returns) |
| 8 | Save/load preserves progression | ✅ Versioned JSON, validation, autosave |
| 9 | Closing the game grants no progress | ✅ All recovery/progress ticks only in `Update` |
| 10 | Ordinary enemies don't scale with the player | ✅ Rival stats are fixed serialized values |

**Remaining for MVP sign-off:** a full playtest of the loop *train → fight → read weakness → train → fight again* (criteria 4 and 6), then a balance pass on the numbers it exposes.

---

## Ren overhaul

- [x] ⚠️ **Ren Circle drill**: flare output above a rising target through pressure waves without crossing the control ceiling. Trains **Max Output**, **Sustained Output**, Aura Capacity and Ren mastery. *Needs playtesting for difficulty and aura cost.*
- [x] ⚠️ **Ren is also defence**: cuts incoming damage more than Ten (doesn't stack with Ten — that's Ken).
- [x] ⚠️ **Output vs. control**: leakage and the chance a hit breaks Ren grow when Max Output outpaces Control Precision; a break locks Ren for 1.5 s and opens a 1 s window of +30% damage taken. Combat report flags Ren breaks.
- [x] ⚠️ **Ren hits feel heavier**: even light Ren hits break guard; Ren hits stagger (a Ren heavy cuts through the rival's wind-up armour) and knock back.
- [ ] 🔥 **Playtest and tune** the numbers above (break chance, lockout, knockback, drill aura cost).
- [ ] ◻️ **Intimidation**: a clearly stronger Ren makes weaker enemies hesitate and guard more (GDD §8.3 power gaps).
- [ ] ◻️ Advanced applications after Ren feels right: **Ken** (Ten + Ren stance), **Ko**, **Ryu**.

---

## ⭐ Progression gaps

- [ ] ⭐ **Every attribute needs a training source.** Today:
  - ~~Max Output — none~~ → Ren Circle
  - Perception — none, and no gameplay effect yet (future: aura cues / Gyo)
  - Durability — only a 0.2 side effect of the Lift Rack
  - Efficiency, Recovery Rate — Efficiency only weakly via the Aura Mat; Recovery Rate none
- [ ] ⭐ **Routine training** (GDD §5.1 hybrid model): configure exercise order, intensity, sets and rest, then perform them in sequence.
- [ ] ⭐ **Breakthroughs** (GDD §5.5): Attempt → Master → Integrate milestones (e.g. "Ten during a full sprint") that unlock exercises/techniques. Currently only per-technique mastery milestones exist.
- [ ] ◻️ **Injury / strain** (GDD §9.3): from overload or failed reps; limits specific activities until rested.
- [ ] ◻️ **Knowledge / insight** beyond aura awakening: exercise variants, enemy counters, technique conditions.
- [ ] ◻️ Exercise variants that unlock at higher levels so late game isn't "same drill, longer" (GDD §6.3).

## ⭐ Aura / Nen

- [ ] ⭐ **Zetsu** and **Gyo** as techniques with their own drills (GDD §5.2).
- [ ] ◻️ **Affinity discovery** test (GDD §7.1).
- [ ] ◻️ **Hatsu builder**: data-driven core effect + activation condition + cost + counterplay + enforceable vow (GDD §7.4). Start with one tested ability (e.g. *Binding Current*).

## ⭐ Combat

- [x] ⚠️ **Animate the rival**: uses the Killua model and clips (run, heavy punch timed to the telegraph, block, hit reaction, knockdown) with a coloured cue light. *Two identical Killuas — consider a different model or colour variant for the rival; check fps with two 100k-vertex hair meshes.*
- [ ] ⭐ Playtest and tune: attack timings, telegraph length, guard adaptation, rival HP/damage.
- [ ] ◻️ More archetypes (GDD §8.4): bruiser, speed specialist, ranged aura user, technical rival.
- [ ] ◻️ Hit feedback: hit-stop, impact VFX, sounds.

## Player model & animation

- [ ] ⭐ **Textures are missing.** The `.blend` links to `..\killua Project\*.png` on the original author's machine. Need: `Killua_Skin.png`, `KilluaNM.png`, `killuaShirt.png`, `killuaShirtNM.png`, `KilluaPants.png`, `killuaPantsNM.png`, `Eye.png`.
- [ ] ◻️ Add a **Meditating** clip (Meditation Stone and Aura Mat currently reuse *Sitting Idle*).
- [ ] ◻️ Check remaining retarget residuals in motion: feet ~5–6° vs source, shins ~7–8°; sitting may sink slightly into the floor.
- [ ] ◻️ Tune the dodge roll's playback speed and the attack clip timing once combat is playtested.
- [ ] ◻️ Performance: hair mesh is ~100k vertices. Decimate if the 144 fps target isn't held in a build.

## UI

- [ ] ◻️ Replace the IMGUI placeholder HUD/dashboard with uGUI or UI Toolkit.
- [ ] ◻️ Routine-builder UI (with routine training).
- [ ] ◻️ Pause/settings menu (sensitivity, key rebinding, VSync toggle).

## World (only after the core loop is proven — GDD §15)

- [ ] ◻️ Hunter's Haven hub, then The Frontier, Combat District, Forbidden Zone (GDD §10).
- [ ] ◻️ Capability-gated routes (e.g. mountain path needs Mobility).

## Tech & quality

- [ ] ⭐ Playtest pass against the MVP definition of done; log findings here.
- [ ] ◻️ Tests for combat scoring, fatigue/readiness and the anti-farming rules (idle, spam, sloppy repetition).
- [ ] ◻️ Profile a standalone build (frame time spikes seen in the Editor ~20 ms).
- [ ] ◻️ **IP:** names, characters and Killua's model are Hunter x Hunter IP. Fine for a personal prototype; a commercial release needs original content (GDD §1).

---

## Done ✅

- Progression service: uncapped attributes with diminishing returns, mastery with milestones, central and validated
- Fatigue: immediate + accumulated, readiness, in-game rest only
- Drills: Lift Rack (Strength), Sprint Course (Speed/Endurance), Precision Pads (Coordination/Mobility), Aura Mat (Ten control)
- Aura awakening (gated by completed sessions), Ten toggle, Ren hold
- Combat: light/heavy with startup/active/recovery, block + parry, dodge with i-frames, mastery from quality, anti-spam
- Sparring rival with telegraphs, guard adaptation, punish windows; combat weakness report
- Save/load (versioned, sanitized, autosave), dashboard, HUD, FPS counter, 144 fps cap
- Killua as a Humanoid (Rigify → game rig export, Mixamo-matched reference pose, Foot IK) with 12 animations driven by gameplay
- EditMode tests for progression math, service and save round-trip
