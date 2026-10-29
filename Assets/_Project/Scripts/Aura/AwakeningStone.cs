using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Aura awakening. Gated by capability (completed training sessions), not a level: the body must be conditioned first.
    /// </summary>
    public class AwakeningStone : Interactable
    {
        [SerializeField] int requiredSessions = 3;
        [SerializeField] float channelSeconds = 8f;
        [SerializeField] Transform kneelPoint;

        PlayerContext player;
        bool channeling;
        float progress;

        public override bool IsBusy => channeling;

        public override string GetPrompt()
        {
            if (Progression.IsAuraAwakened) return "Meditation Stone — your aura is awakened";
            if (channeling) return "[E] Stop meditating";
            int sessions = Progression.TrainingSessions;
            return sessions < requiredSessions
                ? $"Meditation Stone — condition your body first ({sessions}/{requiredSessions} completed training sessions)"
                : "[E] Meditate to awaken your aura";
        }

        public override void Interact(PlayerContext ctx)
        {
            if (Progression.IsAuraAwakened) return;
            if (channeling)
            {
                Stop();
                return;
            }
            if (Progression.TrainingSessions < requiredSessions)
            {
                Session.PushNotice($"You sense nothing yet. Complete {requiredSessions - Progression.TrainingSessions} more training session(s).");
                return;
            }
            player = ctx;
            channeling = true;
            progress = 0f;
            player.Motor.SetLocked(true);
            player.SetPose(PlayerPose.Meditate);
            if (kneelPoint != null) player.Motor.Teleport(kneelPoint.position, kneelPoint.eulerAngles.y);
        }

        void Update()
        {
            if (!channeling) return;
            progress += Time.deltaTime;
            if (progress < channelSeconds) return;

            var ctx = player;
            Stop();
            Progression.AwakenAura(0f);
            ctx.Aura.FillReserve();
        }

        void Stop()
        {
            channeling = false;
            if (player != null)
            {
                player.Motor.SetLocked(false);
                player.SetPose(PlayerPose.None);
            }
            player = null;
        }

        protected override void OnDisable()
        {
            if (channeling) Stop();
            base.OnDisable();
        }

        void OnGUI()
        {
            if (!channeling || Session.InputBlocked) return;
            var meter = HudGui.DrillPanel("Meditating", "Still the body. Feel the energy leaking from your skin...", 20f);
            HudGui.Bar(meter, progress / channelSeconds, new Color(0.7f, 0.5f, 1f), HudGui.Pct(progress / channelSeconds));
        }
    }
}
