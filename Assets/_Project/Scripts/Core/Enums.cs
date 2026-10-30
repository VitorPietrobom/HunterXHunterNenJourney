namespace HunterAwakening
{
    public enum AttributeId
    {
        // Physical
        Strength,
        Speed,
        Endurance,
        Coordination,
        Mobility,
        Durability,
        // Aura (available after awakening)
        AuraCapacity,
        MaxOutput,
        SustainedOutput,
        ControlPrecision,
        Efficiency,
        Perception,
        RecoveryRate
    }

    public enum TechniqueId
    {
        LightAttack,
        HeavyAttack,
        Dodge,
        Block,
        Lift,
        Footwork,
        Ten,
        Ren,
        // Appended (saves store technique ids as ints).
        Zetsu,
        Gyo,
        Ken,
        Ko,
        /// <summary>Gon's Hatsu, Rock: a chanted, charged punch.</summary>
        Jajanken
    }

    public enum BodyArea { None, Upper, Lower }

    public enum ExerciseCategory
    {
        Physical,
        Aura,
        Combat
    }

    public static class AttributeInfo
    {
        public static readonly AttributeId[] Physical =
        {
            AttributeId.Strength, AttributeId.Speed, AttributeId.Endurance,
            AttributeId.Coordination, AttributeId.Mobility, AttributeId.Durability
        };

        public static readonly AttributeId[] Aura =
        {
            AttributeId.AuraCapacity, AttributeId.MaxOutput, AttributeId.SustainedOutput,
            AttributeId.ControlPrecision, AttributeId.Efficiency, AttributeId.Perception, AttributeId.RecoveryRate
        };

        public static bool IsAura(AttributeId id) => id >= AttributeId.AuraCapacity;

        public static string Label(AttributeId id) => id switch
        {
            AttributeId.AuraCapacity => "Aura Capacity",
            AttributeId.MaxOutput => "Max Output",
            AttributeId.SustainedOutput => "Sustained Output",
            AttributeId.ControlPrecision => "Control Precision",
            AttributeId.RecoveryRate => "Aura Recovery",
            _ => id.ToString()
        };

        public static string Label(TechniqueId id) => id switch
        {
            TechniqueId.LightAttack => "Light Attack",
            TechniqueId.HeavyAttack => "Heavy Attack",
            _ => id.ToString()
        };
    }
}
