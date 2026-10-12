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

        [Header("Dodge")]
        [Tooltip("Dash with no direction held, on the ground, is a dodge back: a hop away from where she faces.")]
        [Min(0f)] public float dodgeSpeed = 8f;
        [Min(0.05f)] public float dodgeDuration = 0.5f;
        [Tooltip("Upward speed of the hop back, in m/s.")]
        [Min(0f)] public float dodgeHop = 1.2f;
        [Tooltip("Seconds into the dodge when blows start to pass through her, and when they stop.")]
        public Vector2 dodgeInvulnerable = new Vector2(0.03f, 0.3f);
        [Tooltip("Seconds into the dodge before an attack or a jump can cut it short.")]
        [Min(0f)] public float dodgeCancel = 0.36f;

        [Header("Ledges")]
        [Tooltip("In the air, she grabs a ledge whose top is between these heights above her feet.")]
        public Vector2 ledgeReach = new Vector2(1.2f, 2.3f);
        [Tooltip("How far in front of her body she reaches for a ledge, in metres.")]
        [Min(0f)] public float ledgeProbe = 0.45f;
        [Tooltip("She only grabs on the way down, or near the top of a jump: rising faster than this, in m/s, she does not.")]
        public float ledgeGrabMaxRise = 2f;
        [Tooltip("The ledge's height above her root while she hangs from it: where her hands are in the hang clip. Her " +
                 "root follows her hands, so each ledge clip has its own number, measured from the clip.")]
        [Min(0.5f)] public float hangHeight = 2.2f;
        [Tooltip("The same while the catch clip plays: its hands are lower than the hang's. She blends from this to " +
                 "Hang Height as the catch gives way to the hang.")]
        [Min(0.5f)] public float grabHandsHeight = 1.85f;
        [Tooltip("Seconds the catch plays before the hang takes over.")]
        [Min(0.05f)] public float grabTime = 0.3f;
        [Tooltip("Seconds the catch blends into the hang, and the hang into the climb: the animation's cross-fade time.")]
        [Min(0.01f)] public float hangBlend = 0.12f;
        [Tooltip("How far in front of the ledge's face her root hangs, in metres: her hands reach over the lip, so the " +
                 "root is a little out from the wall.")]
        [Min(0f)] public float hangStandoff = 0.17f;
        [Tooltip("The climb: how high the ledge is above her root over the climb (0 to 1), so that her hands stay on the " +
                 "ledge while she pulls up. It ends at how high the ledge is above her feet once she is up.")]
        public AnimationCurve climbHandsHeight = new AnimationCurve(
            new Keyframe(0f, 1.23f), new Keyframe(0.16f, 1.34f), new Keyframe(0.32f, 1.48f),
            new Keyframe(0.49f, 1.55f), new Keyframe(0.65f, 1.62f), new Keyframe(1f, 1.62f));
        [Tooltip("The climb: how far her hands are in front of her root over the climb, in metres.")]
        public AnimationCurve climbHandsReach = new AnimationCurve(
            new Keyframe(0f, 0.21f), new Keyframe(0.16f, 0.21f), new Keyframe(0.32f, 0.13f),
            new Keyframe(0.49f, 0.09f), new Keyframe(0.65f, 0.05f), new Keyframe(1f, 0.05f));
        [Tooltip("How far her hands reach over the lip of the ledge, in metres, while she pulls up.")]
        [Min(0f)] public float climbGrip = 0.04f;
        [Tooltip("The share of the climb at which her hands let go: until then they stay on the ledge, after it she steps " +
                 "forward onto the top.")]
        [Range(0.2f, 0.95f)] public float climbRelease = 0.65f;
        [Tooltip("Seconds she hangs before she can climb or let go, so a held direction does not climb at once.")]
        [Min(0f)] public float hangSettle = 0.2f;
        [Tooltip("Seconds the climb up onto the ledge takes.")]
        [Min(0.1f)] public float climbDuration = 0.95f;
        [Tooltip("Seconds after letting go before she can grab again.")]
        [Min(0f)] public float regrabDelay = 0.35f;

        [Header("Hurt")]
        [Tooltip("Seconds a hit staggers her. She cannot act until it ends; presses are buffered.")]
        [Min(0f)] public float hurtStun = 0.45f;
        [Tooltip("Extra stagger per m/s of the blow's knockback, so heavy blows rock her for longer.")]
        [Min(0f)] public float hurtStunPerKnockback = 0.03f;
        [Tooltip("Upward speed a hit knocks her into, in m/s, so the knockback reads.")]
        [Min(0f)] public float hurtHop = 3f;
        [Tooltip("Seconds after a hit during which further hits are ignored.")]
        [Min(0f)] public float hurtInvulnerability = 1f;
        [Tooltip("How long the world freezes when she is hit, on top of the attacker's own hit-stop.")]
        [Min(0f)] public float hurtHitStop = 0.1f;
        [Tooltip("How hard a hit on her shakes the camera, in metres.")]
        [Min(0f)] public float hurtShake = 0.16f;

        [Header("Crouch")]
        [Tooltip("Height of her collider while crouched, in metres. Cover lower than this hides her.")]
        [Min(0.3f)] public float crouchHeight = 1.1f;

        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);

        public float JumpVelocity => 2f * jumpHeight / timeToApex;
    }
}
