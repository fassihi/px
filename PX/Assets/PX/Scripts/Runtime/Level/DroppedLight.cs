using System;
using UnityEngine;

namespace PX
{
    /// <summary>The light she dropped when she fell, waiting where she died. Walking into it takes it back.</summary>
    public sealed class DroppedLight : MonoBehaviour
    {
        private CharacterMotor target;
        private float distance;
        private float amount;

        public float Amount => amount;

        public float Distance => distance;

        public event Action<DroppedLight> Recovered;

        public void Place(CharacterMotor player, float atDistance, float atHeight, float light, Rail rail)
        {
            target = player;
            distance = atDistance;
            amount = light;
            RailPoint point = rail.Evaluate(atDistance);
            transform.position = new Vector3(point.Position.x, atHeight + 0.9f, point.Position.z);
        }

        private bool armed;

        private void Update()
        {
            if (target == null)
                return;
            var player = target.GetComponent<PlayerController>();
            if (player != null && player.State == PlayerState.Dead)
                return;
            float along = Mathf.Abs(target.Distance - distance);
            // It waits for her to come back: she has to have left it behind first.
            if (!armed)
            {
                if (along > 2.5f)
                    armed = true;
                return;
            }
            if (along > 1.1f || Mathf.Abs(target.transform.position.y + 0.9f - transform.position.y) > 2f)
                return;
            Recovered?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
