using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PX
{
    /// <summary>
    /// Plays an enemy's animation from what its <see cref="EnemyController"/> reports, the way
    /// <see cref="HeroineAnimator"/> does for her: clip time follows the simulation, so the swing's blade is out
    /// when its hitbox is live, and a stagger plays over exactly the stun. On top of the clips it lays a procedural
    /// layer, because the clips alone play stiff: the body leans into a run and swings back past upright when it
    /// stops (a spring, so it overshoots), the pelvis sways with each stride, the head stays level and on her, it
    /// swivels round when it turns instead of snapping, and a climb is a real jump: a crouch, a leap and a landing.
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
            Run,
        }

        private const int PoseCount = 10;

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
        private bool wasRagdoll;

        // The procedural layer.
        private Transform hips, spine, chest, neck, head;
        private float lean, leanVelocity;
        private float yawOffset;
        private int lastFacing = int.MinValue;
        private float hipsDrop, hipsDropVelocity;
        private float swayPhase;

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
            clips[(int)Pose.Run] = config.run;

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
            hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            spine = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            chest = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
            neck = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Neck) : null;
            head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (chest == null)
                chest = spine;
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

            // A ragdoll has the body: the animation is off until it is revived, and then starts from the new pose.
            if (enemy.IsRagdoll)
            {
                wasRagdoll = true;
                lean = leanVelocity = hipsDrop = hipsDropVelocity = yawOffset = 0f;
                lastFacing = int.MinValue;
                return;
            }

            float dt = Time.deltaTime;
            EnemyConfig config = enemy.Config;
            Pose pose = PoseFor(enemy.State);
            if (wasRagdoll)
            {
                wasRagdoll = false;
                for (int i = 0; i < PoseCount; i++)
                    weights[i] = i == (int)pose ? 1f : 0f;
            }
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

            ApplyProceduralLayer(dt);
        }

        /// <summary>
        /// Adjusts the bones the clips have just posed. The lean is a spring that chases a target set by how fast the
        /// enemy is going forward, so it leans in as it speeds up, and rocks back upright with a little overshoot when
        /// it stops or is thrown back.
        /// </summary>
        private void ApplyProceduralLayer(float dt)
        {
            if (hips == null || dt <= 0f || enemy.State == EnemyState.Dead)
                return;

            int facing = enemy.Motor.Facing;
            EnemyConfig config = enemy.Config;

            // Turning: the body is already turned round, so start it pointing the old way and swing it back.
            if (lastFacing != int.MinValue && facing != lastFacing)
                yawOffset = 180f * facing;
            lastFacing = facing;
            yawOffset = Mathf.MoveTowards(yawOffset, 0f, config.turnSpeed * dt);

            float forward = enemy.Motor.Velocity.x * facing;
            float target = 0f;
            float dropTarget = 0f;
            float weight = 1f;
            switch (enemy.State)
            {
                case EnemyState.Run:
                    target = Mathf.Clamp(forward * config.runLean, 0f, 22f);
                    break;
                case EnemyState.Walk:
                    target = Mathf.Clamp(forward * config.runLean * 0.5f, 0f, 10f);
                    break;
                case EnemyState.WalkBack:
                    target = Mathf.Clamp(forward * config.runLean, -8f, 0f);
                    break;
                case EnemyState.Attack:
                    // The swing's lunge pitches it into the blow; the clip keeps the rest.
                    target = Mathf.Clamp(forward * config.runLean * 0.6f, 0f, 12f);
                    weight = 0.7f;
                    break;
                case EnemyState.Hurt:
                case EnemyState.Parried:
                    // Knocked back, it rocks away from the blow and recovers.
                    target = Mathf.Clamp(forward * config.runLean * 0.8f, -14f, 0f);
                    break;
                case EnemyState.Climb:
                    ClimbPose(config, out target, out dropTarget);
                    break;
            }

            // Underdamped spring: stiff enough to follow, loose enough to overshoot.
            float stiffness = config.leanStiffness;
            float damping = 2f * 0.45f * Mathf.Sqrt(stiffness);
            leanVelocity += ((target - lean) * stiffness - leanVelocity * damping) * dt;
            lean += leanVelocity * dt;
            hipsDropVelocity += ((dropTarget - hipsDrop) * stiffness * 2f - hipsDropVelocity * damping * 1.4f) * dt;
            hipsDrop += hipsDropVelocity * dt;

            // Each stride swings the pelvis one way and the shoulders the other.
            float sway = 0f;
            if (enemy.State == EnemyState.Walk || enemy.State == EnemyState.Run || enemy.State == EnemyState.WalkBack)
            {
                AnimationClip clip = clips[(int)current];
                if (clip != null && clip.length > 0f)
                    swayPhase = (float)(times[(int)current] / clip.length);
                float strength = Mathf.Clamp01(Mathf.Abs(forward) / config.runClipSpeed);
                sway = Mathf.Sin(swayPhase * Mathf.PI * 4f) * config.stride * strength;
            }

            Vector3 right = transform.right;
            Vector3 fwd = transform.forward;
            float leaned = lean * weight;
            if (Mathf.Abs(yawOffset) > 0.01f)
                hips.rotation = Quaternion.AngleAxis(yawOffset, Vector3.up) * hips.rotation;
            hips.position += Vector3.down * hipsDrop;
            hips.rotation = Quaternion.AngleAxis(sway, fwd) * hips.rotation;
            if (spine != null)
                spine.rotation = Quaternion.AngleAxis(leaned * 0.4f - sway * 0.5f, right) * spine.rotation;
            if (chest != null && chest != spine)
                chest.rotation = Quaternion.AngleAxis(leaned * 0.6f - sway * 0.5f, right) * chest.rotation;
            else if (chest != null)
                chest.rotation = Quaternion.AngleAxis(leaned * 0.2f, right) * chest.rotation;
            // The head stays up and on its target while the body pitches.
            Transform level = head ?? neck;
            if (level != null)
                level.rotation = Quaternion.AngleAxis(-leaned * 0.7f + sway * 0.4f, right) * level.rotation;
        }

        /// <summary>
        /// The weight added to the jump over a climb: the hips sink into the crouch before the leap and take the drop
        /// on landing.
        /// </summary>
        private void ClimbPose(EnemyConfig config, out float lean, out float drop)
        {
            float t = Mathf.Clamp01(enemy.StateTime / Mathf.Max(0.1f, config.climbDuration));
            float crouch = Mathf.Sin(Mathf.Clamp01(t / EnemyController.ClimbCrouchEnd) * Mathf.PI);
                        float land = t >= EnemyController.ClimbLandStart
                ? Mathf.Sin(Mathf.Clamp01((t - EnemyController.ClimbLandStart) / (1f - EnemyController.ClimbLandStart)) * Mathf.PI)
                : 0f;
            // The Mixamo jump carries its own lean and stretch; this only adds a little weight to the crouch and landing.
            lean = 4f * crouch + 3f * land;
            drop = config.crouchDepth * (crouch + 0.7f * land);
        }

        private Pose PoseFor(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Walk: return clips[(int)Pose.Walk] != null ? Pose.Walk : Pose.Idle;
                case EnemyState.Run:
                    return clips[(int)Pose.Run] != null ? Pose.Run : clips[(int)Pose.Walk] != null ? Pose.Walk : Pose.Idle;
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
                case Pose.Run:
                {
                    float speed = Mathf.Abs(enemy.Motor.Velocity.x) / config.runClipSpeed;
                    return (time + dt * Mathf.Clamp(speed, 0.5f, 1.6f)) % clip.length;
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
