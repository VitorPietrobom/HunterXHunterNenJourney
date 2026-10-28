using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Immediate + accumulated fatigue (GDD §9). Owned by the ProgressionService.</summary>
    public class FatigueService
    {
        readonly ProgressionConfig config;
        FatigueState state;

        public FatigueService(ProgressionConfig config, FatigueState state)
        {
            this.config = config;
            this.state = state;
        }

        public float Immediate => state.immediate;
        public float Accumulated => state.accumulated;

        /// <summary>Execution readiness, reduced by immediate fatigue.</summary>
        public float Readiness => ProgressionMath.Readiness(state.immediate, config.readinessGamma);

        /// <summary>How much of a session's stimulus turns into adaptation, reduced by accumulated fatigue.</summary>
        public float AdaptationEfficiency => ProgressionMath.Readiness(state.accumulated, 1f);

        /// <summary>Movement / attack / regen multiplier. Fatigue is visible in play, not only in numbers.</summary>
        public float PerformanceMultiplier => Mathf.Lerp(0.7f, 1f, Readiness);

        /// <summary>Training-session load: builds both immediate and accumulated fatigue.</summary>
        public void AddLoad(float load)
        {
            if (!(load > 0f)) return;
            state.immediate = ProgressionMath.NextFatigue(state.immediate, load, 0f);
            state.accumulated = ProgressionMath.NextFatigue(state.accumulated, load * config.accumulatedFraction, 0f);
        }

        /// <summary>Moment-to-moment exertion (sprinting, Ren, combat): mostly immediate fatigue.</summary>
        public void AddExertion(float amount)
        {
            if (!(amount > 0f)) return;
            state.immediate = ProgressionMath.NextFatigue(state.immediate, amount, 0f);
            state.accumulated = ProgressionMath.NextFatigue(state.accumulated, amount * config.accumulatedFraction * 0.25f, 0f);
        }

        /// <summary>Recovery happens only during active play (called from Update), never while the game is closed.</summary>
        public void Recover(float dt, bool resting)
        {
            if (!(dt > 0f)) return;
            float immediateRate = resting ? config.restImmediatePerSecond : config.passiveRecoveryPerSecond;
            float accumulatedRate = resting ? config.restAccumulatedPerSecond : 0f;
            state.immediate = ProgressionMath.NextFatigue(state.immediate, 0f, immediateRate * dt);
            state.accumulated = ProgressionMath.NextFatigue(state.accumulated, 0f, accumulatedRate * dt);
        }
    }
}
