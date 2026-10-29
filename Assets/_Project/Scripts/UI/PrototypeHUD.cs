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
        [SerializeField] float labelRange = 28f;

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
                string mode = a.ZetsuActive ? " · ZETSU" : a.RenBroken ? " · REN BROKEN" : a.RenActive ? " · REN" : a.TenActive ? " · TEN" : "";
                if (a.GyoActive) mode += " · GYO";
                HudGui.Bar(new Rect(x, y, w, h), player.Aura.Current / Mathf.Max(1f, player.Aura.Capacity), new Color(0.4f, 0.8f, 1f),
                    $"Aura {player.Aura.Current:0}/{player.Aura.Capacity:0}{mode}");
                y += h + 4;
            }
            HudGui.Bar(new Rect(x, y, w, h), f.Immediate / 100f, new Color(0.9f, 0.55f, 0.2f), $"Fatigue {f.Immediate:0} · readiness {HudGui.Pct(f.Readiness)}");
            y += h + 4;
            HudGui.Bar(new Rect(x, y, w, h), f.Accumulated / 100f, new Color(0.6f, 0.35f, 0.75f), $"Accumulated {f.Accumulated:0} · adaptation {HudGui.Pct(f.AdaptationEfficiency)}");
            y += h + 6;
            GUI.Label(new Rect(x, y, w, 40), $"<size=11>Training sessions {p.TrainingSessions} · Tab: dashboard · F1: controls</size>", HudGui.Small);
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
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height - 90f, w, 32f);
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
                "T Ten · hold R Ren · Z Zetsu · hold G Gyo (after aura awakening)\n" +
                "Tab dashboard · Esc pause/settings · F5 save · F9 reload · F1 hide";
            var r = new Rect(Screen.width - 476f, 16f, 460f, 112f);
            HudGui.Panel(r);
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, r.height - 12), text, HudGui.Wrap);
        }
    }
}
