using UnityEngine;

namespace PX
{
    /// <summary>
    /// Tuning for how the heroine moves. Jumping is authored as a height and a time to reach it;
    /// gravity and launch speed are derived from those.
    /// </summary>
    [CreateAssetMenu(menuName = "PX/Move Config", fileName = "MoveConfig")]
    public sealed class MoveConfig : ScriptableObject
    {
        [Header("Run")]
        [Min(0f)] public float runSpeed = 9f;
        [Min(0f)] public float groundAcceleration = 90f;
        [Min(0f)] public float groundDeceleration = 110f;
        [Min(0f)] public float airAcceleration = 55f;
        [Min(0f)] public float airDeceleration = 25f;

        [Header("Jump")]
        [Min(0.1f)] public float jumpHeight = 3.2f;
        [Min(0.05f)] public float timeToApex = 0.36f;
        [Tooltip("Gravity multiplier while falling. Above 1 makes the fall snappier than the rise.")]
        [Min(1f)] public float fallGravityMultiplier = 1.6f;
        [Tooltip("Gravity multiplier while rising with the jump button released. This is what makes a tap a short hop.")]
        [Min(1f)] public float releasedGravityMultiplier = 3f;
        [Min(0f)] public float maxFallSpeed = 30f;
        [Tooltip("How long after walking off a ledge a jump still counts as a ground jump.")]
        [Min(0f)] public float coyoteTime = 0.1f;
        [Tooltip("How long a jump press is remembered before landing.")]
        [Min(0f)] public float jumpBufferTime = 0.12f;
        [Min(0)] public int airJumps = 1;

        [Header("Dash")]
        [Min(0f)] public float dashSpeed = 26f;
        [Min(0.01f)] public float dashDuration = 0.16f;
        [Min(0f)] public float dashCooldown = 0.3f;
        [Min(0)] public int airDashes = 1;
        [Tooltip("How long a dash press is remembered while another action finishes.")]
        [Min(0f)] public float dashBufferTime = 0.1f;

        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);

        public float JumpVelocity => 2f * jumpHeight / timeToApex;
    }
}
