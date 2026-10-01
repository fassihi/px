using System;
using System.Collections.Generic;
using UnityEngine;

namespace PX
{
    public enum PlayerState
    {
        Locomotion,
        Dash,
        Attack,
    }

    /// <summary>
    /// The heroine's moment-to-moment control: run, jump, dash and the light combo.
    /// Works in rail space (x = along the rail, y = up) and leaves the 3D part to <see cref="CharacterMotor"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class PlayerController : MonoBehaviour
    {
        // Small constant push into the ground so the motor keeps reporting grounded on flats and slopes.
        private const float GroundStickSpeed = 2f;

        [SerializeField] private MoveConfig move;
        [SerializeField] private ComboConfig combo;

        private readonly Collider[] overlapBuffer = new Collider[16];
        private readonly HashSet<Health> hitThisSwing = new HashSet<Health>();

        private CharacterMotor motor;
        private Health ownHealth;
        private Vector2 velocity;
        private float stateTime;
        private float coyoteTimer;
        private float jumpBuffer;
        private float dashBuffer;
        private float attackBuffer;
        private float dashCooldown;
        private int airJumpsLeft;
        private int airDashesLeft;
        private int dashDirection;

        /// <summary>Where input comes from. Defaults to an <see cref="IInputSource"/> on the same object.</summary>
        public IInputSource InputSource { get; set; }

        public PlayerState State { get; private set; }

        /// <summary>Index of the combo step being performed. Only meaningful in <see cref="PlayerState.Attack"/>.</summary>
        public int ComboIndex { get; private set; }

        /// <summary>True while the current swing's hitbox is live.</summary>
        public bool IsHitboxActive { get; private set; }

        public MoveConfig Move => move;

        public ComboConfig Combo => combo;

        /// <summary>Raised when a swing connects, once per target.</summary>
        public event Action<Health> HitLanded;

        private void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            ownHealth = GetComponent<Health>();
            InputSource ??= GetComponent<IInputSource>();
        }

        private void Update()
        {
            InputFrame input = InputSource != null ? InputSource.Read() : default;

            // Presses are remembered before the early-out below, so nothing is lost during hit-stop.
            if (input.JumpPressed) jumpBuffer = move.jumpBufferTime;
            if (input.DashPressed) dashBuffer = move.dashBufferTime;
            if (input.AttackPressed) attackBuffer = combo.bufferTime;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            if (motor.IsGrounded)
            {
                coyoteTimer = move.coyoteTime;
                airJumpsLeft = move.airJumps;
                airDashesLeft = move.airDashes;
            }

            switch (State)
            {
                case PlayerState.Locomotion:
                    TickLocomotion(input, dt);
                    break;
                case PlayerState.Dash:
                    TickDash(input, dt);
                    break;
                case PlayerState.Attack:
                    TickAttack(input, dt);
                    break;
            }

            motor.Move(velocity, dt);

            if (motor.HitCeiling && velocity.y > 0f)
                velocity.y = 0f;

            stateTime += dt;
            coyoteTimer -= dt;
            jumpBuffer -= dt;
            dashBuffer -= dt;
            attackBuffer -= dt;
            dashCooldown -= dt;
        }

        private void TickLocomotion(InputFrame input, float dt)
        {
            if (input.Move != 0f)
                motor.Facing = input.Move > 0f ? 1 : -1;

            float target = input.Move * move.runSpeed;
            bool accelerating = Mathf.Abs(target) > 0.01f;
            float rate = motor.IsGrounded
                ? (accelerating ? move.groundAcceleration : move.groundDeceleration)
                : (accelerating ? move.airAcceleration : move.airDeceleration);
            velocity.x = Mathf.MoveTowards(velocity.x, target, rate * dt);

            ApplyGravity(input.JumpHeld, dt);

            if (TryStartJump() || TryStartDash(input))
                return;

            if (attackBuffer > 0f && combo.steps.Length > 0)
                StartAttack(0, input);
        }

        private void TickDash(InputFrame input, float dt)
        {
            // A ground dash stays glued to the floor; an air dash travels flat.
            velocity = new Vector2(dashDirection * move.dashSpeed, motor.IsGrounded ? -GroundStickSpeed : 0f);

            if (stateTime >= move.dashDuration)
            {
                // Leave the dash at run speed, not dash speed, so it doesn't feel like sliding on ice.
                velocity.x = dashDirection * move.runSpeed;
                dashCooldown = move.dashCooldown;
                EnterState(PlayerState.Locomotion);
            }
        }

        private void TickAttack(InputFrame input, float dt)
        {
            AttackStep step = combo.steps[ComboIndex];

            if (motor.IsGrounded)
            {
                velocity.x = stateTime < step.ActiveEnd
                    ? motor.Facing * step.lunge
                    : Mathf.MoveTowards(velocity.x, 0f, move.groundDeceleration * dt);
            }
            else
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, move.airDeceleration * dt);
            }

            ApplyGravity(input.JumpHeld, dt);

            IsHitboxActive = stateTime >= step.startup && stateTime < step.ActiveEnd;
            if (IsHitboxActive)
                ApplyHits(step);

            if (stateTime >= step.CancelFrom)
            {
                if (TryStartJump() || TryStartDash(input))
                    return;

                if (attackBuffer > 0f && ComboIndex + 1 < combo.steps.Length)
                {
                    StartAttack(ComboIndex + 1, input);
                    return;
                }
            }

            if (stateTime >= step.Total)
                EnterState(PlayerState.Locomotion);
        }

        private void ApplyGravity(bool jumpHeld, float dt)
        {
            if (motor.IsGrounded && velocity.y <= 0f)
            {
                velocity.y = -GroundStickSpeed;
                return;
            }

            float gravity = move.Gravity;
            if (velocity.y < 0f)
                gravity *= move.fallGravityMultiplier;
            else if (!jumpHeld)
                gravity *= move.releasedGravityMultiplier;

            velocity.y = Mathf.Max(velocity.y - gravity * dt, -move.maxFallSpeed);
        }

        private bool TryStartJump()
        {
            if (jumpBuffer <= 0f)
                return false;

            if (coyoteTimer <= 0f)
            {
                if (airJumpsLeft <= 0)
                    return false;
                airJumpsLeft--;
            }

            velocity.y = move.JumpVelocity;
            jumpBuffer = 0f;
            coyoteTimer = 0f;
            EnterState(PlayerState.Locomotion);
            return true;
        }

        private bool TryStartDash(InputFrame input)
        {
            if (dashBuffer <= 0f || dashCooldown > 0f)
                return false;

            if (!motor.IsGrounded)
            {
                if (airDashesLeft <= 0)
                    return false;
                airDashesLeft--;
            }

            if (input.Move != 0f)
                motor.Facing = input.Move > 0f ? 1 : -1;

            dashDirection = motor.Facing;
            dashBuffer = 0f;
            EnterState(PlayerState.Dash);
            return true;
        }

        private void StartAttack(int index, InputFrame input)
        {
            if (input.Move != 0f)
                motor.Facing = input.Move > 0f ? 1 : -1;

            ComboIndex = index;
            attackBuffer = 0f;
            hitThisSwing.Clear();
            EnterState(PlayerState.Attack);
        }

        private void EnterState(PlayerState next)
        {
            State = next;
            stateTime = 0f;
            IsHitboxActive = false;
        }

        private void ApplyHits(AttackStep step)
        {
            GetHitbox(step, out Vector3 center, out Vector3 halfExtents);
            int count = Physics.OverlapBoxNonAlloc(
                center, halfExtents, overlapBuffer, transform.rotation, ~0, QueryTriggerInteraction.Collide);

            bool landed = false;
            for (int i = 0; i < count; i++)
            {
                Health target = overlapBuffer[i].GetComponentInParent<Health>();
                if (target == null || target == ownHealth || target.IsDead || !hitThisSwing.Add(target))
                    continue;

                target.TakeHit(new Hit(step.damage, transform.forward, gameObject));
                HitLanded?.Invoke(target);
                landed = true;
            }

            if (landed)
                HitStop.Trigger(step.hitStop);
        }

        /// <summary>The current swing's hitbox in world space. The box is oriented by this transform's rotation.</summary>
        public void GetHitbox(AttackStep step, out Vector3 center, out Vector3 halfExtents)
        {
            center = transform.TransformPoint(new Vector3(0f, step.hitboxCenter.y, step.hitboxCenter.x));
            halfExtents = new Vector3(combo.hitboxDepth, step.hitboxSize.y * 0.5f, step.hitboxSize.x * 0.5f);
        }
    }
}
