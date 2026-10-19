using System;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// A floating craft, one of the ornate flying machines of the paintings: it hovers at a height along the rail,
    /// drifting between two posts, and when it sees her it turns its eye on her and fires slow bolts she can dodge,
    /// block or parry back at it. With stealth on it sees only the way it faces, like the guards, and hears blows.
    /// It has no feet and no ragdoll: killed, it drops, and bursts when it hits something.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class Sentinel : MonoBehaviour
    {
        [SerializeField] private Rail rail;
        [SerializeField] private CharacterMotor target;
        [SerializeField] private Bolt boltPrefab;
        [Tooltip("Rail distances it drifts between. The same twice: it holds its place.")]
        [SerializeField] private float patrolFrom;
        [SerializeField] private float patrolTo;
        [Tooltip("Height of its hover above the floor's zero, in metres.")]
        [SerializeField] private float height = 3f;
        [SerializeField, Min(0f)] private float driftSpeed = 1.6f;
        [Tooltip("It sees her within this distance along the rail, the way it faces.")]
        [SerializeField, Min(0f)] private float sightRange = 12f;
        [Tooltip("On: it sees only in front of it. Off: it notices her at its sight range from any side.")]
        [SerializeField] private bool stealth = true;
        [SerializeField, Min(0.2f)] private float fireInterval = 2.4f;
        [Tooltip("Seconds its eye glows before a bolt leaves it: the telegraph.")]
        [SerializeField, Min(0f)] private float aimTime = 0.6f;
        [SerializeField, Min(0f)] private float boltSpeed = 9f;
        [SerializeField, Min(0f)] private float boltDamage = 12f;
        [SerializeField, Min(0f)] private float lightValue = 20f;
        [Tooltip("The craft's visual root, turned to face her and tilted into its drift.")]
        [SerializeField] private Transform visual;
        [SerializeField] private Renderer eye;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Health health;
        private float distance;
        private int heading = 1;
        private int facing = -1;
        private float fireTimer;
        private float aimTimer = -1f;
        private bool alert;
        private float seenTime;
        private float lostTimer;
        private float fallSpeed;
        private bool burst;
        private float postDistance;
        private Vector3 postPosition;
        private bool posted;
        private MaterialPropertyBlock block;
        private Color eyeEmission;
        private float tilt;

        public Health Health => health != null ? health : health = GetComponent<Health>();

        public float Distance => distance;

        public float Height => height;

        public float LightValue => lightValue;

        /// <summary>True once it has seen her and is firing.</summary>
        public bool Alert => alert;

        /// <summary>True while its eye glows before a shot.</summary>
        public bool Aiming => aimTimer >= 0f;

        public int Facing => facing;

        public bool Stealth
        {
            get => stealth;
            set => stealth = value;
        }

        /// <summary>Raised when it fires a bolt.</summary>
        public event Action<Bolt> Fired;

        /// <summary>Raised when a dead craft bursts on the ground, with where.</summary>
        public static event Action<Vector3> AnyBurst;

        private void Awake()
        {
            health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
            if (eye != null && eye.sharedMaterial != null && eye.sharedMaterial.HasProperty(EmissionId))
                eyeEmission = eye.sharedMaterial.GetColor(EmissionId);
        }

        private void OnEnable()
        {
            health.Died += OnDied;
            Noise.Emitted += OnNoise;
        }

        private void OnDisable()
        {
            health.Died -= OnDied;
            Noise.Emitted -= OnNoise;
        }

        private void Start()
        {
            if (rail == null)
                return;
            if (!posted)
            {
                distance = rail.Project(transform.position);
                postDistance = distance;
                postPosition = transform.position;
                posted = true;
            }
            PlaceOnRail();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || rail == null)
                return;

            if (health.IsDead)
            {
                Fall(dt);
                return;
            }

            Sense(dt);
            Drift(dt);
            Aim(dt);
            PlaceOnRail();
            Pose(dt);
        }

        private void Sense(float dt)
        {
            if (target == null)
                return;
            Health targetHealth = target.GetComponent<Health>();
            if (targetHealth != null && targetHealth.IsDead)
            {
                alert = false;
                return;
            }
            float offset = target.Distance - distance;
            float along = Mathf.Abs(offset);
            bool inFront = !stealth || Mathf.Sign(offset) == facing || along < 1.5f;
            bool sees = inFront && along <= sightRange && Mathf.Abs(target.transform.position.y - transform.position.y) <= 6f;
            if (alert)
            {
                if (along > sightRange * 1.6f)
                {
                    lostTimer += dt;
                    if (lostTimer > 3f)
                        alert = false;
                }
                else
                    lostTimer = 0f;
                if (along > 0.05f)
                    facing = offset > 0f ? 1 : -1;
                return;
            }
            seenTime = sees ? seenTime + dt : Mathf.Max(0f, seenTime - dt);
            if (seenTime >= 0.4f)
            {
                alert = true;
                lostTimer = 0f;
                fireTimer = fireInterval * 0.5f;
                facing = offset > 0f ? 1 : -1;
            }
        }

        private void OnNoise(Vector3 position, float radius, GameObject source)
        {
            if (alert || health.IsDead || source == gameObject)
                return;
            if (Vector3.Distance(position, transform.position) <= Mathf.Min(radius, 10f))
            {
                // A noise turns its eye that way; it still has to see her to fire.
                float where = rail.Project(position);
                facing = where >= distance ? 1 : -1;
                seenTime = Mathf.Max(seenTime, 0.2f);
            }
        }

        private void Drift(float dt)
        {
            bool patrols = Mathf.Abs(patrolTo - patrolFrom) > 0.5f;
            if (alert && target != null)
            {
                // Keeps a little distance from her, within its range, holding its height.
                float wanted = Mathf.Clamp(target.Distance - facing * 5f, Mathf.Min(patrolFrom, patrolTo) - 3f, Mathf.Max(patrolFrom, patrolTo) + 3f);
                float step = Mathf.Clamp(wanted - distance, -driftSpeed * dt, driftSpeed * dt);
                distance += step;
                tilt = Mathf.MoveTowards(tilt, Mathf.Sign(step) * 6f, 30f * dt);
                return;
            }
            if (!patrols)
            {
                tilt = Mathf.MoveTowards(tilt, 0f, 30f * dt);
                return;
            }
            float goal = heading > 0 ? Mathf.Max(patrolFrom, patrolTo) : Mathf.Min(patrolFrom, patrolTo);
            float offset = goal - distance;
            if (Mathf.Abs(offset) < 0.2f)
            {
                heading = -heading;
                return;
            }
            float move = Mathf.Sign(offset) * driftSpeed * dt;
            distance += move;
            facing = move > 0f ? 1 : -1;
            tilt = Mathf.MoveTowards(tilt, Mathf.Sign(move) * 8f, 30f * dt);
        }

        private void Aim(float dt)
        {
            if (!alert || target == null || boltPrefab == null)
            {
                aimTimer = -1f;
                return;
            }
            if (aimTimer >= 0f)
            {
                aimTimer += dt;
                if (aimTimer >= aimTime)
                {
                    aimTimer = -1f;
                    Fire();
                }
                return;
            }
            fireTimer -= dt;
            if (fireTimer <= 0f)
            {
                fireTimer = fireInterval;
                aimTimer = 0f;
            }
        }

        private void Fire()
        {
            Vector3 muzzle = transform.position + rail.Evaluate(distance).Tangent * (facing * 1.1f);
            Bolt bolt = Instantiate(boltPrefab, muzzle, Quaternion.identity);
            // Aimed at her chest: along the rail the way it faces, dropping or rising to her height over the flight.
            float targetHeight = target.transform.position.y + 1.0f;
            float along = Mathf.Max(1f, Mathf.Abs(target.Distance - distance));
            float climb = (targetHeight - muzzle.y) / (along / boltSpeed);
            bolt.Launch(rail, distance + facing * 1.1f, muzzle.y, new Vector2(facing * boltSpeed, Mathf.Clamp(climb, -boltSpeed, boltSpeed * 0.5f)), boltDamage, gameObject);
            Fired?.Invoke(bolt);
        }

        private void PlaceOnRail()
        {
            RailPoint point = rail.Evaluate(distance);
            float bob = Mathf.Sin(Time.time * 1.7f + distance) * 0.12f;
            transform.position = new Vector3(point.Position.x, height + bob, point.Position.z);
            transform.rotation = Quaternion.LookRotation(point.Tangent * facing, Vector3.up);
        }

        private void Pose(float dt)
        {
            if (visual != null)
                visual.localRotation = Quaternion.Euler(tilt, 0f, Mathf.Sin(Time.time * 1.3f + distance) * 2f);
            if (eye == null)
                return;
            float glow = aimTimer >= 0f ? 1f + 3f * Mathf.Clamp01(aimTimer / Mathf.Max(0.01f, aimTime)) : alert ? 1.3f : 0.7f;
            eye.GetPropertyBlock(block);
            block.SetColor(EmissionId, eyeEmission * glow);
            eye.SetPropertyBlock(block);
        }

        private void OnDied()
        {
            alert = false;
            aimTimer = -1f;
            fallSpeed = 0f;
            burst = false;
        }

        /// <summary>Dead, it drops and tumbles until it meets the ground, then bursts.</summary>
        private void Fall(float dt)
        {
            if (burst)
                return;
            fallSpeed += 20f * dt;
            Vector3 next = transform.position + Vector3.down * (fallSpeed * dt);
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, fallSpeed * dt + 0.6f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform) && !(hit.collider is CharacterController))
            {
                burst = true;
                AnyBurst?.Invoke(hit.point);
                if (visual != null)
                    visual.gameObject.SetActive(false);
                foreach (Collider collider in GetComponentsInChildren<Collider>())
                    collider.enabled = false;
                return;
            }
            if (next.y < -30f)
            {
                burst = true;
                if (visual != null)
                    visual.gameObject.SetActive(false);
                return;
            }
            transform.position = next;
            if (visual != null)
                visual.localRotation *= Quaternion.Euler(140f * dt, 0f, 90f * dt);
        }

        /// <summary>Back at its post, whole and calm: what resting at a shrine does to it.</summary>
        public void Reset()
        {
            if (!posted)
                return;
            health.Restore();
            distance = postDistance;
            alert = false;
            seenTime = 0f;
            aimTimer = -1f;
            burst = false;
            fallSpeed = 0f;
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                visual.localRotation = Quaternion.identity;
            }
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                collider.enabled = true;
            PlaceOnRail();
        }

        /// <summary>Calm again at its post, if it still lives: what her death does to it.</summary>
        public void ReturnToPost()
        {
            if (health.IsDead)
                return;
            Reset();
        }
    }
}
