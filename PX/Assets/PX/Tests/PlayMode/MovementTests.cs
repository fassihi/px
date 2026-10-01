using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    public sealed class MovementTests : GrayboxFixture
    {
        // A flat, empty straight near the end of the graybox rail.
        private const float OpenStretch = 80f;

        [UnityTest]
        public IEnumerator AtStart_SheStandsOnTheFloorOnTheRail()
        {
            yield return null;

            Assert.That(Motor.IsGrounded, Is.True);
            Assert.That(Motor.transform.position.y, Is.EqualTo(0f).Within(0.1f));
            Assert.That(OffRail(), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator Run_ReachesRunSpeedAndTravelsAlongTheRail()
        {
            yield return PlaceAt(OpenStretch);
            float start = Motor.Distance;

            Input.Move = 1f;
            yield return Frames(60);

            Assert.That(Motor.Velocity.x, Is.EqualTo(Player.Move.runSpeed).Within(0.3f));
            Assert.That(Motor.Distance - start, Is.EqualTo(Player.Move.runSpeed).Within(1f), "about one second of running");
            Assert.That(Motor.Facing, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Run_Backwards_TurnsHerAround()
        {
            yield return PlaceAt(OpenStretch + 10f);
            float start = Motor.Distance;

            Input.Move = -1f;
            yield return Frames(30);

            Assert.That(Motor.Distance, Is.LessThan(start - 2f));
            Assert.That(Motor.Facing, Is.EqualTo(-1));
            Assert.That(Vector3.Dot(Motor.transform.forward, Rail.Evaluate(Motor.Distance).Tangent), Is.LessThan(-0.99f));
        }

        [UnityTest]
        public IEnumerator Jump_Held_ReachesTheAuthoredHeightAndLands()
        {
            yield return PlaceAt(OpenStretch);
            float ground = Motor.transform.position.y;

            Input.PressJump(hold: true);
            float peak = 0f;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                peak = Mathf.Max(peak, Motor.transform.position.y - ground);
            }

            Assert.That(peak, Is.EqualTo(Player.Move.jumpHeight).Within(0.35f));
            Assert.That(Motor.IsGrounded, Is.True, "she should be back on the floor after 1.5 s");
        }

        [UnityTest]
        public IEnumerator Jump_Tapped_IsAShortHop()
        {
            yield return PlaceAt(OpenStretch);
            float ground = Motor.transform.position.y;

            Input.PressJump(hold: false);
            float peak = 0f;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                peak = Mathf.Max(peak, Motor.transform.position.y - ground);
            }

            Assert.That(peak, Is.GreaterThan(0.5f));
            Assert.That(peak, Is.LessThan(Player.Move.jumpHeight * 0.6f));
        }

        [UnityTest]
        public IEnumerator Jump_SecondPressInTheAir_GoesHigher_ThirdDoesNothing()
        {
            yield return PlaceAt(OpenStretch);
            float ground = Motor.transform.position.y;

            float peak = 0f;
            Input.PressJump(hold: true);
            for (int i = 0; i < 180; i++)
            {
                yield return null;
                peak = Mathf.Max(peak, Motor.transform.position.y - ground);

                // Press again around the top of the first jump, and once more around the top of the second.
                if (i == 20 || i == 42)
                    Input.PressJump(hold: true);
            }

            float single = Player.Move.jumpHeight;
            Assert.That(peak, Is.GreaterThan(single * 1.6f), "the air jump should add height");
            Assert.That(peak, Is.LessThan(single * 2.3f), "there is only one air jump");
        }

        [UnityTest]
        public IEnumerator Dash_CoversItsDistanceQuicklyThenHandsBackControl()
        {
            yield return PlaceAt(OpenStretch);
            float start = Motor.Distance;
            MoveConfig move = Player.Move;

            Input.PressDash();
            yield return null;
            Assert.That(Player.State, Is.EqualTo(PlayerState.Dash));

            yield return Frames(Mathf.CeilToInt(move.dashDuration / FrameTime) + 2);

            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
            Assert.That(Motor.Distance - start, Is.EqualTo(move.dashSpeed * move.dashDuration).Within(0.8f));
        }

        [UnityTest]
        public IEnumerator Dash_OnTheGround_DoesNotUseUpTheGroundJump()
        {
            yield return PlaceAt(OpenStretch);

            Input.PressDash();
            yield return Frames(Mathf.CeilToInt(Player.Move.dashDuration / FrameTime) + 3);

            Assert.That(Motor.IsGrounded, Is.True, "a ground dash should stay on the ground");
        }

        [UnityTest]
        public IEnumerator Running_ThroughTheTurn_KeepsHerOnTheRail()
        {
            // The graybox rail bends between roughly 40 m and 56 m.
            yield return PlaceAt(37f);

            Input.Move = 1f;
            float worst = 0f;
            for (int i = 0; i < 150; i++)
            {
                yield return null;
                worst = Mathf.Max(worst, OffRail());
            }

            Assert.That(Motor.Distance, Is.GreaterThan(57f), "she should be out of the turn");
            Assert.That(worst, Is.LessThan(0.05f), "sideways drift from the rail's line");
            Assert.That(Motor.Distance, Is.EqualTo(Rail.Project(Motor.transform.position)).Within(0.1f),
                "tracked distance should match where she actually is");
        }

        [UnityTest]
        public IEnumerator Running_PastTheStartOfTheRail_StopsHerThere()
        {
            yield return PlaceAt(2f);

            Input.Move = -1f;
            yield return Frames(60);

            Assert.That(Motor.Distance, Is.EqualTo(0f).Within(0.01f));
            Vector3 offset = Motor.transform.position - Rail.Evaluate(0f).Position;
            offset.y = 0f;
            Assert.That(offset.magnitude, Is.LessThan(0.02f), "she stops exactly at the end, not past it");
        }

        [UnityTest]
        public IEnumerator Running_IntoAWall_StopsHer()
        {
            // A 2 m wall stands at 35.5 m to 36.5 m.
            yield return PlaceAt(33.5f);

            Input.Move = 1f;
            yield return Frames(60);

            Assert.That(Motor.Distance, Is.LessThan(35.5f));
            Assert.That(Motor.Velocity.x, Is.EqualTo(0f).Within(0.1f));
        }
    }
}
