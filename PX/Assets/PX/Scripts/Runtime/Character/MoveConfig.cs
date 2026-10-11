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
        [Tooltip("Top ground speed in m/s. The run animation speeds up to match, so it can be tuned live in play mode.")]
        [Min(0f)] public float runSpeed = 6f;
        [Min(0f)] public float groundAcceleration = 60f;
        [Min(0f)] public float groundDeceleration = 70f;
        [Min(0f)] public float airAcceleration = 55f;
        [Min(0f)] public float airDeceleration = 25f;

        [Header("Jump")]
        [Min(0.1f)] public float jumpHeight = 3.2f;
        [Min(0.05f)] public float timeToApex = 0.56f;
        [Tooltip("Gravity multiplier while falling. Above 1 makes the fall snappier than the rise.")]
        [Min(1f)] public float fallGravityMultiplier = 1.7f;
        [Tooltip("Gravity multiplier while rising with the jump button released. This is what makes a tap a short hop.")]
        [Min(1f)] public float releasedGravityMultiplier = 3f;
        [Min(0f)] public float maxFallSpeed = 30f;
        [Tooltip("How long after walking off a ledge a jump still counts as a ground jump.")]
        [Min(0f)] public float coyoteTime = 0.1f;
        [Tooltip("How long a jump press is remembered before landing.")]
        [Min(0f)] public float jumpBufferTime = 0.15f;
        [Min(0)] public int airJumps = 1;

        [Header("Landing")]
        [Tooltip("A landing at least this fast, in m/s, shakes the camera. A full jump lands at about 16.")]
        [Min(0f)] public float hardLandingSpeed = 13f;
        [Tooltip("How hard a hard landing shakes the camera, in metres.")]
        [Min(0f)] public float landingShake = 0.05f;
        [Min(0.01f)] public float landingShakeTime = 0.2f;
        [Tooltip("Share of her run speed she keeps through a hard landing. The rest is lost to the impact.")]
        [Range(0f, 1f)] public float hardLandingSpeedKeep = 0.55f;
        [Tooltip("Seconds after a hard landing before she can jump or dash again. Presses in that time are buffered.")]
        [Min(0f)] public float hardLandingRecovery = 0.1f;

        [Header("Turn")]
        [Tooltip("Reversing on the ground from at least this speed, in m/s, is a committed skid turn, not an instant flip.")]
        [Min(0f)] public float turnMinSpeed = 3f;
        [Tooltip("Seconds the skid turn lasts. She cannot turn back during it; holding the other way turns her once it ends.")]
        [Min(0.05f)] public float turnDuration = 0.35f;
        [Tooltip("Seconds into the turn before a jump can cut it short.")]
        [Min(0f)] public float turnJumpCancel = 0.12f;
        [Tooltip("Seconds into the turn before a dash or an attack can cut it short.")]
        [Min(0f)] public float turnActionCancel = 0.2f;

        [Header("Dash")]
        [Min(0f)] public float dashSpeed = 13f;
        [Min(0.01f)] public float dashDuration = 0.3f;
        [Min(0f)] public float dashCooldown = 0.3f;
        [Min(0)] public int airDashes = 1;
        [Tooltip("Seconds into a dash before a jump can cut it short. Attacks wait for the dash to end.")]
        [Min(0f)] public float dashJumpCancel = 0.18f;
        [Tooltip("How long a dash press is remembered while another action finishes.")]
        [Min(0f)] public float dashBufferTime = 0.15f;

        [Header("Hurt")]
        [Tooltip("Seconds a hit staggers her. She cannot act until it ends; presses are buffered.")]
        [Min(0f)] public float hurtStun = 0.4f;
        [Tooltip("Upward speed a hit knocks her into, in m/s, so the knockback reads.")]
        [Min(0f)] public float hurtHop = 4f;
        [Tooltip("Seconds after a hit during which further hits are ignored.")]
        [Min(0f)] public float hurtInvulnerability = 1f;

        [Header("Crouch")]
        [Tooltip("Height of her collider while crouched, in metres. Cover lower than this hides her.")]
        [Min(0.3f)] public float crouchHeight = 1.1f;

        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);

        public float JumpVelocity => 2f * jumpHeight / timeToApex;
    }
}
