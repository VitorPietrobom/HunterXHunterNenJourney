namespace HunterAwakening
{
    /// <summary>
    /// Capability milestones proven in play (GDD §5.5). Each unlocks one concrete, permanent change, checked with
    /// <see cref="ProgressionService.HasBreakthrough"/> by the gameplay code that implements it.
    /// </summary>
    public static class Breakthroughs
    {
        public const string TenInMotion = "ten_in_motion";
        public const string UnbrokenRen = "unbroken_ren";
        public const string ReadAndAnswer = "read_and_answer";
        public const string ShadowStrike = "shadow_strike";
        public const string IronBody = "iron_body";
        public const string PerfectForm = "perfect_form";

        public readonly struct Definition
        {
            public readonly string id, title, challenge, reward;
            public readonly bool requiresAura;

            public Definition(string id, string title, string challenge, string reward, bool requiresAura)
            {
                this.id = id;
                this.title = title;
                this.challenge = challenge;
                this.reward = reward;
                this.requiresAura = requiresAura;
            }
        }

        public static readonly Definition[] All =
        {
            new Definition(PerfectForm, "Perfect Form", "Complete a Lift Rack set at ≥88% of your 1RM with ≥90% form.", "Lift form window 20% wider.", false),
            new Definition(IronBody, "Iron Body", "Defeat the Veteran Hunter or the Elite Nen User.", "+10% max health.", false),
            new Definition(TenInMotion, "Ten in Motion", "Keep Ten up while sprinting for 8 s without stopping.", "Sprinting costs 20% less stamina.", true),
            new Definition(UnbrokenRen, "Unbroken Ren", "In a match, hold Ren for 10 s and absorb 2 hits without it breaking.", "Ren is 30% less likely to break.", true),
            new Definition(ReadAndAnswer, "Read and Answer", "In one match, parry or perfectly dodge 3 attacks with Gyo active.", "Gyo's parry and dodge bonuses +50%.", true),
            new Definition(ShadowStrike, "Shadow Strike", "Land 2 ambushes from Zetsu in one match.", "Ambushes deal ×2 damage instead of ×1.6.", true),
        };

        public static Definition? Find(string id)
        {
            foreach (var d in All)
                if (d.id == id) return d;
            return null;
        }
    }
}
