using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Placeholder IMGUI HUD: vitals, fatigue, world labels, interaction prompt, notices and controls.
    /// Read-only: it never mutates progression.
    /// </summary>
    public class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] PlayerContext player;
        [SerializeField] Camera worldCamera;
        [SerializeField] float labelRange = 60f;

        readonly ProgressionDashboard dashboard = new ProgressionDashboard();
        readonly PauseMenu pauseMenu = new PauseMenu();
        bool pauseMenuOpen;
        float fpsTimer;
        int fpsFrames;
        float fps;
        float worstFrameMs;
        float worstInWindow;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            fpsFrames++;
            fpsTimer += dt;
            worstInWindow = Mathf.Max(worstInWindow, dt * 1000f);
            if (fpsTimer < 0.5f) return;
            fps = fpsFrames / fpsTimer;
            worstFrameMs = worstInWindow;
            fpsFrames = 0;
            fpsTimer = 0f;
            worstInWindow = 0f;
        }

        void OnGUI()
        {
            var session = GameSession.Instance;
            if (session == null || player == null || session.TitleOpen) return;
            if (worldCamera == null) worldCamera = Camera.main;
            HudGui.Use();

            if (session.DashboardOpen)
            {
                dashboard.Draw(session);
                return;
            }

            DrawWorldLabels();
            DrawVitals(session);
            DrawObjective(session);
            DrawChant();
            DrawPrompt();
            DrawNotices(session);
            if (session.ShowHelp) DrawHelp();
            if (GameSettings.ShowFps) DrawFrameRate();

            if (session.Paused)
            {
                if (!pauseMenuOpen) pauseMenu.Open(); // re-read settings each time the menu opens
                pauseMenuOpen = true;
                pauseMenu.Draw(session);
            }
            else pauseMenuOpen = false;
        }

        /// <summary>The goal under the vitals: clear the ant squads, then take the castle, with the way to the next one.</summary>
        void DrawObjective(GameSession session)
        {
            if (Encounter.All.Count == 0) return;
            Encounter boss = null, nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (var e in Encounter.All)
            {
                if (e.IsBoss) boss = e;
                else if (!e.Cleared)
                {
                    float d = Vector3.Distance(player.transform.position, e.transform.position);
                    if (d < nearestDistance)
                    {
                        nearestDistance = d;
                        nearest = e;
                    }
                }
            }
            int total = Encounter.SquadCount(), left = Encounter.SquadsLeft();
            string goal, detail;
            Encounter target;
            if (left > 0)
            {
                goal = $"Clear the chimera ant squads  <color=#ffc552>{total - left}/{total}</color>";
                detail = "They lurk in the woods. Zetsu lets you sneak up for an ambush.";
                target = nearest;
            }
            else if (boss != null && !boss.Cleared)
            {
                goal = "Storm the castle and defeat <color=#b98cff>Meruem</color>";
                detail = "The seal is broken. The castle lies to the north.";
                target = boss;
            }
            else
            {
                goal = "<color=#66dd77>The King has fallen</color>";
                detail = "Keep training — Killua (Godspeed) waits at the arena.";
                target = null;
            }
            if (target != null && !target.Engaged)
            {
                Vector3 to = target.transform.position - player.transform.position;
                to.y = 0f;
                string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                float bearing = (Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + 360f + 22.5f) % 360f;
                detail += $"  <color=#9aa4b2>({to.magnitude:0} m {names[(int)(bearing / 45f) % 8]})</color>";
            }
            const float x = 16f, w = 300f;
            float y = 16f + (session.Progression.IsAuraAwakened ? 168f : 140f) + 4f;
            float h = HudGui.Wrap.CalcHeight(new GUIContent(detail), w) + 34f;
            var r = new Rect(x - 8, y, w + 16, h);
            HudGui.Panel(r);
            HudGui.Fill(new Rect(r.x + 1f, r.y + 7f, 3f, r.height - 14f), HudGui.Gold);
            GUI.Label(new Rect(x + 2, y + 4, w, 22), $"<b>{goal}</b>", HudGui.Label);
            GUI.Label(new Rect(x + 2, y + 26, w, h - 28), $"<size=12>{detail}</size>", HudGui.Wrap);
        }

        void DrawFrameRate()
        {
            float target = GameSettings.VSync || GameSettings.FrameRateCap < 0
                ? Mathf.Max(60f, (float)Screen.currentResolution.refreshRateRatio.value)
                : GameSettings.FrameRateCap;
            string color = fps >= target * 0.95f ? "#66dd77" : fps >= target * 0.6f ? "#eebb44" : "#ff5544";
            var r = new Rect(16f, Screen.height - 40f, 240f, 24f);
            HudGui.Panel(r);
            GUI.Label(new Rect(r.x + 8, r.y + 3, r.width - 16, r.height),
                $"<color={color}><b>{fps:0} FPS</b></color> / {target:0} · worst {worstFrameMs:0.0} ms", HudGui.Label);
        }

        void DrawVitals(GameSession session)
        {
            var p = session.Progression;
            var f = p.Fatigue;
            var a = player.Aura;
            const float x = 16f, w = 300f;
            float y = 16f;
            float height = p.IsAuraAwakened ? 168f : 140f;
            HudGui.Panel(new Rect(x - 8, y - 8, w + 16, height));

            // Name, and chips for whatever the aura is doing right now.
            GUI.Label(new Rect(x, y - 2, 120, 24), "<b>GON</b>  <size=11><color=#9aa4b2>Hunter</color></size>", HudGui.Label);
            float chipX = x + w;
            void Chip(string text, Color color)
            {
                var size = HudGui.Small.CalcSize(new GUIContent(text));
                chipX -= size.x + 18f;
                HudGui.Chip(new Vector2(chipX, y), text, color);
            }
            if (p.IsAuraAwakened)
            {
                if (a.GyoActive) Chip("GYO", new Color(0.6f, 0.8f, 1f));
                if (a.ZetsuActive) Chip("ZETSU", new Color(0.6f, 0.62f, 0.66f));
                else if (a.RenBroken) Chip("REN BROKEN", HudGui.Bad);
                else if (a.KenActive) Chip("KEN", new Color(0.82f, 0.55f, 1f));
                else if (a.RenActive) Chip("REN", new Color(1f, 0.6f, 0.2f));
                else if (a.TenActive) Chip("TEN", HudGui.AuraCyan);
            }
            y += 26f;

            HudGui.Bar(new Rect(x, y, w, 22), player.Health.Fraction, HudGui.Bad, $"<b>{player.Health.Current:0}</b> / {player.Health.Max:0}");
            y += 26f;
            HudGui.Bar(new Rect(x, y, w, 16), player.Vitals.Fraction, player.Vitals.IsExhausted ? HudGui.Warn : HudGui.Good,
                player.Vitals.IsExhausted ? "<size=11><b>EXHAUSTED</b></size>" : $"<size=11><b>Stamina {player.Vitals.Stamina:0}</b></size>");
            y += 20f;
            if (p.IsAuraAwakened)
            {
                HudGui.Bar(new Rect(x, y, w, 16), a.Current / Mathf.Max(1f, a.Capacity), HudGui.AuraCyan, $"<size=11><b>Aura {a.Current:0} / {a.Capacity:0}</b></size>");
                y += 20f;
            }

            // Condition: two slim meters side by side.
            float half = (w - 8f) / 2f;
            GUI.Label(new Rect(x, y, half, 16), $"<size=11>Fatigue {f.Immediate:0} · ready {HudGui.Pct(f.Readiness)}</size>", HudGui.Small);
            GUI.Label(new Rect(x + half + 8f, y, half, 16), $"<size=11>Load {f.Accumulated:0} · adapt {HudGui.Pct(f.AdaptationEfficiency)}</size>", HudGui.Small);
            y += 16f;
            HudGui.Bar(new Rect(x, y, half, 8), f.Immediate / 100f, new Color(0.95f, 0.58f, 0.22f), null);
            HudGui.Bar(new Rect(x + half + 8f, y, half, 8), f.Accumulated / 100f, new Color(0.65f, 0.4f, 0.85f), null);
            y += 14f;

            string keys = "";
            if (a.CanKo || a.KoUnlocked) keys += "<color=#ffc552><b>F</b></color> Ko   ";
            if (a.JajankenUnlocked) keys += "<color=#ffc552><b>V</b></color> Rock   ";
            string strain = p.GetStrain(BodyArea.Upper) > 0.5f || p.GetStrain(BodyArea.Lower) > 0.5f
                ? $"<color=#ff8866>strain {p.GetStrain(BodyArea.Upper):0}/{p.GetStrain(BodyArea.Lower):0}</color>   " : "";
            GUI.Label(new Rect(x, y, w, 18), $"<size=11>{keys}{strain}<color=#9aa4b2>Sessions {p.TrainingSessions} · <b>Tab</b> dashboard · <b>F1</b> controls</color></size>", HudGui.Small);

            // Guided next step, so a new player always knows what to aim for.
            float goalY = 16f + height;
            string goal = NextGoal(p);
            float goalH = HudGui.Wrap.CalcHeight(new GUIContent(goal), w - 20f) + 30f;
            var goalRect = new Rect(x - 8, goalY, w + 16, goalH);
            HudGui.Panel(goalRect);
            HudGui.Fill(new Rect(goalRect.x + 1f, goalRect.y + 8f, 3f, goalRect.height - 16f), HudGui.Gold);
            GUI.Label(new Rect(x + 4, goalY + 6, w, 16), "NEXT GOAL", HudGui.Section);
            GUI.Label(new Rect(x + 4, goalY + 22, w - 8, goalH - 24), goal, HudGui.Wrap);
        }

        static string NextGoal(ProgressionService p)
        {
            if (!p.IsAuraAwakened)
                return p.TrainingSessions < 3
                    ? $"Condition your body: complete training sessions ({p.TrainingSessions}/3) at the Lift Rack, Sprint Course or Precision Pads."
                    : "Meditate at the Meditation Stone to awaken your aura.";
            if (p.BestSparringTime("rival") <= 0f)
                return "Beat the Sparring Rival: dodge or parry the red wind-up, then punish the yellow opening.";
            bool ken = p.KnowsInsight(AuraController.KenInsight);
            if (!ken)
                return $"Fight with Ten (T) and Ren (R) up — Ken unlocks at 25 mastery each (Ten {p.GetMastery(TechniqueId.Ten):0}, Ren {p.GetMastery(TechniqueId.Ren):0}).";
            if (p.BestSparringTime("veteran") <= 0f)
                return "Beat the Veteran Hunter — choose it at the arena post with ] .";
            if (!p.KnowsInsight(AuraController.KoInsight))
                return $"Learn Ko: Ren mastery 40 and Max Output 18 from the Ren Circle (Ren {p.GetMastery(TechniqueId.Ren):0}, Max Output {p.GetAttribute(AttributeId.MaxOutput):0.0}).";
            if (!p.KnowsInsight(AuraController.JajankenInsight))
                return $"Learn Jajanken: Ren mastery 50 (Ren {p.GetMastery(TechniqueId.Ren):0}) — keep fighting with Ren up and training at the Ren Circle.";
            if (p.BestSparringTime("elite") <= 0f)
                return "Defeat the Elite Nen User.";
            int left = 0;
            foreach (var b in Breakthroughs.All)
                if (!p.HasBreakthrough(b.id)) left++;
            return left > 0
                ? $"Hunt the remaining breakthroughs (Tab → Breakthroughs): {left} left."
                : "You've conquered the sandbox. Keep pushing — no attribute has a cap.";
        }

        /// <summary>Jajanken: the chant's progress, centred low on screen while it builds.</summary>
        void DrawChant()
        {
            var c = player.Combat;
            if (!c.IsChargingRock) return;
            const float w = 320f, h = 24f;
            var r = new Rect((Screen.width - w) / 2f, Screen.height * 0.72f, w, h);
            float charge = c.RockCharge;
            string stage = charge >= 1f ? "GUU — release!" : charge >= 2f / 3f ? "Ken…" : charge >= StatEffects.JajankenMinCharge ? "Jan…" : "Saisho wa guu!";
            HudGui.Panel(new Rect(r.x - 6f, r.y - 6f, w + 12f, h + 12f));
            HudGui.Bar(r, charge, charge >= 1f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.15f), $"Jajanken — {stage}");
            string hands = "<color=#ffc552><b>Release V</b></color> Rock";
            if (c.PaperUnlocked) hands += "   <color=#ffc552><b>RMB</b></color> Paper";
            if (c.ScissorsUnlocked) hands += "   <color=#ffc552><b>LMB</b></color> Scissors";
            HudGui.ShadowLabel(new Rect(r.x - 60f, r.yMax + 8f, w + 120f, 20f), $"<size=12>{hands}</size>", HudGui.Center);
        }

        void DrawWorldLabels()
        {
            if (worldCamera == null) return;
            foreach (var i in Interactable.All)
                DrawLabel(i.Position + Vector3.up * 2.6f, i.DisplayName, null);

            bool gyo = player.Aura.GyoActive;
            foreach (var e in EnemyController.All)
            {
                if (e.IsDefeated) continue;
                string state = e.IsTelegraphing ? "<color=#ff4433><b>!! ATTACK !!</b></color>"
                    : e.IsGuarding ? "<color=#66aaff>GUARD</color>"
                    : e.IsVulnerable ? "<color=#ffdd55>OPEN</color>"
                    : e.IsActive && e.LostTrack ? "<color=#aaaaaa><b>?</b> lost track of you</color>"
                    : e.IsActive && e.Intimidated ? "<color=#ff9944>HESITATES</color>" : null;
                // Gyo reveals timing: exact impact during a wind-up, otherwise when the next attack can start.
                if (gyo && e.IsActive)
                {
                    string read = e.IsTelegraphing ? $"<color=#ccddff>impact in {e.TimeToImpact:0.00}s</color>"
                        : $"<color=#ccddff>next attack ≥ {e.AttackCooldownRemaining:0.0}s</color>";
                    state = state == null ? read : state + "  " + read;
                }
                if (player.Combat.LockTarget == e) state = state == null ? "<color=#ffc552><b>◆ LOCKED ◆</b></color>" : "<color=#ffc552><b>◆</b></color> " + state;
                DrawLabel(e.transform.position + Vector3.up * 1.6f, e.DisplayName, e.IsActive ? e.Health : null, state);
            }
            if (player.Aura.ZetsuActive)
                DrawLabel(player.transform.position + Vector3.up * 1.4f, "", null, "<color=#999999>ZETSU — next strike is an ambush</color>");
        }

        void DrawLabel(Vector3 world, string text, Health health, string extra = null)
        {
            Vector3 toTarget = world - worldCamera.transform.position;
            if (toTarget.magnitude > labelRange || Vector3.Dot(toTarget, worldCamera.transform.forward) <= 0f) return;
            Vector3 s = worldCamera.WorldToScreenPoint(world);
            var r = new Rect(s.x - 110, Screen.height - s.y - 24, 220, 22);
            if (text.Length > 0) HudGui.ShadowLabel(r, $"<b>{text}</b>", HudGui.Center);
            if (health != null)
                HudGui.Bar(new Rect(s.x - 60, r.yMax, 120, 9), health.Fraction, HudGui.Bad, null);
            if (extra != null)
                HudGui.ShadowLabel(new Rect(s.x - 110, r.y - 20, 220, 22), extra, HudGui.Center);
        }

        void DrawPrompt()
        {
            var focus = player.Interactor.Focus;
            if (focus == null) return;
            string prompt = focus.GetPrompt();
            float w = Mathf.Min(720f, Screen.width - 40f);
            float h = prompt.Contains("\n") ? 50f : 32f;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height - 72f - h, w, h);
            HudGui.Panel(r);
            GUI.Label(r, HudGui.Keys(prompt), HudGui.Center);
        }

        void DrawNotices(GameSession session)
        {
            const float w = 460f;
            float y = Screen.height - 140f;
            var old = GUI.color;
            for (int i = session.Notices.Count - 1; i >= 0; i--)
            {
                var notice = session.Notices[i];
                // Toasts fade out over their last half second.
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((notice.expires - Time.unscaledTime) / 0.5f));
                float h = Mathf.Max(30f, HudGui.Wrap.CalcHeight(new GUIContent(notice.text), w - 28f) + 12f);
                var r = new Rect(Screen.width - w - 16f, y - h + 30f, w, h);
                HudGui.Panel(r);
                HudGui.Fill(new Rect(r.x + 1f, r.y + 7f, 3f, r.height - 14f), HudGui.Gold);
                GUI.Label(new Rect(r.x + 14, r.y + 6, r.width - 24, r.height - 8), notice.text, HudGui.Wrap);
                y -= h + 6f;
            }
            GUI.color = old;
        }

        void DrawHelp()
        {
            const string text =
                "<b>Controls</b>\n" +
                "WASD move · Mouse look · Shift sprint · Space jump · Ctrl dodge\n" +
                "LMB light · RMB heavy · Q block (tap just before a hit to parry)\n" +
                "E interact · [ / ] drill intensity · Space/LMB drill action\n" +
                "T Ten · hold R Ren · Z Zetsu · hold G Gyo · Ten+Ren Ken\n" +
                "F Ko · hold V Jajanken (release Rock · RMB Paper · LMB Scissors)\n" +
                "Middle mouse / X lock on · light, light, heavy combo\n" +
                "Tab dashboard · Esc pause/settings · F5 save · F9 reload · F1 hide";
            const float w = 460f;
            // Sized to the text, so a new line of controls never spills out of the panel.
            float h = HudGui.Wrap.CalcHeight(new GUIContent(text), w - 20f) + 12f;
            var r = new Rect(Screen.width - w - 16f, 16f, w, h);
            HudGui.Panel(r);
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, r.height - 12), text, HudGui.Wrap);
        }
    }
}
