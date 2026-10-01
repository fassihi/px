using System;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// One swing in a combo. Timings are in seconds and run in order:
    /// startup (wind-up, no hit), active (the hitbox is live), recovery (follow-through).
    /// </summary>
    [Serializable]
    public sealed class AttackStep
    {
        [Min(0f)] public float startup = 0.06f;
        [Min(0.01f)] public float active = 0.08f;
        [Min(0f)] public float recovery = 0.22f;

        [Tooltip("Seconds into recovery before the next swing, a dash or a jump can interrupt it.")]
        [Min(0f)] public float cancelDelay = 0.04f;

        [Min(0f)] public float damage = 10f;

        [Tooltip("Forward speed while winding up and swinging, on the ground.")]
        [Min(0f)] public float lunge = 3f;

        [Tooltip("Hitbox centre relative to the feet. x = forward, y = up.")]
        public Vector2 hitboxCenter = new Vector2(1.1f, 1f);

        [Tooltip("Hitbox size. x = reach, y = height.")]
        public Vector2 hitboxSize = new Vector2(1.8f, 1.4f);

        [Tooltip("How long the world freezes when this swing connects.")]
        [Min(0f)] public float hitStop = 0.05f;

        public float ActiveEnd => startup + active;

        public float CancelFrom => startup + active + Mathf.Min(cancelDelay, recovery);

        public float Total => startup + active + recovery;
    }

    /// <summary>The steps of a ground combo, in the order the attack button plays them.</summary>
    [CreateAssetMenu(menuName = "PX/Combo Config", fileName = "ComboConfig")]
    public sealed class ComboConfig : ScriptableObject
    {
        public AttackStep[] steps = Array.Empty<AttackStep>();

        [Tooltip("How long an attack press is remembered while the current swing finishes.")]
        [Min(0f)] public float bufferTime = 0.2f;

        [Tooltip("How far the hitbox extends to each side of the rail.")]
        [Min(0.1f)] public float hitboxDepth = 1.5f;
    }
}
