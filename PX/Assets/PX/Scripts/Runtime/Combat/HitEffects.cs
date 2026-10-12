using UnityEngine;

namespace PX
{
    /// <summary>
    /// The particles where blows land, for every fighter in the scene: part of what makes a hit read as a hit.
    /// Sparks thrown along the blow and a bright flash, both sized to its damage; a ring and dust for heavy blows; red
    /// sparks when the heroine is the one hit; cool, small sparks on a block; a large burst on a parry. The particle
    /// systems are set up by the graybox builder and run on real time, so they fly during hit-stop.
    /// </summary>
    public sealed class HitEffects : MonoBehaviour
    {
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem flash;
        [SerializeField] private ParticleSystem ring;
        [SerializeField] private ParticleSystem dust;

        [Tooltip("Sparks for a blow of 10 damage. Others scale with their damage.")]
        [SerializeField, Min(1)] private int sparksPer10Damage = 16;
        [Tooltip("Blows with at least this much damage also throw a ring and dust.")]
        [SerializeField, Min(0f)] private float heavyDamage = 20f;

        [SerializeField] private Color hitColor = new Color(1f, 0.82f, 0.35f);
        [SerializeField] private Color hurtColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private Color blockColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] private Color parryColor = new Color(1f, 0.95f, 0.6f);

        private void OnEnable()
        {
            Health.AnyStruck += OnStruck;
        }

        private void OnDisable()
        {
            Health.AnyStruck -= OnStruck;
        }

        private void OnStruck(Health target, Hit hit, HitResult result)
        {
            Vector3 point = hit.Point != Vector3.zero ? hit.Point : target.transform.position + Vector3.up * 1.2f;
            Vector3 direction = hit.Direction.sqrMagnitude > 0f ? hit.Direction.normalized : Vector3.forward;
            bool heroine = target.GetComponent<PlayerController>() != null;

            switch (result)
            {
                case HitResult.Parried:
                    Burst(sparks, point, -direction, 40, parryColor, 1.4f);
                    Burst(flash, point, -direction, 1, parryColor, 1.8f);
                    Burst(ring, point, -direction, 1, parryColor, 1.4f);
                    break;
                case HitResult.Blocked:
                    Burst(sparks, point, -direction, 10, blockColor, 0.8f);
                    Burst(flash, point, -direction, 1, blockColor, 0.8f);
                    break;
                default:
                {
                    float scale = Mathf.Clamp(hit.Damage / 15f, 0.6f, 2.2f);
                    Color color = heroine ? hurtColor : hitColor;
                    int count = Mathf.Max(4, Mathf.RoundToInt(sparksPer10Damage * hit.Damage / 10f));
                    Burst(sparks, point, direction, count, color, scale);
                    Burst(flash, point, direction, 1, Color.white, scale);
                    if (hit.Damage >= heavyDamage)
                    {
                        Burst(ring, point, direction, 1, color, scale);
                        Burst(dust, new Vector3(point.x, target.transform.position.y + 0.05f, point.z), Vector3.up, 12, new Color(0.8f, 0.72f, 0.6f, 0.6f), scale);
                    }
                    break;
                }
            }
        }

        private static void Burst(ParticleSystem system, Vector3 position, Vector3 direction, int count, Color color, float size)
        {
            if (system == null)
                return;
            system.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            var emit = new ParticleSystem.EmitParams
            {
                startColor = color,
                startSize = system.main.startSize.constantMax * Mathf.Sqrt(size),
                applyShapeToPosition = true,
            };
            system.Emit(emit, count);
        }
    }
}
