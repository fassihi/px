using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    /// <summary>Catching ledges, hanging from them, climbing onto them and letting go, on the climbing walls past Enemy A.</summary>
    public sealed class LedgeTests : GrayboxFixture
    {
        // The first climbing wall runs from 130 m along the rail and is 4.5 m tall: too tall to jump onto.
        private const float WallFace = 130f;
        private const float WallTop = 4.5f;

        private IEnumerator JumpAtTheWall()
        {
            yield return PlaceAt(WallFace - 1f);
            Input.Move = 1f;
            yield return Frames(15);
            Input.PressJump();
            for (int i = 0; i < 120 && Player.State != PlayerState.Hang; i++)
                yield return null;
            // Still pushing toward the wall, she would pull herself straight up.
            Input.Move = 0f;
        }

        [UnityTest]
        public IEnumerator JumpingAtAWallTooTallToClear_SheCatchesItsTop()
        {
            yield return JumpAtTheWall();
            Assert.That(Player.State, Is.EqualTo(PlayerState.Hang), "she did not catch the ledge");
            Assert.That(Player.LedgeTop, Is.EqualTo(WallTop).Within(0.05f));
            Assert.That(Motor.transform.position.y, Is.EqualTo(WallTop - Player.Move.hangHeight).Within(0.05f),
                "she hangs by her hands from the top");

            yield return Frames(30);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Hang), "she stays up while she hangs");
        }

        [UnityTest]
        public IEnumerator HoldingTowardTheWall_SheCatchesItAndPullsStraightUp()
        {
            yield return PlaceAt(WallFace - 1f);
            Input.Move = 1f;
            yield return Frames(15);
            Input.PressJump();
            bool hung = false, climbed = false;
            for (int i = 0; i < 240 && !(climbed && Player.State == PlayerState.Locomotion); i++)
            {
                yield return null;
                hung |= Player.State == PlayerState.Hang;
                climbed |= Player.State == PlayerState.Climb;
            }
            Assert.That(hung && climbed, Is.True, "she should catch the ledge, then climb it");
            Assert.That(Motor.transform.position.y, Is.EqualTo(WallTop).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator Hanging_JumpClimbsOntoTheTop()
        {
            yield return JumpAtTheWall();
            Assume.That(Player.State, Is.EqualTo(PlayerState.Hang));
            Input.Move = 0f;
            yield return Frames(Mathf.CeilToInt(Player.Move.hangSettle / FrameTime) + 1);

            Input.PressJump();
            yield return Frames(2);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Climb));
            yield return Frames(Mathf.CeilToInt(Player.Move.climbDuration / FrameTime) + 10);

            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
            Assert.That(Motor.IsGrounded, Is.True);
            Assert.That(Motor.transform.position.y, Is.EqualTo(WallTop).Within(0.1f), "she stands on the top");
            Assert.That(Motor.Distance, Is.GreaterThan(WallFace), "past the edge");
        }

        [UnityTest]
        public IEnumerator Hanging_DownLetsGo()
        {
            yield return JumpAtTheWall();
            Assume.That(Player.State, Is.EqualTo(PlayerState.Hang));
            Input.Move = 0f;
            yield return Frames(Mathf.CeilToInt(Player.Move.hangSettle / FrameTime) + 1);

            Input.CrouchHeld = true;
            yield return Frames(3);
            Input.CrouchHeld = false;
            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
            for (int i = 0; i < 120 && !Motor.IsGrounded; i++)
                yield return null;
            Assert.That(Motor.transform.position.y, Is.LessThan(0.2f), "she dropped to the floor");
        }

        [UnityTest]
        public IEnumerator AWallShortEnoughToJumpOnto_IsNotGrabbed()
        {
            // The step at 8-11 m is 1.5 m tall: she lands on it.
            yield return PlaceAt(6.5f);
            Input.Move = 1f;
            yield return Frames(10);
            Input.PressJump();
            bool hung = false;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                hung |= Player.State == PlayerState.Hang;
            }
            Assert.That(hung, Is.False);
        }
    }
}
