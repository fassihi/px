using System;
using UnityEngine;

namespace PX
{
    /// <summary>A lever that opens doors when she pulls it (Interact, standing by it). Its handle tips over to show it is pulled.</summary>
    public sealed class Lever : MonoBehaviour
    {
        [SerializeField] private float distance;
        [SerializeField] private float height;
        [SerializeField] private Door[] doors = Array.Empty<Door>();
        [SerializeField] private Transform handle;
        [SerializeField] private float pulledAngle = 70f;

        public float Distance => distance;

        public float Height => height;

        public bool IsPulled { get; private set; }

        public event Action Pulled;

        public void Pull()
        {
            if (IsPulled)
                return;
            IsPulled = true;
            foreach (Door door in doors)
            {
                if (door != null)
                    door.Open();
            }
            Pulled?.Invoke();
        }

        private void Update()
        {
            if (handle == null)
                return;
            Quaternion wanted = Quaternion.Euler(IsPulled ? pulledAngle : -pulledAngle * 0.3f, 0f, 0f);
            handle.localRotation = Quaternion.RotateTowards(handle.localRotation, wanted, 240f * Time.deltaTime);
        }
    }
}
