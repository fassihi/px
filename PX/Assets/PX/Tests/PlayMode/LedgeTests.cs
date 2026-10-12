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
            yield return Frames(30);
            Assert.That(Motor.transform.position.y, Is.EqualTo(WallTop - Player.Move.hangHeight).Within(0.05f),
                "she hangs by her hands from the top");
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
            yield return Frames(Mathf.CeilToInt((Player.Move.climbDuration + Player.Move.climbSettle) / FrameTime) + 10);

            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
            Assert.That(Motor.IsGrounded, Is.True);
            Assert.That(Motor.transform.position.y, Is.EqualTo(WallTop).Within(0.1f), "she stands on the top");
            Assert.That(Motor.Distance, Is.GreaterThan(WallFace), "past the edge");
        }

        /// <summary>Where her hands are, in rail space: distance along the rail and height.</summary>
        private Vector2 Hands()
        {
            Animator animator = Player.GetComponentInChildren<Animator>();
            Vector3 hands = (animator.GetBoneTransform(HumanBodyBones.LeftHand).position
                             + animator.GetBoneTransform(HumanBodyBones.RightHand).position) * 0.5f;
            return new Vector2(Rail.Project(hands), hands.y);
        }

        [UnityTest]
        public IEnumerator HerHands_StayOnTheLedge_ThroughTheCatchTheHangAndTheClimb_WithoutPopping()
        {
            yield return JumpAtTheWall();
            Assume.That(Player.State, Is.EqualTo(PlayerState.Hang));
            Input.Move = 0f;

            Vector2 last = Hands();
            float worstStep = 0f, worstHeight = 0f, worstReach = 0f;
            bool climbing = false;
            var trace = new System.Text.StringBuilder();
            for (int i = 0; i < 160 && Player.State != PlayerState.Locomotion; i++)
            {
                if (i == 40)
                {
                    Input.PressJump();
                    climbing = true;
                }
                yield return null;
                if (Player.State != PlayerState.Hang && Player.State != PlayerState.Climb)
                    break;

                Vector2 hands = Hands();
                trace.AppendLine($"{i} {Player.State} t={Player.StateTime:0.00} handsDy={hands.y - Player.LedgeTop:0.00} handsDx={(hands.x - Player.LedgeWall) * Motor.Facing:0.00} rootY={Motor.transform.position.y - Player.LedgeTop:0.00}");
                if (Player.StateTime > 0.1f || Player.State == PlayerState.Climb)
                    worstStep = Mathf.Max(worstStep, (hands - last).magnitude);
                last = hands;

                // Her hands are on the lip until she lets go of it, two thirds of the way through the climb.
                bool gripping = Player.State == PlayerState.Hang
                    || Player.StateTime < Player.Move.climbDuration * Player.Move.climbRelease;
                if (gripping && Player.StateTime > 0.1f)
                {
                    worstHeight = Mathf.Max(worstHeight, Mathf.Abs(hands.y - Player.LedgeTop));
                    worstReach = Mathf.Max(worstReach, Mathf.Abs((hands.x - Player.LedgeWall) * Motor.Facing - 0.1f));
                }
            }
            Assert.That(climbing, Is.True);
            if (worstHeight >= 0.3f || worstReach >= 0.3f || worstStep >= 0.2f)
                Debug.Log(trace.ToString());
            Assert.That(worstHeight, Is.LessThan(0.3f), "her hands stay at the height of the ledge's top");
            Assert.That(worstReach, Is.LessThan(0.3f), "and over its lip, not out in the air in front of the wall");
            Assert.That(worstStep, Is.LessThan(0.2f), "and do not jump from one frame to the next");
        }

        [UnityTest]
        public IEnumerator TheClimbEnds_WithoutHerBodyJumping()
        {
            yield return JumpAtTheWall();
            Assume.That(Player.State, Is.EqualTo(PlayerState.Hang));
            Input.Move = 0f;
            yield return Frames(Mathf.CeilToInt(Player.Move.hangSettle / FrameTime) + 1);
            Input.PressJump();

            Animator animator = Player.GetComponentInChildren<Animator>();
            Vector3 last = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            float worst = 0f;
            var trace = new System.Text.StringBuilder();
            for (int i = 0; i < 200; i++)
            {
                yield return null;
                Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                float step = (hips - last).magnitude;
                trace.AppendLine($"{i} {Player.State} t={Player.StateTime:0.00} step={step:0.000} hipsY={hips.y:0.00} rootY={Motor.transform.position.y:0.00} footY={Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y):0.00} pos={animator.transform.localPosition} wpos={animator.transform.position.y:0.00}");
                if (i > 3) worst = Mathf.Max(worst, step);
                last = hips;
                if (Player.State == PlayerState.Locomotion && Player.StateTime > 0.3f)
                    break;
            }
            Debug.Log(trace.ToString());
            Assert.That(worst, Is.LessThan(0.12f), "her hips do not pop between frames");
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
