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

        [Header("Awareness")]
        [Tooltip("With its brain set to stealth, it only notices her within this distance along the rail, in the direction it faces.")]
        [Min(0f)] public float sightRange = 11f;
        [Tooltip("It sees her only within this height of its own feet, in metres.")]
        [Min(0f)] public float sightHeight = 2.5f;
        [Tooltip("It hears blows and landings within this distance, in metres, from any direction.")]
        [Min(0f)] public float hearingRange = 9f;
        [Tooltip("Seconds it has to see her before it is sure. A shorter glimpse only makes it suspicious.")]
        [Min(0f)] public float noticeTime = 0.45f;
        [Tooltip("Seconds it spends looking into something it saw or heard before going back to its post.")]
        [Min(0f)] public float investigateTime = 4f;
        [Tooltip("Seconds it stands at each end of its patrol.")]
        [Min(0f)] public float patrolWait = 2.5f;
        [Tooltip("Looking about, suspicious. Empty: it stands in its idle.")]
        public ClipWindow lookAround;
        [Tooltip("Light it leaves behind when it dies.")]
        [Min(0f)] public float lightValue = 30f;

        [Header("Stagger")]
        [Tooltip("Stagger it can take before it is thrown open for an execution. Her blows add their damage to it, a parry adds Parry Stagger.")]
        [Min(1f)] public float staggerMax = 60f;
        [Tooltip("Stagger that drains away per second while it is not being hit.")]
        [Min(0f)] public float staggerDecay = 14f;
        [Tooltip("Seconds it stays open once staggered.")]
        [Min(0f)] public float staggeredTime = 2.6f;
        [Tooltip("Stagger a parry adds.")]
        [Min(0f)] public float parryStagger = 30f;
        [Tooltip("Share of its health an execution takes from a boss. Everyone else dies to one.")]
        [Range(0f, 1f)] public float executeDamageShare = 0.25f;

        [Header("Boss")]
        [Tooltip("A boss: it does not come back with the others when she rests, executions only wound it, and it has the moves below.")]
        public bool isBoss;
        [Tooltip("The slam: a leap at her and a blow that shakes the ground, wide and low.")]
        public AttackStep slam = new AttackStep
        {
            startup = 0.75f, active = 0.14f, recovery = 0.9f, cancelDelay = 0f,
            damage = 30f, lunge = 0f, hitStop = 0.16f, shake = 0.35f, knockback = 11f,
            hitboxCenter = new Vector2(1.2f, 0.7f), hitboxSize = new Vector2(3.2f, 1.6f),
        };
        public ClipWindow slamClip;
        [Tooltip("How far along the rail the slam's leap carries it at most, in metres.")]
        [Min(0f)] public float slamLeap = 4.5f;
        [Tooltip("How high the leap arcs, in metres.")]
        [Min(0f)] public float slamArc = 1.8f;
        [Tooltip("The spinning charge: it runs at her and whirls its blade through her.")]
        public AttackStep spin = new AttackStep
        {
            startup = 0.45f, active = 0.3f, recovery = 0.8f, cancelDelay = 0f,
            damage = 26f, lunge = 7f, hitStop = 0.14f, shake = 0.2f, knockback = 9f,
            hitboxCenter = new Vector2(0.9f, 1.1f), hitboxSize = new Vector2(2.6f, 1.8f),
        };
        public ClipWindow spinClip;
        [Tooltip("Roaring into its second phase.")]
        public ClipWindow powerUp;
        [Min(0.1f)] public float powerUpTime = 1.6f;
        [Tooltip("Share of its health at which it enters its second phase.")]
        [Range(0f, 1f)] public float phase2Health = 0.5f;
        [Tooltip("How much faster it moves and swings in its second phase.")]
        [Min(1f)] public float phase2Speed = 1.25f;
        [Tooltip("Its cooldown between attacks is multiplied by this in its second phase.")]
        [Range(0.1f, 1f)] public float phase2Cooldown = 0.6f;
        [Tooltip("Chance, 0 to 1, that it charges when she is far off, instead of walking up.")]
        [Range(0f, 1f)] public float chargeChance = 0.6f;
        [Tooltip("Chance, 0 to 1, that it slams when she is at middle distance.")]
        [Range(0f, 1f)] public float slamChance = 0.5f;
        [Tooltip("She has to be further than this, in metres, for a charge.")]
        [Min(0f)] public float chargeDistance = 6f;

        [Header("Movement")]
        [Min(0f)] public float walkSpeed = 1.6f;
        [Tooltip("Speed when it runs, in m/s: it breaks into a run when she is further off than Run Distance.")]
        [Min(0f)] public float runSpeed = 3.6f;
        [Tooltip("It runs while she is further away than this along the rail, in metres, and walks once it is closer.")]
        [Min(0f)] public float runDistance = 7f;
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
        [Tooltip("How far above the top of the obstacle its jump arcs, in metres.")]
        [Min(0f)] public float climbHop = 0.15f;
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
        [Tooltip("Chance, 0 to 1, that it opens with its second swing instead of the first, so it has two attacks to read " +
                 "and not one swing with a follow-up.")]
        [Range(0f, 1f)] public float secondOpenerChance = 0.35f;
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
        [Tooltip("Empty: it walks everywhere.")]
        public AnimationClip run;
        [Tooltip("Ground speed in m/s at which the run clip plays at normal speed.")]
        [Min(0.1f)] public float runClipSpeed = 3.6f;
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

        [Header("Procedural motion")]
        [Tooltip("Degrees it leans forward per m/s of forward speed, running. 0 turns the lean off.")]
        [Min(0f)] public float runLean = 5f;
        [Tooltip("How fast the lean follows its target. Higher is stiffer; it overshoots a little either way.")]
        [Min(1f)] public float leanStiffness = 90f;
        [Tooltip("Degrees the pelvis rolls with each stride.")]
        [Min(0f)] public float stride = 3f;
        [Tooltip("Degrees its head is tipped up in the idle, on top of the clip, which has it looking down at the floor.")]
        public float idleHeadLift = 20f;
        [Tooltip("Degrees per second it swivels round when it turns to face her.")]
        [Min(1f)] public float turnSpeed = 720f;
        [Tooltip("How far its hips sink, in metres, as it crouches to jump and lands.")]
        [Min(0f)] public float crouchDepth = 0.1f;

        /// <summary>The swing for a step of its combo: the two swings, then the boss's slam and spinning charge.</summary>
        public AttackStep Step(int index) => index == 0 ? attack : index == 1 ? attack2 : index == 2 ? slam : spin;
    }
}
