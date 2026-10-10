using UnityEngine;

namespace PX
{
    /// <summary>
    /// A short camera shake for impacts: hits that land and hard landings. <see cref="RailCamera"/> applies it.
    /// It runs on real time, so it keeps shaking through hit-stop, which is when it reads best.
    /// </summary>
    public static class CameraShake
    {
        private static float amplitude;
        private static float duration;
        private static float remaining;
        private static float clock;
        private static int lastFrame = -1;

        public static bool IsActive => remaining > 0f;

        /// <param name="strength">Largest offset, in metres.</param>
        /// <param name="seconds">How long it takes to die away.</param>
        public static void Trigger(float strength, float seconds)
        {
            if (strength <= 0f || seconds <= 0f || !Application.isPlaying)
                return;

            // A stronger shake replaces a weaker one in progress; a weaker one does not cut a stronger one short.
            if (strength >= Current())
            {
                amplitude = strength;
                duration = seconds;
                remaining = seconds;
            }
        }

        /// <summary>Stops any shake. Tests call this between runs.</summary>
        public static void Clear()
        {
            remaining = 0f;
        }

        /// <summary>This frame's offset in the camera's right and up directions, in metres.</summary>
        public static Vector2 Sample()
        {
            if (lastFrame != Time.frameCount)
            {
                lastFrame = Time.frameCount;
                // Under a fixed capture step (tests, video capture) real time means nothing, so count in steps.
                float dt = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
                clock += dt;
                remaining = Mathf.Max(0f, remaining - dt);
            }

            float strength = Current();
            if (strength <= 0f)
                return Vector2.zero;

            const float frequency = 28f;
            float x = Mathf.PerlinNoise(clock * frequency, 0.5f) * 2f - 1f;
            float y = Mathf.PerlinNoise(0.5f, clock * frequency) * 2f - 1f;
            return new Vector2(x, y) * strength;
        }

        private static float Current()
        {
            if (remaining <= 0f || duration <= 0f)
                return 0f;
            float t = remaining / duration;
            return amplitude * t * t;
        }
    }
}
