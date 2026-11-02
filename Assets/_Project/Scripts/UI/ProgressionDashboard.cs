using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Progression dashboard (GDD §12.4): attributes with their practical effects, mastery and next milestone,
    /// fatigue, the last session's before/after with causal notes, combat analysis and recent history.
    /// </summary>
    public class ProgressionDashboard
    {
        Vector2 leftScroll, middleScroll, rightScroll;
        readonly HuntersGuide guide = new HuntersGuide();
        bool confirmDelete;

        public void Draw(GameSession session)
        {
            var p = session.Progression;
            HudGui.Use();
            float margin = 20f;
            var area = new Rect(margin, margin, Screen.width - margin * 2, Screen.height - margin * 2);
            HudGui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.02f, 0.04f, 0.55f));
            HudGui.Panel(area);

            GUILayout.BeginArea(new Rect(area.x + 18, area.y + 12, area.width - 36, area.height - 24));
            GUILayout.Label("HUNTER: AWAKENING", HudGui.Eyebrow);
            GUILayout.Label(session.GuidePage ? "Hunter's Guide" : "Progression", HudGui.Title);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Progression", !session.GuidePage ? HudGui.PrimaryButton : GUI.skin.button, GUILayout.Width(150), GUILayout.Height(28)))
                session.GuidePage = false;
            if (GUILayout.Button("Hunter's Guide", session.GuidePage ? HudGui.PrimaryButton : GUI.skin.button, GUILayout.Width(150), GUILayout.Height(28)))
                session.GuidePage = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (session.GuidePage)
            {
                GUILayout.EndArea();
                // Below the fixed header (eyebrow, title, page buttons), above the footer.
                const float header = 96f, footer = 50f;
                var guideArea = new Rect(area.x + 18, area.y + 12 + header, area.width - 36, area.height - 24 - header - footer);
                guide.Draw(guideArea, p);
                GUILayout.BeginArea(new Rect(area.x + 18, area.yMax - 50, area.width - 36, 40));
                DrawFooter(session);
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label($"<color=#9aa4b2>Active play</color> {TimeSpan.FromSeconds(p.State.activePlaySeconds):hh\\:mm\\:ss}   " +
                            $"<color=#9aa4b2>Training sessions</color> {p.TrainingSessions}   <color=#9aa4b2>Progress only accrues while playing</color>",
                new GUIStyle(HudGui.Center) { fontSize = 12 });
            GUILayout.Space(8);

            float columnWidth = (area.width - 36 - 30) / 3f;
            float columnHeight = area.height - 150;
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(HudGui.CardBox, GUILayout.Width(columnWidth), GUILayout.Height(columnHeight));
            leftScroll = GUILayout.BeginScrollView(leftScroll);
            DrawAttributes(p);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(HudGui.CardBox, GUILayout.Width(columnWidth), GUILayout.Height(columnHeight));
            middleScroll = GUILayout.BeginScrollView(middleScroll);
            DrawFatigue(p);
            DrawMastery(p);
            DrawBreakthroughs(session, p);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(HudGui.CardBox, GUILayout.Width(columnWidth), GUILayout.Height(columnHeight));
            rightScroll = GUILayout.BeginScrollView(rightScroll);
            DrawLastSession(session.LastTrainingReport);
            DrawCombat(session.LastCombatReport);
            DrawHistory(p);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            DrawFooter(session);
            GUILayout.EndArea();
        }

        static void DrawAttributes(ProgressionService p)
        {
            HudGui.SectionHeader("Body");
            foreach (var id in AttributeInfo.Physical) AttributeRow(p, id);
            HudGui.SectionHeader("Aura");
            if (!p.IsAuraAwakened)
            {
                GUILayout.Label("Dormant. Complete 3 training sessions, then meditate at the Meditation Stone.", HudGui.Wrap);
                return;
            }
            foreach (var id in AttributeInfo.Aura) AttributeRow(p, id);
        }

        static void AttributeRow(ProgressionService p, AttributeId id)
        {
            double v = p.GetAttribute(id);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{AttributeInfo.Label(id)}</b>", HudGui.Label);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<color=#ffc552><b>{v:0.00}</b></color>", HudGui.Label);
            GUILayout.EndHorizontal();
            GUILayout.Label(StatEffects.Describe(id, v), HudGui.Small);
            GUILayout.Space(3);
        }

        static void DrawFatigue(ProgressionService p)
        {
            var f = p.Fatigue;
            HudGui.SectionHeader("Condition");
            GUILayout.Label($"Immediate fatigue <b>{f.Immediate:0}</b> → readiness {HudGui.Pct(f.Readiness)}, performance ×{f.PerformanceMultiplier:0.00}", HudGui.Wrap);
            GUILayout.Label($"Accumulated fatigue <b>{f.Accumulated:0}</b> → adaptation efficiency {HudGui.Pct(f.AdaptationEfficiency)}", HudGui.Wrap);
            string advice = f.Readiness < 0.6f ? "Readiness is low: execution and gains suffer. Rest at the bench."
                : f.AdaptationEfficiency < 0.8f ? "Accumulated fatigue is cutting adaptation. A longer rest clears it."
                : "Fresh enough to train hard.";
            GUILayout.Label(advice, HudGui.Small);
            foreach (var area in new[] { BodyArea.Upper, BodyArea.Lower })
            {
                float s = p.GetStrain(area);
                if (s < 0.5f) continue;
                string blocked = s >= ProgressionService.StrainBlockThreshold ? " — <color=#ff6655>too strained to train</color>" : "";
                GUILayout.Label($"Strain ({ProgressionService.AreaName(area)}) <b>{s:0}</b>: {(area == BodyArea.Upper ? "strength" : "speed")} ×{p.StrainPenalty(area):0.00}, training gains ×{1f - s / 150f:0.00}{blocked}. Rest at the bench to heal.", HudGui.Wrap);
            }
        }

        static void DrawBreakthroughs(GameSession session, ProgressionService p)
        {
            HudGui.SectionHeader("Breakthroughs");
            var tracker = session.Player != null ? session.Player.GetComponent<BreakthroughTracker>() : null;
            foreach (var b in Breakthroughs.All)
            {
                if (b.requiresAura && !p.IsAuraAwakened) continue;
                if (p.HasBreakthrough(b.id))
                {
                    GUILayout.Label($"<color=#66dd77>✔ <b>{b.title}</b></color> — {b.reward}", HudGui.Wrap);
                    continue;
                }
                string progress = tracker != null ? tracker.Progress(b.id) : "";
                GUILayout.Label($"<b>{b.title}</b>{(progress.Length > 0 ? $"  <size=11>({progress})</size>" : "")}", HudGui.Label);
                GUILayout.Label($"{b.challenge} Reward: {b.reward}", HudGui.Small);
            }
            if (!p.IsAuraAwakened) GUILayout.Label("More breakthroughs appear once your aura is awakened.", HudGui.Small);
        }

        static void DrawMastery(ProgressionService p)
        {
            HudGui.SectionHeader("Technique mastery");
            foreach (TechniqueId id in Enum.GetValues(typeof(TechniqueId)))
            {
                bool auraTechnique = id >= TechniqueId.Ten; // Ten, Ren, Zetsu, Gyo, Ken, Ko
                if (auraTechnique && !p.IsAuraAwakened) continue;
                if (id == TechniqueId.Ken && !p.KnowsInsight(AuraController.KenInsight))
                {
                    GUILayout.Label($"<b>Ken</b> <size=11>(locked: Ten and Ren mastery {MasteryMilestones.KenUnlockTenMastery:0}+)</size>", HudGui.Label);
                    continue;
                }
                if (id == TechniqueId.Ko && !p.KnowsInsight(AuraController.KoInsight))
                {
                    GUILayout.Label($"<b>Ko</b> <size=11>(locked: Ren mastery {MasteryMilestones.KoUnlockRenMastery:0}+ and Max Output {MasteryMilestones.KoUnlockMaxOutput:0}+)</size>", HudGui.Label);
                    continue;
                }
                if (id == TechniqueId.Jajanken && !p.KnowsInsight(AuraController.JajankenInsight))
                {
                    GUILayout.Label($"<b>Jajanken</b> <size=11>(locked: know Ko, Ren mastery {MasteryMilestones.JajankenUnlockRenMastery:0}+)</size>", HudGui.Label);
                    continue;
                }
                float m = p.GetMastery(id);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{AttributeInfo.Label(id)}</b>", HudGui.Label);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"<size=11><color=#9aa4b2>{MasteryMilestones.Tier(m)}</color></size>  <color=#ffc552><b>{m:0.0}</b></color>", HudGui.Label);
                GUILayout.EndHorizontal();
                var bar = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
                HudGui.Bar(bar, Mathf.Min(m, 100f) / 100f, m >= 100f ? HudGui.Gold : HudGui.AuraCyan, null);
                GUILayout.Label($"Next: {MasteryMilestones.Next(id, m)}", HudGui.Small);
                GUILayout.Space(2);
            }
        }

        static void DrawLastSession(TrainingReport r)
        {
            HudGui.SectionHeader("Last training session");
            if (r == null)
            {
                GUILayout.Label("No session yet this run. Walk to a station and press E.", HudGui.Small);
                return;
            }
            var o = r.outcome;
            GUILayout.Label($"<b>{r.exerciseName}</b>", HudGui.Label);
            GUILayout.Label($"Completion {HudGui.Pct(o.completion)} · quality {HudGui.Pct(o.quality)} · intensity {HudGui.Pct(o.intensity)} · readiness {HudGui.Pct(o.readiness)}", HudGui.Small);
            foreach (var m in o.metrics) GUILayout.Label("• " + m, HudGui.Small);

            foreach (var c in r.changes)
            {
                GUILayout.Label($"{AttributeInfo.Label(c.attribute)}: {c.before:0.00} → <b>{c.after:0.00}</b> (+{c.Delta:0.000})", HudGui.Label);
                GUILayout.Label($"   {StatEffects.Describe(c.attribute, c.before)}  →  {StatEffects.Describe(c.attribute, c.after)}", HudGui.Small);
            }
            if (r.hasMastery)
                GUILayout.Label($"{AttributeInfo.Label(r.masteryTechnique)} mastery {r.masteryBefore:0.0} → {r.masteryAfter:0.0}", HudGui.Label);
            GUILayout.Label($"Fatigue +{r.fatigueAdded:0.0}", HudGui.Small);

            foreach (var note in r.notes) GUILayout.Label("▸ " + note, HudGui.Wrap);
        }

        static void DrawCombat(CombatReport r)
        {
            HudGui.SectionHeader("Last sparring match");
            if (r == null)
            {
                GUILayout.Label("No match yet this run. The arena post starts one.", HudGui.Small);
                return;
            }
            GUILayout.Label($"<b>{r.headline}</b>", HudGui.Label);
            foreach (var s in r.stats) GUILayout.Label("• " + s, HudGui.Small);
            foreach (var w in r.weaknesses) GUILayout.Label("▸ " + w, HudGui.Wrap);
        }

        static void DrawHistory(ProgressionService p)
        {
            HudGui.SectionHeader("Recent history");
            var history = p.State.history;
            for (int i = history.Count - 1; i >= Mathf.Max(0, history.Count - 10); i--)
            {
                var h = history[i];
                GUILayout.Label($"<b>{h.title}</b> <size=10>@ {TimeSpan.FromSeconds(h.activeTime):hh\\:mm\\:ss}</size>", HudGui.Label);
                GUILayout.Label(h.summary, HudGui.Small);
            }
        }

        void DrawFooter(GameSession session)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save (F5)", GUILayout.Width(120), GUILayout.Height(28)) && session.Save()) session.PushNotice("Game saved.");
            if (GUILayout.Button("Reload save (F9)", GUILayout.Width(150), GUILayout.Height(28))) session.ReloadFromDisk();
            GUILayout.Space(20);
            if (!confirmDelete)
            {
                if (GUILayout.Button("Delete save…", HudGui.DangerButton, GUILayout.Width(130), GUILayout.Height(30))) confirmDelete = true;
            }
            else
            {
                GUILayout.Label("Erase all progress?", HudGui.Label, GUILayout.Width(150));
                if (GUILayout.Button("Yes, delete", HudGui.DangerButton, GUILayout.Width(110), GUILayout.Height(30))) session.DeleteSaveAndRestart();
                if (GUILayout.Button("Cancel", GUILayout.Width(80), GUILayout.Height(28))) confirmDelete = false;
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<size=11>Save file: {session.Saves.FilePath}</size>", HudGui.Small);
            if (GUILayout.Button("Close (Tab)", HudGui.PrimaryButton, GUILayout.Width(130), GUILayout.Height(30))) session.DashboardOpen = false;
            GUILayout.EndHorizontal();
        }
    }
}
