using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Runs a created Hunter's Hatsu: a Lua script (see HatsuLibrary for the format and the generated ones) executed by
    /// MoonSharp in a sandbox — no files, network, OS or code loading — with a step budget on every call so a runaway
    /// loop is stopped instead of freezing the game. The script talks to the world only through the <c>nen</c> table
    /// below. Power is real but bounded: every strike scales with the Hunter's stats, and the total power one use can
    /// deal is capped by the Hatsu's aura cost, so a script can be wild without breaking the game.
    /// Hold V (or press it, for an instant Hatsu) to use it, once it's learned.
    /// </summary>
    [DefaultExecutionOrder(100)] // after PlayerCombat, so charging can slow the walk it sets
    public class HatsuRuntime : MonoBehaviour
    {
        const int StepBudget = 200000;
        const int MaxTimers = 64;
        const float HeavyBase = 20f;

        public struct Meta
        {
            public string name, vow, description;
            public float cost, cooldown, charge;
            /// <summary>The Nen type the script declares (hatsu.type), if any.</summary>
            public NenType? type;
        }

        class Timer
        {
            public float due, interval;
            public int remaining;
            public DynValue fn;
        }

        PlayerContext player;
        Script script;
        DynValue activate;
        readonly List<Timer> timers = new List<Timer>();
        readonly Dictionary<int, EnemyController> handles = new Dictionary<int, EnemyController>();
        readonly Dictionary<EnemyController, int> ids = new Dictionary<EnemyController, int>();
        int lastId;
        float cooldownUntil;
        /// <summary>True only during activate: nen.cancel can refund the use then, not from a later timer.</summary>
        bool canRefund;
        /// <summary>Game time for nen.after / nen.every: it stops while the game is paused.</summary>
        float clock;
        float budget;
        bool charging;
        float chargeStart;
        float damageBuff = 1f, speedBuff = 1f, defenseBuff = 1f, buffUntil;

        public Meta Info { get; private set; }
        public bool Loaded => activate != null;
        public string LastError { get; private set; }
        public bool IsCharging => charging;
        public float Charge => charging && Info.charge > 0f ? Mathf.Clamp01((Time.time - chargeStart) / Info.charge) : 0f;
        public float CooldownRemaining => Mathf.Max(0f, cooldownUntil - Time.time);

        /// <summary>Multipliers a Hatsu can grant for a while (nen.buff).</summary>
        public float DamageMultiplier => Time.time < buffUntil ? damageBuff : 1f;
        public float SpeedMultiplier => Time.time < buffUntil ? speedBuff : 1f;
        public float DefenseMultiplier => Time.time < buffUntil ? defenseBuff : 1f;

        ProgressionService P => GameSession.Instance.Progression;

        void Awake() => player = GetComponent<PlayerContext>();

        void Start() => Reload();

        /// <summary>(Re)loads the Hunter's Hatsu from the save. Gon has none here (he uses Jajanken).</summary>
        public void Reload()
        {
            script = null;
            activate = null;
            timers.Clear();
            LastError = null;
            if (GameSession.Instance == null || !P.IsCustomHunter || string.IsNullOrWhiteSpace(P.Hunter.hatsuSource)) return;
            if (!Compile(P.Hunter.hatsuSource, this, out var s, out var fn, out var meta, out var error))
            {
                LastError = error;
                return;
            }
            script = s;
            activate = fn;
            Info = meta;
        }

        /// <summary>Checks a script without running its effects: syntax, the hatsu table, and its numbers.</summary>
        public static bool Validate(string source, out Meta meta, out string error) => Compile(source, null, out _, out _, out meta, out error);

        static bool Compile(string source, HatsuRuntime host, out Script script, out DynValue activate, out Meta meta, out string error)
        {
            script = new Script(CoreModules.Preset_SoftSandbox);
            activate = null;
            meta = default;
            error = null;
            var log = host;
            script.Options.DebugPrint = text => { if (log != null) Debug.Log("[Hatsu] " + text); };
            script.Globals["nen"] = host != null ? host.BuildApi(script) : new Table(script);
            try
            {
                var chunk = script.LoadString(source ?? "", null, "hatsu");
                if (!RunGuarded(script, chunk, out error)) return false;
            }
            catch (InterpreterException e)
            {
                error = e.DecoratedMessage ?? e.Message;
                return false;
            }
            var hatsu = script.Globals.Get("hatsu");
            if (hatsu.Type != DataType.Table)
            {
                error = "The script must define a table called hatsu.";
                return false;
            }
            var t = hatsu.Table;
            activate = t.Get("activate");
            if (activate.Type != DataType.Function)
            {
                error = "hatsu.activate must be a function: it runs when the Hatsu is used.";
                return false;
            }
            NenType? declared = null;
            var typeValue = t.Get("type");
            if (typeValue.Type == DataType.String)
            {
                if (Enum.TryParse(typeValue.String.Trim(), true, out NenType parsed)) declared = parsed;
                else
                {
                    error = $"hatsu.type \"{typeValue.String}\" isn't a Nen type: use Enhancer, Transmuter, Emitter, Conjurer, Manipulator or Specialist.";
                    return false;
                }
            }
            meta = new Meta
            {
                name = t.Get("name").CastToString() ?? "Hatsu",
                vow = t.Get("vow").CastToString() ?? "",
                description = t.Get("description").CastToString() ?? "",
                type = declared,
                cost = Mathf.Clamp((float)(t.Get("cost").CastToNumber() ?? 40), 10f, 150f),
                cooldown = Mathf.Clamp((float)(t.Get("cooldown").CastToNumber() ?? 3), 0.5f, 30f),
                charge = Mathf.Clamp((float)(t.Get("charge").CastToNumber() ?? 0), 0f, 3f),
            };
            return true;
        }

        /// <summary>Runs a Lua function as a coroutine with a step budget: a runaway loop is stopped, not hung on.</summary>
        static bool RunGuarded(Script script, DynValue fn, out string error, params DynValue[] args)
        {
            error = null;
            try
            {
                var co = script.CreateCoroutine(fn);
                co.Coroutine.AutoYieldCounter = StepBudget;
                co.Coroutine.Resume(args);
                if (co.Coroutine.State != CoroutineState.Dead)
                {
                    error = "The Hatsu ran too long (an endless loop?) and was stopped.";
                    return false;
                }
                return true;
            }
            catch (InterpreterException e)
            {
                error = e.DecoratedMessage ?? e.Message;
                return false;
            }
        }

        bool Call(DynValue fn, params DynValue[] args)
        {
            if (script == null) return false;
            if (RunGuarded(script, fn, out var error, args)) return true;
            Fail(error);
            return false;
        }

        void Fail(string error)
        {
            LastError = error;
            Debug.LogWarning("[Hatsu] " + error);
            GameSession.Instance.PushNotice($"Your Hatsu faltered: {error}  (edit it in the dashboard, Tab → Hatsu)", 6f);
        }

        // ------------------------------------------------------------------ Using it

        void Update()
        {
            if (GameSession.Instance == null) return;
            clock += Time.deltaTime;
            RunTimers();
            if (!Loaded || !P.IsCustomHunter || !player.Aura.JajankenUnlocked) return;

            var session = GameSession.Instance;
            bool blocked = session.InputBlocked || player.Motor.Locked || player.Combat.IsDefeated;
            var input = player.Input;
            if (charging)
            {
                player.Motor.MoveMultiplier = 0.3f;
                if (blocked) charging = false;
                else if (!input.HatsuHeld) Release();
                return;
            }
            if (blocked || !input.HatsuPressed || player.Combat.IsAttacking) return;
            if (Time.time < cooldownUntil)
            {
                session.PushNotice($"{Info.name} is still recovering ({CooldownRemaining:0.0}s).", 1.2f);
                return;
            }
            if (player.Aura.ZetsuActive)
            {
                session.PushNotice("Zetsu has sealed your aura — release it first (Z).", 2f);
                return;
            }
            if (player.Aura.Current < Info.cost)
            {
                session.PushNotice($"Not enough aura for {Info.name} ({Info.cost:0} needed). Rest at the bench.", 2f);
                return;
            }
            if (Info.charge > 0f)
            {
                charging = true;
                chargeStart = Time.time;
                ImpactEffects.ChantBeat(0);
                return;
            }
            Fire(1f);
        }

        void Release()
        {
            charging = false;
            player.Motor.MoveMultiplier = 1f;
            Fire(Mathf.Max(0.25f, Charge));
        }

        void Fire(float power)
        {
            if (!player.Aura.TrySpend(Info.cost)) return;
            cooldownUntil = Time.time + Info.cooldown;
            // What one use may deal in total: a heavy's worth per 10 aura spent, more with a vow.
            budget = Info.cost / 10f * (string.IsNullOrEmpty(Info.vow) ? 1f : 1.25f);
            canRefund = true;
            Call(activate, DynValue.NewNumber(power));
            if (!canRefund) return; // cancelled: no mastery for a use that didn't happen
            canRefund = false;
            ImpactEffects.RenRaise(1.5f);
            P.ApplyMastery(TechniqueId.Jajanken, 0.6f + 0.6f * power);
            P.ApplyMastery(TechniqueId.Ren, 0.4f);
        }

        void RunTimers()
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                if (i >= timers.Count) continue;
                var t = timers[i];
                if (clock < t.due) continue;
                t.remaining--;
                if (t.remaining <= 0) timers.RemoveAt(i);
                else t.due += t.interval;
                Call(t.fn);
            }
        }

        // ------------------------------------------------------------------ The nen API

        float StrikeDamage(float power)
        {
            var p = P;
            double maxOutput = p.GetAttribute(AttributeId.MaxOutput);
            float affinity = p.Hunter.nenType == NenType.Enhancer ? StatEffects.EnhancerStrikeMultiplier : 1.15f;
            return HeavyBase * power
                   * StatEffects.NenStrikeStrengthFactor(p.GetAttribute(AttributeId.Strength))
                   * StatEffects.RenDamageFactor(maxOutput, p.GetAttribute(AttributeId.ControlPrecision))
                   * StatEffects.HunterLevelDamageFactor(p.HunterLevel)
                   * Mathf.Lerp(0.8f, 1f, p.Fatigue.Readiness)
                   * affinity * DamageMultiplier;
        }

        /// <summary>Lands part of the use's power budget on one opponent. Returns the damage dealt.</summary>
        float Strike(EnemyController target, float power, float stagger, float knockback)
        {
            if (target == null || !target.IsTargetable) return 0f;
            power = Mathf.Clamp(power, 0f, 10f);
            float allowed = Mathf.Min(power, budget);
            budget -= allowed;
            if (allowed <= 0.01f)
            {
                DamageNumbers.Callout(target.transform.position + Vector3.up * 2.2f, "SPENT");
                return 0f;
            }
            var hit = new HitInfo
            {
                damage = StrikeDamage(allowed), technique = TechniqueId.Jajanken, heavy = allowed >= 1.5f, ren = true,
                renStagger = Mathf.Clamp(stagger, 0f, 3f), knockback = Mathf.Clamp(knockback, 0f, 12f),
            };
            var result = target.ReceiveHit(hit, out float dealt);
            player.Combat.Tracker?.RegisterPlayerHit(result, dealt, hit.heavy, true, target.IsVulnerable);
            DamageNumbers.Show(target.transform.position + Vector3.up * 1.5f, dealt, DamageNumbers.Kind.Finisher);
            ImpactEffects.Hit(target.transform.position + Vector3.up * 0.6f, hit.heavy, true);
            HitFeedback.Impact(0.04f + 0.02f * allowed, 0.2f + 0.1f * allowed);
            return dealt;
        }

        Table Handle(Script s, EnemyController e)
        {
            if (!ids.TryGetValue(e, out int id))
            {
                id = ids[e] = ++lastId;
                handles[id] = e;
            }
            Vector3 d = e.transform.position - transform.position;
            d.y = 0f;
            var t = new Table(s);
            t["id"] = id;
            t["name"] = e.DisplayName;
            t["x"] = e.transform.position.x;
            t["z"] = e.transform.position.z;
            t["distance"] = d.magnitude;
            t["hp"] = e.Health.Current;
            t["max_hp"] = e.Health.Max;
            t["open"] = e.IsVulnerable;
            t["attacking"] = e.IsTelegraphing;
            return t;
        }

        EnemyController Resolve(DynValue v)
        {
            if (v.Type != DataType.Table) return null;
            var id = v.Table.Get("id");
            return id.Type == DataType.Number && handles.TryGetValue((int)id.Number, out var e) && e != null ? e : null;
        }

        static float Num(CallbackArguments a, int i, float fallback) =>
            a.Count > i && a[i].Type == DataType.Number ? (float)a[i].Number : fallback;

        static float Opt(DynValue table, string key, float fallback)
        {
            if (table.Type != DataType.Table) return fallback;
            var v = table.Table.Get(key);
            return v.Type == DataType.Number ? (float)v.Number : fallback;
        }

        DynValue List(Script s, IEnumerable<EnemyController> enemies)
        {
            var list = new List<EnemyController>(enemies);
            list.Sort((a, b) => Distance(a).CompareTo(Distance(b)));
            var t = new Table(s);
            for (int i = 0; i < list.Count; i++) t[i + 1] = Handle(s, list[i]);
            return DynValue.NewTable(t);
        }

        float Distance(EnemyController e)
        {
            Vector3 d = e.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        IEnumerable<EnemyController> Near(Vector3 center, float radius)
        {
            foreach (var e in EnemyController.All)
            {
                if (!e.IsTargetable) continue;
                Vector3 d = e.transform.position - center;
                d.y = 0f;
                if (d.magnitude <= radius + e.Radius) yield return e;
            }
        }

        IEnumerable<EnemyController> Ahead(float range, float halfAngle)
        {
            foreach (var e in EnemyController.All)
            {
                if (!e.IsTargetable) continue;
                Vector3 d = e.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= range + e.Radius && Vector3.Angle(transform.forward, d) <= halfAngle) yield return e;
            }
        }

        Color AuraColor => P.Hunter.auraColor;

        Table BuildApi(Script s)
        {
            var nen = new Table(s);
            void Fn(string name, Func<CallbackArguments, DynValue> f) => nen[name] = DynValue.NewCallback((c, a) => f(a));

            // --- Sensing
            Fn("enemies", a => List(s, Near(transform.position, Mathf.Clamp(Num(a, 0, 10f), 0f, 40f))));
            Fn("ahead", a => List(s, Ahead(Mathf.Clamp(Num(a, 0, 4f), 0f, 30f), Mathf.Clamp(Num(a, 1, 45f), 1f, 180f))));
            Fn("nearest", a =>
            {
                EnemyController best = null;
                foreach (var e in Near(transform.position, Mathf.Clamp(Num(a, 0, 15f), 0f, 40f)))
                    if (best == null || Distance(e) < Distance(best)) best = e;
                if (player.Combat.LockTarget != null && player.Combat.LockTarget.IsTargetable) best = player.Combat.LockTarget;
                return best != null ? DynValue.NewTable(Handle(s, best)) : DynValue.Nil;
            });
            Fn("me", a =>
            {
                var t = new Table(s);
                t["x"] = transform.position.x;
                t["z"] = transform.position.z;
                t["facing_x"] = transform.forward.x;
                t["facing_z"] = transform.forward.z;
                t["hp"] = player.Health.Current;
                t["max_hp"] = player.Health.Max;
                t["aura"] = player.Aura.Current;
                t["max_aura"] = player.Aura.Capacity;
                t["stamina"] = player.Vitals.Stamina;
                t["ren"] = player.Aura.RenActive;
                t["in_fight"] = player.Combat.Tracker != null;
                return DynValue.NewTable(t);
            });
            Fn("power_left", a => DynValue.NewNumber(budget));
            Fn("time", a => DynValue.NewNumber(Time.time));

            // --- Acting on opponents
            Fn("strike", a =>
            {
                var opts = a.Count > 2 ? a[2] : DynValue.Nil;
                return DynValue.NewNumber(Strike(Resolve(a[0]), Num(a, 1, 1f), Opt(opts, "stagger", 0.3f), Opt(opts, "knockback", 2f)));
            });
            Fn("burst", a =>
            {
                Vector3 at = new Vector3(Num(a, 0, transform.position.x), transform.position.y, Num(a, 1, transform.position.z));
                float radius = Mathf.Clamp(Num(a, 2, 3f), 0.5f, 8f), power = Num(a, 3, 1f);
                var opts = a.Count > 4 ? a[4] : DynValue.Nil;
                int count = 0;
                foreach (var e in new List<EnemyController>(Near(at, radius)))
                {
                    Strike(e, power, Opt(opts, "stagger", 0.4f), Opt(opts, "knockback", 3f));
                    count++;
                }
                ImpactEffects.Ko(at + Vector3.up * 0.4f);
                return DynValue.NewNumber(count);
            });
            Fn("stagger", a =>
            {
                var e = Resolve(a[0]);
                if (e != null && budget > 0.1f)
                {
                    float seconds = Mathf.Clamp(Num(a, 1, 1f), 0f, 3f);
                    budget -= 0.3f * seconds;
                    e.Stagger(seconds);
                }
                return DynValue.Nil;
            });
            Fn("push", a =>
            {
                var e = Resolve(a[0]);
                if (e == null) return DynValue.Nil;
                Vector3 to = new Vector3(Num(a, 1, 0f), 0f, Num(a, 2, 0f)) - e.transform.position;
                to.y = 0f;
                // A positive force pushes away from the point; a negative one pulls toward it.
                float force = Mathf.Clamp(Num(a, 3, 6f), -15f, 15f);
                e.Push(-to.normalized * force);
                return DynValue.Nil;
            });
            Fn("projectile", a =>
            {
                var opts = a.Count > 0 ? a[0] : DynValue.Nil;
                if (opts.Type != DataType.Table) return DynValue.Nil;
                float angle = Opt(opts, "angle", 0f);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
                var target = Resolve(opts.Table.Get("target"));
                if (target != null) dir = target.transform.position - transform.position;
                float power = Opt(opts, "power", 1f), stagger = Opt(opts, "stagger", 0.3f), knock = Opt(opts, "knockback", 2f);
                var onHit = opts.Table.Get("on_hit");
                AuraShot.LaunchCustom(player.Combat, transform.position + transform.forward * 0.6f + Vector3.up * 1.1f, dir, AuraColor,
                    Opt(opts, "size", 0.5f), Opt(opts, "speed", 20f), Mathf.Clamp(Opt(opts, "range", 20f), 2f, 40f),
                    (hit, point) =>
                    {
                        if (hit != null) Strike(hit, power, stagger, knock);
                        if (onHit.Type == DataType.Function && script != null)
                        {
                            var p = new Table(script);
                            p["x"] = point.x;
                            p["z"] = point.z;
                            Call(onHit, hit != null ? DynValue.NewTable(Handle(script, hit)) : DynValue.Nil, DynValue.NewTable(p));
                        }
                    });
                return DynValue.Nil;
            });

            // --- Acting on yourself
            Fn("heal", a =>
            {
                float amount = Mathf.Clamp(Num(a, 0, 10f), 0f, player.Health.Max * 0.4f);
                player.Health.Heal(amount);
                return DynValue.Nil;
            });
            Fn("buff", a =>
            {
                var opts = a.Count > 0 ? a[0] : DynValue.Nil;
                damageBuff = Mathf.Clamp(Opt(opts, "damage", 1f), 0.5f, 1.6f);
                speedBuff = Mathf.Clamp(Opt(opts, "speed", 1f), 0.5f, 1.5f);
                defenseBuff = Mathf.Clamp(Opt(opts, "defense", 1f), 0.4f, 1.5f); // damage taken
                buffUntil = Time.time + Mathf.Clamp(Opt(opts, "seconds", 5f), 0f, 15f);
                return DynValue.Nil;
            });
            Fn("dash", a =>
            {
                float distance = Mathf.Clamp(Num(a, 0, 4f), -8f, 8f);
                var cc = GetComponent<CharacterController>();
                if (cc != null) cc.Move(transform.forward * distance);
                ImpactEffects.Dodge();
                return DynValue.Nil;
            });
            Fn("spend", a =>
            {
                float amount = Mathf.Clamp(Num(a, 0, 0f), 0f, 200f);
                bool ok = player.Aura.TrySpend(amount);
                if (ok) budget += amount / 10f;
                return DynValue.NewBoolean(ok);
            });

            Fn("cancel", a =>
            {
                // The use fizzled (no target, a vow not kept): give the aura back and allow it again at once.
                if (canRefund)
                {
                    canRefund = false;
                    P.SetAuraCurrent(player.Aura.Current + Info.cost, player.Aura.Capacity);
                    cooldownUntil = Time.time;
                    budget = 0f;
                }
                return DynValue.Nil;
            });

            // --- Time
            Fn("after", a =>
            {
                if (timers.Count < MaxTimers && a.Count > 1 && a[1].Type == DataType.Function)
                    timers.Add(new Timer { due = clock + Mathf.Clamp(Num(a, 0, 0.5f), 0f, 30f), remaining = 1, fn = a[1] });
                return DynValue.Nil;
            });
            Fn("every", a =>
            {
                if (timers.Count < MaxTimers && a.Count > 2 && a[2].Type == DataType.Function)
                {
                    float interval = Mathf.Clamp(Num(a, 0, 0.5f), 0.1f, 10f);
                    timers.Add(new Timer { due = clock + interval, interval = interval, remaining = Mathf.Clamp((int)Num(a, 1, 3f), 1, 30), fn = a[2] });
                }
                return DynValue.Nil;
            });

            // --- Show
            Fn("say", a =>
            {
                GameSession.Instance.PushNotice(a[0].CastToString() ?? "", 2.5f);
                return DynValue.Nil;
            });
            Fn("callout", a =>
            {
                DamageNumbers.Callout(transform.position + Vector3.up * 2.4f, a[0].CastToString() ?? "");
                return DynValue.Nil;
            });
            Fn("flash", a =>
            {
                Vector3 at = a.Count >= 2 ? new Vector3(Num(a, 0, 0f), transform.position.y, Num(a, 1, 0f)) : transform.position;
                ImpactEffects.Ko(at + Vector3.up * 0.6f);
                HitFeedback.Shake(0.25f);
                return DynValue.Nil;
            });
            return nen;
        }
    }
}
