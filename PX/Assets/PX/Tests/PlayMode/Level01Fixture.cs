using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PX.Tests
{
    /// <summary>
    /// Loads Level 01 with the heroine under scripted control at a fixed 60 steps per second, and finds the level's
    /// systems. Guards open with their first swing, as in the graybox fixture.
    /// </summary>
    public abstract class Level01Fixture
    {
        protected const float FrameTime = 1f / 60f;

        protected PlayerController Player;
        protected CharacterMotor Motor;
        protected Rail Rail;
        protected RailCamera RailCamera;
        protected ScriptedInput Input;
        protected LevelFlow Flow;
        protected PlayerRespawn Respawn;
        protected CameraDirector Director;

        private readonly System.Collections.Generic.Dictionary<EnemyConfig, float> secondOpeners =
            new System.Collections.Generic.Dictionary<EnemyConfig, float>();

        [UnitySetUp]
        public IEnumerator LoadLevel()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = FrameTime;
            yield return SceneManager.LoadSceneAsync("Level01", LoadSceneMode.Single);

            Player = Object.FindFirstObjectByType<PlayerController>();
            RailCamera = Object.FindFirstObjectByType<RailCamera>();
            Flow = Object.FindFirstObjectByType<LevelFlow>();
            Respawn = Object.FindFirstObjectByType<PlayerRespawn>();
            Director = Object.FindFirstObjectByType<CameraDirector>();
            Assert.That(Player, Is.Not.Null, "Level 01 has no player. Rebuild it from the PX menu.");
            Assert.That(Flow, Is.Not.Null, "Level 01 has no LevelFlow. Rebuild it from the PX menu.");

            Motor = Player.GetComponent<CharacterMotor>();
            Rail = Motor.Rail;
            Input = new ScriptedInput();
            Player.InputSource = Input;

            secondOpeners.Clear();
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (!secondOpeners.ContainsKey(enemy.Config))
                {
                    secondOpeners[enemy.Config] = enemy.Config.secondOpenerChance;
                    enemy.Config.secondOpenerChance = 0f;
                }
            }

            yield return Frames(20);
        }

        [TearDown]
        public void RestoreTime()
        {
            foreach (var pair in secondOpeners)
                pair.Key.secondOpenerChance = pair.Value;
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            HitStop.BaseTimeScale = 1f;
            CameraShake.Clear();
        }

        protected static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        protected static IEnumerator Seconds(float seconds) => Frames(Mathf.CeilToInt(seconds / FrameTime));

        /// <summary>Stands her on the floor at a rail distance and height and waits for her to settle.</summary>
        protected IEnumerator PlaceAt(float distance, float height = 0.1f, bool crouched = false)
        {
            Input.Move = 0f;
            Input.JumpHeld = false;
            Input.CrouchHeld = crouched;
            Input.GuardHeld = false;
            Motor.Teleport(Rail, distance, height);
            RailCamera.Snap();
            yield return Frames(15);
        }

        protected static EnemyController GuardNear(float distance)
        {
            EnemyController best = null;
            float bestDistance = float.MaxValue;
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                float d = Mathf.Abs(enemy.PostDistance - distance);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = enemy;
                }
            }
            return best;
        }

        protected static Sentinel SentinelNear(float distance)
        {
            Sentinel best = null;
            float bestDistance = float.MaxValue;
            foreach (Sentinel sentinel in Object.FindObjectsByType<Sentinel>(FindObjectsSortMode.None))
            {
                float d = Mathf.Abs(sentinel.Distance - distance);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = sentinel;
                }
            }
            return best;
        }

        protected static Shrine ShrineNamed(string name)
        {
            foreach (Shrine shrine in Object.FindObjectsByType<Shrine>(FindObjectsSortMode.None))
            {
                if (shrine.AreaName == name)
                    return shrine;
            }
            return null;
        }

        /// <summary>Renders the main camera to a PNG in the folder named by PX_SCREENSHOT_DIR. Returns false without it.</summary>
        protected static bool Capture(string name, int width = 1280, int height = 720)
        {
            string folder = System.Environment.GetEnvironmentVariable("PX_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(folder))
                return false;
            Camera camera = Camera.main;
            if (camera == null)
                return false;
            Directory.CreateDirectory(folder);
            var target = new RenderTexture(width, height, 24);
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
            Object.Destroy(target);
            return true;
        }
    }
}
