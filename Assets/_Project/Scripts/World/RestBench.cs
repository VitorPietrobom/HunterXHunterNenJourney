using UnityEngine;

namespace HunterAwakening
{
    /// <summary>In-game rest: fast immediate-fatigue recovery, slow accumulated-fatigue recovery. Only active time counts.</summary>
    public class RestBench : Interactable
    {
        [SerializeField] Transform seatPoint;
        [SerializeField] float healPerSecond = 15f;

        PlayerContext player;
        bool resting;
        float restTime;

        public override bool IsBusy => resting;

        public override string GetPrompt() => resting ? "[E] Stand up" : "[E] Rest (recovers fatigue, health and stamina)";

        public override void Interact(PlayerContext ctx)
        {
            if (resting) Stop();
            else Begin(ctx);
        }

        void Begin(PlayerContext ctx)
        {
            player = ctx;
            resting = true;
            restTime = 0f;
            Session.IsResting = true;
            player.Motor.SetLocked(true);
            player.SetPose(PlayerPose.Rest);
            if (seatPoint != null) player.Motor.Teleport(seatPoint.position, seatPoint.eulerAngles.y);
        }

        void Stop()
        {
            if (!resting) return;
            resting = false;
            if (Session != null) Session.IsResting = false; // may already be gone when the scene unloads
            if (player != null)
            {
                player.Motor.SetLocked(false);
                player.SetPose(PlayerPose.None);
            }
            player = null;
        }

        void Update()
        {
            if (!resting) return;
            restTime += Time.deltaTime;
            player.Health.Heal(healPerSecond * Time.deltaTime);
            player.Vitals.RestoreFull();
            if (player.Input.Move.sqrMagnitude > 0.25f) Stop();
        }

        protected override void OnDisable()
        {
            Stop();
            base.OnDisable();
        }

        void OnGUI()
        {
            if (!resting || Session.InputBlocked) return;
            var f = Progression.Fatigue;
            HudGui.DrillPanel("Resting",
                $"Immediate fatigue {f.Immediate:0} (readiness {HudGui.Pct(f.Readiness)})\n" +
                $"Accumulated fatigue {f.Accumulated:0} (adaptation {HudGui.Pct(f.AdaptationEfficiency)}) — clears slowly; rest longer after hard days.\n" +
                $"<size=11>Rested {restTime:0}s · move or [E] to stand up</size>");
        }
    }
}
