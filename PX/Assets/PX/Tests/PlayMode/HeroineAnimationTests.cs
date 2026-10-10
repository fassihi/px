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
        public IEnumerator JumpingWhileRunning_PlaysTheRunningJump_UntilSheLands()
        {
            yield return PlaceAt(OpenStretch);
            Assume.That(Animator.Config.runJump, Is.Not.Null, "The animation config has no running jump.");

            Input.Move = 1f;
            yield return Frames(30);
            Input.PressJump();
            yield return Frames(3);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.RunJump));

            int airborne = 0;
            for (int i = 0; i < 120 && !Motor.IsGrounded; i++, airborne++)
            {
                Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.RunJump), $"She left the running jump {i} frames into the air.");
                yield return null;
            }
            Assert.That(airborne, Is.GreaterThan(10), "She was hardly in the air.");
            yield return Frames(3);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.Run));
        }

        [UnityTest]
        public IEnumerator ReversingWhileRunning_TurnsHerRound()
        {
            yield return PlaceAt(OpenStretch);
            Assume.That(Animator.Config.runTurn, Is.Not.Null, "The animation config has no run turn.");
            var body = Player.GetComponentInChildren<UnityEngine.Animator>();
            Transform leftHip = body.GetBoneTransform(HumanBodyBones.LeftUpperLeg), rightHip = body.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            float Facing() => Vector3.Dot(Vector3.Cross(rightHip.position - leftHip.position, Vector3.up).normalized, Player.transform.forward);

            Input.Move = 1f;
            yield return Frames(30);
            Input.Move = -1f;
            yield return Frames(2);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.Turn));
            Assert.That(Facing(), Is.LessThan(-0.5f), "At the start of the turn her body should still face the way she was running.");

            yield return Frames(Mathf.CeilToInt(Animator.Config.turnDuration * 60f) + 5);
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.Run));
            Assert.That(Facing(), Is.GreaterThan(0.5f), "After the turn her body should face the new way.");
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
        public IEnumerator DashingInTheAir_PlaysTheAirDash_UntilTheDashEnds()
        {
            yield return PlaceAt(OpenStretch);

            Input.PressJump();
            yield return Frames(10);
            Assume.That(Motor.IsGrounded, Is.False);
            Input.PressDash();
            yield return Frames(3);

            Assert.That(Player.State, Is.EqualTo(PlayerState.Dash));
            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.AirDash));

            for (int i = 0; i < 60 && Player.State == PlayerState.Dash; i++)
                yield return null;
            yield return Frames(2);
            Assert.That(Animator.CurrentPose, Is.Not.EqualTo(HeroinePose.AirDash), "The air dash pose outlasts the dash.");
        }

        [UnityTest]
        public IEnumerator Running_HerPlantedFootDoesNotSlide()
        {
            // The run clip plays at ground speed / Run Clip Speed, so the foot on the ground stays put in the world.
            yield return PlaceAt(OpenStretch);
            var body = Player.GetComponentInChildren<UnityEngine.Animator>();
            // The ball of the foot, not the ankle: the ankle rolls forward over a planted foot.
            Transform left = body.GetBoneTransform(HumanBodyBones.LeftToes), right = body.GetBoneTransform(HumanBodyBones.RightToes);

            Input.Move = 1f;
            yield return Frames(40);
            // Only frames where a toe is on the ground count: each foot is down about a quarter of the stride.
            var slides = new System.Collections.Generic.List<float>();
            Vector3 lastLeft = left.position, lastRight = right.position;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                // Along the rail only: a planted foot also pivots a little sideways on its ball, which is not a slide.
                float ground = Player.transform.position.y + 0.03f;
                Vector3 along = Rail.Evaluate(Motor.Distance).Tangent;
                foreach (var (toe, last) in new[] { (left, lastLeft), (right, lastRight) })
                {
                    if (toe.position.y < ground && last.y < ground)
                        slides.Add(Mathf.Abs(Vector3.Dot(toe.position - last, along)) / FrameTime);
                }
                lastLeft = left.position;
                lastRight = right.position;
            }

            Assume.That(Motor.Velocity.x, Is.EqualTo(Player.Move.runSpeed).Within(0.3f));
            Assert.That(slides.Count, Is.GreaterThan(10), "Her toes hardly touch the ground while she runs.");
            slides.Sort();
            float typical = slides[slides.Count / 2];
            Assert.That(typical, Is.LessThan(0.15f * Player.Move.runSpeed),
                $"Her planted foot slides at {typical:0.0} m/s while she runs at {Player.Move.runSpeed:0.0} m/s.");
        }

        [UnityTest]
        public IEnumerator Running_SheLooksWhereSheRuns()
        {
            // Her head bone points out of her face. A twisted avatar reference pose turned it to her side in every
            // retargeted Mixamo clip, while her own idle looked fine.
            yield return PlaceAt(OpenStretch);
            var body = Player.GetComponentInChildren<UnityEngine.Animator>();
            Transform head = body.GetBoneTransform(HumanBodyBones.Head);

            Input.Move = 1f;
            float worst = 0f;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (i < 20)
                    continue;
                Vector3 face = Vector3.ProjectOnPlane(head.up, Vector3.up);
                Vector3 ahead = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up);
                worst = Mathf.Max(worst, Vector3.Angle(face, ahead));
            }

            Assert.That(Animator.CurrentPose, Is.EqualTo(HeroinePose.Run));
            Assert.That(worst, Is.LessThan(35f), $"Her face turns {worst:0} degrees away from where she runs.");
        }

        [UnityTest]
        public IEnumerator Dashing_KeepsHerHipsAboveTheGround()
        {
            // A roll clip retargeted onto her dips her hips to the floor and buries her feet; the dash must not.
            yield return PlaceAt(OpenStretch);
            var body = Player.GetComponentInChildren<UnityEngine.Animator>();
            Transform hips = body.GetBoneTransform(HumanBodyBones.Hips);

            Input.Move = 1f;
            Input.PressDash();
            float lowest = float.MaxValue;
            for (int i = 0; i < 12; i++)
            {
                yield return null;
                if (Player.State == PlayerState.Dash)
                    lowest = Mathf.Min(lowest, hips.position.y - Player.transform.position.y);
            }

            Assert.That(lowest, Is.Not.EqualTo(float.MaxValue), "She never dashed.");
            Assert.That(lowest, Is.GreaterThan(0.6f), $"Her hips drop to {lowest:0.00} m above her feet during the dash.");
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
            Transform hand = body.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.That(hand, Is.Not.Null, "The model has no right hand bone. Is it imported as a humanoid?");

            Input.Move = 1f;
            yield return Frames(20);
            Vector3 first = Player.transform.InverseTransformPoint(hand.position);
            yield return Frames(10);
            Vector3 second = Player.transform.InverseTransformPoint(hand.position);

            Assert.That((second - first).magnitude, Is.GreaterThan(0.02f), "Her hand does not move while she runs.");
        }

        [UnityTest]
        public IEnumerator Idle_StandsUpright_NotLeaningBack()
        {
            // Clips from a different Tripo export than the body lean her backwards, so check the head sits over the hips.
            yield return PlaceAt(OpenStretch);
            yield return Frames(60);
            var body = Player.GetComponentInChildren<UnityEngine.Animator>();
            Vector3 headOverHips = Player.transform.InverseTransformVector(
                body.GetBoneTransform(HumanBodyBones.Head).position - body.GetBoneTransform(HumanBodyBones.Hips).position);

            Assert.That(Mathf.Abs(headOverHips.z) / headOverHips.y, Is.LessThan(0.2f),
                $"Her head is {headOverHips.z:0.00} m in front of her hips while idle: she is leaning.");
        }

        [UnityTest]
        public IEnumerator EachComboStep_PlaysItsOwnClip()
        {
            yield return PlaceAt(85f);
            AnimationClip[] perStep = Player.GetComponent<HeroineAnimator>().Config.comboAttacks;
            Assume.That(perStep.Length, Is.GreaterThan(1), "The animation config has no per-step attack clips.");

            var seen = new System.Collections.Generic.Dictionary<int, AnimationClip>();
            for (int i = 0; i < 90; i++)
            {
                if (i % 5 == 0)
                    Input.PressAttack();
                yield return null;
                if (Player.State == PlayerState.Attack)
                    seen[Player.ComboIndex] = Animator.AttackClip;
            }

            Assert.That(seen.Count, Is.GreaterThan(1), "The combo did not get past its first swing.");
            foreach (var step in seen)
                Assert.That(step.Value, Is.EqualTo(perStep[Mathf.Min(step.Key, perStep.Length - 1)]), $"Combo step {step.Key} plays the wrong clip.");
            Assert.That(seen[1], Is.Not.EqualTo(seen[0]), "The first two swings play the same clip.");
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
