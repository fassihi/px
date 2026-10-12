using UnityEngine;

namespace PX
{
    /// <summary>
    /// A melee AI, written as an input source so the enemy is driven the same way the player is. It notices her when
    /// she comes near and then follows her, climbing what is in its way. In reach it swings, and follows the first swing
    /// with a second only some of the time, so she cannot learn one rhythm. Too close, or sometimes after attacking,
    /// it backs off, still facing her, to set its distance again. Tests can swap it for scripted input.
    /// </summary>
    public sealed class EnemyBrain : MonoBehaviour, IInputSource
    {
        [SerializeField] private EnemyController body;
        [SerializeField] private CharacterMotor target;

        private bool engaged;
        private bool chain;
        private float retreatTimer;
        private EnemyState lastState;
        private int lastCombo;

        public CharacterMotor Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>True once it has noticed her and is following her.</summary>
        public bool Engaged => engaged;

        public InputFrame Read()
        {
            var frame = new InputFrame();
            if (body == null || target == null || body.Config == null)
                return frame;
            EnemyConfig config = body.Config;

            Remember();

            Health targetHealth = target.GetComponent<Health>();
            if (targetHealth != null && targetHealth.IsDead)
            {
                engaged = false;
                return frame;
            }

            float offset = target.Distance - body.Motor.Distance;
            float distance = Mathf.Abs(offset);
            float height = Mathf.Abs(target.transform.position.y - body.transform.position.y);
            if (!engaged && distance <= config.aggroRange && height <= config.aggroHeight)
                engaged = true;
            else if (engaged && distance > config.chaseRange)
                engaged = false;
            if (!engaged)
                return frame;

            float toward = Mathf.Sign(offset);
            frame.Face = toward;

            if (body.State == EnemyState.Attack)
            {
                frame.AttackPressed = chain;
                return frame;
            }

            retreatTimer -= Time.deltaTime;
            if (retreatTimer > 0f || distance < config.tooClose)
                frame.Move = -toward;
            else if (distance > config.attackRange || height > 1f)
            {
                frame.Move = toward;
                frame.Run = distance > config.runDistance;
            }
            else
                frame.AttackPressed = true;
            return frame;
        }

        /// <summary>Rolls the dice as each combo starts and ends: a second swing or not, backing off after it or not.</summary>
        private void Remember()
        {
            EnemyState state = body.State;
            bool startedCombo = state == EnemyState.Attack && body.ComboIndex == 0 && (lastState != EnemyState.Attack || lastCombo != 0);
            if (startedCombo)
                chain = Random.value < body.Config.comboChance;
            if (lastState == EnemyState.Attack && state != EnemyState.Attack && state != EnemyState.Dead)
                retreatTimer = Random.value < body.Config.retreatChance ? body.Config.retreatTime : 0f;
            lastState = state;
            lastCombo = body.ComboIndex;
        }
    }
}
