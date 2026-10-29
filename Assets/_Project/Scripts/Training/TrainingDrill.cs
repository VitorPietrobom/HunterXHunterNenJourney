using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Lifecycle shared by every exercise: start → tick → score → submit to the ProgressionService.
    /// Drills never change attributes themselves; they only build a validated <see cref="ExerciseOutcome"/>.
    /// </summary>
    public abstract class TrainingDrill : Interactable
    {
        [SerializeField] protected ExerciseDefinition definition;
        [SerializeField] protected int intensityLevel = 2;
        [SerializeField] protected float maxSessionSeconds = 120f;

        float readinessSum;
        int readinessSamples;

        protected PlayerContext player;
        protected bool running;
        protected float elapsed;

        public override bool IsBusy => running;
        public ExerciseDefinition Definition => definition;
        protected int MaxLevel => definition != null ? definition.maxIntensityLevel : 5;
        protected float AverageReadiness => readinessSamples > 0 ? readinessSum / readinessSamples : Progression.Fatigue.Readiness;
        protected double Attr(AttributeId id) => Progression.GetAttribute(id);

        protected abstract string IntensityLabel { get; }
        protected abstract void OnBegin();
        protected abstract void OnTick(float dt);
        protected abstract ExerciseOutcome BuildOutcome(bool aborted);
        protected abstract string StatusText { get; }
        protected virtual void OnEnd() { }
        protected virtual void DrawMeter(Rect r) { }
        protected virtual float MeterHeight => 0f;

        public override string GetPrompt()
        {
            if (running) return "[E] End session early";
            if (definition != null && definition.requiresAura && !Progression.IsAuraAwakened)
                return $"{displayName} — requires aura awakening (Meditation Stone)";
            return $"[E] Start {displayName} · {IntensityLabel}   [ / ] change intensity\n<size=11>{Description()}</size>";
        }

        /// <summary>One line for the prompt: what the drill is and what it trains.</summary>
        string Description()
        {
            if (definition == null) return "";
            var trains = new System.Text.StringBuilder();
            foreach (var t in definition.targets)
            {
                if (trains.Length > 0) trains.Append(", ");
                trains.Append(AttributeInfo.Label(t.attribute));
            }
            return $"{definition.description} Trains {trains}.";
        }

        public override void Adjust(int delta)
        {
            int next = Mathf.Clamp(intensityLevel + delta, 1, MaxLevel);
            if (next == intensityLevel) return;
            intensityLevel = next;
            Session.PushNotice($"{displayName} intensity: {IntensityLabel}", 2f);
        }

        public override void Interact(PlayerContext ctx)
        {
            if (running)
            {
                Finish(true);
                return;
            }
            if (definition == null)
            {
                Debug.LogError($"{name}: no ExerciseDefinition assigned.", this);
                return;
            }
            if (definition.requiresAura && !Progression.IsAuraAwakened)
            {
                Session.PushNotice("You cannot sense your aura yet. Awaken it at the Meditation Stone.");
                return;
            }
            var area = definition.stressedArea;
            if (area != BodyArea.None && Progression.GetStrain(area) >= ProgressionService.StrainBlockThreshold)
            {
                Session.PushNotice($"Your {ProgressionService.AreaName(area)} is strained ({Progression.GetStrain(area):0}). Rest at the bench before training it again.", 3f);
                return;
            }
            if (!CanStart(ctx, out string reason))
            {
                Session.PushNotice(reason);
                return;
            }
            player = ctx;
            running = true;
            elapsed = 0f;
            if (definition.category == ExerciseCategory.Physical) player.Aura.SuppressRen = true;
            readinessSum = 0f;
            readinessSamples = 0;
            OnBegin();
        }

        protected virtual bool CanStart(PlayerContext ctx, out string reason)
        {
            reason = null;
            return true;
        }

        protected virtual void Update()
        {
            if (!running) return;
            float dt = Time.deltaTime;
            elapsed += dt;
            readinessSum += Progression.Fatigue.Readiness;
            readinessSamples++;
            OnTick(dt);
            if (running && elapsed > maxSessionSeconds) Finish(true, "Session timed out.");
        }

        protected void Finish(bool aborted, string reason = null)
        {
            if (!running) return;
            running = false;
            var outcome = BuildOutcome(aborted);
            outcome.readiness = AverageReadiness;
            if (reason != null) outcome.endReason = reason;
            else if (aborted && outcome.endReason == null) outcome.endReason = "Session ended early.";
            OnEnd();
            player.Aura.SuppressRen = false;
            Progression.ApplyExercise(definition, outcome);
            player = null;
        }

        protected override void OnDisable()
        {
            if (running)
            {
                running = false;
                OnEnd();
                if (player != null) player.Aura.SuppressRen = false;
            }
            base.OnDisable();
        }

        void OnGUI()
        {
            if (!running || Session == null || Session.InputBlocked) return;
            var meter = HudGui.DrillPanel($"{displayName} — {IntensityLabel}", StatusText + "\n<size=11>[E] end early</size>", MeterHeight);
            if (MeterHeight > 0f) DrawMeter(meter);
        }
    }
}
