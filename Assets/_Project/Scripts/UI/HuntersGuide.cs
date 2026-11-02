using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// The Hunter's Guide: everything the game teaches, in one place to look it up again — the goal, the training
    /// stations, each Nen technique (and how to unlock the ones not learned yet), Water Divination and the six types,
    /// Jajanken, combat and the hunt. A page of the dashboard; the notice board at the training grounds opens it.
    /// </summary>
    public class HuntersGuide
    {
        readonly struct Entry
        {
            public readonly string title, body, locked;
            public readonly Func<ProgressionService, bool> known;

            public Entry(string title, string body, Func<ProgressionService, bool> known = null, string locked = null)
            {
                this.title = title;
                this.body = body;
                this.known = known;
                this.locked = locked;
            }
        }

        readonly struct Chapter
        {
            public readonly string name;
            public readonly Entry[] entries;

            public Chapter(string name, params Entry[] entries)
            {
                this.name = name;
                this.entries = entries;
            }
        }

        static bool Aura(ProgressionService p) => p.IsAuraAwakened;
        static bool Enhancer(ProgressionService p) => p.KnowsInsight(WaterDivination.EnhancerInsight);
        static bool Jajanken(ProgressionService p) => p.KnowsInsight(AuraController.JajankenInsight);

        static readonly Chapter[] Chapters =
        {
            new Chapter("The Path",
                new Entry("Your goal",
                    "Become strong enough to stop the Chimera Ants. Train your body, awaken your aura and learn Nen, then clear the ant " +
                    "camps in the woods. When every camp has fallen, the King's seal on the castle to the north breaks and Meruem waits inside.\n" +
                    "The panels under your health always show the next step."),
                new Entry("Training stations",
                    "<b>Lift Rack</b> — Strength. Press when the needle is in the green.\n" +
                    "<b>Sprint Course</b> — Speed and Endurance. Hold Shift through the posts.\n" +
                    "<b>Precision Pads</b> — Coordination and Mobility. Reach each glowing pad in time; jump for raised ones.\n" +
                    "<b>Aura Mat</b> — aura control (Ten). <b>Ren Circle</b> — aura output (Ren). <b>Gyo Stones</b> — Perception.\n" +
                    "<b>[ / ]</b> changes the difficulty. A station that's too easy stops teaching you: raise it."),
                new Entry("Fatigue and rest",
                    "Every session tires you. Tired training teaches less, and pushing an overloaded body builds strain that can lock a " +
                    "station until you recover. Sit on the <b>Rest Bench</b> (E) to recover fatigue and heal."),
                new Entry("Hunter Level",
                    "Beating opponents gives Hunter XP: chimera ants, the King and arena wins. Each level adds +3% max health and +2% damage, " +
                    "and a level-up refills your health and stamina. Training makes you strong; hunting makes you seasoned.")),

            new Chapter("Nen Basics",
                new Entry("Aura awakening",
                    "Aura is the life energy leaking from every body. After 3 training sessions, meditate at the <b>Meditation Stone</b> (E) " +
                    "to sense it and hold it. Every Nen technique starts here.",
                    Aura, "Complete 3 training sessions, then meditate at the Meditation Stone."),
                new Entry("Ten — press T",
                    "Keep your aura flowing around the body instead of leaking away. A steady shroud that softens hits and slows aura loss. " +
                    "Train it at the Aura Mat.",
                    Aura, "Awaken your aura first."),
                new Entry("Zetsu — press Z",
                    "Seal your aura completely. Opponents lose track of you beyond a few metres, and your next strike is an <b>ambush</b> that " +
                    "ignores guard and staggers. But with no aura around you, every hit lands harder.",
                    Aura, "Awaken your aura first."),
                new Entry("Ren — hold R",
                    "Push out more aura than usual. Strikes hit much harder, guard-break and knock back, and weaker opponents hesitate. " +
                    "Ren drains aura quickly and can break under pressure. Train output at the Ren Circle.",
                    Aura, "Awaken your aura first."),
                new Entry("Hatsu",
                    "Your own ability: Nen shaped by your type and your will. Gon's is Jajanken (see its chapter).",
                    Jajanken, "Learn Ko, then raise Ren mastery to 50.")),

            new Chapter("Advanced Nen",
                new Entry("Gyo — hold G",
                    "Focus aura into the eyes. You see hidden aura (the Gyo Stones drill), and in a fight Gyo reads an opponent's timing: " +
                    "the exact moment a wind-up lands, and when the next one can start.",
                    Aura, "Awaken your aura first."),
                new Entry("Ken — Ten + Ren",
                    "Hold Ren while Ten is up: a rooted, all-round defence far sturdier than either alone, with steadier Ren.",
                    p => p.KnowsInsight(AuraController.KenInsight),
                    $"Reach {MasteryMilestones.KenUnlockTenMastery:0} mastery in both Ten and Ren (fight with them up, train at the Aura Mat and Ren Circle)."),
                new Entry("Ko — F with Ren up",
                    "Pour every drop of aura into one fist. Devastating and expensive, and you're defenceless while it's thrown.",
                    p => p.KnowsInsight(AuraController.KoInsight),
                    $"Ren mastery {MasteryMilestones.KoUnlockRenMastery:0} and Max Output {MasteryMilestones.KoUnlockMaxOutput:0} (the Ren Circle).")),

            new Chapter("Water Divination",
                new Entry("The test",
                    "A glass filled to the brim, a leaf floating on top. Cup your hands around it and use Ren: what happens to the water " +
                    "tells you your Nen type. The glass stands beside the Meditation Stone (E, then hold R)."),
                new Entry("Your type: Enhancer",
                    "The water rose and spilled over. Like Gon, you strengthen what you already have: aura-powered strikes (Ren, Ko, " +
                    "Jajanken) hit 10% harder, and your body heals 50% faster out of combat.",
                    Enhancer, "Take the Water Divination test."),
                new Entry("The six types",
                    "<b>Enhancer</b> — the water's volume grows. Strengthens body and objects.\n" +
                    "<b>Transmuter</b> — the water's taste changes. Gives aura properties (electricity, elasticity).\n" +
                    "<b>Emitter</b> — the water's colour changes. Sends aura away from the body.\n" +
                    "<b>Conjurer</b> — impurities appear in the water. Makes objects out of aura.\n" +
                    "<b>Manipulator</b> — the leaf moves. Controls things and living beings.\n" +
                    "<b>Specialist</b> — anything else. Abilities that fit no other type.")),

            new Chapter("Jajanken",
                new Entry("The chant — hold V",
                    "\"Saisho wa guu… Jan… Ken…\" The longer you chant, the stronger the hand; finish the chant for a bonus. You're rooted " +
                    "and take extra damage while chanting, and a hit scatters part of the charge — find an opening first. " +
                    "Mastery shortens the chant.",
                    Jajanken, "Learn Ko, then raise Ren mastery to 50."),
                new Entry("Rock (Guu) — release V",
                    "A point-blank punch with everything in it. Breaks guard, staggers and launches; its shockwave hits others nearby for half.",
                    Jajanken, "Learn Jajanken."),
                new Entry("Paper (Paa) — RMB while chanting",
                    "Throw the gathered aura as a ball (22 m). Weaker than Rock, but it reaches; it bursts on impact and catches anyone close.",
                    p => p.GetMastery(TechniqueId.Jajanken) >= MasteryMilestones.JajankenPaper,
                    $"Jajanken mastery {MasteryMilestones.JajankenPaper:0}."),
                new Entry("Scissors (Chii) — LMB while chanting",
                    "Shape the aura into a blade and sweep: an 80° arc out to 4 m that cuts everyone in it.",
                    p => p.GetMastery(TechniqueId.Jajanken) >= MasteryMilestones.JajankenScissors,
                    $"Jajanken mastery {MasteryMilestones.JajankenScissors:0}.")),

            new Chapter("Combat",
                new Entry("Reading a fight",
                    "<color=#ff5544>Red</color> glow and \"!! ATTACK !!\" — a wind-up: dodge or parry before it lands.\n" +
                    "<color=#ffdd55>Yellow</color> \"OPEN\" — recovering or staggered: punish now.\n" +
                    "<color=#66aaff>Blue</color> \"GUARD\" — blocking: light hits barely land, heavies and Ren break it.\n" +
                    "Opponents remember: repeat the same attack and they'll start guarding it."),
                new Entry("Attacks and combos",
                    "<b>LMB</b> light, <b>RMB</b> heavy. A light that connects chains into a second light, then a light or heavy finisher — " +
                    "press again during the swing. <b>Middle mouse / X</b> locks on: the camera tracks the target and you strafe."),
                new Entry("Defence",
                    "<b>Q</b> blocks (stamina per hit). Tap Q just before a hit lands to <b>parry</b>: the attacker is stunned open. " +
                    "<b>Ctrl</b> dodges; dodging right as a hit lands is a <b>perfect dodge</b>."),
                new Entry("The arena",
                    "Spar at the arena post (E): Rookie, Sparring Rival, Veteran Hunter, Killua (Godspeed). Choose with [ / ]. Every match is " +
                    "training, and the dashboard (Tab) breaks down what went wrong.")),

            new Chapter("The Hunt",
                new Entry("Chimera ant camps",
                    "Four camps in the woods, each a soldier and two grunts, each tougher than the last. An ant notices you within about 8 m — " +
                    "pull them one at a time. Run far enough and it gives up; fall and you wake at the rest bench. Beaten ants return after " +
                    "a while, so camps are good hunting grounds for XP."),
                new Entry("Sneaking",
                    "In Zetsu, ants only notice you up close. Strike one that hasn't seen you for an ambush — only that one wakes."),
                new Entry("The King's castle",
                    "Sealed by Meruem's aura until every camp has fallen once. Inside, the King waits on his throne: fast, armoured, and " +
                    "far beyond any ant. Come with Ken, Ko and Jajanken.")),
        };

        int chapter;
        Vector2 scroll;

        public void Draw(Rect area, ProgressionService p)
        {
            const float sidebar = 210f;
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(HudGui.CardBox, GUILayout.Width(sidebar), GUILayout.ExpandHeight(true));
            HudGui.SectionHeader("Chapters");
            for (int i = 0; i < Chapters.Length; i++)
            {
                int known = 0;
                foreach (var e in Chapters[i].entries)
                    if (e.known == null || e.known(p)) known++;
                string label = $"{Chapters[i].name}  <size=11><color=#9aa4b2>{known}/{Chapters[i].entries.Length}</color></size>";
                if (GUILayout.Button(label, i == chapter ? HudGui.PrimaryButton : GUI.skin.button, GUILayout.Height(34)))
                {
                    chapter = i;
                    scroll = Vector2.zero;
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("Locked entries say how to unlock them.", HudGui.Small);
            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.BeginVertical(HudGui.CardBox, GUILayout.ExpandHeight(true));
            scroll = GUILayout.BeginScrollView(scroll);
            var current = Chapters[chapter];
            GUILayout.Label(current.name, HudGui.Header);
            GUILayout.Space(6);
            foreach (var e in current.entries)
            {
                bool known = e.known == null || e.known(p);
                if (known)
                {
                    GUILayout.Label($"<b>{e.title}</b>", HudGui.Label);
                    GUILayout.Label(e.body, HudGui.Wrap);
                }
                else
                {
                    GUILayout.Label($"<color=#9aa4b2><b>{e.title}</b>  <size=10>LOCKED</size></color>", HudGui.Label);
                    GUILayout.Label($"<color=#9aa4b2>Not learned yet. {e.locked}</color>", HudGui.Wrap);
                }
                GUILayout.Space(10);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
