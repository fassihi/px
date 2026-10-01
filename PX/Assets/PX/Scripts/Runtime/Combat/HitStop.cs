using UnityEngine;

namespace PX
{
    /// <summary>
    /// Freezes the game for a few frames when a hit lands, to give it weight.
    /// This is the simple global version: it stops time for everything.
    /// </summary>
    public static class HitStop
    {
        private static Runner runner;

        public static bool IsActive => runner != null && runner.Remaining > 0f;

        public static void Trigger(float seconds)
        {
            if (seconds <= 0f || !Application.isPlaying)
                return;

            if (runner == null)
            {
                var host = new GameObject("HitStop") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(host);
                runner = host.AddComponent<Runner>();
            }

            runner.Remaining = Mathf.Max(runner.Remaining, seconds);
            Time.timeScale = 0f;
        }

        private sealed class Runner : MonoBehaviour
        {
            public float Remaining;

            private void Update()
            {
                if (Remaining <= 0f)
                    return;

                // Under a fixed capture step (tests, video capture) real time means nothing, so count in steps.
                Remaining -= Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
                if (Remaining <= 0f)
                    Time.timeScale = 1f;
            }

            private void OnDestroy()
            {
                if (Remaining > 0f)
                    Time.timeScale = 1f;
            }
        }
    }
}
