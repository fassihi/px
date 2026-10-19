using System;
using UnityEngine;

namespace PX
{
    public enum PickupKind
    {
        /// <summary>The tether: the ribbon she throws at the lamps.</summary>
        Tether,
        /// <summary>One more flask to carry.</summary>
        FlaskSlot,
        /// <summary>A cache of light.</summary>
        Light,
    }

    /// <summary>Something she picks up by walking into it: an ability, an upgrade, a cache of light. Taken once.</summary>
    public sealed class Pickup : MonoBehaviour
    {
        [SerializeField] private PickupKind kind;
        [SerializeField] private float amount;
        [SerializeField] private float distance;
        [SerializeField] private float height;
        [SerializeField] private CharacterMotor target;
        [SerializeField, Min(0.1f)] private float reach = 1.3f;

        public PickupKind Kind => kind;

        public float Amount => amount;

        public float Distance => distance;

        public float Height => height;

        public bool Taken { get; private set; }

        public event Action<Pickup> Collected;

        private void Update()
        {
            if (Taken || target == null)
                return;
            if (Mathf.Abs(target.Distance - distance) > reach || Mathf.Abs(target.transform.position.y - height) > 1.8f)
                return;
            Taken = true;
            Collected?.Invoke(this);
            foreach (Transform child in transform)
                child.gameObject.SetActive(false);
        }
    }
}
