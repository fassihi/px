using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PX.Tests
{
    /// <summary>
    /// The locomotion scene: Rana's heavier walk, run, crouch and single jump, with its own move and animation configs.
    /// Runs at a fixed 60 steps per second like the graybox tests.
    /// </summary>
    public sealed class LocomotionTests
    {
        private const float FrameTime = 1f / 60f;

        private PlayerController player;
        private CharacterMotor motor;
        private HeroineAnimator heroine;
        private RailCamera railCamera;
        private ScriptedInput input;

        private MoveConfig Move => player.Move;

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = FrameTime;
            yield return SceneManager.LoadSceneAsync("Locomotion", LoadSceneMode.Single);

            player = Object.FindFirstObjectByType<PlayerController>();
            Assert.That(player, Is.Not.Null, "The locomotion scene has no player. Rebuild it from the PX menu.");
            motor = player.GetComponent<CharacterMotor>();
            heroine = player.GetComponent<HeroineAnimator>();
            railCamera = Object.FindFirstObjectByType<RailCamera>();
            input = new ScriptedInput();
            player.InputSource = input;
            yield return Frames(20);
        }

        [TearDown]
        public void RestoreTime()
        {
            Time.captureDeltaTime = 0f;
            CameraShake.Clear();
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        [UnityTest]
        public IEnumerator SceneUsesTheHeavyTuning()
        {
            Assert.That(Move.walkSpeed, Is.GreaterThan(0f), "She should walk unless Run is held.");
            Assert.That(Move.airJumps, Is.EqualTo(0), "One jump only.");
            Assert.That(Move.runSpeed, Is.LessThan(4.5f), "Slower than the levels.");
            Assert.That(heroine.Config.walk, Is.Not.Null, "The walk clip is missing from HeroineAnimationLocomotion.");
            yield break;
        }

        [UnityTest]
        public IEnumerator WalksUnlessRunIsHeld()
        {
            input.Move = 1f;
            yield return Frames(90);
            Assert.That(motor.Velocity.x, Is.EqualTo(Move.walkSpeed).Within(0.1f), "Walking pace without Run.");
            Assert.That(heroine.CurrentPose, Is.EqualTo(HeroinePose.Walk));

            input.Run = true;
            yield return Frames(120);
            Assert.That(motor.Velocity.x, Is.EqualTo(Move.runSpeed).Within(0.15f), "Running pace with Run held.");
            Assert.That(heroine.CurrentPose, Is.EqualTo(HeroinePose.Run));
        }

        [UnityTest]
        public IEnumerator SheGathersSpeedSlowly()
        {
            input.Move = 1f;
            input.Run = true;
            yield return Frames(12);
            Assert.That(motor.Velocity.x, Is.LessThan(Move.runSpeed * 0.4f), "A fifth of a second in, she is still getting going.");

            yield return Frames(80);
            input.Move = 0f;
            yield return Frames(12);
            Assert.That(motor.Velocity.x, Is.GreaterThan(Move.runSpeed * 0.4f), "She does not stop on the spot.");
        }

        [UnityTest]
        public IEnumerator JumpGathersBeforeLeavingTheGround()
        {
            input.PressJump();
            yield return null;
            Assert.That(player.IsJumpWindup, Is.True, "A standing jump starts with a gather.");

            int framesOnGround = 0;
            while (motor.IsGrounded && framesOnGround < 60)
            {
                framesOnGround++;
                yield return null;
            }
            float expected = Move.jumpWindup / FrameTime;
            Assert.That(framesOnGround, Is.InRange(expected - 2f, expected + 3f), "She leaves the ground when the gather ends.");

            yield return Frames(10);
            Assert.That(heroine.LandDip, Is.LessThan(0.02f), "Her legs straighten as she pushes off.");
        }

        [UnityTest]
        public IEnumerator StandingJumpPlaysItsWholeClipAndLandsHeavily()
        {
            Assert.That(heroine.Config.standJump, Is.Not.Null, "The standing jump clip is missing.");
            input.PressJump();
            yield return Frames(3);
            Assert.That(heroine.CurrentPose, Is.EqualTo(HeroinePose.StandJump), "The gather is the start of the jump clip.");

            yield return Frames(30);
            Assert.That(motor.IsGrounded, Is.False);
            Assert.That(heroine.CurrentPose, Is.EqualTo(HeroinePose.StandJump), "The same clip carries on into the air.");

            for (int i = 0; i < 120 && !motor.IsGrounded; i++)
                yield return null;
            yield return Frames(2);
            Assert.That(heroine.CurrentPose, Is.EqualTo(HeroinePose.LandIdle), "A standing jump lands in the heavy landing.");
        }

        [UnityTest]
        public IEnumerator RunningJumpUsesTheStridingLeap()
        {
            input.Move = 1f;
            input.Run = true;
            yield return Frames(120);
            input.PressJump();
            yield return Frames(12);
            Assert.That(motor.IsGrounded, Is.False);
            Assert.That(heroine.CurrentPose, Is.EqualTo(HeroinePose.RunJump));

            for (int i = 0; i < 120 && !motor.IsGrounded; i++)
                yield return null;
            yield return Frames(2);
            Assert.That(heroine.CurrentPose, Is.EqualTo(HeroinePose.LandRun), "Landing at a run takes the weight and runs on.");
        }

        [UnityTest]
        public IEnumerator OnlyOneJump()
        {
            input.PressJump();
            yield return Frames(20);
            Assert.That(motor.IsGrounded, Is.False);
            float rising = motor.Velocity.y;

            input.PressJump();
            yield return Frames(2);
            Assert.That(motor.Velocity.y, Is.LessThan(rising), "A second press in the air does nothing.");

            float peak = motor.transform.position.y;
            for (int i = 0; i < 120 && !motor.IsGrounded; i++)
            {
                peak = Mathf.Max(peak, motor.transform.position.y);
                yield return null;
            }
            Assert.That(peak, Is.EqualTo(Move.jumpHeight).Within(0.2f), "The jump reaches its set height and no more.");
        }

        [UnityTest]
        public IEnumerator BodySwingsRoundWhenSheTurnsAtAWalk()
        {
            Transform model = player.GetComponentInChildren<Animator>().transform;
            Quaternion rest = model.localRotation;

            input.Move = 1f;
            yield return Frames(60);
            input.Move = -1f;
            yield return Frames(3);
            Assert.That(Quaternion.Angle(model.localRotation, rest), Is.GreaterThan(90f), "Her body lags behind the turn.");

            yield return Frames(Mathf.CeilToInt(heroine.Config.facingTurnTime / FrameTime) + 2);
            Assert.That(Quaternion.Angle(model.localRotation, rest), Is.LessThan(1f), "And has come round once the turn is done.");
        }

        [UnityTest]
        public IEnumerator CrouchesAndStandsBackUp()
        {
            input.CrouchHeld = true;
            yield return Frames(30);
            Assert.That(player.State, Is.EqualTo(PlayerState.Crouch));
            Assert.That(heroine.CurrentPose, Is.EqualTo(HeroinePose.CrouchIdle));

            input.CrouchHeld = false;
            yield return Frames(10);
            Assert.That(player.State, Is.EqualTo(PlayerState.Locomotion));
        }

        [UnityTest]
        public IEnumerator Screenshots()
        {
            string folder = System.Environment.GetEnvironmentVariable("PX_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(folder))
                Assert.Ignore("Set PX_SCREENSHOT_DIR to render the locomotion views.");
            Directory.CreateDirectory(folder);

            yield return Capture(folder, "locomotion_idle");
            input.PressJump();
            yield return Frames(10);
            yield return Capture(folder, "locomotion_stand_gather");
            yield return Frames(22);
            yield return Capture(folder, "locomotion_stand_air");
            for (int i = 0; i < 120 && !motor.IsGrounded; i++)
                yield return null;
            yield return Frames(12);
            yield return Capture(folder, "locomotion_stand_land");
            yield return Frames(60);
            input.Move = 1f;
            yield return Frames(70);
            yield return Capture(folder, "locomotion_walk");
            input.Run = true;
            yield return Frames(90);
            yield return Capture(folder, "locomotion_run");
            input.PressJump();
            yield return Frames(4);
            yield return Capture(folder, "locomotion_jump_gather");
            yield return Frames(20);
            yield return Capture(folder, "locomotion_jump_air");
            for (int i = 0; i < 120 && !motor.IsGrounded; i++)
                yield return null;
            yield return Frames(6);
            yield return Capture(folder, "locomotion_jump_land");
            input.Move = 0f;
            input.Run = false;
            yield return Frames(90);
            input.CrouchHeld = true;
            yield return Frames(40);
            yield return Capture(folder, "locomotion_crouch");
        }

        // No WaitForEndOfFrame: it never comes in batch mode. The camera renders to a texture on its own instead.
        private IEnumerator Capture(string folder, string name)
        {
            yield return null;
            Camera view = railCamera.GetComponent<Camera>();
            var target = new RenderTexture(1280, 720, 24);
            view.targetTexture = target;
            view.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            view.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
            target.Release();
        }
    }
}
