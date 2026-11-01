using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// A sparring opponent's fixed stats. Tiers never scale with the player (GDD §8.3): weaker tiers stay weak, stronger
    /// ones stay dangerous until the player earns the ability to handle them.
    /// </summary>
    [Serializable]
    public class RivalTier
    {
        public string id = "rival";
        public string displayName = "Sparring Rival";
        public float maxHealth = 240f;
        public float moveSpeed = 3.4f;
        public float attackDamage = 24f;
        public float telegraphTime = 0.8f;
        public float recoveryTime = 1f;
        public float attackCooldown = 1.1f;
        [Range(0f, 1f)] public float baseGuardChance = 0.2f;
        [Range(0f, 0.9f)] public float damageReduction = 0.1f;
        [Tooltip("Ren output (aura/s) this opponent can face without hesitating.")]
        public float presence = 16f;
        public float attackRange = 2.1f;
        [Tooltip("Hunter XP for beating this opponent.")]
        public float xp = 20f;

        public static RivalTier[] Defaults() => new[]
        {
            new RivalTier { id = "rookie", displayName = "Rookie", maxHealth = 160f, moveSpeed = 3f, attackDamage = 14f,
                telegraphTime = 1f, recoveryTime = 1.2f, attackCooldown = 1.5f, baseGuardChance = 0.1f, damageReduction = 0f, presence = 10f, xp = 10f },
            new RivalTier { id = "rival", displayName = "Sparring Rival", maxHealth = 240f, moveSpeed = 3.4f, attackDamage = 24f,
                telegraphTime = 0.8f, recoveryTime = 1f, attackCooldown = 1.1f, baseGuardChance = 0.2f, damageReduction = 0.1f, presence = 16f, xp = 20f },
            new RivalTier { id = "veteran", displayName = "Veteran Hunter", maxHealth = 380f, moveSpeed = 3.9f, attackDamage = 34f,
                telegraphTime = 0.62f, recoveryTime = 0.8f, attackCooldown = 0.9f, baseGuardChance = 0.3f, damageReduction = 0.2f, presence = 25f, xp = 40f },
            new RivalTier { id = "elite", displayName = "Killua (Godspeed)", maxHealth = 600f, moveSpeed = 4.4f, attackDamage = 48f,
                telegraphTime = 0.5f, recoveryTime = 0.65f, attackCooldown = 0.7f, baseGuardChance = 0.35f, damageReduction = 0.3f, presence = 40f, xp = 70f },
        };

        /// <summary>Chimera ant soldier (Bloster): big claws, slow wind-up, hits hard. <paramref name="m"/> scales a deeper squad.</summary>
        public static RivalTier AntSoldier(float m) => new RivalTier
        {
            id = "ant_soldier", displayName = "Chimera Ant Soldier", maxHealth = 200f * m, moveSpeed = 3.1f, attackDamage = 20f * m,
            telegraphTime = 0.95f, recoveryTime = 1.1f, attackCooldown = 1.7f, baseGuardChance = 0.15f, damageReduction = 0.1f,
            presence = 12f * m, attackRange = 2.7f, xp = 30f * m,
        };

        /// <summary>Chimera ant grunt (the Koala): quick and fragile.</summary>
        public static RivalTier AntGrunt(float m) => new RivalTier
        {
            id = "ant_grunt", displayName = "Chimera Ant", maxHealth = 110f * m, moveSpeed = 3.7f, attackDamage = 11f * m,
            telegraphTime = 0.8f, recoveryTime = 1.1f, attackCooldown = 1.9f, baseGuardChance = 0.05f, damageReduction = 0f,
            presence = 8f * m, attackRange = 2f, xp = 12f * m,
        };

        /// <summary>Meruem, the King: the castle's final fight.</summary>
        public static RivalTier King() => new RivalTier
        {
            id = "king", displayName = "Meruem, the Chimera Ant King", maxHealth = 1300f, moveSpeed = 4.6f, attackDamage = 52f,
            telegraphTime = 0.5f, recoveryTime = 0.6f, attackCooldown = 0.75f, baseGuardChance = 0.35f, damageReduction = 0.3f,
            presence = 55f, attackRange = 2.4f, xp = 600f,
        };
    }
}
