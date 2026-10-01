using System;
using UnityEngine;

namespace PX
{
    /// <summary>One hit, as received by the thing that was struck.</summary>
    public readonly struct Hit
    {
        public readonly float Damage;

        /// <summary>World-space direction the blow travels in.</summary>
        public readonly Vector3 Direction;
        public readonly GameObject Source;

        public Hit(float damage, Vector3 direction, GameObject source)
        {
            Damage = damage;
            Direction = direction;
            Source = source;
        }
    }

    /// <summary>Anything that can be hit and can die.</summary>
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float max = 100f;

        public float Max => max;

        public float Current { get; private set; }

        public bool IsDead => Current <= 0f;

        public event Action<Hit> Damaged;

        public event Action Died;

        private void Awake()
        {
            Current = max;
        }

        public void TakeHit(Hit hit)
        {
            if (IsDead)
                return;

            Current = Mathf.Max(0f, Current - hit.Damage);
            Damaged?.Invoke(hit);
            if (IsDead)
                Died?.Invoke();
        }

        public void Restore()
        {
            Current = max;
        }
    }
}
