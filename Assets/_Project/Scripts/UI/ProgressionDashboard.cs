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
        bool confirmDelete;

        public void Draw(GameSession session)
        {
            var p = session.Progression;
            float margin = 20f;
            var area = new Rect(margin, margin, Screen.width - margin * 2, Screen.height - margin * 2);
            HudGui.Fill(area, new Color(0.04f, 0.05f, 0.07f, 0.94f));

            GUILayout.BeginArea(new Rect(area.x + 12, area.y + 8, area.width - 24, area.height - 16));
            GUILayout.Label("HUNTER: AWAKENING — Progression", HudGui.Title);
            GUILayout.Label($"Active play {TimeSpan.FromSeconds(p.State.activePlaySeconds):hh\\:mm\\:ss} · training sessions {p.TrainingSessions} · " +
                            "progress only accrues while playing", HudGui.Small);
            GUILayout.Space(6);

            float columnWidth = (area.width - 48) / 3f;
            float columnHeight = area.height - 120;
            GUILayout.BeginHorizontal();

            leftScroll = GUILayout.BeginScrollView(leftScroll, GUILayout.Width(columnWidth), GUILayout.Height(columnHeight));
            DrawAttributes(p);
            GUILayout.EndScrollView();

            middleScroll = GUILayout.BeginScrollView(middleScroll, GUILayout.Width(columnWidth), GUILayout.Height(columnHeight));
            DrawFatigue(p);
            GUILayout.Space(10);
            DrawMastery(p);
            GUILayout.Space(10);
            DrawBreakthroughs(session, p);
            GUILayout.EndScrollView();

            rightScroll = GUILayout.BeginScrollView(rightScroll, GUILayout.Width(columnWidth), GUILayout.Height(columnHeight));
            DrawLastSession(session.LastTrainingReport);
            GUILayout.Space(10);
            DrawCombat(session.LastCombatReport);
            GUILayout.Space(10);
            DrawHistory(p);
            GUILayout.EndScrollView();

            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            DrawFooter(session);
            GUILayout.EndArea();
        }

        static void DrawAttributes(ProgressionService p)
        {
            GUILayout.Label("Body", HudGui.Header);
            foreach (var id in AttributeInfo.Physical) AttributeRow(p, id);
            GUILayout.Space(10);
            GUILayout.Label("Aura", HudGui.Header);
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
            GUILayout.Label($"<b>{AttributeInfo.Label(id)}</b>  {v:0.00}", HudGui.Label);
            GUILayout.Label(StatEffects.Describe(id, v), HudGui.Small);
        }

        static void DrawFatigue(ProgressionService p)
        {
            var f = p.Fatigue;
            GUILayout.Label("Condition", HudGui.Header);
            GUILayout.Label($"Immediate fatigue <b>{f.Immediate:0}</b> → readiness {HudGui.Pct(f.Readiness)}, performance ×{f.PerformanceMultiplier:0.00}", HudGui.Wrap);
            GUILayout.Label($"Accumulated fatigue <b>{f.Accumulated:0}</b> → adaptation efficiency {HudGui.Pct(f.AdaptationEfficiency)}", HudGui.Wrap);
            string advice = f.Readiness < 0.6f ? "Readiness is low: execution and gains suffer. Rest at the bench."
                : f.AdaptationEfficiency < 0.8f ? "Accumulated fatigue is cutting adaptation. A longer rest clears it."
                : "Fresh enough to train hard.";
            GUILayout.Label(advice, HudGui.Small);
        }

        static void DrawBreakthroughs(GameSession session, ProgressionService p)
        {
            GUILayout.Label("Breakthroughs", HudGui.Header);
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
            GUILayout.Label("Technique mastery", HudGui.Header);
            foreach (TechniqueId id in Enum.GetValues(typeof(TechniqueId)))
            {
                bool auraTechnique = id == TechniqueId.Ten || id == TechniqueId.Ren || id == TechniqueId.Zetsu || id == TechniqueId.Gyo;
                if (auraTechnique && !p.IsAuraAwakened) continue;
                float m = p.GetMastery(id);
                GUILayout.Label($"<b>{AttributeInfo.Label(id)}</b>  {m:0.0} ({MasteryMilestones.Tier(m)})", HudGui.Label);
                GUILayout.Label($"Next: {MasteryMilestones.Next(id, m)}", HudGui.Small);
            }
        }

        static void DrawLastSession(TrainingReport r)
        {
            GUILayout.Label("Last training session", HudGui.Header);
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
            GUILayout.Label("Last sparring match", HudGui.Header);
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
            GUILayout.Label("Recent history", HudGui.Header);
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
                if (GUILayout.Button("Delete save…", GUILayout.Width(120), GUILayout.Height(28))) confirmDelete = true;
            }
            else
            {
                GUILayout.Label("Erase all progress?", HudGui.Label, GUILayout.Width(150));
                if (GUILayout.Button("Yes, delete", GUILayout.Width(110), GUILayout.Height(28))) session.DeleteSaveAndRestart();
                if (GUILayout.Button("Cancel", GUILayout.Width(80), GUILayout.Height(28))) confirmDelete = false;
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<size=11>Save file: {session.Saves.FilePath}</size>", HudGui.Small);
            if (GUILayout.Button("Close (Tab)", GUILayout.Width(110), GUILayout.Height(28))) session.DashboardOpen = false;
            GUILayout.EndHorizontal();
        }
    }
}
