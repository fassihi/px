using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    public sealed class CameraTests : GrayboxFixture
    {
        [UnityTest]
        public IEnumerator SideView_PutsHerTravelDirectionOnScreenRight()
        {
            // No camera zone covers 20 m, so the default side view applies.
            yield return PlaceAt(20f);
            yield return Frames(30);

            Vector3 tangent = Rail.Evaluate(Motor.Distance).Tangent;
            Transform cam = RailCamera.transform;

            Assert.That(Vector3.Dot(cam.right, tangent), Is.GreaterThan(0.99f));
            Assert.That(Mathf.Abs(Vector3.Dot(cam.forward, tangent)), Is.LessThan(0.05f), "looking across the rail, not along it");
        }

        [UnityTest]
        public IEnumerator SideView_StaysASideView_AfterTheTurn()
        {
            // 73 m is past the turn, where the rail runs along world Z instead of X.
            yield return PlaceAt(73f);
            yield return Frames(30);

            Vector3 tangent = Rail.Evaluate(Motor.Distance).Tangent;
            Assert.That(Vector3.Dot(RailCamera.transform.right, tangent), Is.GreaterThan(0.99f));
        }

        [UnityTest]
        public IEnumerator WalkingIntoAZone_BlendsTowardItsView()
        {
            // The "into depth" zone starts at 82 m.
            yield return PlaceAt(80f);
            float yawBefore = RailCamera.CurrentView.yaw;
            float zoneYaw = RailCamera.ViewAt(90f).yaw;
            Assert.That(zoneYaw, Is.Not.EqualTo(yawBefore).Within(10f), "the zone needs a different angle for this test to mean anything");

            Input.Move = 1f;
            yield return Frames(45);
            Input.Move = 0f;

            float midway = RailCamera.CurrentView.yaw;
            Assert.That(midway, Is.GreaterThan(yawBefore + 1f), "the angle should already be changing");
            Assert.That(midway, Is.LessThan(zoneYaw), "and it should be a blend, not a cut");

            yield return Frames(120);
            Assert.That(RailCamera.CurrentView.yaw, Is.EqualTo(zoneYaw).Within(1f));

            Vector3 tangent = Rail.Evaluate(Motor.Distance).Tangent;
            Assert.That(Vector3.Dot(RailCamera.transform.forward, tangent), Is.GreaterThan(0.5f), "now looking down the rail");
        }

        /// <summary>
        /// Renders the main views of the graybox to PNG files, for looking at the scene without opening the editor.
        /// Only runs when the PX_SCREENSHOT_DIR environment variable names a folder.
        /// </summary>
        [UnityTest]
        public IEnumerator Screenshots()
        {
            string folder = System.Environment.GetEnvironmentVariable("PX_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(folder))
                Assert.Ignore("Set PX_SCREENSHOT_DIR to capture screenshots.");

            Directory.CreateDirectory(folder);

            yield return PlaceAt(6f);
            yield return Capture(folder, "1-side-view-start");

            yield return PlaceAt(48f);
            yield return Capture(folder, "2-corner-three-quarter");

            yield return PlaceAt(60.6f);
            Input.PressAttack();
            yield return Frames(6);
            yield return Capture(folder, "3-arena-attack");
            yield return Frames(40);

            yield return PlaceAt(88f);
            yield return Capture(folder, "4-into-depth");

            yield return PlaceAt(14f);
            Input.Move = 1f;
            yield return Frames(30);
            yield return Capture(folder, "5-run");
            Input.PressDash();
            yield return Frames(5);
            yield return Capture(folder, "6-ground-dash");
            yield return Frames(30);
            Input.PressJump();
            yield return Frames(12);
            Input.PressDash();
            yield return Frames(5);
            yield return Capture(folder, "7-air-dash");
            Input.Move = 0f;

            // Each combo strike at the moment it lands, in the side view.
            yield return PlaceAt(16f);
            int captured = 0;
            for (int i = 0; i < 120 && captured < Player.Combo.steps.Length; i++)
            {
                if (i % 8 == 0)
                    Input.PressAttack();
                yield return null;
                AttackStep step = Player.Combo.steps[Player.ComboIndex];
                if (Player.State == PlayerState.Attack && Player.ComboIndex == captured
                    && Player.StateTime >= step.startup + step.active * 0.5f)
                {
                    yield return Capture(folder, $"{8 + captured}-strike-{captured + 1}");
                    captured++;
                }
            }

            yield return PlaceAt(16f);
            Input.CrouchHeld = true;
            yield return Frames(40);
            yield return Capture(folder, "11-crouch");
            Input.PressAttack();
            AttackStep sweep = Player.Combo.crouchAttack;
            yield return Frames(Mathf.CeilToInt((sweep.startup + sweep.active * 0.5f) / FrameTime));
            yield return Capture(folder, "12-sweep");
            yield return Frames(40);
            Input.CrouchHeld = false;
            yield return Frames(30);

            Input.PressJump();
            yield return Frames(5);
            for (int i = 0; i < 120 && !Motor.IsGrounded; i++)
                yield return null;
            yield return Frames(6);
            yield return Capture(folder, "13-landing");
        }

        /// <summary>
        /// Close-ups of her poses through the game camera, zoomed in, for judging animation: idle, run, crouch, sweep
        /// and a landing at a run. Only runs when the PX_SCREENSHOT_DIR environment variable names a folder.
        /// </summary>
        [UnityTest]
        public IEnumerator CloseUpScreenshots()
        {
            string folder = System.Environment.GetEnvironmentVariable("PX_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(folder))
                Assert.Ignore("Set PX_SCREENSHOT_DIR to capture screenshots.");
            folder = Path.Combine(folder, "closeups");
            Directory.CreateDirectory(folder);

            yield return PlaceAt(16f);
            yield return Frames(30);
            yield return CaptureCloseUp(folder, "idle-a");
            yield return Frames(50);
            yield return CaptureCloseUp(folder, "idle-b");

            Input.Move = 1f;
            yield return Frames(30);
            for (int i = 0; i < 4; i++)
            {
                yield return CaptureCloseUp(folder, $"run-{i}");
                yield return Frames(3);
            }
            Input.Move = 0f;
            yield return Frames(40);

            Input.PressJump();
            yield return Frames(12);
            Input.PressDash();
            yield return Frames(4);
            yield return CaptureCloseUp(folder, "air-dash");
            yield return Frames(60);

            Input.CrouchHeld = true;
            yield return Frames(40);
            yield return CaptureCloseUp(folder, "crouch");
            Input.PressAttack();
            for (int i = 0; i < 4; i++)
            {
                yield return Frames(6);
                yield return CaptureCloseUp(folder, $"sweep-{i}");
            }
            yield return Frames(40);
            Input.CrouchHeld = false;
            yield return Frames(30);

            yield return PlaceAt(82f);
            Input.Move = 1f;
            yield return Frames(20);
            Input.PressJump();
            yield return Frames(5);
            for (int i = 0; i < 150 && !Motor.IsGrounded; i++)
                yield return null;
            for (int i = 0; i < 5; i++)
            {
                yield return CaptureCloseUp(folder, $"land-run-{i}");
                yield return Frames(3);
            }
            Input.Move = 0f;
        }

        /// <summary>Renders her from a camera close beside her on the rail's camera side, framed on her whole body.</summary>
        private static EnemyController FindEnemy(string name)
        {
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy.name.StartsWith(name))
                    return enemy;
            }
            return null;
        }

        /// <summary>
        /// Close-ups of the moves added with the soulslike layer and the ledges: the dodge, guard, parry and counter,
        /// the last two combo strikes, the hit effects, hanging and climbing, the crouch, and Enemy B.
        /// Only runs when the PX_SCREENSHOT_DIR environment variable names a folder.
        /// </summary>
        [UnityTest]
        public IEnumerator MovesScreenshots()
        {
            string folder = System.Environment.GetEnvironmentVariable("PX_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(folder))
                Assert.Ignore("Set PX_SCREENSHOT_DIR to capture screenshots.");
            folder = Path.Combine(folder, "moves");
            Directory.CreateDirectory(folder);

            // The dodge back and the crouch, in the open.
            yield return PlaceAt(90f);
            Input.PressDash();
            yield return Frames(10);
            yield return CaptureCloseUp(folder, "01-dodge");
            yield return Frames(40);
            Input.CrouchHeld = true;
            yield return Frames(40);
            yield return CaptureCloseUp(folder, "02-crouch");
            Input.CrouchHeld = false;
            yield return Frames(30);
            Input.GuardHeld = true;
            yield return Frames(30);
            yield return CaptureCloseUp(folder, "03-guard");
            Input.GuardHeld = false;
            yield return Frames(20);

            // The whole combo on a training dummy, caught at the moment each strike lands, sparks and all.
            yield return PlaceAt(60.6f);
            int captured = 0;
            for (int i = 0; i < 240 && captured < Player.Combo.steps.Length; i++)
            {
                if (i % 5 == 0)
                    Input.PressAttack();
                yield return null;
                if (Player.State == PlayerState.Attack && Player.ComboIndex == captured
                    && Player.StateTime >= Player.CurrentStep.startup + Player.CurrentStep.active * 0.5f)
                {
                    yield return CaptureCloseUp(folder, $"04-strike-{captured + 1}");
                    captured++;
                }
            }

            // A parry, and the counter after it.
            EnemyController enemy = FindEnemy("EnemyA");
            yield return PlaceAt(enemy.Motor.Distance - 1.5f);
            for (int i = 0; i < 300 && !(enemy.State == EnemyState.Attack && enemy.StateTime >= enemy.Config.attack.startup - 0.08f); i++)
                yield return null;
            Input.PressGuard();
            for (int i = 0; i < 40 && enemy.State != EnemyState.Parried; i++)
                yield return null;
            yield return Frames(4);
            yield return CaptureCloseUp(folder, "05-parry", enemy.transform);
            Input.GuardHeld = false;
            Input.PressAttack();
            for (int i = 0; i < 60 && !(Player.State == PlayerState.Attack && Player.IsHitboxActive); i++)
                yield return null;
            yield return Frames(2);
            yield return CaptureCloseUp(folder, "06-counter", enemy.transform);
            yield return Frames(60);

            // Taking a blow.
            for (int i = 0; i < 300 && !Player.IsHurt; i++)
                yield return null;
            yield return Frames(6);
            yield return CaptureCloseUp(folder, "07-hurt", enemy.transform);

            // The ledges, once she is back on her feet.
            for (int i = 0; i < 120 && (Player.IsHurt || Player.State == PlayerState.Dead); i++)
                yield return null;
            Motor.Facing = 1;
            yield return PlaceAt(129f);
            Input.Move = 1f;
            yield return Frames(15);
            Input.PressJump();
            for (int i = 0; i < 120 && Player.State != PlayerState.Hang; i++)
                yield return null;
            Input.Move = 0f;
            yield return Frames(20);
            yield return CaptureCloseUp(folder, "08-hang");
            Input.PressJump();
            yield return Frames(20);
            yield return CaptureCloseUp(folder, "09-climb");
            yield return Frames(40);
            yield return CaptureCloseUp(folder, "10-on-top");

            // Enemy B.
            EnemyController enemyB = FindEnemy("EnemyB");
            yield return PlaceAt(enemyB.Motor.Distance - 6f);
            yield return Frames(20);
            yield return CaptureCloseUp(folder, "11-enemy-b", enemyB.transform);
            yield return PlaceAt(enemyB.Motor.Distance - 1.6f);
            for (int i = 0; i < 240 && !enemyB.IsHitboxActive; i++)
                yield return null;
            yield return CaptureCloseUp(folder, "12-enemy-b-swing", enemyB.transform);

            // A fall.
            Health health = Player.GetComponent<Health>();
            health.Invulnerable = false;
            health.TakeHit(new Hit(health.Max * 2f, -Player.transform.forward, null, 4f));
            yield return Frames(80);
            yield return CaptureCloseUp(folder, "13-fallen");
        }

        /// <summary>
        /// The fight with Enemy A, framed on the two of them: the knight idle and walking in, its swing winding up and
        /// landing, her staggered, then it staggered by her combo and dead. Only runs when PX_SCREENSHOT_DIR is set.
        /// </summary>
        [UnityTest]
        public IEnumerator EnemyScreenshots()
        {
            string folder = System.Environment.GetEnvironmentVariable("PX_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(folder))
                Assert.Ignore("Set PX_SCREENSHOT_DIR to capture screenshots.");
            folder = Path.Combine(folder, "enemy");
            Directory.CreateDirectory(folder);
            EnemyController enemy = FindEnemy("EnemyA");

            yield return PlaceAt(enemy.Motor.Distance - 10f);
            yield return Frames(20);
            yield return CaptureCloseUp(folder, "1-idle", enemy.transform);
            yield return PlaceAt(enemy.Motor.Distance - 5f);
            yield return Frames(30);
            yield return CaptureCloseUp(folder, "2-walk", enemy.transform);

            for (int i = 0; i < 240 && !(enemy.State == EnemyState.Attack && enemy.StateTime > enemy.Config.attack.startup * 0.6f); i++)
                yield return null;
            yield return CaptureCloseUp(folder, "3-wind-up", enemy.transform);
            for (int i = 0; i < 60 && !enemy.IsHitboxActive; i++)
                yield return null;
            yield return CaptureCloseUp(folder, "4-swing", enemy.transform);
            for (int i = 0; i < 10 && !Player.IsHurt; i++)
                yield return null;
            yield return Frames(4);
            yield return CaptureCloseUp(folder, "5-she-is-hit", enemy.transform);

            yield return PlaceAt(enemy.Motor.Distance - 1.4f);
            for (int i = 0; i < 40 && enemy.State != EnemyState.Hurt; i++)
            {
                if (i % 6 == 0)
                    Input.PressAttack();
                yield return null;
            }
            yield return Frames(3);
            yield return CaptureCloseUp(folder, "6-it-is-hit", enemy.transform);

            for (int i = 0; i < 20 && !enemy.Health.IsDead; i++)
                enemy.Health.TakeHit(new Hit(10f, Player.transform.forward, Player.gameObject, 4f));
            yield return Frames(90);
            yield return CaptureCloseUp(folder, "7-dead", enemy.transform);
        }

        private IEnumerator CaptureCloseUp(string folder, string name, Transform also = null)
        {
            yield return null;
            // Two views side by side: from the side, as the game shows her, and from the front three-quarter, where
            // a foot or knee turned the wrong way shows.
            RailPoint point = Rail.Evaluate(Motor.Distance);
            Vector3 center = Player.transform.position + Vector3.up * 0.8f;
            if (also != null)
                center = (center + also.position + Vector3.up * 0.9f) * 0.5f;
            Vector3 front = (point.Tangent * Motor.Facing * 0.8f + point.Side * 0.6f).normalized;
            var image = new Texture2D(1440, 720, TextureFormat.RGB24, false);
            RenderView(image, 0, center, point.Side);
            RenderView(image, 720, center, front);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
        }

        private static void RenderView(Texture2D image, int x, Vector3 center, Vector3 direction)
        {
            var go = new GameObject("CloseUpCamera");
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 35f;
            go.transform.position = center + direction * 3.6f + Vector3.up * 0.3f;
            go.transform.LookAt(center);

            var target = new RenderTexture(720, 720, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), x, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(go);
        }

        private IEnumerator Capture(string folder, string name)
        {
            yield return null;

            var camera = RailCamera.GetComponent<Camera>();
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;

            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }
    }
}
