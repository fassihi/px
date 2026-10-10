using UnityEngine;

namespace PX
{
    /// <summary>Which animation clips the heroine uses, and how they are blended. Swap clips here, not in code.</summary>
    [CreateAssetMenu(menuName = "PX/Heroine Animation", fileName = "HeroineAnimation")]
    public sealed class HeroineAnimationConfig : ScriptableObject
    {
        [Header("Clips")]
        public AnimationClip idle;
        public AnimationClip run;
        [Tooltip("Played when she reverses while running. Its rotation is kept: the clip must turn her by 180 degrees.")]
        public AnimationClip runTurn;
        public AnimationClip jumpStart;
        public AnimationClip jumpLoop;
        [Tooltip("A whole running jump, takeoff to landing, used instead of Jump Start and Jump Loop for a jump taken at speed.")]
        public AnimationClip runJump;
        [Tooltip("The dash on the ground. A single held pose suits a dash this short.")]
        public AnimationClip dash;
        [Tooltip("The dash in the air. Empty: the air dash uses Dash.")]
        public AnimationClip airDash;
        public AnimationClip attack;

        [Tooltip("Optional clip per combo step. A step without one (or past the end of the list) uses Attack.")]
        public AnimationClip[] comboAttacks = System.Array.Empty<AnimationClip>();

        [Header("Dash")]
        [Tooltip("On: the dash clip is squeezed to the length of the dash, which suits a one-shot move like a roll. " +
                 "Off: it loops at Dash Clip Speed, which suits a fast run.")]
        public bool stretchDashToDuration = true;

        [Min(0.1f)] public float dashClipSpeed = 1.8f;

        [Tooltip("Seconds to blend into the dash pose. Shorter than Fade Time, so the pose shows in a dash this short.")]
        [Min(0f)] public float dashFadeTime = 0.04f;

        [Header("Run turn")]
        [Tooltip("She plays the turn when she reverses from at least this ground speed, in m/s.")]
        [Min(0f)] public float turnMinSpeed = 3f;

        [Tooltip("Seconds the turn takes. The clip is sped up or slowed down to fit.")]
        [Min(0.05f)] public float turnDuration = 0.35f;

        [Header("Running jump")]
        [Tooltip("A jump taken at least this fast, in m/s, uses Run Jump.")]
        [Min(0f)] public float runJumpMinSpeed = 3f;

        [Tooltip("Seconds into Run Jump where her feet leave the ground. The clip starts here.")]
        [Min(0f)] public float runJumpTakeoff = 0.36f;

        [Tooltip("Seconds into Run Jump where her feet touch down. She holds this pose until she lands.")]
        [Min(0f)] public float runJumpLanding = 1.0f;

        [Header("Blending")]
        [Tooltip("Seconds to cross-fade between poses.")]
        public float fadeTime = 0.1f;

        [Tooltip("Ground speed in m/s at which the run clip plays at normal speed.")]
        public float runClipSpeed = 4.5f;

        [Tooltip("The run clip is sped up or slowed down by at most this factor to match ground speed.")]
        public Vector2 runSpeedScaleRange = new Vector2(0.6f, 1.6f);
    }
}
