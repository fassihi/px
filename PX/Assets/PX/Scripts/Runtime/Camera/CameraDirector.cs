using System;
using System.Collections.Generic;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// The cinematic moments: a cut to a close three-quarter view, in slow motion, for a finisher or the last kill of
    /// an encounter; a slow, wide sweep with letterbox and the area's name when she first enters a new place; a long
    /// shot for the boss's fall. It tells <see cref="RailCamera"/> what to play and lets the HUD draw the bars and the
    /// name. Used sparingly on purpose: ordinary fights stay in the zone's view.
    /// </summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        [Serializable]
        public struct Reveal
        {
            public string name;
            [Tooltip("Rail distance she crosses, moving forward, to start the reveal.")]
            public float distance;
            public CameraView view;
            [Min(0.1f)] public float duration;
        }

        [SerializeField] private PlayerController player;
        [SerializeField] private RailCamera railCamera;
        [SerializeField] private Reveal[] reveals = Array.Empty<Reveal>();
        [Tooltip("The close view for a finisher, relative to the rail.")]
        [SerializeField] private CameraView finisherView = new CameraView { yaw = 32f, pitch = 9f, distance = 5.2f, fieldOfView = 30f, focusHeight = 1.0f, lookAhead = 0f };
        [SerializeField] private CameraView bossFallView = new CameraView { yaw = 48f, pitch = 14f, distance = 9f, fieldOfView = 32f, focusHeight = 1.2f, lookAhead = 0f };
        [SerializeField, Range(0.05f, 1f)] private float finisherTimeScale = 0.32f;
        [SerializeField, Min(0f)] private float finisherSlowTime = 0.55f;

        private readonly HashSet<int> revealed = new HashSet<int>();
        private CharacterMotor motor;
        private float lastDistance;
        private float slowLeft;
        private bool slowing;
        private float letterbox;
        private float letterboxTarget;
        private string card;
        private float cardTime;
        private float cardDuration;

        /// <summary>How far the letterbox bars are in, 0 to 1.</summary>
        public float Letterbox => letterbox;

        /// <summary>The name shown for a reveal, and how visible it is right now, 0 to 1.</summary>
        public string Card => card;

        public float CardAlpha
        {
            get
            {
                if (string.IsNullOrEmpty(card) || cardTime >= cardDuration)
                    return 0f;
                float fadeIn = Mathf.Clamp01(cardTime / 0.5f);
                float fadeOut = Mathf.Clamp01((cardDuration - cardTime) / 0.8f);
                return Mathf.Min(fadeIn, fadeOut);
            }
        }

        /// <summary>Raised when a reveal starts, with its name.</summary>
        public event Action<string> Revealed;

        private void Awake()
        {
            motor = player != null ? player.GetComponent<CharacterMotor>() : null;
            lastDistance = motor != null ? motor.Distance : 0f;
        }

        private void OnEnable()
        {
            if (player == null)
                return;
            player.FinisherStarted += OnFinisherStarted;
        }

        private void OnDisable()
        {
            if (player != null)
                player.FinisherStarted -= OnFinisherStarted;
            if (slowing)
                EndSlowMotion();
        }

        private void Update()
        {
            float realDt = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            if (motor != null)
            {
                // A jump of many metres in one frame is a teleport (a respawn, a test), not a walk across the line.
                bool walked = Mathf.Abs(motor.Distance - lastDistance) < 15f;
                for (int i = 0; i < reveals.Length && walked; i++)
                {
                    if (revealed.Contains(i))
                        continue;
                    if (lastDistance < reveals[i].distance && motor.Distance >= reveals[i].distance)
                        StartReveal(i);
                }
                lastDistance = motor.Distance;
            }

            if (slowing)
            {
                slowLeft -= realDt;
                if (slowLeft <= 0f)
                    EndSlowMotion();
            }

            cardTime += realDt;
            if (railCamera != null && !railCamera.IsShotActive)
                letterboxTarget = 0f;
            letterbox = Mathf.MoveTowards(letterbox, letterboxTarget, realDt * 3f);
        }

        private void StartReveal(int index)
        {
            revealed.Add(index);
            Reveal reveal = reveals[index];
            if (railCamera != null)
                railCamera.PlayShot(reveal.view, reveal.duration, 1.2f, 1.4f);
            letterboxTarget = 1f;
            ShowCard(reveal.name, reveal.duration + 0.5f);
            Revealed?.Invoke(reveal.name);
        }

        /// <summary>Shows a name across the screen for a while, as a reveal does.</summary>
        public void ShowCard(string text, float seconds)
        {
            card = text;
            cardTime = 0f;
            cardDuration = seconds;
        }

        /// <summary>A reveal started by the level itself: picking up the tether, the boss's gates closing.</summary>
        public void PlayReveal(string name, CameraView view, float duration)
        {
            if (railCamera != null)
                railCamera.PlayShot(view, duration, 1.0f, 1.4f);
            letterboxTarget = 1f;
            if (!string.IsNullOrEmpty(name))
                ShowCard(name, duration + 0.5f);
        }

        private void OnFinisherStarted(EnemyController target, bool execution)
        {
            float duration = player.Combo != null ? player.Combo.finisher.Total + 0.25f : 1.4f;
            CameraView view = finisherView;
            // Seen from the side she faces, so the blow comes toward the camera a little.
            view.yaw *= motor != null && motor.Facing < 0 ? -1f : 1f;
            railCamera.PlayShot(view, duration, 0.12f, 0.6f, player.transform, target.transform);
            letterboxTarget = 1f;
            StartSlowMotion(finisherTimeScale, finisherSlowTime);
        }

        /// <summary>The last enemy of an encounter falling: a short cut in slow motion, from the attacker's side.</summary>
        public void PlayLastKill(Transform victim)
        {
            if (railCamera == null || player == null)
                return;
            CameraView view = finisherView;
            view.distance += 1.5f;
            view.yaw *= motor != null && motor.Facing < 0 ? -1f : 1f;
            railCamera.PlayShot(view, 1.1f, 0.1f, 0.7f, player.transform, victim);
            letterboxTarget = 1f;
            StartSlowMotion(finisherTimeScale, 0.45f);
        }

        /// <summary>The boss's fall: a long, wide shot while it goes down.</summary>
        public void PlayBossFall(Transform boss)
        {
            if (railCamera == null || player == null)
                return;
            CameraView view = bossFallView;
            view.yaw *= motor != null && motor.Facing < 0 ? -1f : 1f;
            railCamera.PlayShot(view, 3.2f, 0.2f, 1.2f, player.transform, boss);
            letterboxTarget = 1f;
            StartSlowMotion(0.4f, 1.2f);
        }

        private void StartSlowMotion(float scale, float seconds)
        {
            slowing = true;
            slowLeft = seconds;
            HitStop.BaseTimeScale = scale;
            if (!HitStop.IsActive)
                Time.timeScale = scale;
        }

        private void EndSlowMotion()
        {
            slowing = false;
            HitStop.BaseTimeScale = 1f;
            if (!HitStop.IsActive)
                Time.timeScale = 1f;
        }
    }
}
