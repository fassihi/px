using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PX
{
    /// <summary>
    /// Plays an enemy's animation from what its <see cref="EnemyController"/> reports, the way
    /// <see cref="HeroineAnimator"/> does for her: clip time follows the simulation, so the swing's blade is out
    /// when its hitbox is live, and a stagger plays over exactly the stun.
    /// </summary>
    public sealed class EnemyAnimator : MonoBehaviour
    {
        private enum Pose
        {
            Idle,
            Walk,
            WalkBack,
            Attack,
            Attack2,
            Hurt,
            Parried,
            Climb,
            Dead,
        }

        private const int PoseCount = 9;

        [SerializeField] private EnemyController enemy;
        [SerializeField] private Animator animator;

        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[PoseCount];
        private readonly AnimationClip[] clips = new AnimationClip[PoseCount];
        private readonly float[] weights = new float[PoseCount];
        private readonly double[] times = new double[PoseCount];
        private Pose current;
        private EnemyState lastState;
        private int lastCombo;

        private void OnEnable()
        {
            if (animator == null || enemy == null || enemy.Config == null)
                return;

            EnemyConfig config = enemy.Config;
            clips[(int)Pose.Idle] = config.idle;
            clips[(int)Pose.Walk] = config.walk;
            clips[(int)Pose.WalkBack] = config.walkBack;
            clips[(int)Pose.Attack] = config.attackClip.clip;
            clips[(int)Pose.Attack2] = config.attack2Clip.clip;
            clips[(int)Pose.Hurt] = config.hurt.clip;
            clips[(int)Pose.Parried] = config.parried.clip;
            clips[(int)Pose.Climb] = config.climb.clip;
            clips[(int)Pose.Dead] = config.death;

            graph = PlayableGraph.Create("Enemy");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, PoseCount);
            for (int i = 0; i < PoseCount; i++)
            {
                if (clips[i] == null)
                    continue;
                playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                playables[i].SetSpeed(0.0); // time is set by hand in LateUpdate
                graph.Connect(playables[i], 0, mixer, i);
            }
            AnimationPlayableOutput.Create(graph, "Enemy", animator).SetSourcePlayable(mixer);
            graph.Play();
            weights[(int)Pose.Idle] = 1f;
            mixer.SetInputWeight((int)Pose.Idle, 1f);
        }

        private void OnDisable()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        private void LateUpdate()
        {
            if (!graph.IsValid())
                return;

            float dt = Time.deltaTime;
            EnemyConfig config = enemy.Config;
            Pose pose = PoseFor(enemy.State);
            if (pose != current || enemy.State != lastState || enemy.ComboIndex != lastCombo)
                times[(int)pose] = 0.0;
            current = pose;
            lastState = enemy.State;
            lastCombo = enemy.ComboIndex;

            // Eased, normalised cross-fade, as for the heroine.
            float fade = config.fadeTime > 0f ? dt / config.fadeTime : 1f;
            float total = 0f;
            var applied = new float[PoseCount];
            for (int i = 0; i < PoseCount; i++)
            {
                if (clips[i] == null)
                    continue;
                weights[i] = Mathf.MoveTowards(weights[i], i == (int)pose ? 1f : 0f, fade);
                applied[i] = Mathf.SmoothStep(0f, 1f, weights[i]);
                total += applied[i];
            }

            for (int i = 0; i < PoseCount; i++)
            {
                if (clips[i] == null)
                    continue;
                mixer.SetInputWeight(i, total > 0f ? applied[i] / total : 0f);
                if (i == (int)pose)
                    times[i] = TimeFor(pose, times[i], dt);
                playables[i].SetTime(times[i]);
            }
        }

        private Pose PoseFor(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Walk: return clips[(int)Pose.Walk] != null ? Pose.Walk : Pose.Idle;
                case EnemyState.WalkBack:
                    return clips[(int)Pose.WalkBack] != null ? Pose.WalkBack : clips[(int)Pose.Walk] != null ? Pose.Walk : Pose.Idle;
                case EnemyState.Attack:
                    return enemy.ComboIndex == 1 && clips[(int)Pose.Attack2] != null ? Pose.Attack2 : Pose.Attack;
                case EnemyState.Hurt: return Pose.Hurt;
                case EnemyState.Parried: return clips[(int)Pose.Parried] != null ? Pose.Parried : Pose.Hurt;
                case EnemyState.Climb: return clips[(int)Pose.Climb] != null ? Pose.Climb : Pose.Idle;
                case EnemyState.Dead: return Pose.Dead;
                default: return Pose.Idle;
            }
        }

        private double TimeFor(Pose pose, double time, float dt)
        {
            EnemyConfig config = enemy.Config;
            AnimationClip clip = clips[(int)pose];
            switch (pose)
            {
                case Pose.Walk:
                case Pose.WalkBack:
                {
                    float speed = Mathf.Abs(enemy.Motor.Velocity.x) / config.walkClipSpeed;
                    // Without its own clip, backing off plays the walk in reverse.
                    float direction = pose == Pose.Walk || config.walkBack != null ? 1f : -1f;
                    double next = time + direction * dt * Mathf.Clamp(speed, 0.3f, 2f);
                    return (next % clip.length + clip.length) % clip.length;
                }
                case Pose.Attack:
                    return StrikeTime(config.attackClip, config.attack, enemy.StateTime);
                case Pose.Attack2:
                    return StrikeTime(config.attack2Clip, config.attack2, enemy.StateTime);
                case Pose.Hurt:
                    return config.hurt.At(enemy.StateTime / Mathf.Max(0.01f, config.hitStun));
                case Pose.Parried:
                {
                    ClipWindow window = config.parried.clip != null ? config.parried : config.hurt;
                    return window.At(enemy.StateTime / Mathf.Max(0.01f, config.parriedStun));
                }
                case Pose.Climb:
                    return config.climb.At(enemy.StateTime / Mathf.Max(0.01f, config.climbDuration));
                case Pose.Dead:
                    return System.Math.Min(time + dt, clip.length);
                default:
                    return (time + dt) % clip.length;
            }
        }

        /// <summary>The swing's window over the attack, with the blade furthest out in the middle of the hitbox's active time.</summary>
        private static float StrikeTime(ClipWindow window, AttackStep step, float t)
        {
            if (!window.HasImpact)
                return window.At(t / Mathf.Max(0.01f, step.Total));
            float hit = step.startup + step.active * 0.5f;
            if (t < hit)
                return Mathf.Lerp(window.from, window.impact, t / Mathf.Max(0.01f, hit));
            return Mathf.Lerp(window.impact, window.End, (t - hit) / Mathf.Max(0.01f, step.Total - hit));
        }
    }
}
