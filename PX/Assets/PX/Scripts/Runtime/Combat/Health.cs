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

        /// <summary>How hard the blow pushes the target along <see cref="Direction"/>, in m/s.</summary>
        public readonly float Knockback;

        /// <summary>Where the blow lands, in world space: where its effects go.</summary>
        public readonly Vector3 Point;

        public Hit(float damage, Vector3 direction, GameObject source, float knockback = 0f, Vector3 point = default)
        {
            Damage = damage;
            Direction = direction;
            Source = source;
            Knockback = knockback;
            Point = point;
        }

        /// <summary>The same blow, softened: what gets through a guard.</summary>
        public Hit Scaled(float damageScale, float knockbackScale)
        {
            return new Hit(Damage * damageScale, Direction, Source, Knockback * knockbackScale, Point);
        }
    }

    /// <summary>What became of a blow.</summary>
    public enum HitResult
    {
        /// <summary>It did nothing: the target was dead or invulnerable (a dodge, the moment after a hit).</summary>
        Ignored,
        Damaged,
        /// <summary>A guard took it: some damage got through, but no stagger.</summary>
        Blocked,
        /// <summary>Deflected at the last moment: no damage, and the attacker is thrown off balance.</summary>
        Parried,
    }

    /// <summary>Anything that can be hit and can die.</summary>
    public sealed class Health : MonoBehaviour
    {
        /// <summary>Lets a defender turn a blow before it lands: block it (and soften it) or parry it.</summary>
        public delegate HitResult HitGuard(ref Hit hit);

        [SerializeField, Min(1f)] private float max = 100f;

        public float Max => max;

        public float Current { get; private set; }

        public bool IsDead => Current <= 0f;

        /// <summary>Raised when a blow does damage (not when it is blocked or parried).</summary>
        public event Action<Hit> Damaged;

        public event Action Died;

        /// <summary>Raised for every blow that was not ignored, with what became of it. For effects.</summary>
        public static event Action<Health, Hit, HitResult> AnyStruck;

        /// <summary>While true, hits are ignored: no damage, no events. Set briefly after taking a hit, and in a dodge.</summary>
        public bool Invulnerable { get; set; }

        /// <summary>Optional. Asked about every blow before it does damage.</summary>
        public HitGuard Guard { get; set; }

        private void Awake()
        {
            Current = max;
        }

        public HitResult TakeHit(Hit hit)
        {
            if (IsDead || Invulnerable)
                return HitResult.Ignored;

            HitResult result = Guard != null ? Guard(ref hit) : HitResult.Damaged;
            if (result != HitResult.Ignored && result != HitResult.Parried)
                Current = Mathf.Max(0f, Current - hit.Damage);

            if (result != HitResult.Ignored)
                AnyStruck?.Invoke(this, hit, result);
            if (result == HitResult.Damaged || (result == HitResult.Blocked && IsDead))
                Damaged?.Invoke(hit);
            if (IsDead && result != HitResult.Ignored)
                Died?.Invoke();
            return result;
        }

        public void Restore()
        {
            Current = max;
        }
    }
}
