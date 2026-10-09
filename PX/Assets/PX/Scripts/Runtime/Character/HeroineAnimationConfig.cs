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
        public AnimationClip jumpStart;
        public AnimationClip jumpLoop;
        public AnimationClip dash;
        public AnimationClip attack;

        [Tooltip("Optional clip per combo step. A step without one (or past the end of the list) uses Attack.")]
        public AnimationClip[] comboAttacks = System.Array.Empty<AnimationClip>();

        [Header("Dash")]
        [Tooltip("On: the dash clip is squeezed to the length of the dash, which suits a one-shot move like a roll. " +
                 "Off: it loops at Dash Clip Speed, which suits a fast run.")]
        public bool stretchDashToDuration = true;

        [Min(0.1f)] public float dashClipSpeed = 1.8f;

        [Header("Blending")]
        [Tooltip("Seconds to cross-fade between poses.")]
        public float fadeTime = 0.1f;

        [Tooltip("Ground speed in m/s at which the run clip plays at normal speed.")]
        public float runClipSpeed = 4.5f;

        [Tooltip("The run clip is sped up or slowed down by at most this factor to match ground speed.")]
        public Vector2 runSpeedScaleRange = new Vector2(0.6f, 1.6f);
    }
}
