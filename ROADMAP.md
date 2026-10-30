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
- [x] ⚠️ **Ren body enhancement**: +15–35% move speed, +10–25% attack speed and a longer dodge while held; moving with Ren drains extra aura (Sustained Output reduces it). Ren is suppressed in physical drills.
- [x] ⚠️ **Intimidation**: when your Ren output exceeds an opponent's *presence*, it hesitates (slower to attack) and guards more. At starting stats only the Rookie is intimidated.
- [x] ⚠️ **Ken** (Ten + Ren together, unlocks at Ten and Ren mastery 25): rooted stance, damage taken ~0.45×, Ren half as likely to break, combined drain.
- [x] ⚠️ **Ko** (F with Ren up, unlocks at Ren mastery 40 + Max Output 18): 2.5–4× damage, guaranteed guard break and launch, costs 35% of the reserve, ×1.8 damage taken while thrown.
- [ ] ◻️ **Ryu** (shifting aura between offence and defence).

---

## ⭐ Progression gaps

- [ ] ⭐ **Every attribute needs a training source.** Today:
  - ~~Max Output — none~~ → Ren Circle
  - ~~Perception — none~~ → Gyo Reading (reading wind-ups with Gyo in sparring); gameplay effect: wider parry/dodge windows under Gyo
  - ~~Durability — only a Lift Rack side effect~~ → Sparring (every match now counts as training)
  - Efficiency, Recovery Rate — Efficiency only weakly via the Aura Mat; Recovery Rate none
- [ ] ⭐ **Routine training** (GDD §5.1 hybrid model): configure exercise order, intensity, sets and rest, then perform them in sequence.
- [x] ⚠️ **Breakthroughs** (GDD §5.5): six challenges proven in play, each with a permanent reward — Perfect Form, Iron Body, Ten in Motion, Unbroken Ren, Read and Answer, Shadow Strike. Live progress in the dashboard. *Ten in Motion needs ~Endurance 20 (sprint stamina must last 8 s).*
- [ ] ◻️ More breakthroughs that unlock new exercises/techniques (e.g. Ken from Ten + Ren), and multi-stage Attempt → Master → Integrate.
- [x] ⚠️ **Injury / strain** (GDD §9.3): failed reps under overload (≤45 per failure) and training an area while exhausted strain the upper body or legs. Strain cuts gains and strength/speed; at 60+ the area's drills are blocked; rest heals it. No randomness.
- [ ] ◻️ **Knowledge / insight** beyond aura awakening: exercise variants, enemy counters, technique conditions.
- [ ] ◻️ Exercise variants that unlock at higher levels so late game isn't "same drill, longer" (GDD §6.3).

## ⭐ Aura / Nen

- [x] ⚠️ **Zetsu** (Z): aura sealed — hidden from opponents beyond detection range, fast aura/fatigue recovery, ambush strike from hiding; takes ×1.5 damage. Mastery from ambushes and hiding near an active opponent.
- [x] ⚠️ **Gyo** (hold G): reads attack timing (impact countdown, next-attack timer), wider parry/dodge windows scaled by Perception; body slightly less protected. Trains Perception via Gyo Reading.
- [x] ⚠️ **Gyo Stones** drill: find the stone whose aura mark is visible only through Gyo (Perception, Gyo mastery).
- [x] ⚠️ **Aura visuals**: aura flows off the body — a flame shell on a copy of the character's skinned meshes plus flames streaming up from every limb (custom URP shaders in `Resources/Shaders`). Ten is a calm blue shroud, Ren roars orange and erupts when raised, Ken is dense violet, Gyo glows at the eyes, a broken Ren sputters out red. **Ren grows with training** (Ren presence: Max Output + Ren mastery, 1× → 3.5×). *Tune in play; check the fps cost of the shell meshes.*
- [ ] ◻️ Dedicated Zetsu drill (today Zetsu trains only in sparring).
- [ ] ◻️ **Affinity discovery** test (GDD §7.1).
- [ ] ◻️ **Hatsu builder**: data-driven core effect + activation condition + cost + counterplay + enforceable vow (GDD §7.4). Start with one tested ability (e.g. *Binding Current*).

## ⭐ Combat

- [x] ⚠️ **Animate the rival**: Killua model and clips (run, heavy punch timed to the telegraph, block, hit reaction, knockdown), coloured cue light, and a crimson "shadow" colour scheme so it reads as a different fighter. *Check fps with two 100k-vertex hair meshes.*
- [x] ⚠️ **A fighter per tier**: Rookie = Gon, Sparring Rival = Kurapika, Veteran Hunter = Hisoka, Elite Nen User = Meruem (`RivalModels` swaps the model with the tier). Rigged in Blender by an auto-rigger (Mixamo bone names, heat weights, T-pose bind) — *weights are automatic: check deformation in motion (Hisoka's hair, Kurapika's tabard, Meruem's armpits).* Re-run with the Blender scripts in `Art/Characters/Rivals/` (`prepare_characters.py.txt` cleans and scales the downloads, `autorig.py.txt` rigs them).
- [ ] ◻️ Bloster and the Koala chimera ant (AI-generated, ~250k verts, non-humanoid / posed): decimate and give them their own rigs to use as monster opponents.
- [x] ⚠️ **Opponent tiers** at the arena post ([ / ]): Rookie, Sparring Rival, Veteran Hunter, Elite Nen User — fixed stats, best time recorded per tier.
- [x] ⚠️ **Sparring is training**: each match submits a Sparring exercise (Durability, Coordination, Endurance; quality from offense/defense; fights under ~12 s give nothing) and, if Gyo was used, Gyo Reading (Perception).
- [ ] ⭐ Playtest and tune: attack timings, telegraph length, guard adaptation, rival HP/damage.
- [ ] ◻️ More archetypes (GDD §8.4): bruiser, speed specialist, ranged aura user, technical rival.
- [x] ⚠️ **Hit feedback**: hit-stop, camera shake, slow motion on parries/perfect dodges/ambushes/knockouts (toggleable).
- [x] ⚠️ **Impact VFX and sounds**: spark bursts and code-synthesised sounds (thud, crack, clang, block, whoosh, Ko boom); volume in the pause menu. *Placeholder quality — replace with real assets later.*

## Player model & animation

- [ ] ⭐ **Textures are missing.** The `.blend` links to `..\killua Project\*.png` on the original author's machine. Need: `Killua_Skin.png`, `KilluaNM.png`, `killuaShirt.png`, `killuaShirtNM.png`, `KilluaPants.png`, `killuaPantsNM.png`, `Eye.png`.
- [ ] ◻️ Add a **Meditating** clip (Meditation Stone and Aura Mat currently reuse *Sitting Idle*).
- [ ] ◻️ Check remaining retarget residuals in motion: feet ~5–6° vs source, shins ~7–8°; sitting may sink slightly into the floor.
- [ ] ◻️ Tune the dodge roll's playback speed and the attack clip timing once combat is playtested.
- [ ] ◻️ Performance: hair mesh is ~100k vertices. Decimate if the 144 fps target isn't held in a build.

## UI

- [ ] ◻️ Replace the IMGUI placeholder HUD/dashboard with uGUI or UI Toolkit.
- [ ] ◻️ Routine-builder UI (with routine training).
- [x] ⚠️ **Pause/settings menu** (Esc): sensitivity, invert Y, fps cap, VSync, FPS counter, camera shake and hit-stop toggles (saved in PlayerPrefs).
- [ ] ◻️ Key rebinding.
- [x] **Playability pass**: dashboard pauses the game; out-of-combat health regen; drill prompts explain what each drill trains; station labels visible from 60 m; arena remembers the chosen opponent; HUD "Next goal" guide from first workout to Elite.
- [x] **Bug fixes**: pause menu no longer writes settings to disk every frame; Zetsu blocked during physical drills (its fatigue recovery inflated them); Gyo Stones no longer fails the round right after a correct touch; lift strain capped per failure.

## Look

- [x] ⚠️ **Scene look** (`SceneLook`, generated by the scene builder): late-afternoon sun with soft shadows, procedural sky, three-colour ambient, distance fog; post-processing (ACES tonemapping, grading, bloom for aura, vignette, SMAA); generated grass (with a large-scale variation map against tiling), sand and stone textures; ~170 low-poly trees, boulders and horizon hills around a 400 m field. *Check fps in a build; the stations are still placeholder primitives.*
- [x] ⚠️ **Anime shading** (`Art/Shaders/Toon.shader`, applied by `ToonMaterials`): cel ramp with coloured shadows that respect cast shadows, rim light, glint, and screen-constant ink outlines on every fighter. *Tune outline width and shadow tint per character in play.*
- [x] ⚠️ **Station props** (`StationProps`): wooden bench, squat rack with chrome bar, stacked plates and a plate tree, stone plinths under the pads, signposts, sprint pennants, a boulder for the Meditation Stone, glowing seals on the Aura Mat / Ren Circle / Gyo Stones, arena pillars and banners. Gameplay colliders are unchanged.
- [ ] ◻️ Grass and flower clumps near the stations; a dirt path linking them.

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
