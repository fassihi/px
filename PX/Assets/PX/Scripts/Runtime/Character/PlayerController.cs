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
        Crouch,
        Hurt,
        /// <summary>A hop back with a moment of invulnerability.</summary>
        Dodge,
        /// <summary>Guard up: blocks, and parries just after it goes up.</summary>
        Guard,
        /// <summary>Hanging from a ledge by her hands.</summary>
        Hang,
        /// <summary>Pulling herself up from a ledge onto its top.</summary>
        Climb,
        Dead,
        /// <summary>Drinking a flask: standing still, open to a blow.</summary>
        Drink,
        /// <summary>Pulled through the air toward a lamp by the tether.</summary>
        Tether,
        /// <summary>A takedown from behind, or the execution of a staggered enemy: a killing blow that plays out.</summary>
        Finisher,
    }

    /// <summary>
    /// The heroine's moment-to-moment control: run, jump, dash, dodge, crouch, guard and parry, the combo, the
    /// crouching sweep, the counter, the lunge out of a dash, takedowns and executions, drinking a flask, the tether
    /// to the lamps, and hanging from and climbing onto ledges.
    /// Works in rail space (x = along the rail, y = up) and leaves the 3D part to <see cref="CharacterMotor"/>.
    /// Moves commit: each one says from when it can be cut short (its cancel window), and a press that comes too early
    /// is buffered and carried out when the window opens, instead of being dropped or acted on at once.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class PlayerController : MonoBehaviour
    {
        // Small constant push into the ground so the motor keeps reporting grounded on flats and slopes.
        private const float GroundStickSpeed = 2f;

        // A Guard press is remembered this long while another move finishes.
        private const float GuardBufferTime = 0.12f;

        [SerializeField] private MoveConfig move;
        [SerializeField] private ComboConfig combo;
        [SerializeField] private PlayerCombatConfig combat;

        private readonly Collider[] overlapBuffer = new Collider[16];
        private readonly HashSet<Health> hitThisSwing = new HashSet<Health>();

        private CharacterMotor motor;
        private Health ownHealth;
        private Stamina stamina;
        private Vector2 velocity;
        private float stateTime;
        private float coyoteTimer;
        private float jumpBuffer;
        private float dashBuffer;
        private float attackBuffer;
        private float guardBuffer;
        private float dashCooldown;
        private int airJumpsLeft;
        private int airDashesLeft;
        private int dashDirection;
        private float turnTime = float.MaxValue;
        private float landingRecovery;
        // The gather before a jump leaves the ground: its length, and how far into it she is. A zero length: none.
        private float windupDuration;
        private float windupTime;
        private bool swingConnected;
        private float invulnerable;
        private float lastGuardPress = float.MinValue;
        private bool parryArmed;
        private float counterTimer;
        private float regrabTimer;
        private float ledgeTop;
        private float ledgeWall;
        private bool guardBroken;
        private bool finisherLanded;
        private bool drankThisDrink;
        private Vector2 tetherPoint;
        private int tetherFacing;
        private TetherAnchor lastAnchor;

        /// <summary>Where input comes from. Defaults to an <see cref="IInputSource"/> on the same object.</summary>
        public IInputSource InputSource { get; set; }

        public PlayerState State { get; private set; }

        /// <summary>Index of the combo step being performed. Only meaningful in <see cref="PlayerState.Attack"/>.</summary>
        public int ComboIndex { get; private set; }

        /// <summary>True while the attack in progress is the sweep from a crouch, not a combo step.</summary>
        public bool IsCrouchAttack { get; private set; }

        /// <summary>The attack is the flying kick thrown in the air.</summary>
        public bool IsAirAttack { get; private set; }

        /// <summary>True while the attack in progress is the counter after a parry.</summary>
        public bool IsCounter { get; private set; }

        /// <summary>True while the attack in progress is the lunge out of a dash.</summary>
        public bool IsDashAttack { get; private set; }

        /// <summary>In a finisher: true for the execution of a staggered enemy, false for a takedown from behind.</summary>
        public bool IsExecution { get; private set; }

        /// <summary>The enemy a finisher is being done to.</summary>
        public EnemyController FinisherTarget { get; private set; }

        /// <summary>The attack being performed. Only meaningful in <see cref="PlayerState.Attack"/> and <see cref="PlayerState.Finisher"/>.</summary>
        public AttackStep CurrentStep =>
            State == PlayerState.Finisher ? combo.finisher
            : IsCounter ? combo.counterAttack : IsCrouchAttack ? combo.crouchAttack : IsAirAttack ? combo.airAttack
            : IsDashAttack ? combo.dashAttack : combo.steps[ComboIndex];

        /// <summary>Whether she has found the tether, the ribbon she throws at the lamps.</summary>
        public bool HasTether { get; set; }

        /// <summary>The lamp she is being pulled to, while she is.</summary>
        public TetherAnchor Tether { get; private set; }

        /// <summary>Healing flasks she carries now, and the most she can carry. Refilled at a shrine.</summary>
        public int Flasks { get; private set; }

        public int MaxFlasks { get; set; }

        /// <summary>
        /// Asked first when Interact is pressed, so the level can take the press for a shrine, a lever or a pickup.
        /// Returns true when it did; otherwise the press goes to the tether.
        /// </summary>
        public Func<bool> Interaction { get; set; }

        /// <summary>Seconds since the current state began.</summary>
        public float StateTime => stateTime;

        /// <summary>True while the current swing's hitbox is live.</summary>
        public bool IsHitboxActive { get; private set; }

        /// <summary>True during a skid turn: she reversed at speed and is committed to it for a moment.</summary>
        public bool IsTurning => turnTime < move.turnDuration;

        /// <summary>Seconds since the current skid turn began.</summary>
        public float TurnTime => turnTime;

        /// <summary>True while she is recovering from a hard landing and cannot jump or dash yet.</summary>
        public bool IsRecoveringFromLanding => landingRecovery > 0f;

        /// <summary>True while she gathers herself on the ground before a jump leaves it.</summary>
        public bool IsJumpWindup => windupDuration > 0f;

        /// <summary>How far through the jump's wind-up she is, 0 to 1.</summary>
        public float JumpWindupProgress => windupDuration > 0f ? Mathf.Clamp01(windupTime / windupDuration) : 0f;

        /// <summary>True during the stagger after a hit.</summary>
        public bool IsHurt => State == PlayerState.Hurt;

        /// <summary>How long the current stagger lasts. Heavier blows stagger her for longer.</summary>
        public float HurtDuration { get; private set; }

        /// <summary>True while blows pass through her in a dodge.</summary>
        public bool IsDodgeInvulnerable =>
            State == PlayerState.Dodge && stateTime >= move.dodgeInvulnerable.x && stateTime < move.dodgeInvulnerable.y;

        /// <summary>Seconds since her guard last took a blow, or since her last parry. Large when neither happened.</summary>
        public float GuardReactionTime { get; private set; } = float.MaxValue;

        /// <summary>True when the last blow on her guard was parried rather than blocked.</summary>
        public bool LastGuardWasParry { get; private set; }

        /// <summary>True while a counter is on offer after a parry.</summary>
        public bool CanCounter => counterTimer > 0f;

        /// <summary>True in the last moments of the climb, when the pull-up pose fades into standing.</summary>
        public bool ClimbSettling => State == PlayerState.Climb && stateTime >= move.climbDuration;

        /// <summary>Height of the ledge she hangs from or climbs onto.</summary>
        public float LedgeTop => ledgeTop;

        /// <summary>Rail distance of the face of the ledge she hangs from or climbs.</summary>
        public float LedgeWall => ledgeWall;

        public MoveConfig Move => move;

        public ComboConfig Combo => combo;

        public PlayerCombatConfig Combat => combat;

        public Stamina Stamina => stamina;

        /// <summary>Raised when a swing connects, once per target.</summary>
        public event Action<Health> HitLanded;

        /// <summary>Raised when she jumps again in the air (the double jump).</summary>
        public event Action AirJumped;

        /// <summary>Raised when a dash starts; true when it is an air dash.</summary>
        public event Action<bool> Dashed;

        /// <summary>Raised when she parries a blow. The argument is whatever struck at her.</summary>
        public event Action<GameObject> Parried;

        /// <summary>Raised when her health runs out.</summary>
        public event Action Died;

        /// <summary>Raised as a finisher starts, with its target; true for an execution, false for a takedown.</summary>
        public event Action<EnemyController, bool> FinisherStarted;

        /// <summary>Raised as a finisher's killing blow lands.</summary>
        public event Action<EnemyController, bool> FinisherLanded;

        /// <summary>Raised as the tether catches a lamp.</summary>
        public event Action<TetherAnchor> Tethered;

        /// <summary>Raised when a flask heals her.</summary>
        public event Action Drank;

        private void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            ownHealth = GetComponent<Health>();
            stamina = GetComponent<Stamina>();
            InputSource ??= GetComponent<IInputSource>();
            MaxFlasks = combat != null ? combat.flasks : 3;
            Flasks = MaxFlasks;
        }

        private void OnEnable()
        {
            if (ownHealth != null)
            {
                ownHealth.Damaged += OnHurt;
                ownHealth.Guard = OnIncomingBlow;
            }
        }

        private void OnDisable()
        {
            if (ownHealth != null)
            {
                ownHealth.Damaged -= OnHurt;
                ownHealth.Guard = null;
            }
        }

        // ------------------------------------------------------------ taking blows

        /// <summary>
        /// Decides what a blow does before it lands. With her guard up and facing it, she parries it if Guard went down
        /// just now, and blocks it otherwise: a block costs stamina, lets a little damage through and pushes her back,
        /// and when the stamina runs out it breaks her guard.
        /// </summary>
        private HitResult OnIncomingBlow(ref Hit hit)
        {
            if (State != PlayerState.Guard || combat == null)
                return HitResult.Damaged;
            // A guard only covers her front: the blow has to travel against the way she faces.
            if (Vector3.Dot(hit.Direction, transform.forward) > 0f)
                return HitResult.Damaged;

            if (parryArmed && stateTime <= combat.parryWindow)
            {
                parryArmed = false;
                LastGuardWasParry = true;
                GuardReactionTime = 0f;
                counterTimer = combat.counterWindow;
                HitStop.Trigger(combat.parryHitStop);
                CameraShake.Trigger(combat.parryShake, 0.25f);
                Parried?.Invoke(hit.Source);
                return HitResult.Parried;
            }

            float cost = hit.Damage * combat.blockStaminaPerDamage;
            bool broken = stamina != null && stamina.Current < cost;
            stamina?.Spend(cost);
            if (broken)
            {
                // Out of stamina, the guard gives: the blow lands in full and staggers her for longer.
                guardBroken = true;
                return HitResult.Damaged;
            }

            hit = hit.Scaled(combat.blockDamageScale, combat.blockKnockbackScale);
            LastGuardWasParry = false;
            GuardReactionTime = 0f;
            velocity.x = KnockbackAlongRail(hit);
            return HitResult.Blocked;
        }

        private float KnockbackAlongRail(Hit hit)
        {
            Vector3 along = motor.Rail != null ? motor.Rail.Evaluate(motor.Distance).Tangent : Vector3.right;
            return Mathf.Sign(Vector3.Dot(hit.Direction, along)) * hit.Knockback;
        }

        /// <summary>
        /// A hit staggers her: knocked back along the rail the way the blow travels, a short hop, no control until the
        /// stun ends, and a moment of invulnerability after. Heavier blows stagger for longer. Whatever she was doing is
        /// interrupted; at zero health she falls.
        /// </summary>
        private void OnHurt(Hit hit)
        {
            float push = KnockbackAlongRail(hit);
            velocity = new Vector2(push, move.hurtHop);
            if (push != 0f)
                motor.Facing = -(int)Mathf.Sign(push);
            if (motor.IsCrouched)
                motor.TryStand();
            if (State == PlayerState.Hang || State == PlayerState.Climb)
            {
                regrabTimer = move.regrabDelay;
                StepOutOfWall();
            }
            Tether = null;
            FinisherTarget = null;
            turnTime = float.MaxValue;
            counterTimer = 0f;
            invulnerable = move.hurtInvulnerability;
            ownHealth.Invulnerable = true;
            HitStop.Trigger(move.hurtHitStop);
            CameraShake.Trigger(move.hurtShake, 0.3f);

            if (ownHealth.IsDead)
            {
                EnterState(PlayerState.Dead);
                Died?.Invoke();
                return;
            }

            HurtDuration = (guardBroken ? combat.guardBreakStun : move.hurtStun) + hit.Knockback * move.hurtStunPerKnockback;
            guardBroken = false;
            EnterState(PlayerState.Hurt);
        }

        /// <summary>Gets her back up after a fall: full health and stamina, standing at a rail distance and height.</summary>
        public void Respawn(float distance, float height = 0.1f)
        {
            ownHealth.Restore();
            stamina?.Restore();
            motor.Teleport(motor.Rail, distance, height);
            velocity = Vector2.zero;
            invulnerable = move.hurtInvulnerability;
            counterTimer = 0f;
            Tether = null;
            FinisherTarget = null;
            EnterState(PlayerState.Locomotion);
        }

        /// <summary>Full health, stamina and flasks: what a rest at a shrine gives her.</summary>
        public void RestoreAll()
        {
            ownHealth.Restore();
            stamina?.Restore();
            Flasks = MaxFlasks;
        }

        /// <summary>One more flask to carry, full: a shrine upgrade.</summary>
        public void AddFlaskSlot()
        {
            MaxFlasks++;
            Flasks = MaxFlasks;
        }

        // ----------------------------------------------------------------- update

        private void Update()
        {
            InputFrame input = InputSource != null && State != PlayerState.Dead ? InputSource.Read() : default;

            // Presses are remembered before the early-out below, so nothing is lost during hit-stop.
            if (input.JumpPressed) jumpBuffer = move.jumpBufferTime;
            if (input.DashPressed) dashBuffer = move.dashBufferTime;
            if (input.AttackPressed) attackBuffer = combo.bufferTime;
            if (input.GuardPressed) guardBuffer = GuardBufferTime;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            if (input.InteractPressed && State != PlayerState.Dead && State != PlayerState.Finisher)
            {
                bool taken = Interaction != null && Interaction();
                if (!taken)
                    TryStartTether();
            }
            if (input.FlaskPressed)
                TryStartDrink();

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
                case PlayerState.Crouch:
                    TickCrouch(input, dt);
                    break;
                case PlayerState.Hurt:
                    TickHurt(dt);
                    break;
                case PlayerState.Dodge:
                    TickDodge(input, dt);
                    break;
                case PlayerState.Guard:
                    TickGuard(input, dt);
                    break;
                case PlayerState.Hang:
                    TickHang(input);
                    break;
                case PlayerState.Climb:
                    TickClimb();
                    break;
                case PlayerState.Dead:
                    TickDead(dt);
                    break;
                case PlayerState.Drink:
                    TickDrink(dt);
                    break;
                case PlayerState.Tether:
                    TickTether(dt);
                    break;
                case PlayerState.Finisher:
                    TickFinisher(dt);
                    break;
            }

            // On a ledge the position is set by hand; everything else moves through the motor.
            if (State != PlayerState.Hang && State != PlayerState.Climb)
            {
                float fallSpeed = -velocity.y;
                bool wasGrounded = motor.IsGrounded;
                motor.Move(velocity, dt);
                if (!wasGrounded && motor.IsGrounded && fallSpeed >= move.hardLandingSpeed && State != PlayerState.Dead)
                    LandHard();
                if (motor.HitCeiling && velocity.y > 0f)
                    velocity.y = 0f;
            }

            stateTime += dt;
            coyoteTimer -= dt;
            jumpBuffer -= dt;
            dashBuffer -= dt;
            attackBuffer -= dt;
            guardBuffer -= dt;
            dashCooldown -= dt;
            landingRecovery -= dt;
            counterTimer -= dt;
            regrabTimer -= dt;
            if (GuardReactionTime < float.MaxValue)
                GuardReactionTime += dt;
            if (invulnerable > 0f)
                invulnerable -= dt;
            if (turnTime < float.MaxValue)
                turnTime += dt;

            if (ownHealth != null)
                ownHealth.Invulnerable = invulnerable > 0f || IsDodgeInvulnerable || State == PlayerState.Dead || State == PlayerState.Finisher;
            if (stamina != null)
                stamina.RegenScale = State == PlayerState.Guard ? combat.guardRegenScale : 1f;
        }

        /// <summary>A hard landing costs speed and a moment of recovery, so it reads as weight. The guards hear it.</summary>
        private void LandHard()
        {
            CameraShake.Trigger(move.landingShake, move.landingShakeTime);
            velocity.x *= move.hardLandingSpeedKeep;
            landingRecovery = move.hardLandingRecovery;
            Noise.Emit(transform.position, 7f, gameObject);
        }

        // ------------------------------------------------------ flask, tether

        /// <summary>Drinks a flask, if she has one, is on her feet with her hands free, and is hurt.</summary>
        private bool TryStartDrink()
        {
            if (combat == null || Flasks <= 0 || !motor.IsGrounded)
                return false;
            if (State != PlayerState.Locomotion && State != PlayerState.Crouch && State != PlayerState.Guard)
                return false;
            if (ownHealth != null && ownHealth.Current >= ownHealth.Max)
                return false;
            if (motor.IsCrouched && !motor.TryStand())
                return false;
            Flasks--;
            drankThisDrink = false;
            turnTime = float.MaxValue;
            EnterState(PlayerState.Drink);
            return true;
        }

        /// <summary>Standing still with the flask: the health arrives part way through, and a hit spills the rest.</summary>
        private void TickDrink(float dt)
        {
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, move.groundDeceleration * dt);
            ApplyGravity(false, dt);
            if (!drankThisDrink && stateTime >= combat.flaskDrinkTime * combat.flaskHealAt)
            {
                drankThisDrink = true;
                ownHealth?.Heal(combat.flaskHeal);
                Drank?.Invoke();
            }
            if (stateTime >= combat.flaskDrinkTime)
                EnterState(PlayerState.Locomotion);
        }

        /// <summary>
        /// Throws the tether at the best lamp in reach: ahead of her or above her, within range along the rail and
        /// in height. Works from the ground and from the air, so a jump can be carried on from.
        /// </summary>
        private bool TryStartTether()
        {
            if (!HasTether || State == PlayerState.Dead || State == PlayerState.Hurt || State == PlayerState.Finisher
                || State == PlayerState.Tether || State == PlayerState.Hang || State == PlayerState.Climb)
                return false;
            TetherAnchor anchor = FindAnchor();
            if (anchor == null)
                return false;

            if (motor.IsCrouched)
                motor.TryStand();
            Tether = anchor;
            lastAnchor = anchor;
            tetherPoint = new Vector2(anchor.Distance, anchor.Height - move.tetherStandoff);
            float along = anchor.Distance - motor.Distance;
            tetherFacing = Mathf.Abs(along) > 0.3f ? (int)Mathf.Sign(along) : motor.Facing;
            motor.Facing = tetherFacing;
            velocity = Vector2.zero;
            turnTime = float.MaxValue;
            counterTimer = 0f;
            EnterState(PlayerState.Tether);
            Tethered?.Invoke(anchor);
            return true;
        }

        /// <summary>The lamp she can reach that is nearest the way she faces, or straight above her.</summary>
        public TetherAnchor FindAnchor()
        {
            if (!HasTether)
                return null;
            TetherAnchor best = null;
            float bestScore = float.MaxValue;
            float feet = transform.position.y;
            foreach (TetherAnchor anchor in TetherAnchor.All)
            {
                if (anchor == null || !anchor.isActiveAndEnabled)
                    continue;
                // Not the lamp she just let go of, until she has landed: the next throw goes on, not back up.
                if (anchor == lastAnchor && !motor.IsGrounded)
                    continue;
                float along = anchor.Distance - motor.Distance;
                float up = anchor.Height - feet;
                if (Mathf.Abs(along) > move.tetherRange || up > move.tetherHeight || up < move.tetherMinHeight)
                    continue;
                // Behind her only when it is nearly overhead.
                if (Mathf.Sign(along) != motor.Facing && Mathf.Abs(along) > 1.5f)
                    continue;
                float score = Mathf.Abs(along) + Mathf.Abs(up) * 0.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = anchor;
                }
            }
            return best;
        }

        /// <summary>
        /// The pull: a moment for the ribbon to fly, then a straight run through the air to just under the lamp, where
        /// she is flung up and on, her air moves given back. A jump on the way lets go early.
        /// </summary>
        private void TickTether(float dt)
        {
            if (Tether == null)
            {
                EnterState(PlayerState.Locomotion);
                return;
            }
            if (stateTime < move.tetherThrowTime)
            {
                velocity = new Vector2(Mathf.MoveTowards(velocity.x, 0f, move.groundDeceleration * dt), motor.IsGrounded ? -GroundStickSpeed : velocity.y * 0.5f);
                return;
            }

            Vector2 here = new Vector2(motor.Distance, transform.position.y);
            Vector2 toLamp = tetherPoint - here;
            airJumpsLeft = move.airJumps;
            airDashesLeft = move.airDashes;

            bool arrived = toLamp.magnitude < 0.35f;
            bool stuck = stateTime > move.tetherThrowTime + 0.2f && motor.Velocity.sqrMagnitude < 0.5f;
            if (arrived || stuck || (jumpBuffer > 0f && stateTime > move.tetherThrowTime + 0.1f))
            {
                bool jumped = jumpBuffer > 0f;
                jumpBuffer = 0f;
                velocity = new Vector2(tetherFacing * move.runSpeed * move.tetherExitRun, jumped ? move.JumpVelocity : move.tetherExitRise);
                Tether = null;
                coyoteTimer = 0f;
                EnterState(PlayerState.Locomotion);
                return;
            }
            velocity = toLamp.normalized * move.tetherSpeed;
        }

        // ----------------------------------------------------------- finishers

        /// <summary>
        /// A takedown or an execution, if one is on offer: an unaware enemy just ahead with its back to her, or a
        /// staggered one in front of her. She steps in and the killing blow plays out.
        /// </summary>
        private bool TryStartFinisher()
        {
            if (combat == null || !motor.IsGrounded)
                return false;
            EnemyController target = FindFinisherTarget(out bool execution);
            if (target == null)
                return false;

            FinisherTarget = target;
            IsExecution = execution;
            finisherLanded = false;
            attackBuffer = 0f;
            turnTime = float.MaxValue;
            counterTimer = 0f;
            velocity = Vector2.zero;
            // Facing it, a blade's length away.
            motor.Facing = target.Motor.Distance >= motor.Distance ? 1 : -1;
            float reach = Mathf.Min(0.95f, Mathf.Abs(target.Motor.Distance - motor.Distance));
            motor.Place(target.Motor.Distance - motor.Facing * reach, transform.position.y);
            EnterState(PlayerState.Finisher);
            FinisherStarted?.Invoke(target, execution);
            return true;
        }

        /// <summary>The enemy a finisher could be done to right now, and which kind, or null.</summary>
        public EnemyController FindFinisherTarget(out bool execution)
        {
            execution = false;
            if (combat == null)
                return null;
            EnemyController best = null;
            float bestDistance = float.MaxValue;
            foreach (EnemyController enemy in EnemyController.All)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Config == null)
                    continue;
                float along = enemy.Motor.Distance - motor.Distance;
                if (Mathf.Abs(enemy.transform.position.y - transform.position.y) > 1.2f)
                    continue;
                bool inFront = Mathf.Abs(along) < 0.4f || Mathf.Sign(along) == motor.Facing;
                if (!inFront)
                    continue;
                bool staggered = enemy.IsStaggered && Mathf.Abs(along) <= combat.executeRange;
                bool unaware = !enemy.Config.isBoss && enemy.Mood != Mood.Alert && enemy.Motor.Facing == motor.Facing
                    && Mathf.Abs(along) <= combat.takedownRange && enemy.State != EnemyState.Dead;
                if (!staggered && !unaware)
                    continue;
                if (Mathf.Abs(along) < bestDistance)
                {
                    bestDistance = Mathf.Abs(along);
                    best = enemy;
                    execution = staggered;
                }
            }
            return best;
        }

        /// <summary>She stands over it; the blow lands at the step's startup, whatever its health, and then she recovers.</summary>
        private void TickFinisher(float dt)
        {
            AttackStep step = combo.finisher;
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, move.groundDeceleration * dt);
            ApplyGravity(false, dt);
            if (FinisherTarget == null)
            {
                EnterState(PlayerState.Locomotion);
                return;
            }
            if (!finisherLanded && stateTime >= step.startup)
            {
                finisherLanded = true;
                IsHitboxActive = true;
                FinisherTarget.Finish(transform.forward, gameObject, IsExecution);
                HitStop.Trigger(step.hitStop);
                CameraShake.Trigger(step.shake, combo.shakeTime);
                FinisherLanded?.Invoke(FinisherTarget, IsExecution);
            }
            if (stateTime >= step.ActiveEnd)
                IsHitboxActive = false;
            if (stateTime >= step.Total)
            {
                FinisherTarget = null;
                EnterState(PlayerState.Locomotion);
            }
        }

        // -------------------------------------------------------------- moving

        private void TickLocomotion(InputFrame input, float dt)
        {
            if (IsJumpWindup)
            {
                TickJumpWindup(dt);
                return;
            }

            Steer(input);

            // During a skid turn she keeps heading the new way; pushing back is held over until the turn ends.
            float speed = move.walkSpeed > 0f && !input.Run ? move.walkSpeed : move.runSpeed;
            float target = IsTurning ? motor.Facing * Mathf.Abs(input.Move) * speed : input.Move * speed;
            bool accelerating = Mathf.Abs(target) > 0.01f;
            float rate = motor.IsGrounded
                ? (accelerating ? move.groundAcceleration : move.groundDeceleration)
                : (accelerating ? move.airAcceleration : move.airDeceleration);
            velocity.x = Mathf.MoveTowards(velocity.x, target, rate * dt);

            ApplyGravity(input.JumpHeld, dt);

            if (TryStartJump() || TryStartDash(input) || TryGrabLedge(input))
                return;

            if (input.CrouchHeld && motor.IsGrounded)
            {
                motor.Crouch(move.crouchHeight);
                EnterState(PlayerState.Crouch);
                return;
            }

            if (TryStartGuard(input))
                return;

            if (attackBuffer > 0f && CanAct(move.turnActionCancel))
                TryStartAttackOrCounter(input);
        }

        /// <summary>
        /// Faces where the stick points. Reversing on the ground at speed starts a skid turn, during which she cannot
        /// turn back. Slow reversals, and any in the air, are instant.
        /// </summary>
        private void Steer(InputFrame input)
        {
            if (input.Move == 0f || IsTurning)
                return;

            int wanted = input.Move > 0f ? 1 : -1;
            if (wanted == motor.Facing)
                return;

            if (motor.IsGrounded && velocity.x * motor.Facing >= move.turnMinSpeed)
                turnTime = 0f;
            motor.Facing = wanted;
        }

        /// <summary>False while a skid turn is younger than <paramref name="turnCancel"/> or a hard landing recovers.</summary>
        private bool CanAct(float turnCancel)
        {
            return !(IsTurning && turnTime < turnCancel) && landingRecovery <= 0f;
        }

        private void TickHurt(float dt)
        {
            float rate = motor.IsGrounded ? move.groundDeceleration * 0.5f : move.airDeceleration;
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, rate * dt);
            ApplyGravity(false, dt);
            if (stateTime >= HurtDuration)
                EnterState(PlayerState.Locomotion);
        }

        private void TickDead(float dt)
        {
            float rate = motor.IsGrounded ? move.groundDeceleration * 0.3f : move.airDeceleration;
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, rate * dt);
            ApplyGravity(false, dt);
        }

        private void TickCrouch(InputFrame input, float dt)
        {
            // She can turn round in cover, but not walk.
            if (input.Move != 0f)
                motor.Facing = input.Move > 0f ? 1 : -1;
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, move.groundDeceleration * dt);
            ApplyGravity(input.JumpHeld, dt);

            if (attackBuffer > 0f && HasStamina())
            {
                StartCrouchAttack();
                return;
            }

            // Leaving the crouch needs headroom: under a low ceiling she stays down.
            bool wantsUp = !input.CrouchHeld || !motor.IsGrounded || jumpBuffer > 0f || dashBuffer > 0f;
            if (!wantsUp || !motor.TryStand())
                return;

            EnterState(PlayerState.Locomotion);
            if (!TryStartJump())
                TryStartDash(input);
        }

        private void TickDash(InputFrame input, float dt)
        {
            // A ground dash stays glued to the floor; an air dash travels flat.
            velocity = new Vector2(dashDirection * move.dashSpeed, motor.IsGrounded ? -GroundStickSpeed : 0f);

            // Attack out of a ground dash is the lunge: the dash's speed carried into a thrust.
            if (attackBuffer > 0f && motor.IsGrounded && stateTime >= 0.04f && HasStamina() && combo.dashAttack != null)
            {
                dashCooldown = move.dashCooldown;
                StartDashAttack();
                return;
            }

            // Late in the dash a jump can cut it short, carrying run speed into the jump.
            if (stateTime >= move.dashJumpCancel && jumpBuffer > 0f && (coyoteTimer > 0f || airJumpsLeft > 0))
            {
                velocity.x = dashDirection * move.runSpeed;
                dashCooldown = move.dashCooldown;
                if (TryStartJump())
                    return;
            }

            if (stateTime >= move.dashDuration)
            {
                // Leave the dash at run speed, not dash speed, so it doesn't feel like sliding on ice.
                velocity.x = dashDirection * move.runSpeed;
                dashCooldown = move.dashCooldown;
                EnterState(PlayerState.Locomotion);
            }
        }

        /// <summary>
        /// The dodge back: a fast hop away from where she faces that slows to a stop, still facing forward, with blows
        /// passing through her early on. Late in it an attack or a jump can cut it short.
        /// </summary>
        private void TickDodge(InputFrame input, float dt)
        {
            float t = Mathf.Clamp01(stateTime / move.dodgeDuration);
            velocity.x = dashDirection * move.dodgeSpeed * (1f - t * t);
            ApplyGravity(false, dt);

            if (stateTime >= move.dodgeCancel)
            {
                if (TryStartJump())
                    return;
                if (attackBuffer > 0f && TryStartAttackOrCounter(input))
                    return;
            }

            if (stateTime >= move.dodgeDuration)
            {
                dashCooldown = move.dashCooldown;
                EnterState(PlayerState.Locomotion);
            }
        }

        // --------------------------------------------------------------- guard

        private bool TryStartGuard(InputFrame input)
        {
            if (!motor.IsGrounded || combat == null || !(input.GuardHeld || guardBuffer > 0f))
                return false;

            // Only a fresh press opens a parry window, and not one hard on the heels of the last.
            parryArmed = guardBuffer > 0f && Time.time - lastGuardPress >= combat.parryRepressDelay;
            if (guardBuffer > 0f)
                lastGuardPress = Time.time;
            guardBuffer = 0f;
            turnTime = float.MaxValue;
            EnterState(PlayerState.Guard);
            return true;
        }

        /// <summary>
        /// Guard up: she stands her ground facing the way she was, blocks what comes from the front, and slides back a
        /// little when a block lands. Attack leaves the guard with a strike, or with the counter just after a parry;
        /// Dash leaves it with a dodge or a dash; letting go of Guard lowers it.
        /// </summary>
        private void TickGuard(InputFrame input, float dt)
        {
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, move.groundDeceleration * 0.5f * dt);
            ApplyGravity(false, dt);

            // Pressing Guard again re-opens the parry window, as long as it is not mashed.
            if (guardBuffer > 0f && Time.time - lastGuardPress >= combat.parryRepressDelay)
            {
                parryArmed = true;
                lastGuardPress = Time.time;
                guardBuffer = 0f;
                stateTime = 0f;
            }

            bool reacting = GuardReactionTime < combat.blockStun;
            if (!reacting && (TryStartDash(input) || TryStartJump()))
                return;
            if (attackBuffer > 0f && (CanCounter || !reacting) && TryStartAttackOrCounter(input))
                return;
            if (!input.GuardHeld && !reacting && stateTime >= 0.1f)
                EnterState(PlayerState.Locomotion);
        }

        // ------------------------------------------------------------- ledges

        /// <summary>
        /// In the air, falling or near the top of a jump, she catches a ledge in front of her that is within reach of
        /// her hands, unless Down is held or she is pushing away from it.
        /// </summary>
        private bool TryGrabLedge(InputFrame input)
        {
            if (motor.IsGrounded || velocity.y > move.ledgeGrabMaxRise || regrabTimer > 0f || input.CrouchHeld)
                return false;
            if (input.Move * motor.Facing < 0f)
                return false;
            if (!motor.FindLedge(move.ledgeReach.x, move.ledgeReach.y, move.ledgeProbe, out float top, out float wall))
                return false;

            ledgeTop = top;
            ledgeWall = wall;
            velocity = Vector2.zero;
            airJumpsLeft = move.airJumps;
            airDashesLeft = move.airDashes;
            EnterState(PlayerState.Hang);
            PlaceHanging();
            return true;
        }

        /// <summary>
        /// Hanging: Jump or a push toward the wall climbs up; Down lets go; Jump while pushing away springs off the wall.
        /// </summary>
        private void TickHang(InputFrame input)
        {
            velocity = Vector2.zero;
            PlaceHanging();
            if (stateTime < move.hangSettle)
                return;

            float toward = input.Move * motor.Facing;
            if (jumpBuffer > 0f && toward < -0.5f)
            {
                // Spring off the wall, back the way she came.
                jumpBuffer = 0f;
                regrabTimer = move.regrabDelay;
                StepOutOfWall();
                motor.Facing = -motor.Facing;
                velocity = new Vector2(motor.Facing * move.runSpeed, move.JumpVelocity * 0.8f);
                EnterState(PlayerState.Locomotion);
                return;
            }
            if (jumpBuffer > 0f || toward > 0.5f)
            {
                jumpBuffer = 0f;
                EnterState(PlayerState.Climb);
                return;
            }
            if (input.CrouchHeld)
            {
                regrabTimer = move.regrabDelay;
                StepOutOfWall();
                EnterState(PlayerState.Locomotion);
            }
        }

        /// <summary>
        /// Her hands are on the lip, so her root hangs inside the wall's face. Before she is a body in the world again, she
        /// is moved clear of it, or the collision would push her out sideways.
        /// </summary>
        private void StepOutOfWall()
        {
            motor.Place(ledgeWall - motor.Facing * (motor.Radius + 0.02f), motor.transform.position.y);
        }

        /// <summary>
        /// Where her root hangs. Each clip has her hands at a different height above her root, so the root follows the
        /// hands: low for the catch, then eased up to the hang as the animation cross-fades into it, so her hands stay
        /// on the ledge the whole time instead of sliding along it.
        /// </summary>
        private void PlaceHanging()
        {
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((stateTime - move.grabTime) / move.hangBlend));
            float depth = Mathf.Lerp(move.grabHandsHeight, move.hangHeight, blend);
            motor.Place(ledgeWall - motor.Facing * move.hangStandoff, ledgeTop - depth);
        }

        /// <summary>
        /// Pulls her up onto the ledge. Her hands stay on the lip while she pulls (the root follows them, see
        /// <see cref="MoveConfig.climbHandsHeight"/>), then she lets go and steps forward over the edge; when it ends
        /// she stands on the top.
        /// </summary>
        private void TickClimb()
        {
            velocity = Vector2.zero;
            float t = Mathf.Clamp01(stateTime / move.climbDuration);
            float onTop = ledgeWall + motor.Facing * (motor.Radius + 0.15f);

            if (t >= 1f)
            {
                // The clip's pose sits low by how far her root still has to rise (the clip carries her body up the wall
                // itself). The animator fades the pose out into the idle over the settle time, and the root rises by
                // the share of the pose that is left, so her body goes on smoothly from the climb to standing.
                float settle = Mathf.Max(0.01f, move.climbSettle);
                float p = Mathf.Clamp01((stateTime - move.climbDuration) / settle);
                float low = ledgeTop - move.climbHandsHeight.Evaluate(1f);
                float top = ledgeTop + 0.02f;
                motor.Place(onTop, top - (top - low) * (1f - Mathf.SmoothStep(0f, 1f, p)));
                if (p >= 1f)
                    EnterState(PlayerState.Locomotion);
                return;
            }

            // Hands on the lip: the root sits where those hands put it.
            float gripX = ledgeWall + motor.Facing * (move.climbGrip - move.climbHandsReach.Evaluate(t));
            // The pose cross-fades from the hang into the climb, so the root eases from one hands height to the other
            // over the same time, and her hands do not fly up while the old pose fades out.
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateTime / move.hangBlend));
            float y = ledgeTop - Mathf.Lerp(move.hangHeight, move.climbHandsHeight.Evaluate(t), blend);
            float step = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - move.climbRelease) / (1f - move.climbRelease)));
            motor.Place(Mathf.Lerp(gripX, onTop, step), y);
        }

        // ------------------------------------------------------------- attacks

        private void TickAttack(InputFrame input, float dt)
        {
            AttackStep step = CurrentStep;

            if (IsAirAttack)
            {
                // The flying kick carries her forward through its strike, then she drifts to a stop.
                velocity.x = stateTime < step.ActiveEnd
                    ? motor.Facing * step.lunge
                    : Mathf.MoveTowards(velocity.x, 0f, move.airDeceleration * dt);
            }
            else if (motor.IsGrounded)
            {
                velocity.x = stateTime < step.ActiveEnd
                    ? motor.Facing * step.lunge
                    : Mathf.MoveTowards(velocity.x, 0f, move.groundDeceleration * dt);
            }
            else
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, move.airDeceleration * dt);
            }

            ApplyGravity(input.JumpHeld || IsAirAttack, dt);

            IsHitboxActive = stateTime >= step.startup && stateTime < step.ActiveEnd;
            if (IsHitboxActive)
                ApplyHits(step);

            if (IsAirAttack)
            {
                // Touching down ends the kick; otherwise it plays out and she can cut it short with a jump or a dash.
                if (motor.IsGrounded && stateTime > 0.05f)
                {
                    EnterState(PlayerState.Locomotion);
                    return;
                }
                if (stateTime >= step.CancelFrom && (TryStartJump() || TryStartDash(input) || TryGrabLedge(input)))
                    return;
                if (stateTime >= step.Total)
                    EnterState(PlayerState.Locomotion);
                return;
            }

            if (IsCrouchAttack)
            {
                // From a crouch the only follow-up is another sweep; she stays down until it ends.
                if (stateTime >= step.CancelFrom && attackBuffer > 0f && HasStamina())
                {
                    StartCrouchAttack();
                    return;
                }
                if (stateTime >= step.Total)
                    EnterState(PlayerState.Crouch);
                return;
            }

            if (stateTime >= step.CancelFrom && (TryStartJump() || TryStartDash(input)))
                return;

            // The next strike can follow once the hitbox closes if this one connected; a whiff has to recover first.
            if (stateTime >= (swingConnected ? step.ActiveEnd : step.CancelFrom))
            {
                if (attackBuffer > 0f && !IsCounter && ComboIndex + 1 < combo.steps.Length && HasStamina())
                {
                    StartAttack(ComboIndex + 1, input);
                    return;
                }
                if (stateTime >= step.CancelFrom && TryStartGuard(input))
                    return;
            }

            if (stateTime >= step.Total)
                EnterState(PlayerState.Locomotion);
        }

        private bool HasStamina() => stamina == null || stamina.HasAny;

        /// <summary>The counter while one is on offer, a finisher if one is, the first strike of the combo otherwise.</summary>
        private bool TryStartAttackOrCounter(InputFrame input)
        {
            if (CanCounter)
            {
                counterTimer = 0f;
                StartStrike(input, 0, counter: true);
                return true;
            }
            if (motor.IsGrounded && TryStartFinisher())
                return true;
            if (combo.steps.Length == 0 || !HasStamina())
                return false;
            if (!motor.IsGrounded)
            {
                StartAirAttack(input);
                return true;
            }
            StartAttack(0, input);
            return true;
        }

        private void StartAttack(int index, InputFrame input) => StartStrike(input, index, counter: false);

        private void StartDashAttack()
        {
            ComboIndex = 0;
            IsCrouchAttack = false;
            IsAirAttack = false;
            IsCounter = false;
            IsDashAttack = true;
            attackBuffer = 0f;
            swingConnected = false;
            turnTime = float.MaxValue;
            hitThisSwing.Clear();
            stamina?.Spend(combo.dashAttack.staminaCost);
            EnterState(PlayerState.Attack);
        }

        private void StartStrike(InputFrame input, int index, bool counter)
        {
            if (input.Move != 0f)
                motor.Facing = input.Move > 0f ? 1 : -1;

            ComboIndex = index;
            IsCrouchAttack = false;
            IsAirAttack = false;
            IsDashAttack = false;
            IsCounter = counter;
            attackBuffer = 0f;
            swingConnected = false;
            turnTime = float.MaxValue;
            hitThisSwing.Clear();
            stamina?.Spend(CurrentStep.staminaCost);
            EnterState(PlayerState.Attack);
        }

        /// <summary>The flying kick: thrown from the air, with a little lift so it reads as a leap.</summary>
        private void StartAirAttack(InputFrame input)
        {
            if (input.Move != 0f)
                motor.Facing = input.Move > 0f ? 1 : -1;

            ComboIndex = 0;
            IsCrouchAttack = false;
            IsAirAttack = true;
            IsDashAttack = false;
            IsCounter = false;
            attackBuffer = 0f;
            swingConnected = false;
            turnTime = float.MaxValue;
            hitThisSwing.Clear();
            velocity.y = Mathf.Max(velocity.y, combo.airAttackLift);
            stamina?.Spend(combo.airAttack.staminaCost);
            EnterState(PlayerState.Attack);
        }

        private void StartCrouchAttack()
        {
            ComboIndex = 0;
            IsCrouchAttack = true;
            IsAirAttack = false;
            IsDashAttack = false;
            IsCounter = false;
            attackBuffer = 0f;
            swingConnected = false;
            hitThisSwing.Clear();
            stamina?.Spend(combo.crouchAttack.staminaCost);
            EnterState(PlayerState.Attack);
        }

        // ------------------------------------------------------- jumps, dashes

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
            if (jumpBuffer <= 0f || !CanAct(move.turnJumpCancel))
                return false;

            if (coyoteTimer <= 0f)
            {
                if (airJumpsLeft <= 0)
                    return false;
                airJumpsLeft--;
                AirJumped?.Invoke();
            }

            jumpBuffer = 0f;
            coyoteTimer = 0f;
            turnTime = float.MaxValue;
            EnterState(PlayerState.Locomotion);

            // From the ground a jump can gather first: she sinks into her knees, then leaves.
            float speedShare = move.runSpeed > 0f ? Mathf.Clamp01(Mathf.Abs(velocity.x) / move.runSpeed) : 0f;
            float windup = Mathf.Lerp(move.jumpWindup, move.runJumpWindup, speedShare);
            if (motor.IsGrounded && windup > 0f)
            {
                windupDuration = windup;
                windupTime = 0f;
                return true;
            }

            velocity.y = move.JumpVelocity;
            return true;
        }

        /// <summary>
        /// The gather before the jump: she carries most of her speed into it and leaves the ground when it ends, or at
        /// once if the ground goes from under her.
        /// </summary>
        private void TickJumpWindup(float dt)
        {
            windupTime += dt;
            if (windupTime < windupDuration && motor.IsGrounded)
            {
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, move.groundDeceleration * 0.25f * dt);
                ApplyGravity(true, dt);
                return;
            }

            windupDuration = 0f;
            velocity.y = move.JumpVelocity;
        }

        /// <summary>
        /// Dash. With no direction held on the ground it is a dodge back instead, the soulslike backstep: she stays
        /// facing her opponent and hops away from them.
        /// </summary>
        private bool TryStartDash(InputFrame input)
        {
            if (dashBuffer <= 0f || dashCooldown > 0f || !CanAct(move.turnActionCancel) || !HasStamina())
                return false;

            if (motor.IsGrounded && input.Move == 0f)
            {
                dashDirection = -motor.Facing;
                dashBuffer = 0f;
                turnTime = float.MaxValue;
                stamina?.Spend(combat != null ? combat.dodgeCost : 0f);
                if (motor.IsCrouched)
                    motor.TryStand();
                velocity = new Vector2(dashDirection * move.dodgeSpeed, move.dodgeHop);
                EnterState(PlayerState.Dodge);
                return true;
            }

            bool airDash = !motor.IsGrounded;
            if (airDash)
            {
                if (airDashesLeft <= 0)
                    return false;
                airDashesLeft--;
            }

            if (input.Move != 0f)
                motor.Facing = input.Move > 0f ? 1 : -1;

            dashDirection = motor.Facing;
            dashBuffer = 0f;
            turnTime = float.MaxValue;
            stamina?.Spend(combat != null ? combat.dashCost : 0f);
            EnterState(PlayerState.Dash);
            Dashed?.Invoke(airDash);
            return true;
        }

        private void EnterState(PlayerState next)
        {
            State = next;
            stateTime = 0f;
            windupDuration = 0f;
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

                // Where the blow meets the body: the point on it nearest her, at the height of the strike.
                Vector3 point = overlapBuffer[i].ClosestPoint(new Vector3(transform.position.x, center.y, transform.position.z));
                HitResult result = target.TakeHit(new Hit(step.damage, transform.forward, gameObject, step.knockback, point));
                if (result == HitResult.Ignored)
                    continue;
                HitLanded?.Invoke(target);
                landed = true;
            }

            if (landed)
            {
                swingConnected = true;
                HitStop.Trigger(step.hitStop);
                CameraShake.Trigger(step.shake, combo.shakeTime);
                // A blow landing rings through the hall: the guards nearby come to look.
                Noise.Emit(center, 9f, gameObject);
            }
        }

        /// <summary>The current swing's hitbox in world space. The box is oriented by this transform's rotation.</summary>
        public void GetHitbox(AttackStep step, out Vector3 center, out Vector3 halfExtents)
        {
            center = transform.TransformPoint(new Vector3(0f, step.hitboxCenter.y, step.hitboxCenter.x));
            halfExtents = new Vector3(combo.hitboxDepth, step.hitboxSize.y * 0.5f, step.hitboxSize.x * 0.5f);
        }
    }
}
