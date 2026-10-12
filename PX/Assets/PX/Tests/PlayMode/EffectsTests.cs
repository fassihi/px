using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    /// <summary>The trails of her air dash and double jump, and the procedural motion on the enemies.</summary>
    public sealed class EffectsTests : GrayboxFixture
    {
        private static EnemyController Named(string name)
        {
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy.name == name)
                    return enemy;
            }
            Assert.Fail("No enemy called " + name);
            return null;
        }

        [UnityTest]
        public IEnumerator AirDash_DrawsATrail_ThatEndsWithTheDash()
        {
            var effects = Object.FindFirstObjectByType<MovementEffects>();
            Assert.That(effects, Is.Not.Null, "the scene has movement effects");
            yield return PlaceAt(20f);
            Assert.That(effects.IsTrailing, Is.False);

            Input.PressJump();
            yield return Frames(25);
            Assert.That(Motor.IsGrounded, Is.False);
            Input.Move = 1f;
            Input.PressDash();
            yield return Frames(5);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Dash));
            Assert.That(effects.IsTrailing, Is.True, "the air dash leaves a trail");

            yield return Frames(30);
            Assert.That(effects.IsTrailing, Is.False, "and it stops with the dash");
        }

        [UnityTest]
        public IEnumerator DoubleJump_DrawsATrail_ButTheFirstJumpDoesNot()
        {
            var effects = Object.FindFirstObjectByType<MovementEffects>();
            yield return PlaceAt(20f);

            Input.PressJump();
            yield return Frames(20);
            Assert.That(effects.IsTrailing, Is.False, "the first jump is plain");

            Input.PressJump();
            yield return Frames(3);
            Assert.That(effects.IsTrailing, Is.True, "the second jump trails");
            yield return Frames(60);
            Assert.That(effects.IsTrailing, Is.False, "and the trail ends");
        }

        [UnityTest]
        public IEnumerator Enemy_LeansIntoItsRun_AndSwivelsRoundWhenItTurns()
        {
            EnemyController enemy = Named("EnemyA");
            var animator = enemy.GetComponentInChildren<Animator>();
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            float Pitch() => Vector3.SignedAngle(Vector3.up, spine.up, enemy.transform.right);

            float idle = Pitch();
            yield return PlaceAt(enemy.Motor.Distance - 8.5f);
            float lean = float.MinValue;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                if (enemy.State == EnemyState.Run)
                    lean = Mathf.Max(lean, Pitch());
            }
            Assert.That(lean, Is.GreaterThan(idle + 3f), "it leans into the run");

            // Turning: it starts out facing the old way and comes round over several frames. Its brain is off, or it
            // would turn straight back to her.
            enemy.InputSource = new ScriptedInput();
            yield return Frames(30);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            // Which way its shoulders face: +1 when they square to the way it faces now, -1 when it is turned round.
            float Squareness() => Vector3.Dot((rightArm.position - left.position).normalized, enemy.transform.right);

            float steady = Squareness();
            enemy.Motor.Facing = -enemy.Motor.Facing;
            yield return Frames(2);
            float first = Squareness();
            yield return Frames(40);
            float later = Squareness();
            Assert.That(later, Is.EqualTo(steady).Within(0.3f), "it ends up facing the new way");
            Assert.That(Mathf.Abs(first - later), Is.GreaterThan(0.8f), "it swings round and does not snap");
        }

        [UnityTest]
        public IEnumerator Enemy_JumpsUpAndOverTheCrate_NotJustLiftsOntoIt()
        {
            EnemyController enemy = Named("EnemyA");
            Motor.Teleport(Rail, 118.5f, 1.5f);
            Motor.Facing = -1;
            RailCamera.Snap();

            float top = 0f;
            float lowestInCrouch = float.MaxValue;
            for (int i = 0; i < 700; i++)
            {
                yield return null;
                if (enemy.State == EnemyState.Climb)
                {
                    top = Mathf.Max(top, enemy.transform.position.y);
                    if (enemy.StateTime < 0.2f)
                        lowestInCrouch = Mathf.Min(lowestInCrouch, enemy.transform.position.y);
                }
                else if (top > 0f)
                    break;
            }
            Assert.That(top, Is.GreaterThan(1.4f + 0.04f), "it arcs above the crate's top on the way over");
            Assert.That(lowestInCrouch, Is.LessThan(0.2f), "and crouches on the ground before it leaps");
        }
    }
}
