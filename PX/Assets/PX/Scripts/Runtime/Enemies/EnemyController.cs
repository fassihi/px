using System;
using UnityEngine;

namespace PX
{
    public enum EnemyState
    {
        Idle,
        Walk,
        Attack,
        Hurt,
        Dead,
    }

    /// <summary>
    /// A melee enemy's body: walks along its rail, swings, staggers when hit and dies. Decisions come from an
    /// <see cref="IInputSource"/> (normally <see cref="EnemyBrain"/>); this only carries them out, with the same
    /// commitment rules as the player: a swing, once started, plays out unless a hit staggers it.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor), typeof(Health))]
    public sealed class EnemyController : MonoBehaviour
    {
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

        public IInputSource InputSource { get; set; }

        public EnemyConfig Config => config;

        public CharacterMotor Motor => motor;

        public Health Health => health;

        public EnemyState State { get; private set; }

        public float StateTime { get; private set; }

        public bool IsHitboxActive { get; private set; }

        /// <summary>Raised when its swing connects with something that has health.</summary>
        public event Action<Health> HitLanded;

        private void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            health = GetComponent<Health>();
            InputSource ??= GetComponent<IInputSource>();
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

            InputFrame input = State == EnemyState.Dead || InputSource == null ? default : InputSource.Read();

            switch (State)
            {
                case EnemyState.Idle:
                case EnemyState.Walk:
                    TickLocomotion(input, dt);
                    break;
                case EnemyState.Attack:
                    TickAttack(dt);
                    break;
                case EnemyState.Hurt:
                    if (StateTime >= config.hitStun)
                        Enter(EnemyState.Idle);
                    velocity.x = knockback;
                    break;
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
            if (input.Move != 0f)
                motor.Facing = input.Move > 0f ? 1 : -1;

            float target = Mathf.Abs(input.Move) >= WalkThreshold ? Mathf.Sign(input.Move) * config.walkSpeed : 0f;
            velocity.x = Mathf.MoveTowards(velocity.x, target, config.acceleration * dt);

            if (input.AttackPressed && cooldown <= 0f && motor.IsGrounded)
            {
                swingConnected = false;
                Enter(EnemyState.Attack);
                return;
            }

            EnemyState next = Mathf.Abs(velocity.x) > 0.05f ? EnemyState.Walk : EnemyState.Idle;
            if (next != State)
                Enter(next);
        }

        private void TickAttack(float dt)
        {
            AttackStep step = config.attack;
            velocity.x = StateTime < step.ActiveEnd
                ? motor.Facing * step.lunge
                : Mathf.MoveTowards(velocity.x, 0f, config.acceleration * dt);

            IsHitboxActive = StateTime >= step.startup && StateTime < step.ActiveEnd;
            if (IsHitboxActive && !swingConnected)
                ApplyHits(step);

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

                target.TakeHit(new Hit(step.damage, transform.forward, gameObject, step.knockback));
                swingConnected = true;
                HitLanded?.Invoke(target);
                HitStop.Trigger(step.hitStop);
                CameraShake.Trigger(step.shake, 0.22f);
                return;
            }
        }

        /// <summary>Its swing's hitbox in world space, oriented by this transform's rotation.</summary>
        public void GetHitbox(out Vector3 center, out Vector3 halfExtents)
        {
            AttackStep step = config.attack;
            center = transform.TransformPoint(new Vector3(0f, step.hitboxCenter.y, step.hitboxCenter.x));
            halfExtents = new Vector3(config.hitboxDepth, step.hitboxSize.y * 0.5f, step.hitboxSize.x * 0.5f);
        }

        private void OnDamaged(Hit hit)
        {
            // Pushed along the rail the way the blow travels, and turned to face whoever struck it.
            Vector3 along = motor.Rail != null ? motor.Rail.Evaluate(motor.Distance).Tangent : Vector3.right;
            float direction = Mathf.Sign(Vector3.Dot(hit.Direction, along));
            knockback = direction * hit.Knockback;
            motor.Facing = -(int)direction;
            if (!health.IsDead)
                Enter(EnemyState.Hurt);
        }

        private void OnDied()
        {
            Enter(EnemyState.Dead);
            // The body stays where it fell, but no longer blocks her way.
            GetComponent<CharacterController>().enabled = false;
        }

        private void Enter(EnemyState next)
        {
            State = next;
            StateTime = 0f;
            IsHitboxActive = false;
        }
    }
}
