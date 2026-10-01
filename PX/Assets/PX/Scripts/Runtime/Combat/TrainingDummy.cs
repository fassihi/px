using UnityEngine;

namespace PX
{
    /// <summary>
    /// Something to hit while tuning combat. Flashes and recoils when struck,
    /// falls over when its health runs out, and stands back up a moment later.
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

        private Health health;
        private MaterialPropertyBlock block;
        private Color baseColor;
        private Quaternion restRotation;
        private Vector3 recoilAxis;
        private float flash;
        private float respawnTimer;

        private void Awake()
        {
            health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
            restRotation = transform.rotation;
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
            // Tip away from the blow: rotate around the horizontal axis perpendicular to it.
            recoilAxis = Vector3.Cross(Vector3.up, hit.Direction);
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

            float angle = health.IsDead ? 85f : recoilAngle * flash;
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
