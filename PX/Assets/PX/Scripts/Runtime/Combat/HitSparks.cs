using UnityEngine;

namespace PX
{
    /// <summary>
    /// A burst of sparks where a blow lands, sized to the blow: part of what makes a hit read as a hit.
    /// The particle system is set up by the graybox builder; this only places and fires it.
    /// </summary>
    public sealed class HitSparks : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private ParticleSystem sparks;

        [Tooltip("Sparks for a blow of 10 damage. Others scale with their damage.")]
        [SerializeField, Min(1)] private int sparksPer10Damage = 14;

        private void OnEnable()
        {
            if (player != null)
                player.HitLanded += OnHit;
        }

        private void OnDisable()
        {
            if (player != null)
                player.HitLanded -= OnHit;
        }

        private void OnHit(Health target)
        {
            if (sparks == null)
                return;

            // Where the hitbox meets the target: the point on its collider nearest the hitbox centre.
            player.GetHitbox(player.CurrentStep, out Vector3 center, out _);
            Collider body = target.GetComponentInChildren<Collider>();
            Vector3 point = body != null ? body.ClosestPoint(center) : target.transform.position + Vector3.up;

            sparks.transform.SetPositionAndRotation(point, Quaternion.LookRotation(player.transform.forward));
            sparks.Emit(Mathf.Max(1, Mathf.RoundToInt(sparksPer10Damage * player.CurrentStep.damage / 10f)));
        }
    }
}
