using UnityEngine;

namespace PX
{
    /// <summary>
    /// The particles that show her movement: a ribbon trailing from her body and a spray of streaks behind an air dash,
    /// and a ring at her feet with two ribbons off her heels as she springs up in a double jump. The particle systems
    /// and trails are set up by the graybox builder.
    /// </summary>
    public sealed class MovementEffects : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private TrailRenderer dashTrail;
        [SerializeField] private TrailRenderer jumpTrail;
        [SerializeField] private TrailRenderer jumpTrailBack;
        [SerializeField] private ParticleSystem streaks;
        [SerializeField] private ParticleSystem ring;

        [Tooltip("Height above her feet, in metres, where the dash ribbon leaves her body.")]
        [SerializeField, Min(0f)] private float dashTrailHeight = 1f;
        [Tooltip("Seconds the double jump's ribbons keep being drawn after the jump.")]
        [SerializeField, Min(0f)] private float jumpTrailTime = 0.4f;

        [SerializeField] private Color dashColor = new Color(0.55f, 0.9f, 1f);
        [SerializeField] private Color jumpColor = new Color(1f, 0.85f, 0.45f);

        private float jumpTrailLeft;
        private bool dashing;

        public float DashTrailHeight => dashTrailHeight;

        /// <summary>True while a ribbon is being drawn from the dash or the double jump.</summary>
        public bool IsTrailing => dashing || jumpTrailLeft > 0f;

        private void OnEnable()
        {
            if (player == null)
                return;
            player.AirJumped += OnAirJumped;
            player.Dashed += OnDashed;
        }

        private void OnDisable()
        {
            if (player == null)
                return;
            player.AirJumped -= OnAirJumped;
            player.Dashed -= OnDashed;
        }

        private void OnAirJumped()
        {
            jumpTrailLeft = jumpTrailTime;
            Vector3 feet = player.transform.position + Vector3.up * 0.05f;
            Place(jumpTrail, feet + player.transform.forward * 0.12f);
            Place(jumpTrailBack, feet - player.transform.forward * 0.12f);
            if (ring != null)
            {
                ring.transform.SetPositionAndRotation(feet, Quaternion.identity);
                var emit = new ParticleSystem.EmitParams { startColor = jumpColor, applyShapeToPosition = true };
                ring.Emit(emit, 1);
            }
            Burst(Vector3.down, 24, jumpColor, 1.4f);
        }

        private void OnDashed(bool air)
        {
            dashing = air;
            if (air)
            {
                Place(dashTrail, player.transform.position + Vector3.up * dashTrailHeight);
                Burst(-player.transform.forward, 30, dashColor, 1.6f);
            }
        }

        private void LateUpdate()
        {
            if (player == null)
                return;

            if (dashing && player.State != PlayerState.Dash)
                dashing = false;
            if (dashTrail != null)
            {
                dashTrail.emitting = dashing;
                if (dashing)
                    dashTrail.transform.position = player.transform.position + Vector3.up * dashTrailHeight;
            }

            jumpTrailLeft -= Time.deltaTime;
            bool jumping = jumpTrailLeft > 0f;
            if (jumpTrail != null)
            {
                jumpTrail.emitting = jumping;
                if (jumping)
                    jumpTrail.transform.position = player.transform.position + Vector3.up * 0.05f + player.transform.forward * 0.12f;
            }
            if (jumpTrailBack != null)
            {
                jumpTrailBack.emitting = jumping;
                if (jumping)
                    jumpTrailBack.transform.position = player.transform.position + Vector3.up * 0.05f - player.transform.forward * 0.12f;
            }

            // Streaks keep coming off her through the dash.
            if (dashing && streaks != null && Time.frameCount % 2 == 0)
                Burst(-player.transform.forward, 4, dashColor, 1.3f);
        }

        private static void Place(TrailRenderer trail, Vector3 position)
        {
            if (trail == null)
                return;
            trail.Clear();
            trail.transform.position = position;
            trail.Clear();
        }

        private void Burst(Vector3 direction, int count, Color color, float size)
        {
            if (streaks == null)
                return;
            Vector3 origin = player.transform.position + Vector3.up * (direction == Vector3.down ? 0.1f : dashTrailHeight);
            streaks.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            var emit = new ParticleSystem.EmitParams { startColor = color, startSize = streaks.main.startSize.constantMax * size };
            streaks.Emit(emit, count);
        }
    }
}
