using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PX
{
    public enum HeroinePose
    {
        Idle,
        Run,
        JumpStart,
        JumpLoop,
        Dash,
        Attack,
        Turn,
        RunJump,
        AirDash,
        LandRun,
        LandIdle,
        CrouchDown,
        CrouchIdle,
        CrouchUp,
        CrouchAttack,
        Hurt,
        Dead,
        Dodge,
        Guard,
        Parry,
        GuardHit,
        LedgeGrab,
        Hang,
        Climb,
        AirAttack,
    }

    /// <summary>
    /// Plays the heroine's animation from what <see cref="PlayerController"/> and <see cref="CharacterMotor"/> report.
    /// Gameplay never reads from here: animation follows the simulation, not the other way round.
    /// Clip time is set by hand: strikes follow the attack's own timing, so the blow lands when the hitbox is live,
    /// and landings and crouch transitions play a window of their clip over a set time.
    /// </summary>
    public sealed class HeroineAnimator : MonoBehaviour
    {
        private static readonly int PoseCount = System.Enum.GetValues(typeof(HeroinePose)).Length;

        // Below this ground speed, in m/s, she counts as standing still.
        private const float MovingSpeed = 0.3f;

        [SerializeField] private PlayerController player;
        [SerializeField] private CharacterMotor motor;
        [SerializeField] private Animator animator;
        [SerializeField] private HeroineAnimationConfig config;

        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationScriptPlayable bodyCap;
        private NativeArray<float> measuredBody;
        private readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[PoseCount];
        private readonly AnimationClip[] clips = new AnimationClip[PoseCount];
        private readonly float[] weights = new float[PoseCount];
        private readonly float[] applied = new float[PoseCount];
        private readonly double[] times = new double[PoseCount];
        private bool lastCounter;

        private float airTime;
        private float poseTime;
        private float crouchTime;
        private PlayerState lastState;
        private int lastComboIndex;
        private bool lastCrouchAttack;
        private bool lastAirAttack;
        private float lastVelocityY;
        private bool runningJump;
        private bool airDash;
        private bool restartPose;
        private bool standingUp;
        private HeroinePose? landing;
        private ClipWindow attackWindow;
        private Quaternion modelRotation;
        private float standingBody;
        private float landDipTime = float.MaxValue;
        private float landDipStrength;

        public HeroinePose CurrentPose { get; private set; }

        public HeroineAnimationConfig Config => config;

        /// <summary>The clip behind the attack pose right now, which changes with the combo step.</summary>
        public AnimationClip AttackClip => clips[(int)HeroinePose.Attack];

        private void OnEnable()
        {
            if (animator == null || config == null)
                return;

            attackWindow = AttackWindowFor(0);
            clips[(int)HeroinePose.Idle] = config.idle;
            clips[(int)HeroinePose.Run] = config.run;
            clips[(int)HeroinePose.JumpStart] = config.jumpStart;
            clips[(int)HeroinePose.JumpLoop] = config.jumpLoop;
            clips[(int)HeroinePose.Dash] = config.dash;
            clips[(int)HeroinePose.Attack] = attackWindow.clip;
            clips[(int)HeroinePose.Turn] = config.runTurn;
            clips[(int)HeroinePose.RunJump] = config.runJump;
            clips[(int)HeroinePose.AirDash] = config.airDash;
            clips[(int)HeroinePose.LandRun] = config.landRun.clip;
            clips[(int)HeroinePose.LandIdle] = config.landIdle.clip;
            clips[(int)HeroinePose.CrouchDown] = config.crouchDown.clip;
            clips[(int)HeroinePose.CrouchIdle] = config.crouchIdle;
            clips[(int)HeroinePose.CrouchUp] = config.crouchUp.clip;
            clips[(int)HeroinePose.CrouchAttack] = config.crouchAttack.clip;
            clips[(int)HeroinePose.Hurt] = config.hurt.clip;
            clips[(int)HeroinePose.Dead] = config.death.clip;
            clips[(int)HeroinePose.Dodge] = config.dodge.clip;
            clips[(int)HeroinePose.Guard] = config.guard;
            clips[(int)HeroinePose.Parry] = config.parry.clip;
            clips[(int)HeroinePose.GuardHit] = config.guardHit.clip;
            clips[(int)HeroinePose.LedgeGrab] = config.ledgeGrab.clip;
            clips[(int)HeroinePose.Hang] = config.ledgeHang;
            clips[(int)HeroinePose.Climb] = config.ledgeClimb.clip;
            clips[(int)HeroinePose.AirAttack] = config.airAttack.clip;
            modelRotation = animator.transform.localRotation;

            graph = PlayableGraph.Create("Heroine");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, PoseCount);
            for (int i = 0; i < PoseCount; i++)
            {
                if (clips[i] == null)
                    continue;
                playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                playables[i].SetSpeed(0.0); // time is set by hand in LateUpdate
                graph.Connect(playables[i], 0, mixer, i);
                mixer.SetInputWeight(i, 0f);
            }

            // The sweep is held down to her crouch, so the pose goes through a body-height cap after the mixer.
            measuredBody = new NativeArray<float>(1, Allocator.Persistent);
            bodyCap = AnimationScriptPlayable.Create(graph, new BodyHeightJob { measured = measuredBody }, 1);
            graph.Connect(mixer, 0, bodyCap, 0);
            bodyCap.SetInputWeight(0, 1f);

            AnimationPlayableOutput.Create(graph, "Heroine", animator).SetSourcePlayable(bodyCap);
            graph.Play();

            CurrentPose = HeroinePose.Idle;
            weights[(int)HeroinePose.Idle] = 1f;
            MeasureBody();
            mixer.SetInputWeight((int)HeroinePose.Idle, 1f);
        }

        private void OnDisable()
        {
            if (graph.IsValid())
                graph.Destroy();
            if (measuredBody.IsCreated)
                measuredBody.Dispose();
        }

        /// <summary>The highest her body may be during the sweep, in the animation stream's body space.</summary>
        public float CrouchAttackCeiling { get; private set; } = float.MaxValue;

        /// <summary>How far her body is lowered by the landing dip right now, in the stream's body space.</summary>
        public float LandDip { get; private set; }

        /// <summary>
        /// Poses her once standing and once in the crouch idle, to learn how high her body is in each: the landing dip
        /// is a share of the first, and the sweep's ceiling is just above the second.
        /// </summary>
        private void MeasureBody()
        {
            standingBody = MeasureBodyIn(HeroinePose.Idle);
            float crouched = MeasureBodyIn(HeroinePose.CrouchIdle);
            if (crouched > 0f)
                CrouchAttackCeiling = crouched * (1f + config.crouchAttackRise);
            bodyCap.SetJobData(new BodyHeightJob { ceiling = CrouchAttackCeiling, measured = measuredBody });
        }

        private float MeasureBodyIn(HeroinePose pose)
        {
            int slot = (int)pose;
            if (clips[slot] == null)
                return 0f;

            mixer.SetInputWeight(slot, 1f);
            bodyCap.SetJobData(new BodyHeightJob { measure = true, measured = measuredBody });
            graph.Evaluate(0f);
            mixer.SetInputWeight(slot, 0f);
            return measuredBody[0];
        }

        /// <summary>
        /// The landing dip, added on top of whatever she plays: a quick sink and a slower rise, as deep as the landing
        /// was hard. It is what makes a landing that runs straight on read as a landing.
        /// </summary>
        private float DipAt(float t)
        {
            if (t >= config.landDipDown + config.landDipUp)
                return 0f;
            float shape = t < config.landDipDown
                ? Mathf.SmoothStep(0f, 1f, t / config.landDipDown)
                : 1f - Mathf.SmoothStep(0f, 1f, (t - config.landDipDown) / config.landDipUp);
            return shape * landDipStrength * config.landDip * standingBody;
        }

        private float FadeTimeInto(HeroinePose pose)
        {
            switch (pose)
            {
                case HeroinePose.Dash:
                case HeroinePose.AirDash:
                    return config.dashFadeTime;
                case HeroinePose.Attack:
                case HeroinePose.CrouchAttack:
                case HeroinePose.AirAttack:
                case HeroinePose.Hurt:
                case HeroinePose.Dead:
                case HeroinePose.Dodge:
                case HeroinePose.Parry:
                case HeroinePose.GuardHit:
                case HeroinePose.LedgeGrab:
                    return config.attackFadeTime;
                case HeroinePose.LandRun:
                case HeroinePose.LandIdle:
                    return config.landFadeTime;
                case HeroinePose.CrouchIdle:
                    return config.crouchFadeTime;
                default:
                    return config.fadeTime;
            }
        }

        private void LateUpdate()
        {
            if (!graph.IsValid() || player == null || motor == null)
                return;

            float dt = Time.deltaTime;
            HeroinePose pose = ChoosePose(dt);

            bool attackStarted = NewAttackStarted();
            if (attackStarted && !player.IsCrouchAttack && !player.IsAirAttack)
            {
                attackWindow = player.IsCounter && config.counter.clip != null ? config.counter : AttackWindowFor(player.ComboIndex);
                UseAttackClip(attackWindow.clip);
            }

            if (pose != CurrentPose || attackStarted || restartPose)
            {
                times[(int)pose] = pose == HeroinePose.RunJump ? config.runJumpTakeoff : 0.0;
                poseTime = 0f;
            }
            else
            {
                poseTime += dt;
            }

            // The turn clip spins her round, so the body is turned back to face where she came from while it plays.
            // That cannot be blended with other poses, so the turn cuts in and out.
            bool cut = (pose == HeroinePose.Turn) != (CurrentPose == HeroinePose.Turn);
            animator.transform.localRotation = pose == HeroinePose.Turn ? Quaternion.Euler(0f, 180f, 0f) * modelRotation : modelRotation;

            CurrentPose = pose;
            lastState = player.State;
            lastComboIndex = player.ComboIndex;
            lastCrouchAttack = player.IsCrouchAttack;
            lastAirAttack = player.IsAirAttack;
            lastCounter = player.IsCounter;
            lastVelocityY = motor.Velocity.y;

            // Cross-fade: each weight moves linearly, and is eased on the way into the mixer, so blends start and
            // end softly instead of with a kink. Weights are normalised so the pose never loses or gains strength.
            float fadeTime = player.ClimbSettling ? player.Move.climbSettle : FadeTimeInto(pose);
            float fade = fadeTime > 0f ? dt / fadeTime : 1f;
            float total = 0f;
            for (int i = 0; i < PoseCount; i++)
            {
                if (clips[i] == null)
                    continue;
                weights[i] = Mathf.MoveTowards(weights[i], i == (int)pose ? 1f : 0f, cut ? 1f : fade);
                applied[i] = Mathf.SmoothStep(0f, 1f, weights[i]);
                total += applied[i];
            }

            for (int i = 0; i < PoseCount; i++)
            {
                if (clips[i] == null)
                    continue;

                mixer.SetInputWeight(i, total > 0f ? applied[i] / total : 0f);

                if (weights[i] <= 0f && i != (int)pose)
                    continue;

                if (IsWindowed((HeroinePose)i))
                {
                    // A windowed pose that is fading out holds its last frame.
                    if (i == (int)pose)
                        times[i] = WindowTime(pose);
                }
                else
                {
                    times[i] += dt * ClipSpeed((HeroinePose)i);
                    double length = clips[i].length;
                    if (i == (int)HeroinePose.RunJump)
                        length = System.Math.Min(length, config.runJumpLanding);
                    times[i] = IsLooping((HeroinePose)i) && length > 0.0 ? times[i] % length : System.Math.Min(times[i], length);
                }
                playables[i].SetTime(times[i]);
            }

            landDipTime += dt;
            LandDip = DipAt(landDipTime);
            bodyCap.SetJobData(new BodyHeightJob
            {
                weight = weights[(int)HeroinePose.CrouchAttack], ceiling = CrouchAttackCeiling, dip = LandDip,
                headLift = config.idleHeadLift * (total > 0f ? applied[(int)HeroinePose.Idle] / total : 0f),
                measured = measuredBody,
            });
        }

        private HeroinePose ChoosePose(float dt)
        {
            restartPose = false;

            bool crouched = player.State == PlayerState.Crouch || (player.State == PlayerState.Attack && player.IsCrouchAttack);
            if (crouched)
            {
                crouchTime += dt;
            }
            else
            {
                if (crouchTime > 0f)
                    standingUp = true;
                crouchTime = 0f;
            }

            switch (player.State)
            {
                case PlayerState.Dead:
                    landing = null;
                    standingUp = false;
                    return clips[(int)HeroinePose.Dead] != null ? HeroinePose.Dead : HeroinePose.Hurt;
                case PlayerState.Dodge:
                    landing = null;
                    standingUp = false;
                    return clips[(int)HeroinePose.Dodge] != null ? HeroinePose.Dodge : HeroinePose.Dash;
                case PlayerState.Guard:
                    landing = null;
                    standingUp = false;
                    if (player.LastGuardWasParry && player.GuardReactionTime < config.parryDuration && clips[(int)HeroinePose.Parry] != null)
                        return HeroinePose.Parry;
                    if (!player.LastGuardWasParry && player.GuardReactionTime < GuardHitDuration && clips[(int)HeroinePose.GuardHit] != null)
                        return HeroinePose.GuardHit;
                    return clips[(int)HeroinePose.Guard] != null ? HeroinePose.Guard : HeroinePose.Idle;
                case PlayerState.Hang:
                    landing = null;
                    standingUp = false;
                    airTime = 0f;
                    if (player.StateTime < player.Move.grabTime && clips[(int)HeroinePose.LedgeGrab] != null)
                        return HeroinePose.LedgeGrab;
                    return clips[(int)HeroinePose.Hang] != null ? HeroinePose.Hang : HeroinePose.JumpLoop;
                case PlayerState.Climb:
                    landing = null;
                    standingUp = false;
                    airTime = 0f;
                    if (player.ClimbSettling)
                        return HeroinePose.Idle;
                    return clips[(int)HeroinePose.Climb] != null ? HeroinePose.Climb : HeroinePose.JumpLoop;
                case PlayerState.Hurt:
                    landing = null;
                    standingUp = false;
                    return clips[(int)HeroinePose.Hurt] != null ? HeroinePose.Hurt : HeroinePose.JumpLoop;
                case PlayerState.Dash:
                    landing = null;
                    standingUp = false;
                    // Ground or air is decided when the dash starts, so a ground dash off a ledge keeps its pose.
                    if (lastState != PlayerState.Dash)
                        airDash = !motor.IsGrounded && clips[(int)HeroinePose.AirDash] != null;
                    return airDash ? HeroinePose.AirDash : HeroinePose.Dash;
                case PlayerState.Attack:
                    landing = null;
                    standingUp = false;
                    if (player.IsAirAttack && clips[(int)HeroinePose.AirAttack] != null)
                        return HeroinePose.AirAttack;
                    return player.IsCrouchAttack && clips[(int)HeroinePose.CrouchAttack] != null ? HeroinePose.CrouchAttack : HeroinePose.Attack;
                case PlayerState.Crouch:
                    landing = null;
                    if (crouchTime < config.crouchDownDuration && clips[(int)HeroinePose.CrouchDown] != null)
                        return HeroinePose.CrouchDown;
                    return clips[(int)HeroinePose.CrouchIdle] != null ? HeroinePose.CrouchIdle : HeroinePose.Idle;
            }

            if (!motor.IsGrounded)
            {
                landing = null;
                standingUp = false;

                // A jump, from the ground or in the air, throws her upwards. One taken at speed is a running jump.
                bool jumped = motor.Velocity.y > 0f && (airTime == 0f || motor.Velocity.y > lastVelocityY + 1f);
                if (jumped)
                {
                    runningJump = clips[(int)HeroinePose.RunJump] != null && Mathf.Abs(motor.Velocity.x) >= config.runJumpMinSpeed;
                    restartPose = airTime > 0f;
                    airTime = 0f;
                }
                else if (airTime == 0f)
                {
                    runningJump = false; // walked off a ledge
                }

                airTime += dt;
                if (runningJump)
                    return HeroinePose.RunJump;

                float startLength = clips[(int)HeroinePose.JumpStart] != null ? clips[(int)HeroinePose.JumpStart].length : 0f;
                bool rising = motor.Velocity.y > 0f;
                return rising && airTime < startLength ? HeroinePose.JumpStart : HeroinePose.JumpLoop;
            }

            bool landed = airTime >= config.landMinAirTime;
            airTime = 0f;
            if (landed)
            {
                Vector2 range = config.landDipSpeedRange;
                landDipStrength = Mathf.Clamp01((-lastVelocityY - range.x) / Mathf.Max(0.01f, range.y - range.x));
                landDipTime = 0f;
            }
            if (Turning())
            {
                landing = null;
                return HeroinePose.Turn;
            }

            bool moving = Mathf.Abs(motor.Velocity.x) > MovingSpeed;
            if (landed)
            {
                HeroinePose land = moving ? HeroinePose.LandRun : HeroinePose.LandIdle;
                landing = clips[(int)land] != null ? land : (HeroinePose?)null;
            }

            if (landing.HasValue)
            {
                // A landing at a run gives way to the run; one at a standstill gives way as soon as she moves.
                bool runLanding = landing == HeroinePose.LandRun;
                float duration = runLanding ? config.landRunDuration : config.landIdleDuration;
                bool done = CurrentPose == landing && poseTime >= duration;
                if (!done && moving == runLanding)
                    return landing.Value;
                landing = null;
            }

            if (standingUp)
            {
                bool done = CurrentPose == HeroinePose.CrouchUp && poseTime >= config.crouchUpDuration;
                if (!done && !moving && clips[(int)HeroinePose.CrouchUp] != null)
                    return HeroinePose.CrouchUp;
                standingUp = false;
            }

            return moving ? HeroinePose.Run : HeroinePose.Idle;
        }

        /// <summary>The turn plays while the controller is in a skid turn; gameplay decides when one starts and ends.</summary>
        private bool Turning()
        {
            return clips[(int)HeroinePose.Turn] != null && player.IsTurning;
        }

        private ClipWindow AttackWindowFor(int comboIndex)
        {
            ClipWindow[] perStep = config.comboAttacks;
            ClipWindow window = perStep != null && perStep.Length > 0 ? perStep[Mathf.Clamp(comboIndex, 0, perStep.Length - 1)] : default;
            return window.clip != null ? window : new ClipWindow { clip = config.attack };
        }

        /// <summary>Swaps the clip behind the attack pose, so each combo step can show its own animation.</summary>
        private void UseAttackClip(AnimationClip clip)
        {
            const int slot = (int)HeroinePose.Attack;
            if (clip == null || clip == clips[slot])
                return;

            if (playables[slot].IsValid())
            {
                graph.Disconnect(mixer, slot);
                playables[slot].Destroy();
            }

            clips[slot] = clip;
            playables[slot] = AnimationClipPlayable.Create(graph, clip);
            playables[slot].SetSpeed(0.0);
            graph.Connect(playables[slot], 0, mixer, slot);
            mixer.SetInputWeight(slot, weights[slot]);
        }

        private bool NewAttackStarted()
        {
            return player.State == PlayerState.Attack
                && (lastState != PlayerState.Attack || player.ComboIndex != lastComboIndex
                    || player.IsCrouchAttack != lastCrouchAttack || player.IsAirAttack != lastAirAttack || player.IsCounter != lastCounter
                    || player.StateTime <= Time.deltaTime * 1.001f);
        }

        private static bool IsWindowed(HeroinePose pose)
        {
            return pose == HeroinePose.Attack || pose == HeroinePose.CrouchAttack || pose == HeroinePose.AirAttack
                || pose == HeroinePose.LandRun || pose == HeroinePose.LandIdle
                || pose == HeroinePose.CrouchDown || pose == HeroinePose.CrouchUp || pose == HeroinePose.Hurt
                || pose == HeroinePose.Dead || pose == HeroinePose.Dodge || pose == HeroinePose.Parry
                || pose == HeroinePose.GuardHit || pose == HeroinePose.LedgeGrab || pose == HeroinePose.Climb;
        }

        private float GuardHitDuration => player.Combat != null ? Mathf.Max(0.2f, player.Combat.blockStun * 1.5f) : 0.35f;

        /// <summary>Clip time for a windowed pose.</summary>
        private float WindowTime(HeroinePose pose)
        {
            switch (pose)
            {
                case HeroinePose.Attack:
                    return StrikeTime(attackWindow, player.CurrentStep, player.StateTime);
                case HeroinePose.CrouchAttack:
                    return StrikeTime(config.crouchAttack, player.CurrentStep, player.StateTime);
                case HeroinePose.AirAttack:
                    return StrikeTime(config.airAttack, player.CurrentStep, player.StateTime);
                case HeroinePose.LandRun:
                    return config.landRun.At(poseTime / config.landRunDuration);
                case HeroinePose.LandIdle:
                    return config.landIdle.At(poseTime / config.landIdleDuration);
                case HeroinePose.CrouchDown:
                    return config.crouchDown.At(poseTime / config.crouchDownDuration);
                case HeroinePose.CrouchUp:
                    return config.crouchUp.At(poseTime / config.crouchUpDuration);
                case HeroinePose.Hurt:
                    return config.hurt.At(player.StateTime / Mathf.Max(0.01f, player.HurtDuration));
                case HeroinePose.Dead:
                    return config.death.At(player.StateTime / config.deathDuration);
                case HeroinePose.Dodge:
                    return config.dodge.At(player.StateTime / Mathf.Max(0.01f, player.Move.dodgeDuration));
                case HeroinePose.Parry:
                    return config.parry.At(player.GuardReactionTime / config.parryDuration);
                case HeroinePose.GuardHit:
                    return config.guardHit.At(player.GuardReactionTime / GuardHitDuration);
                case HeroinePose.LedgeGrab:
                    return config.ledgeGrab.At(player.StateTime / player.Move.grabTime);
                case HeroinePose.Climb:
                    return config.ledgeClimb.At(player.StateTime / Mathf.Max(0.01f, player.Move.climbDuration));
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Plays a strike's window over the attack, with the blow landing in the middle of the hitbox's active time:
        /// the wind-up is fitted to the attack's startup and the follow-through to the rest.
        /// </summary>
        private static float StrikeTime(ClipWindow window, AttackStep step, float t)
        {
            if (!window.HasImpact)
                return window.At(t / Mathf.Max(0.01f, step.Total));

            float hit = step.startup + step.active * 0.5f;
            if (t < hit)
                return Mathf.Lerp(window.from, window.impact, t / Mathf.Max(0.01f, hit));
            return Mathf.Lerp(window.impact, window.End, (t - hit) / Mathf.Max(0.01f, step.Total - hit));
        }

        private bool IsLooping(HeroinePose pose)
        {
            return pose == HeroinePose.Idle || pose == HeroinePose.Run || pose == HeroinePose.JumpLoop
                || pose == HeroinePose.CrouchIdle || pose == HeroinePose.Guard || pose == HeroinePose.Hang
                || (IsDash(pose) && !config.stretchDashToDuration);
        }

        private static bool IsDash(HeroinePose pose) => pose == HeroinePose.Dash || pose == HeroinePose.AirDash;

        private float ClipSpeed(HeroinePose pose)
        {
            AnimationClip clip = clips[(int)pose];
            switch (pose)
            {
                case HeroinePose.Run:
                {
                    float scale = Mathf.Abs(motor.Velocity.x) / Mathf.Max(0.1f, config.runClipSpeed);
                    return Mathf.Clamp(scale, config.runSpeedScaleRange.x, config.runSpeedScaleRange.y);
                }
                case HeroinePose.Dash:
                case HeroinePose.AirDash:
                    return config.stretchDashToDuration ? StretchTo(clip, player.Move.dashDuration) : config.dashClipSpeed;
                case HeroinePose.Turn:
                    return StretchTo(clip, player.Move.turnDuration);
                default:
                    return 1f;
            }
        }

        private static float StretchTo(AnimationClip clip, float seconds)
        {
            return seconds > 0.01f ? clip.length / seconds : 1f;
        }
    }
}
