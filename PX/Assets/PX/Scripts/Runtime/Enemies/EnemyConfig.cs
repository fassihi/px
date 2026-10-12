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
        [Tooltip("Once it has noticed her, it follows her until she is this far away along the rail.")]
        [Min(0f)] public float chaseRange = 30f;
        [Tooltip("It notices her only within this height of itself, in metres. Once following, it goes after her at any height.")]
        [Min(0f)] public float aggroHeight = 2.5f;
        [Tooltip("It attacks when the player is within this distance along the rail, in metres.")]
        [Min(0f)] public float attackRange = 1.9f;
        [Tooltip("Closer than this, in metres, it backs off to get room to swing.")]
        [Min(0f)] public float tooClose = 1.1f;

        [Header("Movement")]
        [Min(0f)] public float walkSpeed = 1.6f;
        [Tooltip("Speed backing away while facing her, in m/s.")]
        [Min(0f)] public float walkBackSpeed = 1.2f;
        [Min(0f)] public float acceleration = 12f;
        [Min(0f)] public float gravity = 30f;

        [Header("Climbing")]
        [Tooltip("It climbs onto obstacles up to this high above its feet, in metres, when one blocks its way.")]
        [Min(0f)] public float climbHeight = 1.7f;
        [Tooltip("How far ahead of its body it looks for something to climb, in metres.")]
        [Min(0f)] public float climbProbe = 0.5f;
        [Min(0.1f)] public float climbDuration = 0.9f;
        [Tooltip("Seconds it has to be stopped by an obstacle before it climbs it.")]
        [Min(0f)] public float climbAfterBlocked = 0.15f;

        [Header("Attack")]
        [Tooltip("The first swing of its combo. The startup is the telegraph the player reads.")]
        public AttackStep attack = new AttackStep
        {
            startup = 0.55f, active = 0.15f, recovery = 0.65f, cancelDelay = 0.25f,
            damage = 15f, lunge = 1.5f, hitStop = 0.12f, shake = 0.14f, knockback = 7f,
            hitboxCenter = new Vector2(1.1f, 1.1f), hitboxSize = new Vector2(1.6f, 1.6f),
        };
        [Tooltip("The second swing, which it follows the first with only some of the time.")]
        public AttackStep attack2 = new AttackStep
        {
            startup = 0.4f, active = 0.18f, recovery = 0.8f, cancelDelay = 0f,
            damage = 20f, lunge = 2.5f, hitStop = 0.14f, shake = 0.18f, knockback = 10f,
            hitboxCenter = new Vector2(1.1f, 1.0f), hitboxSize = new Vector2(1.7f, 1.4f),
        };
        [Tooltip("Chance, 0 to 1, that it follows its first swing with the second.")]
        [Range(0f, 1f)] public float comboChance = 0.5f;
        [Tooltip("Chance, 0 to 1, that it backs off for a moment after attacking, to set up its distance again.")]
        [Range(0f, 1f)] public float retreatChance = 0.35f;
        [Tooltip("Seconds it backs off for when it does.")]
        [Min(0f)] public float retreatTime = 0.8f;
        [Tooltip("Seconds after an attack before it attacks again.")]
        [Min(0f)] public float attackCooldown = 0.9f;
        [Tooltip("How far the hitbox extends to each side of the rail.")]
        [Min(0.1f)] public float hitboxDepth = 0.8f;

        [Header("Taking hits")]
        [Tooltip("Seconds a hit staggers it.")]
        [Min(0f)] public float hitStun = 0.45f;
        [Tooltip("Damage it shrugs off during a swing before it is staggered out of it. Light strikes do not interrupt it.")]
        [Min(0f)] public float attackPoise = 20f;
        [Tooltip("Seconds it is thrown off balance when its blow is parried: the opening for her counter.")]
        [Min(0f)] public float parriedStun = 1.3f;
        [Tooltip("How quickly a knocked-back enemy stops sliding, in m/s per second.")]
        [Min(0f)] public float knockbackFriction = 25f;

        [Header("Animation")]
        public AnimationClip idle;
        public AnimationClip walk;
        [Tooltip("Walking backwards while facing her. Empty: the walk plays in reverse.")]
        public AnimationClip walkBack;
        [Tooltip("Ground speed in m/s at which the walk clip plays at normal speed.")]
        [Min(0.1f)] public float walkClipSpeed = 1.4f;
        [Tooltip("The first swing, trimmed to the part that plays; impact is where the blade is furthest out.")]
        public ClipWindow attackClip;
        public ClipWindow attack2Clip;
        public ClipWindow hurt;
        [Tooltip("Thrown off balance by a parry.")]
        public ClipWindow parried;
        [Tooltip("Climbing onto an obstacle.")]
        public ClipWindow climb;
        public AnimationClip death;
        [Min(0f)] public float fadeTime = 0.12f;

        /// <summary>The swing for a step of its combo.</summary>
        public AttackStep Step(int index) => index == 0 ? attack : attack2;
    }
}
