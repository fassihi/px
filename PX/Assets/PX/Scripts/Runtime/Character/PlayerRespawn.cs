using UnityEngine;

namespace PX
{
    /// <summary>
    /// Death and getting back up, soulslike: when she falls, she gets up again a moment later at the last checkpoint
    /// she passed, with full health and stamina, and the enemies still standing are back at their posts with full
    /// health. The ones she killed stay dead.
    /// </summary>
    public sealed class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [Tooltip("Rail distances of the checkpoints, in increasing order. Passing one makes it the place she gets up.")]
        [SerializeField] private float[] checkpoints = { 3f };
        [SerializeField] private EnemyController[] enemies = System.Array.Empty<EnemyController>();

        private CharacterMotor motor;
        private float respawnTimer = -1f;

        /// <summary>The rail distance she gets up at.</summary>
        public float Checkpoint { get; private set; }

        private void Awake()
        {
            motor = player != null ? player.GetComponent<CharacterMotor>() : null;
            Checkpoint = checkpoints.Length > 0 ? checkpoints[0] : 0f;
        }

        private void OnEnable()
        {
            if (player != null)
                player.Died += OnDied;
        }

        private void OnDisable()
        {
            if (player != null)
                player.Died -= OnDied;
        }

        private void OnDied()
        {
            respawnTimer = player.Combat != null ? player.Combat.respawnDelay : 2f;
        }

        private void Update()
        {
            if (player == null || motor == null)
                return;

            if (player.State != PlayerState.Dead)
            {
                foreach (float checkpoint in checkpoints)
                {
                    if (motor.Distance >= checkpoint && checkpoint > Checkpoint)
                        Checkpoint = checkpoint;
                }
                return;
            }

            if (respawnTimer < 0f)
                return;
            respawnTimer -= Time.deltaTime;
            if (respawnTimer >= 0f)
                return;

            player.Respawn(Checkpoint);
            foreach (EnemyController enemy in enemies)
            {
                if (enemy != null)
                    enemy.ReturnToPost();
            }
        }
    }
}
