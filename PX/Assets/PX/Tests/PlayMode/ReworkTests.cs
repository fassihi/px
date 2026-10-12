using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    /// <summary>The air attack, the enemies' two swings, their run, their ragdoll death and reviving them.</summary>
    public sealed class ReworkTests : GrayboxFixture
    {
        private static EnemyController Named(string name)
        {
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy.name.StartsWith(name))
                    return enemy;
            }
            return null;
        }

        private static void Kill(EnemyController enemy, GameObject by, Vector3 direction)
        {
            for (int i = 0; i < 40 && !enemy.Health.IsDead; i++)
                enemy.Health.TakeHit(new Hit(40f, direction, by, 8f));
        }

        [UnityTest]
        public IEnumerator AttackingInTheAir_ThrowsTheFlyingKick_AndLandingEndsIt()
        {
            yield return PlaceAt(16f);
            float start = Motor.Distance;
            Input.PressJump();
            yield return Frames(14);
            Assume.That(Motor.IsGrounded, Is.False);

            Input.PressAttack();
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Attack));
            Assert.That(Player.IsAirAttack, Is.True, "an attack in the air is its own move");
            Assert.That(Player.ComboIndex, Is.EqualTo(0));
            yield return Frames(3);
            Assert.That(GetAnimator().CurrentPose, Is.EqualTo(HeroinePose.AirAttack));

            for (int i = 0; i < 120 && Player.State == PlayerState.Attack; i++)
                yield return null;
            Assert.That(Player.State, Is.Not.EqualTo(PlayerState.Attack), "the kick ends");
            Assert.That(Motor.Distance, Is.GreaterThan(start + 0.5f), "and it carried her forward");

            for (int i = 0; i < 120 && !Motor.IsGrounded; i++)
                yield return null;
            Assert.That(Motor.IsGrounded, Is.True);
            yield return Frames(5);
            Input.PressAttack();
            yield return Frames(3);
            Assert.That(Player.IsAirAttack, Is.False, "on the ground the combo starts as before");
        }

        [UnityTest]
        public IEnumerator TheAirAttack_HitsWhatIsInFrontOfHer()
        {
            EnemyController enemy = Named("EnemyA");
            yield return PlaceAt(enemy.Motor.Distance - 3.4f);
            Input.PressJump();
            // The kick comes on the way down, onto the enemy.
            for (int i = 0; i < 90 && Motor.Velocity.y > -1f; i++)
                yield return null;
            Input.Move = 1f;
            Input.PressAttack();
            float before = enemy.Health.Current;
            string trace = "";
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (Player.IsHitboxActive)
                    trace += $"[{i}: gap {enemy.Motor.Distance - Motor.Distance:0.00}, her y {Motor.transform.position.y:0.00}, enemy {enemy.State}] ";
            }
            Assert.That(enemy.Health.Current, Is.LessThan(before), "the kick landed. " + trace);
        }

        [UnityTest]
        public IEnumerator Enemies_CanOpenWithEitherSwing()
        {
            foreach (string name in new[] { "EnemyA", "EnemyB" })
            {
                EnemyController enemy = Named(name);
                enemy.Config.secondOpenerChance = 1f;
                yield return PlaceAt(enemy.Motor.Distance - 1.4f);
                for (int i = 0; i < 240 && enemy.State != EnemyState.Attack; i++)
                    yield return null;
                Assert.That(enemy.State, Is.EqualTo(EnemyState.Attack), name);
                Assert.That(enemy.ComboIndex, Is.EqualTo(1), $"{name} opens with its second swing when it rolls it");
                enemy.Config.secondOpenerChance = 0f;
                yield return Frames(120);
            }
        }

        [UnityTest]
        public IEnumerator FromAFar_AnEnemyRuns_AndWalksOnceItIsNear()
        {
            EnemyController enemy = Named("EnemyA");
            yield return PlaceAt(enemy.Motor.Distance - 8.5f);
            bool ran = false;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                ran |= enemy.State == EnemyState.Run;
            }
            Assert.That(ran, Is.True, "it runs while she is far");
            for (int i = 0; i < 240 && enemy.State != EnemyState.Attack; i++)
                yield return null;
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Attack), "it arrives and swings");
        }

        [UnityTest]
        public IEnumerator WhenItDies_ItBecomesARagdoll_AndRevivingStandsItUpAgain()
        {
            EnemyController enemy = Named("EnemyA");
            yield return PlaceAt(enemy.Motor.Distance - 3f);
            Assert.That(enemy.Ragdoll, Is.Not.Null, "the enemy has a ragdoll");
            Assert.That(enemy.Ragdoll.BodyCount, Is.GreaterThanOrEqualTo(10));
            Animator animator = enemy.GetComponentInChildren<Animator>();

            Kill(enemy, Player.gameObject, Vector3.right);
            yield return Frames(3);
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Dead));
            Assert.That(enemy.IsRagdoll, Is.True);
            Assert.That(animator.enabled, Is.False, "the animation lets go of the body");

            Vector3 hipsStart = enemy.Ragdoll.HipsPosition;
            yield return Frames(150);
            Vector3 hips = enemy.Ragdoll.HipsPosition;
            Assert.That(hips.y, Is.LessThan(hipsStart.y - 0.3f), "it fell");
            Assert.That(hips.y, Is.GreaterThan(-0.5f), "and did not fall through the floor");
            Assert.That(float.IsNaN(hips.x), Is.False);

            enemy.Revive();
            yield return Frames(30);
            Assert.That(enemy.IsRagdoll, Is.False);
            Assert.That(animator.enabled, Is.True, "the animation takes over again");
            Assert.That(enemy.Health.Current, Is.EqualTo(enemy.Health.Max));
            Assert.That(enemy.State, Is.Not.EqualTo(EnemyState.Dead));
            Assert.That(enemy.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(enemy.Ragdoll.HipsPosition.y, Is.GreaterThan(0.5f), "standing again");
        }

        [UnityTest]
        public IEnumerator TheReviveButton_BringsBackEveryDeadEnemy()
        {
            var overlay = Object.FindFirstObjectByType<GrayboxOverlay>();
            Assert.That(overlay, Is.Not.Null);
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                Kill(enemy, Player.gameObject, Vector3.right);
            yield return Frames(10);
            overlay.ReviveEnemies();
            yield return Frames(10);
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                Assert.That(enemy.Health.IsDead, Is.False, enemy.name);
        }

        private HeroineAnimator GetAnimator() => Player.GetComponentInChildren<HeroineAnimator>();
    }
}
