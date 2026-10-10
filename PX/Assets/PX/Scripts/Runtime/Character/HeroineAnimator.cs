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
    }

    /// <summary>
    /// Plays the heroine's animation from what <see cref="PlayerController"/> and <see cref="CharacterMotor"/> report.
    /// Gameplay never reads from here: animation follows the simulation, not the other way round.
    /// Clip time is advanced by hand so attack and dash clips can be stretched to the length of the move.
    /// </summary>
    public sealed class HeroineAnimator : MonoBehaviour
    {
        private const int PoseCount = 8;

        [SerializeField] private PlayerController player;
        [SerializeField] private CharacterMotor motor;
        [SerializeField] private Animator animator;
        [SerializeField] private HeroineAnimationConfig config;

        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[PoseCount];
        private readonly AnimationClip[] clips = new AnimationClip[PoseCount];
        private readonly float[] weights = new float[PoseCount];
        private readonly double[] times = new double[PoseCount];

        private float airTime;
        private PlayerState lastState;
        private int lastComboIndex;
        private int lastFacing;
        private float lastSpeed;
        private float lastVelocityY;
        private bool runningJump;
        private bool restartPose;
        private Quaternion modelRotation;

        public HeroinePose CurrentPose { get; private set; }

        public HeroineAnimationConfig Config => config;

        /// <summary>The clip behind the attack pose right now, which changes with the combo step.</summary>
        public AnimationClip AttackClip => clips[(int)HeroinePose.Attack];

        private void OnEnable()
        {
            if (animator == null || config == null)
                return;

            clips[(int)HeroinePose.Idle] = config.idle;
            clips[(int)HeroinePose.Run] = config.run;
            clips[(int)HeroinePose.JumpStart] = config.jumpStart;
            clips[(int)HeroinePose.JumpLoop] = config.jumpLoop;
            clips[(int)HeroinePose.Dash] = config.dash;
            clips[(int)HeroinePose.Attack] = AttackClipFor(0);
            clips[(int)HeroinePose.Turn] = config.runTurn;
            clips[(int)HeroinePose.RunJump] = config.runJump;
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

            AnimationPlayableOutput.Create(graph, "Heroine", animator).SetSourcePlayable(mixer);
            graph.Play();

            CurrentPose = HeroinePose.Idle;
            weights[(int)HeroinePose.Idle] = 1f;
        }

        private void OnDisable()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        private void LateUpdate()
        {
            if (!graph.IsValid() || player == null || motor == null)
                return;

            float dt = Time.deltaTime;
            HeroinePose pose = ChoosePose(dt);

            bool attackStarted = NewAttackStarted();
            if (attackStarted)
                UseAttackClip(AttackClipFor(player.ComboIndex));

            if (pose != CurrentPose || attackStarted || restartPose)
                times[(int)pose] = pose == HeroinePose.RunJump ? config.runJumpTakeoff : 0.0;

            // The turn clip spins her round, so the body is turned back to face where she came from while it plays.
            // That cannot be blended with other poses, so the turn cuts in and out.
            bool cut = (pose == HeroinePose.Turn) != (CurrentPose == HeroinePose.Turn);
            animator.transform.localRotation = pose == HeroinePose.Turn ? Quaternion.Euler(0f, 180f, 0f) * modelRotation : modelRotation;

            CurrentPose = pose;
            lastState = player.State;
            lastComboIndex = player.ComboIndex;
            lastFacing = motor.Facing;
            lastSpeed = Mathf.Abs(motor.Velocity.x);
            lastVelocityY = motor.Velocity.y;

            float fade = config.fadeTime > 0f ? dt / config.fadeTime : 1f;
            for (int i = 0; i < PoseCount; i++)
            {
                if (clips[i] == null)
                    continue;

                weights[i] = Mathf.MoveTowards(weights[i], i == (int)pose ? 1f : 0f, cut ? 1f : fade);
                mixer.SetInputWeight(i, weights[i]);

                if (weights[i] <= 0f && i != (int)pose)
                    continue;

                times[i] += dt * ClipSpeed((HeroinePose)i);
                double length = clips[i].length;
                if (i == (int)HeroinePose.RunJump)
                    length = System.Math.Min(length, config.runJumpLanding);
                times[i] = IsLooping((HeroinePose)i) && length > 0.0 ? times[i] % length : System.Math.Min(times[i], length);
                playables[i].SetTime(times[i]);
            }
        }

        private HeroinePose ChoosePose(float dt)
        {
            restartPose = false;
            switch (player.State)
            {
                case PlayerState.Dash:
                    return HeroinePose.Dash;
                case PlayerState.Attack:
                    return HeroinePose.Attack;
            }

            if (!motor.IsGrounded)
            {
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

            airTime = 0f;
            if (Turning())
                return HeroinePose.Turn;
            return Mathf.Abs(motor.Velocity.x) > 0.3f ? HeroinePose.Run : HeroinePose.Idle;
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

        private AnimationClip AttackClipFor(int comboIndex)
        {
            AnimationClip[] perStep = config.comboAttacks;
            AnimationClip clip = perStep != null && perStep.Length > 0 ? perStep[Mathf.Clamp(comboIndex, 0, perStep.Length - 1)] : null;
            return clip != null ? clip : config.attack;
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
                && (lastState != PlayerState.Attack || player.ComboIndex != lastComboIndex);
        }

        private bool IsLooping(HeroinePose pose)
        {
            return pose == HeroinePose.Idle || pose == HeroinePose.Run || pose == HeroinePose.JumpLoop
                || (pose == HeroinePose.Dash && !config.stretchDashToDuration);
        }

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
                    return config.stretchDashToDuration ? StretchTo(clip, player.Move.dashDuration) : config.dashClipSpeed;
                case HeroinePose.Attack:
                    return StretchTo(clip, player.Combo.steps[Mathf.Clamp(player.ComboIndex, 0, player.Combo.steps.Length - 1)].Total);
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
