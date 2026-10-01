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
    }

    /// <summary>
    /// Plays the heroine's animation from what <see cref="PlayerController"/> and <see cref="CharacterMotor"/> report.
    /// Gameplay never reads from here: animation follows the simulation, not the other way round.
    /// Clip time is advanced by hand so attack and dash clips can be stretched to the length of the move.
    /// </summary>
    public sealed class HeroineAnimator : MonoBehaviour
    {
        private const int PoseCount = 6;

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

        public HeroinePose CurrentPose { get; private set; }

        private void OnEnable()
        {
            if (animator == null || config == null)
                return;

            clips[(int)HeroinePose.Idle] = config.idle;
            clips[(int)HeroinePose.Run] = config.run;
            clips[(int)HeroinePose.JumpStart] = config.jumpStart;
            clips[(int)HeroinePose.JumpLoop] = config.jumpLoop;
            clips[(int)HeroinePose.Dash] = config.dash;
            clips[(int)HeroinePose.Attack] = config.attack;

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

            if (pose != CurrentPose || NewAttackStarted())
                times[(int)pose] = 0.0;
            CurrentPose = pose;
            lastState = player.State;
            lastComboIndex = player.ComboIndex;

            float fade = config.fadeTime > 0f ? dt / config.fadeTime : 1f;
            for (int i = 0; i < PoseCount; i++)
            {
                if (clips[i] == null)
                    continue;

                weights[i] = Mathf.MoveTowards(weights[i], i == (int)pose ? 1f : 0f, fade);
                mixer.SetInputWeight(i, weights[i]);

                if (weights[i] <= 0f && i != (int)pose)
                    continue;

                times[i] += dt * ClipSpeed((HeroinePose)i);
                double length = clips[i].length;
                times[i] = IsLooping((HeroinePose)i) && length > 0.0 ? times[i] % length : System.Math.Min(times[i], length);
                playables[i].SetTime(times[i]);
            }
        }

        private HeroinePose ChoosePose(float dt)
        {
            switch (player.State)
            {
                case PlayerState.Dash:
                    return HeroinePose.Dash;
                case PlayerState.Attack:
                    return HeroinePose.Attack;
            }

            if (!motor.IsGrounded)
            {
                airTime += dt;
                float startLength = clips[(int)HeroinePose.JumpStart] != null ? clips[(int)HeroinePose.JumpStart].length : 0f;
                bool rising = motor.Velocity.y > 0f;
                return rising && airTime < startLength ? HeroinePose.JumpStart : HeroinePose.JumpLoop;
            }

            airTime = 0f;
            return Mathf.Abs(motor.Velocity.x) > 0.3f ? HeroinePose.Run : HeroinePose.Idle;
        }

        private bool NewAttackStarted()
        {
            return player.State == PlayerState.Attack
                && (lastState != PlayerState.Attack || player.ComboIndex != lastComboIndex);
        }

        private static bool IsLooping(HeroinePose pose)
        {
            return pose == HeroinePose.Idle || pose == HeroinePose.Run || pose == HeroinePose.JumpLoop;
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
                    return StretchTo(clip, player.Move.dashDuration);
                case HeroinePose.Attack:
                    return StretchTo(clip, player.Combo.steps[Mathf.Clamp(player.ComboIndex, 0, player.Combo.steps.Length - 1)].Total);
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
