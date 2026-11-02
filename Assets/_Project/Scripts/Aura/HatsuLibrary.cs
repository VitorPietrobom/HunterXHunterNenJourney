using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// The interview and the offline Hatsu author. Each answer leans toward Nen types (after the personality chart in
    /// Hunter x Hunter: Enhancers simple and determined, Transmuters whimsical, Emitters quick-tempered, Conjurers
    /// high-strung and careful, Manipulators logical, Specialists individualists) and toward traits that shape the
    /// ability: one target or many, a vow, how it feels. From that it writes a real Lua script — commented, so the
    /// player can read and change it — in the format HatsuRuntime runs.
    /// </summary>
    public static class HatsuLibrary
    {
        public readonly struct Answer
        {
            public readonly string text;
            public readonly NenType type;
            public readonly int weight;
            public readonly string trait;

            public Answer(string text, NenType type, int weight = 2, string trait = null)
            {
                this.text = text;
                this.type = type;
                this.weight = weight;
                this.trait = trait;
            }
        }

        public readonly struct Question
        {
            public readonly string text;
            public readonly Answer[] answers;

            public Question(string text, params Answer[] answers)
            {
                this.text = text;
                this.answers = answers;
            }
        }

        public static readonly Question[] Interview =
        {
            new Question("Someone insults a friend of yours, right in front of you. What do you do?",
                new Answer("Hit him. Right now.", NenType.Enhancer),
                new Answer("Laugh it off — then make him regret it in a way he'll never see coming.", NenType.Manipulator),
                new Answer("Out-talk him until he's the joke.", NenType.Transmuter),
                new Answer("Lose my temper and say something I'll regret.", NenType.Emitter)),
            new Question("In a fight, where do you want to be?",
                new Answer("Up close. Toe to toe.", NenType.Enhancer, 2, "single"),
                new Answer("Far away, where they can't touch me.", NenType.Emitter, 2, "range"),
                new Answer("Somewhere I prepared in advance.", NenType.Conjurer, 2, "trap"),
                new Answer("Wherever I decide they should be.", NenType.Manipulator, 2, "control")),
            new Question("Choose something to carry with you forever.",
                new Answer("Nothing. My hands are enough.", NenType.Enhancer),
                new Answer("A chain.", NenType.Conjurer),
                new Answer("A pack of gum.", NenType.Transmuter),
                new Answer("Something nobody else in the world has.", NenType.Specialist, 2)),
            new Question("Your plan just fell apart. Now what?",
                new Answer("Push harder. It'll work if I want it enough.", NenType.Enhancer),
                new Answer("Improvise — this is the fun part.", NenType.Transmuter),
                new Answer("I already had a backup plan.", NenType.Manipulator),
                new Answer("Fall back and rebuild it properly.", NenType.Conjurer)),
            new Question("How would your friends describe you?",
                new Answer("Honest and stubborn.", NenType.Enhancer),
                new Answer("Unpredictable. A bit of a liar.", NenType.Transmuter),
                new Answer("Quick to anger, quick to forgive.", NenType.Emitter),
                new Answer("Not like anyone else they know.", NenType.Specialist, 3)),
            new Question("Five enemies surround you.",
                new Answer("Good. All of them at once.", NenType.Emitter, 2, "group"),
                new Answer("One at a time — I'll make sure of it.", NenType.Conjurer, 1, "single"),
                new Answer("Turn them on each other.", NenType.Manipulator, 2, "group"),
                new Answer("Take down the strongest. The rest will run.", NenType.Enhancer, 1, "single")),
            new Question("What would you give up for power?",
                new Answer("Nothing. Power on my terms, or not at all.", NenType.Enhancer, 1, "vow_none"),
                new Answer("My safety — I'd only use it when I'm already hurt.", NenType.Specialist, 1, "vow_wounded"),
                new Answer("Time — I'd wait as long as it takes.", NenType.Conjurer, 1, "vow_time"),
                new Answer("Freedom — I'd only ever use it one on one.", NenType.Manipulator, 1, "vow_duel")),
            new Question("Last one. Your ability should feel like…",
                new Answer("One blow nobody gets up from.", NenType.Enhancer, 2, "blow"),
                new Answer("A storm.", NenType.Emitter, 2, "storm"),
                new Answer("A cage they can't escape.", NenType.Conjurer, 2, "bind"),
                new Answer("Something no one has seen before.", NenType.Specialist, 2, "strange")),
        };

        public static Color TypeColor(NenType type) => type switch
        {
            NenType.Enhancer => new Color(1f, 0.55f, 0.15f),
            NenType.Transmuter => new Color(0.75f, 0.35f, 1f),
            NenType.Emitter => new Color(1f, 0.3f, 0.3f),
            NenType.Conjurer => new Color(0.35f, 0.6f, 1f),
            NenType.Manipulator => new Color(0.35f, 0.95f, 0.55f),
            _ => new Color(1f, 0.85f, 0.35f),
        };

        public static string Divination(NenType type) => type switch
        {
            NenType.Enhancer => "The water rose and spilled over the rim.",
            NenType.Transmuter => "The water's taste changed — sweet, of all things.",
            NenType.Emitter => "The water changed colour.",
            NenType.Conjurer => "Tiny crystals appeared in the water.",
            NenType.Manipulator => "The leaf began to move on its own.",
            _ => "Something happened that none of the other types would cause.",
        };

        /// <summary>Picks the type from the answers (Specialists are rare: they need a clear lead).</summary>
        public static NenType ScoreType(IList<int> picks, out HashSet<string> traits)
        {
            var score = new Dictionary<NenType, int>();
            traits = new HashSet<string>();
            for (int q = 0; q < Interview.Length && q < picks.Count; q++)
            {
                var a = Interview[q].answers[Mathf.Clamp(picks[q], 0, Interview[q].answers.Length - 1)];
                score[a.type] = (score.TryGetValue(a.type, out int s) ? s : 0) + a.weight;
                if (a.trait != null) traits.Add(a.trait);
            }
            NenType best = NenType.Enhancer;
            int bestScore = -1;
            foreach (var pair in score)
            {
                int value = pair.Key == NenType.Specialist ? pair.Value - 2 : pair.Value;
                if (value > bestScore)
                {
                    best = pair.Key;
                    bestScore = value;
                }
            }
            return best;
        }

        static readonly Dictionary<NenType, (string[] first, string[] second)> Names = new Dictionary<NenType, (string[], string[])>
        {
            [NenType.Enhancer] = (new[] { "Iron", "Wild", "Lion's", "Crimson", "Mountain" }, new[] { "Fang", "Hammer", "Fist", "Roar", "Breaker" }),
            [NenType.Transmuter] = (new[] { "Static", "Velvet", "Thunder", "Rubber", "Mirage" }, new[] { "Thread", "Spark", "Ribbon", "Bind", "Coil" }),
            [NenType.Emitter] = (new[] { "Comet", "Scatter", "Falling", "Hail", "Burning" }, new[] { "Barrage", "Shot", "Rain", "Lance", "Volley" }),
            [NenType.Conjurer] = (new[] { "Binding", "Silver", "Phantom", "Judgement", "Iron" }, new[] { "Chain", "Cage", "Blade", "Shackle", "Lock" }),
            [NenType.Manipulator] = (new[] { "Puppet", "Marionette", "Tide", "Gravity", "Shepherd's" }, new[] { "Strings", "Pull", "Command", "Well", "Call" }),
            [NenType.Specialist] = (new[] { "Borrowed", "Fated", "Hollow", "Mirror", "Last" }, new[] { "Hour", "Dice", "Echo", "Crown", "Promise" }),
        };

        /// <summary>Writes the Hatsu: a name, a description, a vow and its Lua source.</summary>
        public static void Author(HunterProfile hunter, IList<int> picks)
        {
            var type = ScoreType(picks, out var traits);
            int seed = hunter.name.GetHashCode() ^ string.Join(",", picks).GetHashCode();
            var rng = new System.Random(seed);
            var (first, second) = Names[type];
            string name = $"{first[rng.Next(first.Length)]} {second[rng.Next(second.Length)]}";

            string vowKind = traits.Contains("vow_wounded") ? "wounded" : traits.Contains("vow_time") ? "time"
                           : traits.Contains("vow_duel") ? "duel" : "none";
            string vow = vowKind switch
            {
                "wounded" => "Only when I'm already hurt (below half health).",
                "time" => "I gather it slowly — a long charge, for a heavier blow.",
                "duel" => "Never against a crowd: with two or more foes close, it fails.",
                _ => "",
            };

            string body;
            string description;
            float cost, cooldown, charge;
            switch (type)
            {
                case NenType.Enhancer when traits.Contains("storm") || traits.Contains("group"):
                    (body, description, cost, cooldown, charge) = (EnhancerFlurry, "A flurry of reinforced blows on everyone in front of you.", 45, 5, 0.8f);
                    break;
                case NenType.Enhancer:
                    (body, description, cost, cooldown, charge) = (EnhancerBlow, "Gather everything into one fist, close the distance and strike — the shockwave hits anyone nearby.", 45, 4, 1.4f);
                    break;
                case NenType.Transmuter when traits.Contains("group") || traits.Contains("storm"):
                    (body, description, cost, cooldown, charge) = (TransmuterChain, "Your aura turns to lightning that leaps from foe to foe, stunning each one.", 40, 5, 0.6f);
                    break;
                case NenType.Transmuter:
                    (body, description, cost, cooldown, charge) = (TransmuterTether, "Aura with the properties of rubber: stick it to a foe, then yank them in for a hit, again and again.", 40, 5, 0f);
                    break;
                case NenType.Emitter:
                    (body, description, cost, cooldown, charge) = (EmitterVolley, "Release a fan of aura blasts — the longer you hold, the more you fire.", 40, 4, 1.2f);
                    break;
                case NenType.Conjurer when traits.Contains("storm") || traits.Contains("group"):
                    (body, description, cost, cooldown, charge) = (ConjurerBlades, "Conjure a ring of blades that cut everyone around you, twice.", 45, 6, 0.6f);
                    break;
                case NenType.Conjurer:
                    (body, description, cost, cooldown, charge) = (ConjurerChain, "A conjured chain shackles one foe in place while you strike it, link by link.", 45, 7, 0f);
                    break;
                case NenType.Manipulator:
                    (body, description, cost, cooldown, charge) = (ManipulatorPull, "Invisible strings drag every nearby foe to one point in front of you — then the knot bursts.", 45, 7, 0.5f);
                    break;
                default:
                    (body, description, cost, cooldown, charge) = (SpecialistHour, "Borrow a few seconds from fate: faster, harder, tougher, and healed — but only for a moment.", 50, 14, 0f);
                    break;
            }
            if (vowKind == "time") charge += 1f;

            var sb = new StringBuilder();
            sb.AppendLine($"-- {name} — {hunter.name}'s Hatsu ({type})");
            sb.AppendLine($"-- {description}");
            sb.AppendLine("-- This is real code: change anything. Everything the Hatsu can do goes through the nen table");
            sb.AppendLine("-- (see the reference on the Hatsu page). power is how much you charged it, 0.25 to 1.");
            sb.AppendLine();
            sb.AppendLine("hatsu = {");
            sb.AppendLine($"  name = \"{name}\",");
            // Invariant numbers: Lua needs "1.4", whatever the player's locale writes.
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            sb.AppendLine($"  cost = {cost.ToString("0", inv)},        -- aura spent each use (more aura = more power per use)");
            sb.AppendLine($"  cooldown = {cooldown.ToString("0.#", inv)},     -- seconds before you can use it again");
            sb.AppendLine($"  charge = {charge.ToString("0.0", inv)},     -- seconds of holding V for full power (0 = instant)");
            sb.AppendLine($"  vow = \"{vow}\",");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine(VowCheck(vowKind));
            sb.Append(body);

            hunter.nenType = type;
            hunter.hatsuName = name;
            hunter.hatsuDescription = description + (vow.Length > 0 ? $" Vow: {vow}" : "");
            hunter.hatsuVow = vow;
            hunter.hatsuSource = hunter.generatedSource = sb.ToString();
        }

        static string VowCheck(string kind) => kind switch
        {
            "wounded" =>
@"-- The vow: only when already hurt. Keeping it makes every hit 40% stronger.
local function vow_kept()
  local me = nen.me()
  if me.hp > me.max_hp * 0.5 then
    nen.say(""The vow holds you back — you're not hurt enough yet."")
    nen.cancel()
    return false
  end
  return true
end
local VOW_POWER = 1.4
",
            "duel" =>
@"-- The vow: never against a crowd. Keeping it makes every hit 40% stronger.
local function vow_kept()
  if #nen.enemies(8) >= 2 then
    nen.say(""Too many foes — the vow forbids it."")
    nen.cancel()
    return false
  end
  return true
end
local VOW_POWER = 1.4
",
            "time" =>
@"-- The vow: a long charge. A full one hits 30% harder.
local function vow_kept() return true end
local VOW_POWER = 1.3
",
            _ =>
@"-- No vow: nothing given up, nothing extra gained.
local function vow_kept() return true end
local VOW_POWER = 1.0
",
        };

        const string EnhancerBlow =
@"function hatsu.activate(power)
  if not vow_kept() then return end
  local target = nen.nearest(6)
  if target == nil then
    nen.say(""Nothing to hit."")
    nen.cancel()
    return
  end
  -- Close the gap, then everything into one fist.
  if target.distance > 2 then nen.dash(math.min(target.distance - 1.5, 5)) end
  nen.callout(hatsu.name .. ""!"")
  nen.strike(target, (2 + 2.5 * power) * VOW_POWER, { stagger = 0.8 + power, knockback = 7 })
  -- The shockwave catches anyone standing close.
  nen.after(0.1, function()
    nen.burst(target.x, target.z, 3, 0.6 * power * VOW_POWER, { knockback = 4 })
  end)
end
";

        const string EnhancerFlurry =
@"function hatsu.activate(power)
  if not vow_kept() then return end
  nen.callout(hatsu.name .. ""!"")
  local hits = 4 + math.floor(power * 4)
  nen.every(0.12, hits, function()
    for _, foe in ipairs(nen.ahead(3.5, 70)) do
      nen.strike(foe, 0.55 * VOW_POWER, { stagger = 0.2, knockback = 1 })
    end
  end)
end
";

        const string TransmuterTether =
@"function hatsu.activate(power)
  if not vow_kept() then return end
  local target = nen.nearest(14)
  if target == nil then
    nen.say(""No one to stick to."")
    nen.cancel()
    return
  end
  nen.callout(hatsu.name .. ""!"")
  nen.strike(target, 0.5 * VOW_POWER, { stagger = 0.4 })
  -- The aura stretches, then snaps back: drag them to you and hit them, four times.
  nen.every(0.35, 4, function()
    local me = nen.me()
    nen.push(target, me.x, me.z, -9)
    nen.after(0.12, function() nen.strike(target, 0.7 * VOW_POWER, { stagger = 0.3, knockback = 0 }) end)
  end)
end
";

        const string TransmuterChain =
@"function hatsu.activate(power)
  if not vow_kept() then return end
  local first = nen.nearest(10)
  if first == nil then
    nen.say(""Nothing to strike."")
    nen.cancel()
    return
  end
  nen.callout(hatsu.name .. ""!"")
  -- Lightning jumps to the next foe within 6 m, up to 3 + power * 3 times.
  local hit, current = {}, first
  local jumps = 3 + math.floor(power * 3)
  for i = 1, jumps do
    if current == nil then break end
    hit[current.id] = true
    nen.strike(current, 1.0 * VOW_POWER, { stagger = 0.9, knockback = 1 })
    nen.flash(current.x, current.z)
    local nextFoe = nil
    for _, foe in ipairs(nen.enemies(20)) do
      local dx, dz = foe.x - current.x, foe.z - current.z
      if not hit[foe.id] and dx * dx + dz * dz < 36 then nextFoe = foe break end
    end
    current = nextFoe
  end
end
";

        const string EmitterVolley =
@"function hatsu.activate(power)
  if not vow_kept() then return end
  nen.callout(hatsu.name .. ""!"")
  -- A fan of shots: 3 at a tap, up to 7 fully charged.
  local shots = 3 + math.floor(power * 4)
  local spread = 50
  for i = 1, shots do
    local angle = -spread / 2 + spread * (i - 1) / math.max(shots - 1, 1)
    nen.projectile({ angle = angle, power = 0.9 * VOW_POWER, speed = 24, range = 22, size = 0.4, stagger = 0.3 })
  end
end
";

        const string ConjurerChain =
@"function hatsu.activate(power)
  if not vow_kept() then return end
  local target = nen.nearest(8)
  if target == nil then
    nen.say(""The chain finds no one."")
    nen.cancel()
    return
  end
  nen.callout(hatsu.name .. ""!"")
  -- Shackled: held in place, struck link by link.
  nen.stagger(target, 2.5)
  nen.every(0.5, 5, function()
    nen.stagger(target, 0.6)
    nen.strike(target, 0.75 * VOW_POWER, { stagger = 0.5, knockback = 0 })
  end)
end
";

        const string ConjurerBlades =
@"function hatsu.activate(power)
  if not vow_kept() then return end
  nen.callout(hatsu.name .. ""!"")
  local me = nen.me()
  -- Two sweeps of conjured blades around you.
  nen.burst(me.x, me.z, 3.5 + power, 1.0 * VOW_POWER, { stagger = 0.6, knockback = 3 })
  nen.after(0.6, function()
    local now = nen.me()
    nen.burst(now.x, now.z, 3.5 + power, 1.0 * VOW_POWER, { stagger = 0.6, knockback = 5 })
  end)
end
";

        const string ManipulatorPull =
@"function hatsu.activate(power)
  if not vow_kept() then return end
  local me = nen.me()
  -- The knot: a point 3 m in front of you.
  local kx, kz = me.x + me.facing_x * 3, me.z + me.facing_z * 3
  local foes = nen.enemies(9)
  if #foes == 0 then
    nen.say(""No strings to pull."")
    nen.cancel()
    return
  end
  nen.callout(hatsu.name .. ""!"")
  for _, foe in ipairs(foes) do
    nen.stagger(foe, 1.2)
    nen.push(foe, kx, kz, -12)
  end
  nen.after(0.45, function()
    nen.burst(kx, kz, 2.5, (1.2 + power) * VOW_POWER, { stagger = 1.0, knockback = 6 })
  end)
end
";

        const string SpecialistHour =
@"function hatsu.activate(power)
  if not vow_kept() then return end
  nen.callout(hatsu.name)
  local me = nen.me()
  nen.heal(me.max_hp * 0.15)
  -- Six seconds that don't quite belong to you.
  nen.buff({ damage = 1.25 * math.min(VOW_POWER, 1.2), speed = 1.3, defense = 0.75, seconds = 6 })
  nen.flash()
  nen.after(6, function() nen.say(""The borrowed time runs out."") end)
end
";

        /// <summary>The API reference shown on the Hatsu page.</summary>
        public const string Reference =
@"hatsu = { name, cost (10-150), cooldown (0.5-30 s), charge (0-3 s), vow }
function hatsu.activate(power)   -- runs on use; power 0.25..1 from charging

SENSE   nen.nearest(radius) -> foe or nil (your lock-on target first)
        nen.enemies(radius) / nen.ahead(range, half_angle) -> list of foes, nearest first
        foe = { id, name, x, z, distance, hp, max_hp, open, attacking }
        nen.me() -> { x, z, facing_x, facing_z, hp, max_hp, aura, max_aura, stamina, ren, in_fight }
        nen.power_left() -> power this use can still deal   nen.time() -> seconds
HIT     nen.strike(foe, power, { stagger, knockback }) -> damage   (1 power = a heavy punch)
        nen.burst(x, z, radius, power, { stagger, knockback }) -> foes hit
        nen.projectile({ angle | target, power, speed, range, size, stagger, knockback, on_hit = function(foe, point) })
        nen.stagger(foe, seconds)   nen.push(foe, x, z, force)  (negative force pulls toward x, z)
        nen.cancel() -> inside activate: the use fizzled, refund its aura and cooldown
SELF    nen.heal(amount)   nen.dash(metres)   nen.buff({ damage, speed, defense, seconds })
        nen.spend(aura) -> true/false (pay more aura for more power this use)
TIME    nen.after(seconds, fn)   nen.every(seconds, times, fn)
SHOW    nen.say(text)   nen.callout(text)   nen.flash([x, z])
Limits: each use can deal about cost/10 power in total (x1.25 with a vow); a script that runs too long is stopped.";
    }
}
