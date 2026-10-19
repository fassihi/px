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
        [Tooltip("On: every regular enemy comes back when she gets up, dead or alive, as at a rest. Off: only the living return to their posts.")]
        [SerializeField] private bool reviveOnDeath;

        private CharacterMotor motor;
        private float respawnTimer = -1f;

        /// <summary>The rail distance she gets up at.</summary>
        public float Checkpoint { get; private set; }

        /// <summary>The height she gets up at.</summary>
        public float CheckpointHeight { get; private set; } = 0.1f;

        /// <summary>Raised the moment she gets up again.</summary>
        public event System.Action Respawned;

        /// <summary>Raised when a new checkpoint is set: by passing one, or by a shrine.</summary>
        public event System.Action CheckpointChanged;

        private void Awake()
        {
            motor = player != null ? player.GetComponent<CharacterMotor>() : null;
            Checkpoint = checkpoints.Length > 0 ? checkpoints[0] : 0f;
        }

        /// <summary>Makes a place the one she gets up at: what lighting a shrine does.</summary>
        public void SetCheckpoint(float distance, float height)
        {
            Checkpoint = distance;
            CheckpointHeight = height;
            CheckpointChanged?.Invoke();
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
                        SetCheckpoint(checkpoint, 0.1f);
                }
                return;
            }

            if (respawnTimer < 0f)
                return;
            respawnTimer -= Time.deltaTime;
            if (respawnTimer >= 0f)
                return;

            player.Respawn(Checkpoint, CheckpointHeight);
            foreach (EnemyController enemy in enemies)
            {
                if (enemy == null)
                    continue;
                if (reviveOnDeath && !(enemy.Config != null && enemy.Config.isBoss))
                    enemy.Reset();
                else
                    enemy.ReturnToPost();
            }
            Respawned?.Invoke();
        }
    }
}
