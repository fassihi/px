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

        [Tooltip("Her reaction to being hit, played over the stun.")]
        public ClipWindow hurt;

        [Tooltip("Her fall when her health runs out. The window plays over Death Duration, then holds its last frame.")]
        public ClipWindow death;
        [Min(0.1f)] public float deathDuration = 1.3f;

        [Tooltip("The counter after a parry, timed like a combo strike.")]
        public ClipWindow counter;

        [Tooltip("The attack in the air: a flying kick, timed like a combo strike.")]
        public ClipWindow airAttack;

        [Tooltip("The lunge out of a dash, timed like a combo strike. Empty: the first combo clip.")]
        public ClipWindow dashAttack;

        [Tooltip("The takedown from behind and the execution: a stab, with the blow at its impact. Empty: the first combo clip.")]
        public ClipWindow finisher;

        [Tooltip("Drinking a flask, played over the drink. Empty: she holds her guard pose.")]
        public ClipWindow drink;

        [Header("Dodge and guard")]
        [Tooltip("The dodge back, played over the dodge.")]
        public ClipWindow dodge;
        [Tooltip("Holding the guard up.")]
        public AnimationClip guard;
        [Tooltip("The parry: her arm sweeps the blow aside. Played over Parry Duration.")]
        public ClipWindow parry;
        [Min(0.05f)] public float parryDuration = 0.4f;
        [Tooltip("A blow landing on her guard, played over the block's stun.")]
        public ClipWindow guardHit;

        [Header("Ledges")]
        [Tooltip("Catching the ledge, played over the move config's Grab Time as she starts to hang.")]
        public ClipWindow ledgeGrab;
        [Tooltip("Hanging from the ledge.")]
        public AnimationClip ledgeHang;
        [Tooltip("Pulling up onto the ledge, played over the climb.")]
        public ClipWindow ledgeClimb;

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

        [Tooltip("Seconds to blend from standing into the held crouch pose. With no Crouch Down clip this is the whole " +
                 "movement, so she lowers in one go instead of squatting and then snapping to the pose.")]
        [Min(0f)] public float crouchFadeTime = 0.22f;
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

        [Header("Running jump")]
        [Tooltip("A jump taken at least this fast, in m/s, uses Run Jump.")]
        [Min(0f)] public float runJumpMinSpeed = 3f;

        [Tooltip("Seconds into Run Jump where her feet leave the ground. The clip starts here.")]
        [Min(0f)] public float runJumpTakeoff = 0.36f;

        [Tooltip("Seconds into Run Jump where her feet touch down. She holds this pose until she lands.")]
        [Min(0f)] public float runJumpLanding = 1.0f;

        [Header("Blending")]
        [Tooltip("Seconds to cross-fade between poses. Weights ease in and out, so a fade starts and ends softly.")]
        public float fadeTime = 0.12f;

        [Tooltip("Seconds to blend into a strike. Short, so the wind-up shows.")]
        [Min(0f)] public float attackFadeTime = 0.05f;

        [Tooltip("Seconds to blend into a landing. Short, so the touchdown reads as a contact.")]
        [Min(0f)] public float landFadeTime = 0.06f;

        [Header("Head")]
        [Tooltip("How far her head is tipped up in the idle, in muscle units (-1 to 1 across the neck's and head's range), " +
                 "on top of the clip, which has her looking down at the floor. Faded with the idle pose.")]
        [Range(-1f, 1f)] public float idleHeadLift = 0.4f;

        [Header("Landing dip")]
        [Tooltip("How far her body drops on a landing, added on top of whatever she is playing, as a fraction of her " +
                 "standing body height. Her feet stay planted, so her knees take it.")]
        [Range(0f, 0.5f)] public float landDip = 0.14f;

        [Tooltip("Landing speed in m/s below which she does not dip, and at which the dip is full.")]
        public Vector2 landDipSpeedRange = new Vector2(4f, 15f);

        [Tooltip("Seconds to sink into the dip, and to rise back out of it.")]
        [Min(0.01f)] public float landDipDown = 0.06f;
        [Min(0.01f)] public float landDipUp = 0.3f;

        [Tooltip("Ground speed in m/s at which the run clip plays at normal speed: how fast her planted foot sweeps " +
                 "backwards in it. 3.4 for the Mixamo Female Ninja Run at her size (3.1 for the Female Locomotion Pack running). Measure again when the run clip or her " +
                 "height changes, or her feet slide.")]
        public float runClipSpeed = 3.1f;

        [Tooltip("The run clip is sped up or slowed down by at most this factor to match ground speed. Past either end " +
                 "her feet slide, so keep the top above Run Speed / Run Clip Speed.")]
        public Vector2 runSpeedScaleRange = new Vector2(0.5f, 3f);
    }
}
