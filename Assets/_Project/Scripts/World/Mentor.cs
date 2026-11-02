using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Kite, the player's mentor: Ging's student, a Hunter tracking the Chimera Ants. He tells the story in beats that
    /// unlock with progress (awakening, Water Divination, the first fights, the castle, the King), marks a new beat with
    /// "!" over his head, and otherwise points at the next goal. He greets a new player as soon as the game starts.
    /// </summary>
    public class Mentor : Interactable
    {
        const string Name = "Kite";
        const float LookRadius = 9f;

        readonly struct Beat
        {
            public readonly string id;
            public readonly Func<ProgressionService, bool> ready;
            public readonly string[] lines;

            public Beat(string id, Func<ProgressionService, bool> ready, params string[] lines)
            {
                this.id = id;
                this.ready = ready;
                this.lines = lines;
            }
        }

        static readonly Beat[] Beats =
        {
            new Beat("intro", p => true,
                "So you're Gon. Ging's kid. I'm Kite — I'm the one who found your father, back when I was about your age.",
                "I didn't come all this way to talk about him, though. There are Chimera Ants in these woods. Big ones. And something behind them that worries me a lot more.",
                "You're not ready to face them. Not yet. A Hunter's strength starts with the body: the lift, the sprints, the pads over there. Finish three proper sessions.",
                "Then sit at the Meditation Stone and feel what's been leaking out of you all your life. That's aura. If you forget anything, the board behind me has it all written down."),
            new Beat("awakened", p => p.IsAuraAwakened,
                "You felt it, didn't you? That warmth, like steam coming off your skin. That's your aura. Most people never notice it.",
                "Keep it close with Ten — press T. Shut it off completely with Zetsu, Z, and you'll vanish to anyone sensing for you. Push it out with Ren, hold R, and every punch carries it.",
                "Before anything else, though: there's a glass of water by the stone. Hold Ren over it. What the water does tells you what kind of Nen user you are."),
            new Beat("enhancer", p => p.KnowsInsight(WaterDivination.EnhancerInsight),
                "The water spilled over. An Enhancer — of course you are. Simple, straightforward, and you hit hard. Like father, like son.",
                "Enhancers make what they already have stronger: your fists, your body, even your healing. Train Ren at the circle and Ten on the mat until they're second nature.",
                "Then prove it in the arena. Beat the Sparring Rival. A real fight teaches what drills can't — watch for the red wind-up, dodge or parry it, and punish the opening."),
            new Beat("hunt", p => p.BestSparringTime("rival") > 0f || p.KnowsInsight(AuraController.KenInsight),
                "Not bad. I think you're ready to see what we're up against.",
                "There are four ant camps in the woods around us. Don't charge in — get close to one and only that one will notice you. Pull them apart. Fight them one at a time.",
                "Go in with Zetsu and you can land the first blow before they even know you're there. And if it goes badly, run. Living to train another day is part of the job."),
            new Beat("first_camp", p => Encounter.All.Exists(e => !e.IsBoss && e.Cleared),
                "You cleared a camp. I saw the trees shake from here.",
                "The ones further out are tougher. They'll come back too, if you leave them alone long enough — good hunting, if you need it.",
                "Keep pushing your Ren. When it's strong enough, you'll learn to pour everything into one fist. That's Ko. After that… we'll see what kind of Hatsu you build."),
            new Beat("jajanken", p => p.KnowsInsight(AuraController.JajankenInsight),
                "Rock, paper, scissors? Ha! Only you would turn a children's game into a technique.",
                "It suits you, though. Rock to break, Paper to reach, Scissors to cut through a crowd. Keep using it and the chant will get faster, and the other hands will come."),
            new Beat("gate", p => Encounter.SquadCount() > 0 && Encounter.SquadsLeft() == 0,
                "The seal on the castle is gone. Every camp has fallen, and the King knows it.",
                "Listen to me, Gon. What's waiting in there isn't like anything you've fought. Fast, armoured, and he doesn't hesitate.",
                "Come with everything: Ken to take the hits, Ko and Jajanken to answer them. And come back alive. That's an order."),
            new Beat("king", p => Encounter.All.Exists(e => e.IsBoss && e.Cleared),
                "You actually did it. The King…",
                "When I first met you, I thought you'd need years. You needed something else — the stubbornness to keep getting up.",
                "There's always a stronger opponent somewhere. But today, you're a real Hunter. Ging would be proud. I am too."),
        };

        [SerializeField] Animator animator;
        Quaternion restRotation;
        bool greeted;

        public void Configure(Animator model) => animator = model;

        void Start()
        {
            restRotation = transform.rotation;
            displayName = Name;
        }

        Beat? Pending()
        {
            var p = Progression;
            foreach (var beat in Beats)
                if (beat.ready(p) && !p.HasHeardStory(beat.id)) return beat;
            return null;
        }

        void Update()
        {
            var session = Session;
            if (session == null || session.Player == null) return;
            displayName = Pending() != null ? $"{Name}  <color=#ffc552>(!)</color>" : Name;

            // He greets a new arrival as soon as the title screen is gone.
            if (!greeted && !session.TitleOpen && !session.InputBlocked)
            {
                greeted = true;
                if (!Progression.HasHeardStory("intro")) Talk();
            }

            // Turn to face the player when they're near; otherwise look back out over the grounds.
            Vector3 to = session.Player.transform.position - transform.position;
            to.y = 0f;
            var target = to.magnitude < LookRadius && to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to) : restRotation;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 240f * Time.deltaTime);
        }

        public override string GetPrompt() => Pending() != null ? $"[E] Talk to {Name} — he has something to tell you" : $"[E] Talk to {Name}";

        public override void Interact(PlayerContext player)
        {
            if (Time.unscaledTime - DialogueBox.ClosedAt < 0.3f) return; // the key that closed the last line
            Talk();
        }

        void Talk()
        {
            var p = Progression;
            var beat = Pending();
            if (beat == null)
            {
                DialogueBox.Play(Name, new[] { Advice(p) });
                return;
            }

            if (beat.Value.id == "intro" && Beats[1].ready(p))
            {
                // An old save meeting Kite for the first time: skip the beginner's speech and the beats already lived.
                foreach (var b in Beats)
                    if (b.ready(p)) p.MarkStory(b.id);
                DialogueBox.Play(Name, new[]
                {
                    "So you're Gon. Ging's kid. I'm Kite — I've been watching you train for a while now.",
                    "You've come a long way on your own. There are Chimera Ants in these woods, and something behind them that worries me. I could use someone like you.",
                    "Come talk to me whenever you need a push in the right direction. And the board behind me has everything you've learned written down.",
                });
                return;
            }
            p.MarkStory(beat.Value.id);
            DialogueBox.Play(Name, beat.Value.lines);
        }

        static string Advice(ProgressionService p) =>
            "Here's what I'd do next: " + char.ToLower(PrototypeHUD.NextGoal(p)[0]) + PrototypeHUD.NextGoal(p).Substring(1);
    }
}
