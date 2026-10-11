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
        private IEnumerator CaptureCloseUp(string folder, string name)
        {
            yield return null;
            Vector3 side = Rail.Evaluate(Motor.Distance).Side;
            Vector3 center = Player.transform.position + Vector3.up * 0.8f;
            var go = new GameObject("CloseUpCamera");
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 35f;
            go.transform.position = center + side * 3.6f + Vector3.up * 0.3f;
            go.transform.LookAt(center);

            var target = new RenderTexture(720, 720, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
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
