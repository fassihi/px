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
    }

    /// <summary>
    /// Plays the heroine's animation from what <see cref="PlayerController"/> and <see cref="CharacterMotor"/> report.
    /// Gameplay never reads from here: animation follows the simulation, not the other way round.
    /// Clip time is set by hand: strikes follow the attack's own timing, so the blow lands when the hitbox is live,
    /// and landings and crouch transitions play a window of their clip over a set time.
    /// </summary>
    public sealed class HeroineAnimator : MonoBehaviour
    {
        private const int PoseCount = 15;

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
        private readonly double[] times = new double[PoseCount];

        private float airTime;
        private float poseTime;
        private float crouchTime;
        private PlayerState lastState;
        private int lastComboIndex;
        private bool lastCrouchAttack;
        private int lastFacing;
        private float lastSpeed;
        private float lastVelocityY;
        private bool runningJump;
        private bool airDash;
        private bool restartPose;
        private bool standingUp;
        private HeroinePose? landing;
        private ClipWindow attackWindow;
        private Quaternion modelRotation;

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
            modelRotation = animator.transform.localRotation;
            lastFacing = motor != null ? motor.Facing : 1;

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
            MeasureCrouch();
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

        /// <summary>Poses her in the crouch idle once, to learn how high her body is there.</summary>
        private void MeasureCrouch()
        {
            const int slot = (int)HeroinePose.CrouchIdle;
            if (clips[slot] == null)
                return;

            mixer.SetInputWeight(slot, 1f);
            bodyCap.SetJobData(new BodyHeightJob { measure = true, measured = measuredBody });
            graph.Evaluate(0f);
            mixer.SetInputWeight(slot, 0f);
            CrouchAttackCeiling = measuredBody[0] * (1f + config.crouchAttackRise);
            bodyCap.SetJobData(new BodyHeightJob { ceiling = CrouchAttackCeiling, measured = measuredBody });
        }

        private void LateUpdate()
        {
            if (!graph.IsValid() || player == null || motor == null)
                return;

            float dt = Time.deltaTime;
            HeroinePose pose = ChoosePose(dt);

            bool attackStarted = NewAttackStarted();
            if (attackStarted && !player.IsCrouchAttack)
            {
                attackWindow = AttackWindowFor(player.ComboIndex);
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
            lastFacing = motor.Facing;
            lastSpeed = Mathf.Abs(motor.Velocity.x);
            lastVelocityY = motor.Velocity.y;

            float fadeTime = IsDash(pose) ? config.dashFadeTime : config.fadeTime;
            float fade = fadeTime > 0f ? dt / fadeTime : 1f;
            for (int i = 0; i < PoseCount; i++)
            {
                if (clips[i] == null)
                    continue;

                weights[i] = Mathf.MoveTowards(weights[i], i == (int)pose ? 1f : 0f, cut ? 1f : fade);
                mixer.SetInputWeight(i, weights[i]);

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

            bodyCap.SetJobData(new BodyHeightJob
            {
                weight = weights[(int)HeroinePose.CrouchAttack], ceiling = CrouchAttackCeiling, measured = measuredBody,
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

        /// <summary>Starts the turn when she reverses at speed, and keeps it until the clip ends or she turns back.</summary>
        private bool Turning()
        {
            AnimationClip clip = clips[(int)HeroinePose.Turn];
            if (clip == null || motor.Facing != lastFacing && CurrentPose == HeroinePose.Turn)
                return false;
            if (motor.Facing != lastFacing)
                return lastSpeed >= config.turnMinSpeed;
            return CurrentPose == HeroinePose.Turn && times[(int)HeroinePose.Turn] < clip.length;
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
                    || player.IsCrouchAttack != lastCrouchAttack || player.StateTime <= Time.deltaTime * 1.001f);
        }

        private static bool IsWindowed(HeroinePose pose)
        {
            return pose == HeroinePose.Attack || pose == HeroinePose.CrouchAttack
                || pose == HeroinePose.LandRun || pose == HeroinePose.LandIdle
                || pose == HeroinePose.CrouchDown || pose == HeroinePose.CrouchUp;
        }

        /// <summary>Clip time for a windowed pose.</summary>
        private float WindowTime(HeroinePose pose)
        {
            switch (pose)
            {
                case HeroinePose.Attack:
                    return StrikeTime(attackWindow, player.CurrentStep, player.StateTime);
                case HeroinePose.CrouchAttack:
                    return StrikeTime(config.crouchAttack, player.CurrentStep, player.StateTime);
                case HeroinePose.LandRun:
                    return config.landRun.At(poseTime / config.landRunDuration);
                case HeroinePose.LandIdle:
                    return config.landIdle.At(poseTime / config.landIdleDuration);
                case HeroinePose.CrouchDown:
                    return config.crouchDown.At(poseTime / config.crouchDownDuration);
                case HeroinePose.CrouchUp:
                    return config.crouchUp.At(poseTime / config.crouchUpDuration);
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
                || pose == HeroinePose.CrouchIdle || (IsDash(pose) && !config.stretchDashToDuration);
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
                    return StretchTo(clip, config.turnDuration);
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
