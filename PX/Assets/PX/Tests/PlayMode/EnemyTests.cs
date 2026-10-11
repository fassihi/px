using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    public sealed class EnemyTests : GrayboxFixture
    {
        private EnemyController Enemy => Object.FindFirstObjectByType<EnemyController>();

        [UnityTest]
        public IEnumerator EnemyA_WaitsAtTheEndOfTheLevel_IdleWhileSheIsFarAway()
        {
            yield return PlaceAt(90f);
            EnemyController enemy = Enemy;
            Assert.That(enemy, Is.Not.Null, "No enemy in the scene. Rebuild the graybox scene.");
            Assert.That(enemy.Motor.Distance, Is.GreaterThan(Rail.Length - 12f), "Enemy A belongs near the end of the level.");

            float start = enemy.Motor.Distance;
            yield return Frames(60);
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(enemy.Motor.Distance, Is.EqualTo(start).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator WhenSheComesNear_ItWalksToHer()
        {
            EnemyController enemy = Enemy;
            yield return PlaceAt(enemy.Motor.Distance - 6f);
            float start = enemy.Motor.Distance;

            yield return Frames(40);
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Walk));
            Assert.That(enemy.Motor.Distance, Is.LessThan(start - 0.3f), "it closes in");
            Assert.That(enemy.Motor.Facing, Is.EqualTo(-1), "facing her");
        }

        [UnityTest]
        public IEnumerator InReach_ItSwingsAfterAWindUp_AndTheHitStaggersHer()
        {
            EnemyController enemy = Enemy;
            yield return PlaceAt(enemy.Motor.Distance - 1.5f);
            Health health = Player.GetComponent<Health>();

            bool swung = false;
            float swingTimeAtHit = -1f;
            for (int i = 0; i < 180 && swingTimeAtHit < 0f; i++)
            {
                yield return null;
                swung |= enemy.State == EnemyState.Attack;
                if (Player.IsHurt)
                    swingTimeAtHit = enemy.StateTime;
            }

            Assert.That(swung, Is.True, "it never swung");
            Assert.That(swingTimeAtHit, Is.GreaterThanOrEqualTo(0f), "the swing never hit her");
            Assert.That(swingTimeAtHit, Is.GreaterThanOrEqualTo(enemy.Config.attack.startup - FrameTime),
                "the wind-up is the telegraph: the blow lands no sooner than its startup");
            Assert.That(health.Current, Is.EqualTo(health.Max - enemy.Config.attack.damage).Within(0.01f));
            Assert.That(health.Invulnerable, Is.True, "she is briefly invulnerable after a hit");

            yield return Frames(Mathf.CeilToInt((Player.Move.hurtStun + enemy.Config.attack.hitStop) / FrameTime) + 2);
            Assert.That(Player.IsHurt, Is.False, "the stagger ends");
        }

        [UnityTest]
        public IEnumerator HittingItDuringItsWindUp_InterruptsTheSwing()
        {
            EnemyController enemy = Enemy;
            yield return PlaceAt(enemy.Motor.Distance - 1.5f);

            for (int i = 0; i < 180 && enemy.State != EnemyState.Attack; i++)
                yield return null;
            Assume.That(enemy.State, Is.EqualTo(EnemyState.Attack));

            Input.PressAttack();
            for (int i = 0; i < 20 && enemy.State == EnemyState.Attack; i++)
                yield return null;

            Assert.That(enemy.State, Is.EqualTo(EnemyState.Hurt), "her jab staggered it");
            Assert.That(Player.IsHurt, Is.False, "and its swing never landed");
        }

        [UnityTest]
        public IEnumerator EnoughHits_KillIt_AndThenItDoesNothing()
        {
            EnemyController enemy = Enemy;
            yield return PlaceAt(enemy.Motor.Distance - 3f);
            for (int i = 0; i < 20 && !enemy.Health.IsDead; i++)
                enemy.Health.TakeHit(new Hit(10f, -enemy.transform.forward, Player.gameObject));

            yield return Frames(10);
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Dead));
            Assert.That(enemy.GetComponent<CharacterController>().enabled, Is.False, "the body no longer blocks her way");

            yield return PlaceAt(enemy.Motor.Distance - 1.5f);
            yield return Frames(120);
            Assert.That(Player.IsHurt, Is.False, "a dead enemy does not swing");
        }

        [UnityTest]
        public IEnumerator ItsSwing_HasTheBladeOut_WhileTheHitboxIsLive()
        {
            EnemyController enemy = Enemy;
            yield return PlaceAt(enemy.Motor.Distance - 2.2f);
            var body = enemy.GetComponentInChildren<UnityEngine.Animator>();
            Transform hand = body.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = body.GetBoneTransform(HumanBodyBones.Hips);

            float furthest = float.MinValue, at = -1f;
            for (int i = 0; i < 240; i++)
            {
                yield return null;
                if (enemy.State != EnemyState.Attack)
                {
                    if (at >= 0f)
                        break;
                    continue;
                }
                float reach = Vector3.Dot(hand.position - hips.position, enemy.transform.forward);
                if (reach > furthest)
                {
                    furthest = reach;
                    at = enemy.StateTime;
                }
            }

            AttackStep step = enemy.Config.attack;
            Assert.That(at, Is.GreaterThanOrEqualTo(0f), "it never swung");
            Assert.That(at, Is.InRange(step.startup - 0.1f, step.ActiveEnd + 0.1f),
                $"Its hand is furthest out {at:0.00} s into the swing; the hitbox is live from {step.startup:0.00} to {step.ActiveEnd:0.00} s.");
        }
    }
}
