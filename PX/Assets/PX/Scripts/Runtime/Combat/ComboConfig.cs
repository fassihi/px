using System;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// One swing in a combo. Timings are in seconds and run in order:
    /// startup (wind-up, no hit), active (the hitbox is live), recovery (follow-through).
    /// </summary>
    [Serializable]
    public sealed class AttackStep
    {
        [Min(0f)] public float startup = 0.06f;
        [Min(0.01f)] public float active = 0.08f;
        [Min(0f)] public float recovery = 0.22f;

        [Tooltip("Seconds into recovery before the next swing, a dash or a jump can interrupt it.")]
        [Min(0f)] public float cancelDelay = 0.04f;

        [Min(0f)] public float damage = 10f;

        [Tooltip("Forward speed while winding up and swinging, on the ground.")]
        [Min(0f)] public float lunge = 3f;

        [Tooltip("Hitbox centre relative to the feet. x = forward, y = up.")]
        public Vector2 hitboxCenter = new Vector2(1.1f, 1f);

        [Tooltip("Hitbox size. x = reach, y = height.")]
        public Vector2 hitboxSize = new Vector2(1.8f, 1.4f);

        [Tooltip("How long the world freezes when this swing connects.")]
        [Min(0f)] public float hitStop = 0.05f;

        [Tooltip("How hard the camera shakes when this swing connects, in metres.")]
        [Min(0f)] public float shake = 0.05f;

        [Tooltip("How hard a connecting swing pushes the target back, in m/s.")]
        [Min(0f)] public float knockback = 3f;

        [Tooltip("Stamina it costs to throw. Enemies ignore it.")]
        [Min(0f)] public float staminaCost = 8f;

        public float ActiveEnd => startup + active;

        public float CancelFrom => startup + active + Mathf.Min(cancelDelay, recovery);

        public float Total => startup + active + recovery;
    }

    /// <summary>The steps of a ground combo, in the order the attack button plays them.</summary>
    [CreateAssetMenu(menuName = "PX/Combo Config", fileName = "ComboConfig")]
    public sealed class ComboConfig : ScriptableObject
    {
        public AttackStep[] steps = Array.Empty<AttackStep>();

        [Tooltip("The special attack from a crouch: a low sweep.")]
        public AttackStep crouchAttack = new AttackStep
        {
            startup = 0.1f, active = 0.12f, recovery = 0.3f, cancelDelay = 0.1f,
            damage = 14f, lunge = 0f, hitStop = 0.11f, shake = 0.1f, knockback = 2f, staminaCost = 12f,
            hitboxCenter = new Vector2(0.95f, 0.35f), hitboxSize = new Vector2(1.4f, 0.6f),
        };

        [Tooltip("The counter after a parry: one heavy blow while the enemy is off balance.")]
        public AttackStep counterAttack = new AttackStep
        {
            startup = 0.1f, active = 0.14f, recovery = 0.45f, cancelDelay = 0.2f,
            damage = 45f, lunge = 4f, hitStop = 0.22f, shake = 0.24f, knockback = 9f, staminaCost = 0f,
            hitboxCenter = new Vector2(0.9f, 1.1f), hitboxSize = new Vector2(1.4f, 1.2f),
        };

        [Tooltip("The attack in the air: a flying kick that carries her forward.")]
        public AttackStep airAttack = new AttackStep
        {
            startup = 0.14f, active = 0.22f, recovery = 0.3f, cancelDelay = 0.12f,
            damage = 22f, lunge = 6f, hitStop = 0.14f, shake = 0.14f, knockback = 7f, staminaCost = 14f,
            hitboxCenter = new Vector2(1f, 0.55f), hitboxSize = new Vector2(1.7f, 1.8f),
        };

        [Tooltip("Upward speed the flying kick gives her at the start, in m/s, if she is not already rising faster. " +
                 "Zero: she throws it from wherever she is, rising or falling.")]
        [Min(0f)] public float airAttackLift = 0f;

        [Tooltip("How long an attack press is remembered while the current swing finishes.")]
        [Min(0f)] public float bufferTime = 0.2f;

        [Tooltip("Seconds a hit's camera shake takes to die away.")]
        [Min(0.01f)] public float shakeTime = 0.22f;

        [Tooltip("How far the hitbox extends to each side of the rail. Enough to cover a body on the rail, no more.")]
        [Min(0.1f)] public float hitboxDepth = 0.6f;
    }
}
