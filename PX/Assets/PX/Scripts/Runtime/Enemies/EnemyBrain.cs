using UnityEngine;

namespace PX
{
    /// <summary>
    /// A simple melee AI, written as an input source so the enemy is driven the same way the player is: it walks to
    /// the player when they come near and swings when they are in reach. Tests can swap it for scripted input.
    /// </summary>
    public sealed class EnemyBrain : MonoBehaviour, IInputSource
    {
        [SerializeField] private EnemyController body;
        [SerializeField] private CharacterMotor target;

        public CharacterMotor Target
        {
            get => target;
            set => target = value;
        }

        public InputFrame Read()
        {
            var frame = new InputFrame();
            if (body == null || target == null || body.Config == null)
                return frame;

            Health targetHealth = target.GetComponent<Health>();
            if (targetHealth != null && targetHealth.IsDead)
                return frame;

            float offset = target.Distance - body.Motor.Distance;
            float distance = Mathf.Abs(offset);
            if (distance > body.Config.aggroRange || Mathf.Abs(target.transform.position.y - body.transform.position.y) > 2f)
                return frame;

            if (distance > body.Config.attackRange)
                frame.Move = Mathf.Sign(offset);
            else
                frame.AttackPressed = true;

            // Facing the player even when it stands still, so its swing goes the right way.
            if (frame.Move == 0f)
                frame.Move = Mathf.Sign(offset) * 0.01f;
            return frame;
        }
    }
}
