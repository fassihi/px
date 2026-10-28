using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PX.Tests
{
    /// <summary>Input a test can script frame by frame. Presses last one frame, like a real button.</summary>
    public sealed class ScriptedInput : IInputSource
    {
        public float Move;
        public bool Run;
        public bool JumpHeld;
        public bool CrouchHeld;
        public bool GuardHeld;

        private bool jumpPressed;
        private bool guardPressed;
        private bool dashPressed;
        private bool attackPressed;
        private bool interactPressed;
        private bool flaskPressed;

        public void PressJump(bool hold = true)
        {
            jumpPressed = true;
            JumpHeld = hold;
        }

        public void PressDash() => dashPressed = true;

        public void PressAttack() => attackPressed = true;

        public void PressInteract() => interactPressed = true;

        public void PressFlask() => flaskPressed = true;

        /// <summary>Puts the guard up (and keeps it up while <see cref="GuardHeld"/> stays true).</summary>
        public void PressGuard(bool hold = true)
        {
            guardPressed = true;
            GuardHeld = hold;
        }

        public InputFrame Read()
        {
            var frame = new InputFrame
            {
                Move = Move,
                Run = Run,
                JumpHeld = JumpHeld,
                JumpPressed = jumpPressed,
                DashPressed = dashPressed,
                AttackPressed = attackPressed,
                CrouchHeld = CrouchHeld,
                GuardPressed = guardPressed,
                GuardHeld = GuardHeld,
                InteractPressed = interactPressed,
                FlaskPressed = flaskPressed,
            };
            jumpPressed = dashPressed = attackPressed = guardPressed = interactPressed = flaskPressed = false;
            return frame;
        }
    }

    /// <summary>
    /// Loads the graybox scene with the heroine under scripted control, at a fixed 60 steps per second
    /// so results do not depend on how fast the machine runs the test.
    /// </summary>
    public abstract class GrayboxFixture
    {
        protected const float FrameTime = 1f / 60f;

        protected PlayerController Player;
        protected CharacterMotor Motor;
        protected Rail Rail;
        protected RailCamera RailCamera;
        protected ScriptedInput Input;

        [UnitySetUp]
        public IEnumerator LoadGraybox()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = FrameTime;
            yield return SceneManager.LoadSceneAsync("Graybox", LoadSceneMode.Single);

            Player = Object.FindFirstObjectByType<PlayerController>();
            RailCamera = Object.FindFirstObjectByType<RailCamera>();
            Assert.That(Player, Is.Not.Null, "The graybox scene has no player. Rebuild it from the PX menu.");
            Assert.That(RailCamera, Is.Not.Null, "The graybox scene has no rail camera.");

            Motor = Player.GetComponent<CharacterMotor>();
            Rail = Motor.Rail;
            Input = new ScriptedInput();
            Player.InputSource = Input;

            // Tests that count on an enemy's first swing get it: the chance of opening with the second is rolled in its own test.
            secondOpeners.Clear();
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                secondOpeners[enemy.Config] = enemy.Config.secondOpenerChance;
                enemy.Config.secondOpenerChance = 0f;
            }

            yield return Frames(20);
        }

        private readonly System.Collections.Generic.Dictionary<EnemyConfig, float> secondOpeners =
            new System.Collections.Generic.Dictionary<EnemyConfig, float>();

        [TearDown]
        public void RestoreTime()
        {
            foreach (var pair in secondOpeners)
                pair.Key.secondOpenerChance = pair.Value;
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            CameraShake.Clear();
        }

        protected static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        /// <summary>Stands her on the floor at a rail distance and waits for her to settle.</summary>
        protected IEnumerator PlaceAt(float distance)
        {
            Input.Move = 0f;
            Input.JumpHeld = false;
            Motor.Teleport(Rail, distance, 0.1f);
            RailCamera.Snap();
            yield return Frames(15);
        }

        /// <summary>How far she is from the rail's line, ignoring height.</summary>
        protected float OffRail()
        {
            RailPoint point = Rail.Evaluate(Motor.Distance);
            Vector3 offset = Motor.transform.position - point.Position;
            offset.y = 0f;
            return (offset - point.Tangent * Vector3.Dot(offset, point.Tangent)).magnitude;
        }
    }
}
