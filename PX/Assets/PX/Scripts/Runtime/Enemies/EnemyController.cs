using System;
using UnityEngine;

namespace PX
{
    public enum EnemyState
    {
        Idle,
        Walk,
        /// <summary>Backing away while still facing her.</summary>
        WalkBack,
        Attack,
        Hurt,
        /// <summary>Thrown off balance by a parry: wide open.</summary>
        Parried,
        /// <summary>Climbing onto an obstacle in its way.</summary>
        Climb,
        Dead,
        /// <summary>Running toward her from a distance.</summary>
        Run,
    }

    /// <summary>
    /// A melee enemy's body: walks along its rail, backs off, climbs what blocks it, swings a one- or two-hit combo,
    /// staggers when hit and dies. Decisions come from an <see cref="IInputSource"/> (normally <see cref="EnemyBrain"/>);
    /// this only carries them out, with the same commitment rules as the player: a swing, once started, plays out
    /// unless a heavy enough blow staggers it out, or it is parried.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor), typeof(Health))]
    public sealed class EnemyController : MonoBehaviour
    {
        /// <summary>The jump over a climb: it crouches until this share of the climb, leaps, and lands from the second.</summary>
        public const float ClimbCrouchEnd = 0.25f;
        public const float ClimbLandStart = 0.78f;

        // Small constant push into the ground so the motor keeps reporting grounded.
        private const float GroundStickSpeed = 2f;

        // Input below this is "face that way", not "walk".
        private const float WalkThreshold = 0.1f;

        [SerializeField] private EnemyConfig config;

        private readonly Collider[] overlapBuffer = new Collider[16];
        private CharacterMotor motor;
        private Health health;
        private Vector2 velocity;
        private float knockback;
        private float cooldown;
        private bool swingConnected;
        private float poiseTaken;
        private float blockedTime;
        private float climbTop;
        private float climbWall;
        private Vector2 climbFrom;
        private bool posted;
        private float postDistance;
        private int postFacing;
        private Hit lastHit;
        private bool hasHit;
        private EnemyRagdoll ragdoll;

        public IInputSource InputSource { get; set; }

        public EnemyConfig Config => config;

        public CharacterMotor Motor => motor;

        public Health Health => health;

        public EnemyState State { get; private set; }

        public float StateTime { get; private set; }

        /// <summary>Which swing of its combo is in progress: 0 or 1. Only meaningful in <see cref="EnemyState.Attack"/>.</summary>
        public int ComboIndex { get; private set; }

        /// <summary>The swing in progress.</summary>
        public AttackStep CurrentStep => config.Step(ComboIndex);

        public bool IsHitboxActive { get; private set; }

        /// <summary>True while its body is a ragdoll: the animation is off.</summary>
        public bool IsRagdoll => ragdoll != null && ragdoll.IsActive;

        public EnemyRagdoll Ragdoll => ragdoll;

        /// <summary>True from the point in its first swing where the second could follow.</summary>
        public bool CanChain => State == EnemyState.Attack && ComboIndex == 0 && StateTime >= config.attack.CancelFrom;

        /// <summary>Raised when its swing connects with something that has health.</summary>
        public event Action<Health> HitLanded;

        private void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            health = GetComponent<Health>();
            InputSource ??= GetComponent<IInputSource>();
            ragdoll = GetComponent<EnemyRagdoll>();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || config == null)
                return;

            if (!posted && motor.Rail != null)
            {
                posted = true;
                postDistance = motor.Distance;
                postFacing = motor.Facing;
            }

            InputFrame input = State == EnemyState.Dead || InputSource == null ? default : InputSource.Read();

            switch (State)
            {
                case EnemyState.Idle:
                case EnemyState.Walk:
                case EnemyState.WalkBack:
                case EnemyState.Run:
                    TickLocomotion(input, dt);
                    break;
                case EnemyState.Attack:
                    TickAttack(input, dt);
                    break;
                case EnemyState.Hurt:
                    if (StateTime >= config.hitStun)
                        Enter(EnemyState.Idle);
                    velocity.x = knockback;
                    break;
                case EnemyState.Parried:
                    if (StateTime >= config.parriedStun)
                        Enter(EnemyState.Idle);
                    velocity.x = knockback;
                    break;
                case EnemyState.Climb:
                    TickClimb();
                    StateTime += dt;
                    return;
                case EnemyState.Dead:
                    // It lies where it fell; its collider is off, so it no longer moves.
                    StateTime += dt;
                    return;
            }

            knockback = Mathf.MoveTowards(knockback, 0f, config.knockbackFriction * dt);
            velocity.y = motor.IsGrounded && velocity.y <= 0f ? -GroundStickSpeed : velocity.y - config.gravity * dt;
            motor.Move(velocity, dt);

            StateTime += dt;
            cooldown -= dt;
        }

        private void TickLocomotion(InputFrame input, float dt)
        {
            if (input.Face != 0f)
                motor.Facing = input.Face > 0f ? 1 : -1;
            else if (input.Move != 0f)
                motor.Facing = input.Move > 0f ? 1 : -1;

            bool walking = Mathf.Abs(input.Move) >= WalkThreshold;
            bool backing = walking && Mathf.Sign(input.Move) != motor.Facing;
            bool running = walking && !backing && input.Run && config.runSpeed > config.walkSpeed;
            float target = walking
                ? Mathf.Sign(input.Move) * (backing ? config.walkBackSpeed : running ? config.runSpeed : config.walkSpeed)
                : 0f;
            velocity.x = Mathf.MoveTowards(velocity.x, target, config.acceleration * dt);

            if (input.AttackPressed && cooldown <= 0f && motor.IsGrounded)
            {
                StartSwing(UnityEngine.Random.value < config.secondOpenerChance ? 1 : 0);
                return;
            }

            // Walking into something it cannot pass: climb it if it is low enough.
            bool blocked = walking && !backing && motor.IsGrounded && Mathf.Abs(motor.Velocity.x) < 0.1f;
            blockedTime = blocked ? blockedTime + dt : 0f;
            if (blockedTime >= config.climbAfterBlocked && TryStartClimb())
                return;

            // Running from the moment it is past walking pace, and back to a walk as it slows through it.
            float runPace = config.walkSpeed + 0.5f * (config.runSpeed - config.walkSpeed);
            EnemyState next = Mathf.Abs(velocity.x) <= 0.05f ? EnemyState.Idle
                : Mathf.Sign(velocity.x) != motor.Facing ? EnemyState.WalkBack
                : running && Mathf.Abs(velocity.x) > runPace ? EnemyState.Run : EnemyState.Walk;
            if (next != State)
                Enter(next);
        }

        private bool TryStartClimb()
        {
            if (!motor.FindLedge(0.3f, config.climbHeight, config.climbProbe, out float top, out float wall))
                return false;
            climbTop = top;
            climbWall = wall;
            climbFrom = new Vector2(motor.Distance, transform.position.y);
            blockedTime = 0f;
            velocity = Vector2.zero;
            Enter(EnemyState.Climb);
            return true;
        }

        /// <summary>
        /// Jumps onto the obstacle: a crouch, then a leap that arcs above the top and comes down on it, then the landing.
        /// </summary>
        private void TickClimb()
        {
            float t = Mathf.Clamp01(StateTime / config.climbDuration);
            float u = Mathf.Clamp01((t - ClimbCrouchEnd) / (ClimbLandStart - ClimbCrouchEnd));
            float onTop = climbWall + motor.Facing * (motor.Radius + 0.15f);
            // Fast off the ground and slowing to the top; the arc carries it above the edge on the way over.
            float rise = 1f - (1f - u) * (1f - u);
            float arc = 4f * u * (1f - u) * config.climbHop;
            motor.Place(Mathf.Lerp(climbFrom.x, onTop, Mathf.SmoothStep(0f, 1f, u)),
                Mathf.Lerp(climbFrom.y, climbTop + 0.02f, rise) + arc);
            if (t >= 1f)
                Enter(EnemyState.Idle);
        }

        private void StartSwing(int index)
        {
            ComboIndex = index;
            swingConnected = false;
            poiseTaken = 0f;
            Enter(EnemyState.Attack);
        }

        private void TickAttack(InputFrame input, float dt)
        {
            AttackStep step = CurrentStep;
            velocity.x = StateTime < step.ActiveEnd
                ? motor.Facing * step.lunge
                : Mathf.MoveTowards(velocity.x, 0f, config.acceleration * dt);

            IsHitboxActive = StateTime >= step.startup && StateTime < step.ActiveEnd;
            if (IsHitboxActive && !swingConnected)
                ApplyHits(step);

            if (CanChain && input.AttackPressed)
            {
                if (input.Face != 0f)
                    motor.Facing = input.Face > 0f ? 1 : -1;
                StartSwing(1);
                return;
            }

            if (StateTime >= step.Total)
            {
                cooldown = config.attackCooldown;
                Enter(EnemyState.Idle);
            }
        }

        private void ApplyHits(AttackStep step)
        {
            GetHitbox(out Vector3 center, out Vector3 halfExtents);
            int count = Physics.OverlapBoxNonAlloc(
                center, halfExtents, overlapBuffer, transform.rotation, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Health target = overlapBuffer[i].GetComponentInParent<Health>();
                if (target == null || target == health || target.IsDead || target.Invulnerable)
                    continue;
                // Enemies do not hurt each other.
                if (target.GetComponent<EnemyController>() != null)
                    continue;

                // Where the blow meets the body: the point on it nearest the attacker, at the height of the swing.
                Vector3 point = overlapBuffer[i].ClosestPoint(new Vector3(transform.position.x, center.y, transform.position.z));
                HitResult result = target.TakeHit(new Hit(step.damage, transform.forward, gameObject, step.knockback, point));
                if (result == HitResult.Ignored)
                    continue;

                swingConnected = true;
                if (result == HitResult.Parried)
                {
                    // Its blade is turned aside and it reels back, wide open.
                    knockback = -motor.Facing * 2.5f;
                    Enter(EnemyState.Parried);
                    return;
                }

                HitLanded?.Invoke(target);
                HitStop.Trigger(result == HitResult.Blocked ? step.hitStop * 0.6f : step.hitStop);
                CameraShake.Trigger(result == HitResult.Blocked ? step.shake * 0.6f : step.shake, 0.25f);
                return;
            }
        }

        /// <summary>Its swing's hitbox in world space, oriented by this transform's rotation.</summary>
        public void GetHitbox(out Vector3 center, out Vector3 halfExtents)
        {
            AttackStep step = CurrentStep;
            center = transform.TransformPoint(new Vector3(0f, step.hitboxCenter.y, step.hitboxCenter.x));
            halfExtents = new Vector3(config.hitboxDepth, step.hitboxSize.y * 0.5f, step.hitboxSize.x * 0.5f);
        }

        private void OnDamaged(Hit hit)
        {
            lastHit = hit;
            hasHit = true;
            if (health.IsDead)
                return;

            // Mid-swing it shrugs off light blows: only enough damage staggers it out of the attack.
            if (State == EnemyState.Attack && StateTime < CurrentStep.ActiveEnd)
            {
                poiseTaken += hit.Damage;
                if (poiseTaken < config.attackPoise)
                    return;
            }
            // Climbing, it is committed too.
            if (State == EnemyState.Climb)
                return;

            // Pushed along the rail the way the blow travels, and turned to face whoever struck it.
            Vector3 along = motor.Rail != null ? motor.Rail.Evaluate(motor.Distance).Tangent : Vector3.right;
            float direction = Mathf.Sign(Vector3.Dot(hit.Direction, along));
            knockback = direction * hit.Knockback;
            motor.Facing = -(int)direction;
            // A parried enemy stays open for the counter: the blow lands but does not restart its stagger.
            if (State != EnemyState.Parried)
                Enter(EnemyState.Hurt);
        }

        private void OnDied()
        {
            Enter(EnemyState.Dead);
            // The body no longer blocks her way, and goes limp: thrown the way the killing blow travelled.
            GetComponent<CharacterController>().enabled = false;
            if (ragdoll == null || ragdoll.BodyCount == 0)
                return;

            Vector3 along = motor.Rail != null ? motor.Rail.Evaluate(motor.Distance).Tangent : transform.forward;
            Vector3 push = hasHit ? lastHit.Direction : -transform.forward;
            float speed = hasHit ? lastHit.Knockback : 4f;
            Vector3 velocity = Vector3.Project(push, along).normalized * (speed * 0.35f + 1.5f) + Vector3.up * 2f;
            if (InputSource is EnemyBrain brain && brain.Target != null)
                ragdoll.IgnoreCollisionsWith(brain.Target.GetComponent<CharacterController>());
            ragdoll.Activate(velocity);
        }

        /// <summary>
        /// Back on its feet where it fell, at full health, ready to fight. Works on a dead enemy and on a living one
        /// (which is only healed and sent back to idle). For testing the fights over and over.
        /// </summary>
        public void Revive()
        {
            bool wasDead = health.IsDead;
            if (wasDead && ragdoll != null && ragdoll.IsActive)
            {
                // Stand up where the body came to rest, which may be some way from where it was struck.
                float distance = motor.Rail != null ? motor.Rail.Project(ragdoll.HipsPosition) : motor.Distance;
                ragdoll.Deactivate();
                GetComponent<CharacterController>().enabled = true;
                motor.Place(distance, ragdoll.LowestPoint + 0.05f);
            }
            else if (wasDead)
            {
                GetComponent<CharacterController>().enabled = true;
                motor.Place(motor.Distance, transform.position.y + 0.05f);
            }
            health.Restore();
            velocity = Vector2.zero;
            knockback = 0f;
            cooldown = 0f;
            blockedTime = 0f;
            hasHit = false;
            Enter(EnemyState.Idle);
        }

        /// <summary>
        /// Back to where it started, at full health, as if she had never come. Called when she gets up after dying.
        /// A dead enemy stays dead.
        /// </summary>
        public void ReturnToPost()
        {
            if (health.IsDead || !posted)
                return;
            health.Restore();
            motor.Place(postDistance, 0.05f);
            motor.Facing = postFacing;
            velocity = Vector2.zero;
            knockback = 0f;
            cooldown = 0f;
            Enter(EnemyState.Idle);
        }

        private void Enter(EnemyState next)
        {
            State = next;
            StateTime = 0f;
            IsHitboxActive = false;
        }
    }
}
