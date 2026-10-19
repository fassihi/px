using UnityEngine;

namespace PX
{
    /// <summary>
    /// The water. Anything with health that sinks below the level dies: she falls, and gets up at her last shrine.
    /// Remembers where she last stood on solid ground, so what she drops on a fall is left there and not in the deep.
    /// </summary>
    public sealed class KillPlane : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [Tooltip("Height below which a character is lost, in metres.")]
        [SerializeField] private float level = -2f;

        private CharacterMotor motor;
        private Vector2 lastSafe;

        /// <summary>Where she last stood on the ground: rail distance and height.</summary>
        public Vector2 LastSafe => lastSafe;

        /// <summary>True when her last death was a fall into the water.</summary>
        public bool Fell { get; private set; }

        public float Level => level;

        private void Awake()
        {
            motor = player != null ? player.GetComponent<CharacterMotor>() : null;
            if (motor != null)
                lastSafe = new Vector2(motor.Distance, motor.transform.position.y);
        }

        private void Update()
        {
            if (player == null || motor == null)
                return;
            if (player.State == PlayerState.Dead)
                return;
            if (motor.IsGrounded && motor.transform.position.y > level + 0.5f)
            {
                lastSafe = new Vector2(motor.Distance, motor.transform.position.y);
                Fell = false;
            }
            if (motor.transform.position.y < level)
            {
                Fell = true;
                Health health = player.GetComponent<Health>();
                health.Invulnerable = false;
                health.TakeHit(new Hit(health.Max * 10f, Vector3.up, gameObject));
            }
        }
    }
}
