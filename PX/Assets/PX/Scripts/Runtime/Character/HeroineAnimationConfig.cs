using System;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// The part of a clip that plays for a move, so a long Mixamo take can be trimmed to its strike or its landing.
    /// </summary>
    [Serializable]
    public struct ClipWindow
    {
        public AnimationClip clip;

        [Tooltip("Seconds into the clip where the part that plays starts.")]
        [Min(0f)] public float from;

        [Tooltip("Strikes only: seconds into the clip where the blow lands. It is lined up with the middle of the " +
                 "hitbox's active time. 0: the window is played evenly over the move.")]
        [Min(0f)] public float impact;

        [Tooltip("Seconds into the clip where the part that plays ends. 0: the end of the clip.")]
        [Min(0f)] public float to;

        public float End => to > from ? to : clip != null ? clip.length : 0f;

        public bool HasImpact => impact > from && impact < End;

        /// <summary>Clip time at a fraction (0 to 1) of the window.</summary>
        public float At(float fraction) => Mathf.Lerp(from, End, Mathf.Clamp01(fraction));
    }

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
        public ClipWindow[] comboAttacks = Array.Empty<ClipWindow>();

        [Header("Crouch")]
        public ClipWindow crouchDown;
        public AnimationClip crouchIdle;
        public ClipWindow crouchUp;
        [Tooltip("The sweep from a crouch.")]
        public ClipWindow crouchAttack;

        [Tooltip("How far above her crouch the sweep may lift her body, as a fraction of its height in the crouch idle. " +
                 "Her feet stay put and her legs bend, so a sweep made from a higher stance still plays from the crouch.")]
        [Min(0f)] public float crouchAttackRise = 0.1f;

        [Tooltip("Seconds the crouch down and the stand up take on screen. Their windows are sped up to fit.")]
        [Min(0.05f)] public float crouchDownDuration = 0.3f;
        [Min(0.05f)] public float crouchUpDuration = 0.3f;

        [Header("Landing")]
        [Tooltip("Landing while running: a slight bend, then the run. Its window starts at touchdown.")]
        public ClipWindow landRun;
        [Tooltip("Landing without running on: a deeper bend that settles to idle. Its window starts at touchdown.")]
        public ClipWindow landIdle;

        [Tooltip("Seconds the landings take on screen. Moving cuts the idle landing short.")]
        [Min(0.05f)] public float landRunDuration = 0.36f;
        [Min(0.05f)] public float landIdleDuration = 0.5f;

        [Tooltip("A fall shorter than this, in seconds, lands without a landing animation, so small steps do not bob.")]
        [Min(0f)] public float landMinAirTime = 0.35f;

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

        [Tooltip("Ground speed in m/s at which the run clip plays at normal speed: how fast her planted foot sweeps " +
                 "backwards in it. 3.1 for the Mixamo Running clip (Female Locomotion Pack) at her size. Measure again when the run clip or her " +
                 "height changes, or her feet slide.")]
        public float runClipSpeed = 3.1f;

        [Tooltip("The run clip is sped up or slowed down by at most this factor to match ground speed. Past either end " +
                 "her feet slide, so keep the top above Run Speed / Run Clip Speed.")]
        public Vector2 runSpeedScaleRange = new Vector2(0.5f, 3f);
    }
}
