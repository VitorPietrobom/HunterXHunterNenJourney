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
            if (session == null || player == null) return;
            if (worldCamera == null) worldCamera = Camera.main;

            if (session.DashboardOpen)
            {
                dashboard.Draw(session);
                return;
            }

            DrawWorldLabels();
            DrawVitals(session);
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
            float x = 16f, y = 16f, w = 280f, h = 20f;
            HudGui.Panel(new Rect(x - 6, y - 6, w + 12, p.IsAuraAwakened ? 172 : 146));

            HudGui.Bar(new Rect(x, y, w, h), player.Health.Fraction, HudGui.Bad, $"Health {player.Health.Current:0}/{player.Health.Max:0}");
            y += h + 4;
            HudGui.Bar(new Rect(x, y, w, h), player.Vitals.Fraction, player.Vitals.IsExhausted ? HudGui.Warn : HudGui.Good,
                player.Vitals.IsExhausted ? "Stamina — EXHAUSTED" : $"Stamina {player.Vitals.Stamina:0}/{player.Vitals.MaxStamina:0}");
            y += h + 4;
            if (p.IsAuraAwakened)
            {
                var a = player.Aura;
                string mode = a.ZetsuActive ? " · ZETSU" : a.RenBroken ? " · REN BROKEN" : a.KenActive ? " · KEN" : a.RenActive ? " · REN" : a.TenActive ? " · TEN" : "";
                if (a.CanKo) mode += " · F: KO";
                if (a.JajankenUnlocked) mode += " · V: ROCK";
                if (a.GyoActive) mode += " · GYO";
                HudGui.Bar(new Rect(x, y, w, h), player.Aura.Current / Mathf.Max(1f, player.Aura.Capacity), new Color(0.4f, 0.8f, 1f),
                    $"Aura {player.Aura.Current:0}/{player.Aura.Capacity:0}{mode}");
                y += h + 4;
            }
            HudGui.Bar(new Rect(x, y, w, h), f.Immediate / 100f, new Color(0.9f, 0.55f, 0.2f), $"Fatigue {f.Immediate:0} · readiness {HudGui.Pct(f.Readiness)}");
            y += h + 4;
            HudGui.Bar(new Rect(x, y, w, h), f.Accumulated / 100f, new Color(0.6f, 0.35f, 0.75f), $"Accumulated {f.Accumulated:0} · adaptation {HudGui.Pct(f.AdaptationEfficiency)}");
            y += h + 6;
            string strain = p.GetStrain(BodyArea.Upper) > 0.5f || p.GetStrain(BodyArea.Lower) > 0.5f
                ? $" · <color=#ff8866>strain: upper {p.GetStrain(BodyArea.Upper):0} / legs {p.GetStrain(BodyArea.Lower):0}</color>" : "";
            GUI.Label(new Rect(x, y, w, 40), $"<size=11>Training sessions {p.TrainingSessions}{strain} · Tab: dashboard · F1: controls</size>", HudGui.Small);

            // Guided next step, so a new player always knows what to aim for.
            y += 40f;
            var goalRect = new Rect(x - 6, y, w + 12, 46f);
            HudGui.Panel(goalRect);
            GUI.Label(new Rect(x, y + 4, w, 40f), $"<color=#ffdd66><b>Next goal</b></color>  <size=11>{NextGoal(p)}</size>", HudGui.Wrap);
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
        }

        void DrawWorldLabels()
        {
            if (worldCamera == null) return;
            foreach (var i in Interactable.All)
                DrawLabel(i.Position + Vector3.up * 2.6f, i.DisplayName, null);

            bool gyo = player.Aura.GyoActive;
            foreach (var e in EnemyController.All)
            {
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
            GUI.Label(r, $"<b>{text}</b>", HudGui.Center);
            if (health != null)
                HudGui.Bar(new Rect(s.x - 60, r.yMax, 120, 10), health.Fraction, HudGui.Bad, null);
            if (extra != null)
                GUI.Label(new Rect(s.x - 110, r.y - 20, 220, 22), extra, HudGui.Center);
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
            GUI.Label(r, prompt, HudGui.Center);
        }

        void DrawNotices(GameSession session)
        {
            float w = 460f;
            float y = Screen.height - 140f;
            for (int i = session.Notices.Count - 1; i >= 0; i--)
            {
                var r = new Rect(Screen.width - w - 16f, y, w, 26f);
                HudGui.Panel(r);
                GUI.Label(new Rect(r.x + 8, r.y + 3, r.width - 16, r.height), session.Notices[i].text, HudGui.Label);
                y -= 30f;
            }
        }

        void DrawHelp()
        {
            const string text =
                "<b>Controls</b>\n" +
                "WASD move · Mouse look · Shift sprint · Space jump · Ctrl dodge\n" +
                "LMB light · RMB heavy · Q block (tap just before a hit to parry)\n" +
                "E interact · [ / ] drill intensity · Space/LMB drill action\n" +
                "T Ten · hold R Ren · Z Zetsu · hold G Gyo · Ten+Ren Ken\n" +
                "F Ko · hold V Jajanken: Rock (once learned)\n" +
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
