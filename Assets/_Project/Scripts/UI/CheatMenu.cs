using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HunterAwakening
{
    /// <summary>
    /// , + .: a cheat panel for testing and messing around — refills, god mode, XP, every unlock, every attribute and
    /// mastery, the ant camps, and teleports. Changes go straight into the save state (and are saved like any progress).
    /// </summary>
    public class CheatMenu : MonoBehaviour
    {
        /// <summary>The player takes no damage (checked by PlayerCombat).</summary>
        public static bool GodMode { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => GodMode = false;

        GameSession session;
        bool open;
        Vector2 scroll;
        float inner; // usable row width inside the scroll view

        void Awake() => session = GetComponent<GameSession>();

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || session.TitleOpen) return;
            if (!open && GUIUtility.keyboardControl != 0) return; // typing (e.g. in the Hatsu editor), not a hotkey
            // Comma and period together, in either order.
            if ((kb.commaKey.isPressed && kb.periodKey.wasPressedThisFrame) || (kb.periodKey.isPressed && kb.commaKey.wasPressedThisFrame)) Toggle();
            else if (open && kb.escapeKey.wasPressedThisFrame) Toggle();
        }

        void Toggle()
        {
            open = !open;
            session.CheatsOpen = open;
            GUIUtility.keyboardControl = 0;
        }

        ProgressionService P => session.Progression;
        PlayerContext Player => session.Player;

        void OnGUI()
        {
            if (!open) return;
            HudGui.Use();
            GUI.depth = -15;
            float w = Mathf.Min(460f, Screen.width - 40f), h = Screen.height - 40f;
            var area = new Rect(Screen.width - w - 20f, 20f, w, h);
            HudGui.Fill(area, new Color(0.06f, 0.07f, 0.09f, 0.97f)); // opaque, so the HUD underneath doesn't show through
            HudGui.Panel(area);
            GUILayout.BeginArea(new Rect(area.x + 16, area.y + 12, area.width - 32, area.height - 24));
            GUILayout.Label("CHEATS  <size=11><color=#9aa4b2>, + . or Esc to close · changes are saved</color></size>", HudGui.Header);
            inner = area.width - 32 - 22;
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

            Quick();
            Unlocks();
            Attributes();
            Mastery();
            World();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static bool Button(string label, float width) => GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(26));

        /// <summary>A button sized to share its row evenly with <paramref name="perRow"/> − 1 others.</summary>
        bool Button(string label, int perRow = 1) => Button(label, (inner - 4f * (perRow - 1)) / perRow);

        void Changed() => P.NotifyChanged();

        // ------------------------------------------------------------------ Quick

        void Quick()
        {
            HudGui.SectionHeader("Quick");
            GUILayout.BeginHorizontal();
            if (Button("Full heal", 3))
            {
                Player.Health.RestoreFull();
                Player.Vitals.RestoreFull();
                if (P.IsAuraAwakened) Player.Aura.FillReserve();
            }
            if (Button("Clear fatigue", 3))
            {
                P.State.fatigue.immediate = P.State.fatigue.accumulated = 0f;
                P.State.injury.upper = P.State.injury.lower = 0f;
                Changed();
            }
            GodMode = GUILayout.Toggle(GodMode, " God mode", GUILayout.Width((inner - 8f) / 3), GUILayout.Height(26));
            GUILayout.EndHorizontal();

            GUILayout.Label($"Hunter Level {P.HunterLevel} · {P.HunterXp:0} XP", HudGui.Label);
            GUILayout.BeginHorizontal();
            if (Button("+100 XP", 3)) P.AddHunterXp(100);
            if (Button("+1000 XP", 3)) P.AddHunterXp(1000);
            if (Button("+1 level", 3))
            {
                var (into, needed) = StatEffects.LevelProgress(P.HunterXp);
                P.AddHunterXp(needed - into);
            }
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ Unlocks

        void Unlock(string id, string title)
        {
            if (!P.IsAuraAwakened) P.AwakenAura(0f);
            P.UnlockInsight(id, title, "Unlocked with the cheat menu.");
            Changed();
        }

        void Unlocks()
        {
            HudGui.SectionHeader("Unlocks");
            GUILayout.BeginHorizontal();
            if (Button(P.IsAuraAwakened ? "Aura awakened (done)" : "Awaken aura", 2) && !P.IsAuraAwakened)
            {
                P.AwakenAura(0f);
                Player.Aura.FillReserve();
            }
            if (Button(P.KnowsInsight(WaterDivination.EnhancerInsight) ? "Nen type known (done)" : "Water Divination", 2))
                Unlock(WaterDivination.EnhancerInsight, $"Nen type: {P.Hunter.nenType}");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Button(P.KnowsInsight(AuraController.KenInsight) ? "Ken (done)" : "Learn Ken", 3)) Unlock(AuraController.KenInsight, "Ken");
            if (Button(P.KnowsInsight(AuraController.KoInsight) ? "Ko (done)" : "Learn Ko", 3)) Unlock(AuraController.KoInsight, "Ko");
            string hatsu = P.IsCustomHunter ? P.Hunter.hatsuName : "Jajanken";
            string hatsuButton = P.IsCustomHunter ? "Hatsu" : "Jajanken";
            if (Button(P.KnowsInsight(AuraController.JajankenInsight) ? $"{hatsuButton} (done)" : $"Learn {hatsuButton}", 3))
            {
                Unlock(AuraController.KoInsight, "Ko");
                Unlock(AuraController.JajankenInsight, P.IsCustomHunter ? $"Hatsu — {hatsu}" : "Jajanken — Rock");
            }
            GUILayout.EndHorizontal();
            if (Button("Unlock everything (mastery to 60+)"))
            {
                P.AwakenAura(0f);
                foreach (var (id, title) in new[] { (WaterDivination.EnhancerInsight, $"Nen type: {P.Hunter.nenType}"), (AuraController.KenInsight, "Ken"),
                             (AuraController.KoInsight, "Ko"), (AuraController.JajankenInsight, P.IsCustomHunter ? $"Hatsu — {hatsu}" : "Jajanken — Rock") })
                    P.UnlockInsight(id, title, "Unlocked with the cheat menu.", announce: false);
                foreach (TechniqueId t in Enum.GetValues(typeof(TechniqueId))) P.State.Mastery(t).value = Mathf.Max(P.State.Mastery(t).value, 60f);
                Player.Aura.FillReserve();
                Changed();
                session.PushNotice("Everything unlocked.", 2f);
            }
        }

        // ------------------------------------------------------------------ Attributes and mastery

        void Attributes()
        {
            HudGui.SectionHeader("Attributes");
            foreach (AttributeId id in Enum.GetValues(typeof(AttributeId)))
            {
                var entry = P.State.Attribute(id);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{AttributeInfo.Label(id)}", HudGui.Label, GUILayout.Width(150));
                GUILayout.Label($"<b>{entry.value:0.0}</b>", HudGui.Label, GUILayout.Width(60));
                foreach (int step in new[] { -10, -1, 1, 10 })
                    if (Button(step > 0 ? $"+{step}" : step.ToString(), 46f))
                    {
                        entry.value = Math.Max(PlayerState.Baseline, entry.value + step);
                        Changed();
                    }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (Button("All +10", 2))
            {
                foreach (AttributeId id in Enum.GetValues(typeof(AttributeId))) P.State.Attribute(id).value += 10;
                Changed();
            }
            if (Button("All to baseline", 2))
            {
                foreach (AttributeId id in Enum.GetValues(typeof(AttributeId))) P.State.Attribute(id).value = PlayerState.Baseline;
                Changed();
            }
            GUILayout.EndHorizontal();
        }

        void Mastery()
        {
            HudGui.SectionHeader("Mastery (0–100)");
            foreach (TechniqueId id in Enum.GetValues(typeof(TechniqueId)))
            {
                var entry = P.State.Mastery(id);
                GUILayout.BeginHorizontal();
                GUILayout.Label(AttributeInfo.Label(id), HudGui.Label, GUILayout.Width(150));
                GUILayout.Label($"<b>{entry.value:0}</b>", HudGui.Label, GUILayout.Width(40));
                float value = GUILayout.HorizontalSlider(entry.value, 0f, 100f, GUILayout.Height(20));
                if (!Mathf.Approximately(value, entry.value))
                {
                    entry.value = Mathf.Round(value);
                    Changed();
                }
                GUILayout.EndHorizontal();
            }
        }

        // ------------------------------------------------------------------ World

        void World()
        {
            HudGui.SectionHeader("World");
            GUILayout.BeginHorizontal();
            if (Button("Clear every camp", 2))
            {
                foreach (var e in Encounter.All)
                    if (!e.IsBoss) P.MarkEncounterCleared(e.Id);
                Changed();
            }
            if (Button("Reset camps + King", 2))
            {
                P.State.clearedEncounters.Clear();
                foreach (var e in Encounter.All)
                    foreach (var m in e.GetComponentsInChildren<EnemyController>(true))
                    {
                        m.gameObject.SetActive(true);
                        m.ResetToIdle();
                    }
                Changed();
                session.PushNotice("Camps and the King are back.", 2f);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Teleport", HudGui.Label);
            int camps = 0;
            foreach (var e in Encounter.All) if (!e.IsBoss) camps++;
            GUILayout.BeginHorizontal();
            foreach (var e in Encounter.All)
            {
                if (e.IsBoss) continue;
                if (Button(e.Id.Replace("ants_", "Camp "), Mathf.Max(1, camps))) TeleportNear(e.transform);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Button("Training grounds", 2)) Teleport(new Vector3(0f, 1f, 0f), 0f);
            foreach (var e in Encounter.All)
                if (e.IsBoss && Button("The King's castle", 2))
                    Teleport(e.transform.position + new Vector3(0f, 1f, -26f), 0f);
            GUILayout.EndHorizontal();
        }

        void TeleportNear(Transform camp)
        {
            // Just outside the ants' notice range, facing the camp.
            Vector3 at = camp.position + camp.forward * 14f + Vector3.up;
            Teleport(at, Quaternion.LookRotation(-camp.forward).eulerAngles.y);
        }

        void Teleport(Vector3 position, float yaw)
        {
            Player.Motor.Teleport(position, yaw);
            Toggle();
        }
    }
}
