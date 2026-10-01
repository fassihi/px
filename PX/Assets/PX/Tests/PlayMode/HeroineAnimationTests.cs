using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    public sealed class HeroineAnimationTests : GrayboxFixture
    {
        private const float OpenStretch = 80f;

        private HeroineAnimator Animator => Player.GetComponent<HeroineAnimator>();

        [UnityTest]
        public IEnumerator Standing_PlaysIdle()
        {
            yield return PlaceAt(OpenStretch);

            Assert.That(Animator, Is.Not.Null, "The player has no HeroineAnimator. Delete Player.prefab and rebuild the scene.");
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.Idle));
        }

        [UnityTest]
        public IEnumerator Running_PlaysRun_AndStoppingGoesBackToIdle()
        {
            yield return PlaceAt(OpenStretch);

            Input.Move = 1f;
            yield return Frames(30);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.Run));

            Input.Move = 0f;
            yield return Frames(60);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.Idle));
        }

        [UnityTest]
        public IEnumerator Jumping_PlaysJumpPoses()
        {
            yield return PlaceAt(OpenStretch);

            Input.PressJump();
            yield return Frames(3);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.JumpStart));

            // She switches to the looping pose once she starts falling.
            for (int i = 0; i < 90 && Motor.Velocity.y >= 0f; i++)
                yield return null;
            yield return Frames(2);
            Assert.That(Motor.IsGrounded, Is.False);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.JumpLoop));
        }

        [UnityTest]
        public IEnumerator Dashing_PlaysDash()
        {
            yield return PlaceAt(OpenStretch);

            Input.PressDash();
            yield return Frames(3);

            Assert.That(Player.State, Is.EqualTo(PlayerState.Dash));
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.Dash));
        }

        [UnityTest]
        public IEnumerator Attacking_PlaysAttack()
        {
            yield return PlaceAt(OpenStretch);

            Input.PressAttack();
            yield return Frames(3);

            Assert.That(Player.State, Is.EqualTo(PlayerState.Attack));
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.Attack));
        }

        [UnityTest]
        public IEnumerator TheBody_FollowsTheAnimation()
        {
            yield return PlaceAt(OpenStretch);
            var body = Player.GetComponentInChildren<UnityEngine.Animator>();
            Transform hand = FindBone(body.transform, "hand_r");
            Assert.That(hand, Is.Not.Null, "No bone named hand_r on the model.");

            Input.Move = 1f;
            yield return Frames(20);
            Vector3 first = Player.transform.InverseTransformPoint(hand.position);
            yield return Frames(10);
            Vector3 second = Player.transform.InverseTransformPoint(hand.position);

            Assert.That((second - first).magnitude, Is.GreaterThan(0.02f), "Her hand does not move while she runs.");
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
                if (t.name == boneName)
                    return t;
            return null;
        }
    }
}
