using UnityEngine;

namespace PX
{
    /// <summary>
    /// The soulslike layer of her fighting: stamina and what it pays for, the guard, the parry and the counter that
    /// follows it, and what happens when she dies. Movement is in <see cref="MoveConfig"/>, the strikes in
    /// <see cref="ComboConfig"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "PX/Player Combat Config", fileName = "PlayerCombat")]
    public sealed class PlayerCombatConfig : ScriptableObject
    {
        [Header("Stamina")]
        [Min(1f)] public float maxStamina = 100f;
        [Tooltip("Stamina regained per second, once the delay after spending it has passed.")]
        [Min(0f)] public float staminaRegen = 45f;
        [Tooltip("Seconds after spending stamina before it starts to come back.")]
        [Min(0f)] public float staminaRegenDelay = 0.6f;
        [Tooltip("The longer wait when she runs it all the way down: being out of breath is a punishment.")]
        [Min(0f)] public float exhaustedRegenDelay = 1.3f;
        [Tooltip("Share of the regeneration rate she gets while holding her guard up.")]
        [Range(0f, 1f)] public float guardRegenScale = 0.35f;
        [Tooltip("Cost of a dash. Strikes have their own costs in the combo config. An action needs some stamina left " +
                 "to start, and may take her to zero.")]
        [Min(0f)] public float dashCost = 12f;
        [Min(0f)] public float dodgeCost = 18f;

        [Header("Guard")]
        [Tooltip("Seconds after Guard goes down during which a blow is parried rather than blocked.")]
        [Min(0f)] public float parryWindow = 0.18f;
        [Tooltip("Pressing Guard again sooner than this after the last press does not open a new parry window, so " +
                 "mashing it does not work.")]
        [Min(0f)] public float parryRepressDelay = 0.4f;
        [Tooltip("Share of a blocked blow's damage that still gets through.")]
        [Range(0f, 1f)] public float blockDamageScale = 0.2f;
        [Tooltip("Share of a blocked blow's knockback that still gets through.")]
        [Range(0f, 1f)] public float blockKnockbackScale = 0.5f;
        [Tooltip("Stamina a blocked blow costs, per point of its full damage. When it runs out the guard breaks.")]
        [Min(0f)] public float blockStaminaPerDamage = 2f;
        [Tooltip("Seconds a blocked blow pushes her back and holds her in the guard.")]
        [Min(0f)] public float blockStun = 0.25f;
        [Tooltip("Seconds she staggers when her guard is broken: longer than an ordinary hit.")]
        [Min(0f)] public float guardBreakStun = 0.9f;

        [Header("Parry and counter")]
        [Tooltip("How long the world freezes on a parry. It should feel like a clash.")]
        [Min(0f)] public float parryHitStop = 0.16f;
        [Min(0f)] public float parryShake = 0.12f;
        [Tooltip("Seconds after a parry during which Attack throws the counter instead of the combo.")]
        [Min(0f)] public float counterWindow = 1.1f;

        [Header("Death")]
        [Tooltip("Seconds from her fall to getting up again at the last checkpoint.")]
        [Min(0f)] public float respawnDelay = 2.5f;
    }
}
