using System;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// What her actions cost. Dashes, dodges, strikes and blocks spend it; it comes back on its own after a short
    /// pause. An action needs some left to start, and may take it to zero, which costs a longer pause.
    /// </summary>
    public sealed class Stamina : MonoBehaviour
    {
        [SerializeField] private PlayerCombatConfig config;

        private float delay;

        public float Max => config != null ? config.maxStamina : 100f;

        public float Current { get; private set; }

        /// <summary>True while there is any left: enough to start an action.</summary>
        public bool HasAny => Current > 0f;

        /// <summary>Scales regeneration. Lowered while she holds her guard up.</summary>
        public float RegenScale { get; set; } = 1f;

        public PlayerCombatConfig Config => config;

        /// <summary>Raised when an action takes it to zero.</summary>
        public event Action Exhausted;

        private void Awake()
        {
            Current = Max;
        }

        /// <summary>Spends <paramref name="amount"/>, down to zero at most, and pauses regeneration.</summary>
        public void Spend(float amount)
        {
            if (amount <= 0f || config == null)
                return;
            Current = Mathf.Max(0f, Current - amount);
            bool exhausted = Current <= 0f;
            delay = exhausted ? config.exhaustedRegenDelay : config.staminaRegenDelay;
            if (exhausted)
                Exhausted?.Invoke();
        }

        public void Restore()
        {
            Current = Max;
            delay = 0f;
        }

        private void Update()
        {
            if (config == null)
                return;
            float dt = Time.deltaTime;
            if (delay > 0f)
            {
                delay -= dt;
                return;
            }
            Current = Mathf.Min(Max, Current + config.staminaRegen * RegenScale * dt);
        }
    }
}
