using UnityEngine;

namespace PX
{
    /// <summary>
    /// Tuning and clips for one kind of enemy. The fight reads from the timings here: the attack's wind-up is its
    /// telegraph, and its recovery is the opening the player punishes.
    /// </summary>
    [CreateAssetMenu(menuName = "PX/Enemy Config", fileName = "EnemyConfig")]
    public sealed class EnemyConfig : ScriptableObject
    {
        [Header("Senses")]
        [Tooltip("It notices the player within this distance along the rail, in metres.")]
        [Min(0f)] public float aggroRange = 9f;
        [Tooltip("It attacks when the player is within this distance along the rail, in metres.")]
        [Min(0f)] public float attackRange = 1.9f;

        [Header("Movement")]
        [Min(0f)] public float walkSpeed = 1.6f;
        [Min(0f)] public float acceleration = 12f;
        [Min(0f)] public float gravity = 30f;

        [Header("Attack")]
        [Tooltip("Its one attack: a sword swing. The startup is the telegraph the player reads.")]
        public AttackStep attack = new AttackStep
        {
            startup = 0.55f, active = 0.15f, recovery = 0.65f, cancelDelay = 0f,
            damage = 15f, lunge = 1.5f, hitStop = 0.08f, shake = 0.1f, knockback = 7f,
            hitboxCenter = new Vector2(1.2f, 1.1f), hitboxSize = new Vector2(1.8f, 1.6f),
        };
        [Tooltip("Seconds after an attack before it attacks again.")]
        [Min(0f)] public float attackCooldown = 0.9f;
        [Tooltip("How far the hitbox extends to each side of the rail.")]
        [Min(0.1f)] public float hitboxDepth = 1.5f;

        [Header("Taking hits")]
        [Tooltip("Seconds a hit staggers it. A stagger interrupts its attack.")]
        [Min(0f)] public float hitStun = 0.45f;
        [Tooltip("How quickly a knocked-back enemy stops sliding, in m/s per second.")]
        [Min(0f)] public float knockbackFriction = 25f;

        [Header("Animation")]
        public AnimationClip idle;
        public AnimationClip walk;
        [Tooltip("Ground speed in m/s at which the walk clip plays at normal speed.")]
        [Min(0.1f)] public float walkClipSpeed = 1.4f;
        [Tooltip("The swing, trimmed to the part that plays; impact is where the blade is furthest out.")]
        public ClipWindow attackClip;
        public ClipWindow hurt;
        public AnimationClip death;
        [Min(0f)] public float fadeTime = 0.12f;
    }
}
