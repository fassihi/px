using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    public sealed class CrouchTests : GrayboxFixture
    {
        private const float OpenStretch = 85f;

        private HeroineAnimator Animator => Player.GetComponent<HeroineAnimator>();

        private float ColliderHeight => Player.GetComponent<CharacterController>().height;

        [UnityTest]
        public IEnumerator HoldingDown_Crouches_AndLetGoStandsUp()
        {
            yield return PlaceAt(OpenStretch);
            float standing = ColliderHeight;

            Input.CrouchHeld = true;
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Crouch));
            Assert.That(ColliderHeight, Is.EqualTo(Player.Move.crouchHeight).Within(0.01f), "her collider should drop so she fits behind cover");
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.CrouchDown));

            yield return Frames(40);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.CrouchIdle));

            Input.CrouchHeld = false;
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
            Assert.That(ColliderHeight, Is.EqualTo(standing).Within(0.01f));
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.CrouchUp));

            yield return Frames(40);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.Idle));
        }

        [UnityTest]
        public IEnumerator Crouching_SheTurnsButDoesNotWalk()
        {
            yield return PlaceAt(OpenStretch);
            Input.CrouchHeld = true;
            yield return Frames(10);
            float start = Motor.Distance;

            Input.Move = -1f;
            yield return Frames(30);

            Assert.That(Motor.Facing, Is.EqualTo(-1), "she can turn round in cover");
            Assert.That(Motor.Distance, Is.EqualTo(start).Within(0.05f), "but she does not walk");
            Assert.That(Player.State, Is.EqualTo(PlayerState.Crouch));
        }

        [UnityTest]
        public IEnumerator UnderALowCeiling_SheStaysDownUntilThereIsRoom()
        {
            yield return PlaceAt(OpenStretch);
            Input.CrouchHeld = true;
            yield return Frames(10);

            // A slab just above her crouched head and below her standing one.
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.transform.position = Motor.transform.position + Vector3.up * (Player.Move.crouchHeight + 0.25f);
            slab.transform.localScale = new Vector3(3f, 0.2f, 3f);
            yield return Frames(2);

            Input.CrouchHeld = false;
            yield return Frames(10);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Crouch), "there is no room to stand");

            Object.Destroy(slab);
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
        }

        [UnityTest]
        public IEnumerator AttackingFromACrouch_Sweeps_AndStaysDown()
        {
            yield return PlaceAt(OpenStretch);
            Input.CrouchHeld = true;
            yield return Frames(10);

            Input.PressAttack();
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Attack));
            Assert.That(Player.IsCrouchAttack, Is.True);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.CrouchAttack));
            Assert.That(ColliderHeight, Is.EqualTo(Player.Move.crouchHeight).Within(0.01f), "the sweep is low");

            yield return Frames(Mathf.CeilToInt(Player.Combo.crouchAttack.Total / FrameTime) + 2);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Crouch));
        }

        [UnityTest]
        public IEnumerator TheSweep_HitsTheDummy()
        {
            // The first dummy stands at 62 m.
            yield return PlaceAt(60.6f);
            Health dummy = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None)
                .OrderBy(d => (d.transform.position - Motor.transform.position).sqrMagnitude)
                .First()
                .GetComponent<Health>();

            Input.CrouchHeld = true;
            yield return Frames(10);
            Input.PressAttack();
            yield return Frames(40);

            Assert.That(dummy.Current, Is.EqualTo(dummy.Max - Player.Combo.crouchAttack.damage).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator JumpingFromACrouch_StandsAndJumps()
        {
            yield return PlaceAt(OpenStretch);
            Input.CrouchHeld = true;
            yield return Frames(10);

            Input.PressJump();
            yield return Frames(5);

            Assert.That(Motor.IsGrounded, Is.False);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
            Assert.That(Motor.IsCrouched, Is.False);
        }
    }
}
