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

        [Header("Blending")]
        [Tooltip("Seconds to cross-fade between poses.")]
        public float fadeTime = 0.1f;

        [Tooltip("Ground speed in m/s at which the run clip plays at normal speed.")]
        public float runClipSpeed = 4.5f;

        [Tooltip("The run clip is sped up or slowed down by at most this factor to match ground speed.")]
        public Vector2 runSpeedScaleRange = new Vector2(0.6f, 1.6f);
    }
}
