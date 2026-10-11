using UnityEngine;

namespace PX
{
    /// <summary>
    /// Something to hit while tuning combat. Flashes, squashes, tips and slides back when struck (harder blows push
    /// further), falls over when its health runs out, and stands back up and returns to its spot a moment later.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer body;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField, Min(0.01f)] private float flashTime = 0.12f;
        [SerializeField, Min(0f)] private float recoilAngle = 14f;
        [SerializeField, Min(0f)] private float respawnDelay = 1.5f;
        [Tooltip("How quickly a knocked-back dummy stops sliding, in m/s per second.")]
        [SerializeField, Min(0f)] private float knockbackFriction = 30f;
        [Tooltip("Seconds for a knocked-back dummy to drift back to its spot once it stops.")]
        [SerializeField, Min(0.01f)] private float returnTime = 0.8f;
        [Tooltip("How much a blow squashes it for an instant, as a fraction of its height.")]
        [SerializeField, Min(0f)] private float squash = 0.12f;

        private Health health;
        private MaterialPropertyBlock block;
        private Color baseColor;
        private Quaternion restRotation;
        private Vector3 restPosition;
        private Vector3 restScale;
        private Vector3 knockbackVelocity;
        private float impact;
        private float tip;
        private Vector3 recoilAxis;
        private float flash;
        private float respawnTimer;

        private void Awake()
        {
            health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
            restRotation = transform.rotation;
            restPosition = transform.position;
            restScale = transform.localScale;
            if (body != null)
                baseColor = body.sharedMaterial.GetColor(BaseColorId);
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

        private void OnDamaged(Hit hit)
        {
            flash = 1f;
            impact = 1f;
            // Tip away from the blow: rotate around the horizontal axis perpendicular to it. Harder blows tip it further.
            recoilAxis = Vector3.Cross(Vector3.up, hit.Direction);
            tip = recoilAngle * (1f + hit.Knockback * 0.25f);
            Vector3 push = Vector3.ProjectOnPlane(hit.Direction, Vector3.up).normalized * hit.Knockback;
            knockbackVelocity += push;
        }

        private void OnDied()
        {
            respawnTimer = respawnDelay;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (health.IsDead)
            {
                respawnTimer -= dt;
                if (respawnTimer <= 0f)
                    health.Restore();
            }

            flash = Mathf.MoveTowards(flash, 0f, dt / flashTime);
            impact = Mathf.MoveTowards(impact, 0f, dt / flashTime);

            // Slide with the blow, then drift back to the spot.
            transform.position += knockbackVelocity * dt;
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackFriction * dt);
            if (knockbackVelocity == Vector3.zero)
                transform.position = Vector3.Lerp(transform.position, restPosition, 1f - Mathf.Exp(-3f * dt / returnTime));

            float s = squash * impact;
            transform.localScale = Vector3.Scale(restScale, new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.5f));

            float angle = health.IsDead ? 85f : tip * flash;
            Quaternion target = Quaternion.AngleAxis(angle, recoilAxis == Vector3.zero ? Vector3.right : recoilAxis) * restRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-30f * dt));

            if (body != null)
            {
                block.SetColor(BaseColorId, Color.Lerp(baseColor, flashColor, flash));
                body.SetPropertyBlock(block);
            }
        }
    }
}
