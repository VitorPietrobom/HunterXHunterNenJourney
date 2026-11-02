using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HunterAwakening.Tests
{
    public class ProgressionMathTests
    {
        [Test]
        public void ResponseMultiplier_MatchesCurveAtReferencePoints()
        {
            // A0 = 10, C = 100, p = 2 (GDD §6.2). Note: the GDD's "0.45 at 100" is approximate; the formula gives 0.55.
            Assert.AreEqual(1.0, ProgressionMath.ResponseMultiplier(10, 10, 100, 2), 1e-9);
            Assert.AreEqual(1.0 / 1.81, ProgressionMath.ResponseMultiplier(100, 10, 100, 2), 1e-9);
            Assert.AreEqual(1.0 / 16.21, ProgressionMath.ResponseMultiplier(400, 10, 100, 2), 1e-9);
        }

        [Test]
        public void AttributeGain_IsPositiveAndDiminishing_WithNoHardCap()
        {
            double previous = double.MaxValue;
            foreach (double a in new[] { 10.0, 50, 100, 400, 1000, 1e6 })
            {
                double gain = ProgressionMath.AttributeGain(a, 1.0, 2.0, 10, 100, 2);
                Assert.Greater(gain, 0.0, $"gain must stay positive at {a}");
                Assert.Less(gain, previous);
                previous = gain;
            }
        }

        [Test]
        public void AttributeGain_IsZeroForInvalidStimulus()
        {
            Assert.AreEqual(0.0, ProgressionMath.AttributeGain(10, 0, 2, 10, 100, 2));
            Assert.AreEqual(0.0, ProgressionMath.AttributeGain(10, -5, 2, 10, 100, 2));
            Assert.AreEqual(0.0, ProgressionMath.AttributeGain(10, double.NaN, 2, 10, 100, 2));
        }

        [Test]
        public void EffectiveStimulus_ClampsEveryFactor()
        {
            Assert.AreEqual(1.5, ProgressionMath.EffectiveStimulus(5, 99, 3, 2, 7), 1e-9);
            Assert.AreEqual(0.0, ProgressionMath.EffectiveStimulus(-1, 1, 1, 1, 1));
            Assert.AreEqual(0.0, ProgressionMath.EffectiveStimulus(1, double.NaN, 1, 1, 1));
        }

        [Test]
        public void MasteryGain_SlowsNear100_ButContinuesBeyond()
        {
            float early = ProgressionMath.MasteryGain(0, 1, 4, 1.5f);
            float late = ProgressionMath.MasteryGain(90, 1, 4, 1.5f);
            float beyond = ProgressionMath.MasteryGain(150, 1, 4, 1.5f);
            Assert.Greater(early, late);
            Assert.Greater(beyond, 0f);
            Assert.AreEqual(0f, ProgressionMath.MasteryGain(50, 0, 4, 1.5f));
        }

        [Test]
        public void Readiness_StaysInRange()
        {
            Assert.AreEqual(1f, ProgressionMath.Readiness(0, 1.5f), 1e-6f);
            Assert.AreEqual(0f, ProgressionMath.Readiness(100, 1.5f), 1e-6f);
            Assert.AreEqual(0f, ProgressionMath.Readiness(500, 1.5f), 1e-6f);
            Assert.AreEqual(1f, ProgressionMath.Readiness(-20, 1.5f), 1e-6f);
            Assert.AreEqual(1f, ProgressionMath.Readiness(float.NaN, 1.5f), 1e-6f);
        }

        [Test]
        public void AuraCost_NeverNegativeOrInfinite()
        {
            Assert.AreEqual(10f / ProgressionMath.MinEfficiency, ProgressionMath.AuraCost(10, 1, 0), 1e-4f);
            Assert.AreEqual(10f / ProgressionMath.MinEfficiency, ProgressionMath.AuraCost(10, 1, -3), 1e-4f);
            Assert.AreEqual(0f, ProgressionMath.AuraCost(-10, 1, 1));
            Assert.AreEqual(0f, ProgressionMath.AuraCost(10, -1, 1));
            Assert.IsFalse(float.IsInfinity(ProgressionMath.AuraCost(10, 1, float.NaN)));
        }

        [Test]
        public void StatEffects_StayFiniteForHugeAttributes()
        {
            const double huge = 1e12;
            Assert.LessOrEqual(StatEffects.SprintSpeed(huge), 32f);
            Assert.LessOrEqual(StatEffects.JumpHeight(huge), 8f);
            Assert.Less(StatEffects.DamageReduction(huge), 1f);
            Assert.IsFalse(float.IsInfinity(StatEffects.StrengthDamageFactor(huge)));
        }

        [Test]
        public void Ren_BecomesUnstableWhenOutputOutpacesControl()
        {
            Assert.AreEqual(0f, StatEffects.RenOverdrive(10, 10));
            Assert.AreEqual(0f, StatEffects.RenOverdrive(20, 40), "control ahead of output means no overdrive");
            Assert.Greater(StatEffects.RenBreakChance(60, 10, 0), StatEffects.RenBreakChance(10, 10, 0));
            Assert.Greater(StatEffects.RenLeakage(60, 10, 0), StatEffects.RenLeakage(10, 10, 0));
            Assert.Less(StatEffects.RenBreakChance(60, 10, 100), StatEffects.RenBreakChance(60, 10, 0), "Ren mastery steadies it");
            Assert.LessOrEqual(StatEffects.RenBreakChance(1e9, 10, 0), 0.7f);
            Assert.GreaterOrEqual(StatEffects.RenBreakChance(10, 1e9, 1000), 0.03f);
        }

        [Test]
        public void Jajanken_HandsAndCombosScale()
        {
            Assert.Less(StatEffects.JajankenPaperFactor, StatEffects.JajankenScissorsFactor, "Paper trades power for range");
            Assert.Less(StatEffects.JajankenScissorsFactor, 1f, "Rock is the strongest hand");
            Assert.Greater(StatEffects.ComboDamageMultiplier(1, false), StatEffects.ComboDamageMultiplier(0, false));
            Assert.Greater(StatEffects.ComboDamageMultiplier(2, true), StatEffects.ComboDamageMultiplier(2, false), "the finisher hits hardest");
            Assert.Less(MasteryMilestones.JajankenPaper, MasteryMilestones.JajankenScissors);
        }

        [Test]
        public void NenStrikes_LeanOnAuraMoreThanMuscle()
        {
            Assert.AreEqual(1f, StatEffects.NenStrikeStrengthFactor(10), 1e-4f, "no bonus at baseline strength");
            Assert.Less(StatEffects.NenStrikeStrengthFactor(90), StatEffects.StrengthDamageFactor(90));
            Assert.Greater(StatEffects.NenStrikeStrengthFactor(90), 1f, "strength still helps");
        }

        [Test]
        public void Jajanken_PowerGrowsWithTheChantAndMastery()
        {
            Assert.Less(StatEffects.JajankenChargeTime(100f), StatEffects.JajankenChargeTime(0f), "mastery shortens the chant");
            Assert.Greater(StatEffects.JajankenDamageMultiplier(0.9f, 10, 0f), StatEffects.JajankenDamageMultiplier(0.5f, 10, 0f));
            Assert.Greater(StatEffects.JajankenDamageMultiplier(1f, 10, 0f), StatEffects.JajankenDamageMultiplier(0.99f, 10, 0f) * 1.2f,
                "completing the chant adds the vow's burst");
            Assert.Greater(StatEffects.JajankenDamageMultiplier(1f, 10, 100f), StatEffects.JajankenDamageMultiplier(1f, 10, 0f));
            Assert.LessOrEqual(StatEffects.JajankenDamageMultiplier(1f, 1e9, 1e6f), 9f);
        }

        [Test]
        public void Ren_PresenceGrowsWithTrainingAndIsCapped()
        {
            Assert.AreEqual(1f, StatEffects.RenPresence(10, 0), 1e-4f);
            Assert.Greater(StatEffects.RenPresence(40, 0), StatEffects.RenPresence(10, 0), "Max Output enlarges Ren");
            Assert.Greater(StatEffects.RenPresence(10, 60), StatEffects.RenPresence(10, 0), "Ren mastery enlarges Ren");
            Assert.LessOrEqual(StatEffects.RenPresence(1e9, 1000), 3.5f);
        }

        [Test]
        public void Ren_BodyEnhancementIsCapped()
        {
            Assert.Greater(StatEffects.RenSpeedMultiplier(10), 1f);
            Assert.LessOrEqual(StatEffects.RenSpeedMultiplier(1e9), 1.35f);
            Assert.LessOrEqual(StatEffects.RenAttackSpeedMultiplier(1e9), 1.25f);
            Assert.Less(StatEffects.RenMovingDrainMultiplier(200), StatEffects.RenMovingDrainMultiplier(10), "Sustained Output cuts moving drain");
        }

        [Test]
        public void ZetsuAndGyo_ScaleWithMasteryAndPerception()
        {
            Assert.Less(StatEffects.ZetsuDetectionRange(100), StatEffects.ZetsuDetectionRange(0), "mastery lets you hide closer");
            Assert.Greater(StatEffects.GyoParryBonus(60), StatEffects.GyoParryBonus(10));
            Assert.LessOrEqual(StatEffects.GyoParryBonus(1e9), 0.15f);
            Assert.LessOrEqual(StatEffects.GyoDodgeBonus(1e9), 0.08f);
            Assert.Less(StatEffects.GyoDrain(100), StatEffects.GyoDrain(0));
        }

        [Test]
        public void KenAndKo_AreStrongButBounded()
        {
            Assert.Less(StatEffects.KenDamageTakenMultiplier(10, 10, 0), StatEffects.RenDamageTakenMultiplier(10), "Ken out-defends Ren");
            Assert.GreaterOrEqual(StatEffects.KenDamageTakenMultiplier(1e9, 1e9, 1000), 0.2f);
            Assert.LessOrEqual(StatEffects.KoDamageMultiplier(1e9), 4f);
            Assert.Less(StatEffects.KoAuraCost(100f), StatEffects.KoAuraCost(0f), "Ko mastery cuts the cost");
        }

        [Test]
        public void Ren_DefenceIsStrongerThanTenAndBounded()
        {
            Assert.Less(StatEffects.RenDamageTakenMultiplier(10), StatEffects.TenDamageMultiplier(10, 0));
            Assert.GreaterOrEqual(StatEffects.RenDamageTakenMultiplier(1e9), 0.3f);
            Assert.LessOrEqual(StatEffects.RenKnockback(1e9), 8f);
            Assert.LessOrEqual(StatEffects.RenStagger(1e9), 0.9f);
        }
    }

    public class ProgressionServiceTests
    {
        ExerciseDefinition definition;
        ProgressionConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ProgressionConfig.CreateRuntimeDefault();
            definition = ScriptableObject.CreateInstance<ExerciseDefinition>();
            definition.id = "test";
            definition.displayName = "Test";
            definition.targets = new List<AttributeTarget> { new AttributeTarget { attribute = AttributeId.Strength, specificity = 1f } };
            definition.fatigueCost = 20f;
            definition.minCompletionForAdaptation = 0.3f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(config);
        }

        static ExerciseOutcome Good() => new ExerciseOutcome { completion = 1f, quality = 0.9f, intensity = 1f, readiness = 1f };

        [Test]
        public void GoodSession_RaisesTargetAttribute_AndAddsFatigue()
        {
            var p = new ProgressionService(config, PlayerState.CreateNew());
            var report = p.ApplyExercise(definition, Good());
            Assert.Greater(p.GetAttribute(AttributeId.Strength), 10.0);
            Assert.Greater(p.Fatigue.Immediate, 0f);
            Assert.IsTrue(report.adapted);
            Assert.AreEqual(1, p.TrainingSessions);
        }

        [Test]
        public void Interview_AlwaysWritesAValidHatsu()
        {
            var rng = new System.Random(7);
            var types = new System.Collections.Generic.HashSet<NenType>();
            for (int run = 0; run < 200; run++)
            {
                var picks = new System.Collections.Generic.List<int>();
                foreach (var q in HatsuLibrary.Interview) picks.Add(rng.Next(q.answers.Length));
                var hunter = new HunterProfile { custom = true, name = "Test" + run };
                HatsuLibrary.Author(hunter, picks);
                types.Add(hunter.nenType);
                Assert.IsTrue(HatsuRuntime.Validate(hunter.hatsuSource, out var meta, out var error), $"{hunter.nenType} '{hunter.hatsuName}': {error}");
                Assert.AreEqual(hunter.hatsuName, meta.name);
            }
            Assert.GreaterOrEqual(types.Count, 5, "the interview reaches most of the six types");
        }

        [Test]
        public void HatsuSandbox_StopsLoopsAndHidesTheSystem()
        {
            Assert.IsFalse(HatsuRuntime.Validate("hatsu = {}\nfunction hatsu.activate(p) end\nwhile true do end", out _, out var loop));
            StringAssert.Contains("too long", loop);
            Assert.IsFalse(HatsuRuntime.Validate("io.open('save.json')", out _, out _), "no file access");
            Assert.IsFalse(HatsuRuntime.Validate("os.execute('calc')", out _, out _), "no shell");
            Assert.IsFalse(HatsuRuntime.Validate("load('return 1')()", out _, out _), "no loading code");
            Assert.IsFalse(HatsuRuntime.Validate("hatsu = { name = 'x' }", out _, out var missing));
            StringAssert.Contains("activate", missing);
        }

        [Test]
        public void Aura_IsADailyBudget()
        {
            float pool = StatEffects.AuraCapacity(10);
            float renPerSecond = StatEffects.RenOutput(10) * StatEffects.AuraDrainScale;
            Assert.Greater(pool / renPerSecond, 150f, "a full pool holds Ren through a long fight");
            Assert.Less(StatEffects.JajankenChantCost(10), pool * 0.15f, "several Rocks fit in one pool");
            Assert.Greater(StatEffects.KoAuraCost(0f), renPerSecond * 10f, "a Ko costs far more than holding Ren for a while");
            Assert.Less(StatEffects.RestRefillSeconds(40), StatEffects.RestRefillSeconds(10), "Recovery Rate speeds up resting");
            Assert.Less(StatEffects.ZetsuAuraRecovery(10) * 60f, pool * 0.5f, "Zetsu recovery is slow: a minute gives back less than half");
        }

        [Test]
        public void HunterXp_LevelsUpAndBoostsStats()
        {
            var p = new ProgressionService(config, PlayerState.CreateNew());
            Assert.AreEqual(1, p.HunterLevel);
            Assert.AreEqual(0, p.AddHunterXp(StatEffects.XpToNextLevel(1) - 1), "one short of the next level");
            Assert.AreEqual(1, p.AddHunterXp(1), "the last point levels up");
            Assert.AreEqual(2, p.HunterLevel);
            Assert.Greater(StatEffects.XpToNextLevel(5), StatEffects.XpToNextLevel(1), "later levels take more XP");
            Assert.AreEqual(1f, StatEffects.HunterLevelDamageFactor(1), 1e-4f, "no bonus at level 1");
            Assert.Greater(StatEffects.HunterLevelHealthFactor(10), 1f);
            var (into, needed) = StatEffects.LevelProgress(StatEffects.XpToNextLevel(1) + 5);
            Assert.AreEqual(5, into, 1e-6);
            Assert.AreEqual(StatEffects.XpToNextLevel(2), needed, 1e-6);
        }

        [Test]
        public void IncompleteSession_GivesNoAdaptation_ButStillCostsFatigue()
        {
            var p = new ProgressionService(config, PlayerState.CreateNew());
            var o = Good();
            o.completion = 0.1f;
            var report = p.ApplyExercise(definition, o);
            Assert.AreEqual(10.0, p.GetAttribute(AttributeId.Strength), 1e-9);
            Assert.IsFalse(report.adapted);
            Assert.Greater(p.Fatigue.Immediate, 0f);
        }

        [Test]
        public void Fatigue_ReducesGains()
        {
            var fresh = new ProgressionService(config, PlayerState.CreateNew());
            fresh.ApplyExercise(definition, Good());
            double freshGain = fresh.GetAttribute(AttributeId.Strength) - 10.0;

            var tired = new ProgressionService(config, PlayerState.CreateNew());
            var o = Good();
            o.readiness = 0.3f;
            tired.ApplyExercise(definition, o);
            double tiredGain = tired.GetAttribute(AttributeId.Strength) - 10.0;

            Assert.Less(tiredGain, freshGain);
        }

        [Test]
        public void RepeatedSloppySessions_HaveDiminishingReturns()
        {
            var p = new ProgressionService(config, PlayerState.CreateNew());
            var sloppy = new ExerciseOutcome { completion = 1f, quality = 0.2f, intensity = 1f, readiness = 1f };
            p.ApplyExercise(definition, sloppy);
            double first = p.GetAttribute(AttributeId.Strength) - 10.0;
            p.State.fatigue.immediate = 0f;
            p.State.fatigue.accumulated = 0f;
            double before = p.GetAttribute(AttributeId.Strength);
            p.ApplyExercise(definition, sloppy);
            double second = p.GetAttribute(AttributeId.Strength) - before;
            Assert.Less(second, first);
        }

        [Test]
        public void Breakthroughs_UnlockOnce_AndSurviveSaveRoundTrip()
        {
            var p = new ProgressionService(config, PlayerState.CreateNew());
            Assert.IsFalse(p.HasBreakthrough(Breakthroughs.IronBody));
            Assert.IsTrue(p.UnlockBreakthrough(Breakthroughs.IronBody));
            Assert.IsFalse(p.UnlockBreakthrough(Breakthroughs.IronBody), "second unlock is a no-op");
            Assert.IsFalse(p.UnlockBreakthrough("not_a_breakthrough"));

            var loaded = JsonUtility.FromJson<PlayerState>(JsonUtility.ToJson(p.State));
            loaded.Sanitize();
            Assert.IsTrue(new ProgressionService(config, loaded).HasBreakthrough(Breakthroughs.IronBody));
        }

        [Test]
        public void Strain_FromOverload_ReducesGainsAndHealsWithRest()
        {
            definition.stressedArea = BodyArea.Upper;
            var p = new ProgressionService(config, PlayerState.CreateNew());
            var failed = Good();
            failed.strain = 70f;
            var report = p.ApplyExercise(definition, failed);
            Assert.AreEqual(70f, p.GetStrain(BodyArea.Upper), 1e-3f);
            Assert.AreEqual(0f, p.GetStrain(BodyArea.Lower));
            Assert.Greater(report.strainAdded, 0f);
            Assert.Less(p.StrainPenalty(BodyArea.Upper), 1f);

            // A strained area trains less effectively.
            var fresh = new ProgressionService(config, PlayerState.CreateNew());
            fresh.ApplyExercise(definition, Good());
            double freshGain = fresh.GetAttribute(AttributeId.Strength) - 10.0;
            p.State.fatigue.immediate = p.State.fatigue.accumulated = 0f;
            double before = p.GetAttribute(AttributeId.Strength);
            p.ApplyExercise(definition, Good());
            Assert.Less(p.GetAttribute(AttributeId.Strength) - before, freshGain);

            p.TickActiveTime(10f, true);
            Assert.Less(p.GetStrain(BodyArea.Upper), 70f, "rest heals strain");
        }

        [Test]
        public void SparringRecords_ArePerTier_AndKeepPreTierSaves()
        {
            var state = PlayerState.CreateNew();
            state.Record("sparring_rival").bestMetric = 50f; // a pre-tier save's best win
            var p = new ProgressionService(config, state);
            Assert.AreEqual(50f, p.BestSparringTime("rival"));
            p.RecordCombat("veteran", true, 80f, "win");
            p.RecordCombat("veteran", true, 90f, "slower win");
            Assert.AreEqual(80f, p.BestSparringTime("veteran"));
            Assert.AreEqual(0f, p.BestSparringTime("elite"));
            Assert.AreEqual(50f, p.BestSparringTime("rival"));
        }

        [Test]
        public void RequiresAura_BlocksAdaptationUntilAwakened()
        {
            definition.requiresAura = true;
            var p = new ProgressionService(config, PlayerState.CreateNew());
            Assert.IsFalse(p.ApplyExercise(definition, Good()).adapted);
            p.AwakenAura(100f);
            Assert.IsTrue(p.ApplyExercise(definition, Good()).adapted);
        }
    }

    public class PlayerStateTests
    {
        [Test]
        public void Sanitize_RepairsInvalidValues()
        {
            var s = PlayerState.CreateNew();
            s.Attribute(AttributeId.Speed).value = double.NaN;
            s.Mastery(TechniqueId.Dodge).value = float.PositiveInfinity;
            s.fatigue.immediate = 900f;
            s.aura.current = 50f; // not awakened
            s.history = null;
            s.Sanitize();
            Assert.AreEqual(PlayerState.Baseline, s.Attribute(AttributeId.Speed).value);
            Assert.AreEqual(0f, s.Mastery(TechniqueId.Dodge).value);
            Assert.AreEqual(100f, s.fatigue.immediate);
            Assert.AreEqual(0f, s.aura.current);
            Assert.IsNotNull(s.history);
        }

        [Test]
        public void JsonRoundTrip_PreservesProgress()
        {
            var s = PlayerState.CreateNew();
            s.Attribute(AttributeId.Strength).value = 23.456789;
            s.Mastery(TechniqueId.Ten).value = 31f;
            s.aura.awakened = true;
            s.aura.current = 42f;
            s.trainingSessions = 7;

            var loaded = JsonUtility.FromJson<PlayerState>(JsonUtility.ToJson(s));
            SaveService.Migrate(loaded);
            loaded.Sanitize();

            Assert.AreEqual(23.456789, loaded.Attribute(AttributeId.Strength).value, 1e-9);
            Assert.AreEqual(31f, loaded.Mastery(TechniqueId.Ten).value);
            Assert.IsTrue(loaded.aura.awakened);
            Assert.AreEqual(42f, loaded.aura.current);
            Assert.AreEqual(7, loaded.trainingSessions);
            Assert.AreEqual(PlayerState.CurrentSchemaVersion, loaded.schemaVersion);
        }
    }
}
