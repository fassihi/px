using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    public sealed class EnemyTests : GrayboxFixture
    {
        private EnemyController Enemy => Named("EnemyA");

        private static EnemyController Named(string name)
        {
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy.name.StartsWith(name))
                    return enemy;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator EnemyA_WaitsOnTheLastStraight_AndEnemyBAtTheEnd_IdleWhileSheIsFarAway()
        {
            yield return PlaceAt(90f);
            EnemyController enemy = Enemy;
            Assert.That(enemy, Is.Not.Null, "No Enemy A in the scene. Rebuild the graybox scene.");
            Assert.That(enemy.Motor.Distance, Is.InRange(100f, 125f), "Enemy A waits in its arena on the last straight.");
            EnemyController enemyB = Named("EnemyB");
            Assert.That(enemyB, Is.Not.Null, "No Enemy B in the scene. Rebuild the graybox scene.");
            Assert.That(enemyB.Motor.Distance, Is.GreaterThan(Rail.Length - 25f), "Enemy B belongs near the end of the level.");

            float start = enemy.Motor.Distance;
            yield return Frames(60);
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(enemy.Motor.Distance, Is.EqualTo(start).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator ReachingTheEnemy_TheCameraTurnsBackToASideView()
        {
            EnemyController enemy = Enemy;
            yield return PlaceAt(enemy.Motor.Distance - 20f);
            yield return Frames(90);
            Assume.That(RailCamera.CurrentView.yaw, Is.GreaterThan(45f), "before the fight it looks down the rail");

            yield return PlaceAt(enemy.Motor.Distance - 6f);
            yield return Frames(120);
            Assert.That(RailCamera.CurrentView.yaw, Is.EqualTo(0f).Within(3f), "the fight is seen from the side");
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
            Assert.That(enemy.ComboIndex, Is.EqualTo(0), "the blow that landed was the first of its combo");
            Assert.That(swingTimeAtHit, Is.GreaterThanOrEqualTo(0f), "the swing never hit her");
            Assert.That(swingTimeAtHit, Is.GreaterThanOrEqualTo(enemy.Config.attack.startup - FrameTime),
                "the wind-up is the telegraph: the blow lands no sooner than its startup");
            Assert.That(health.Current, Is.EqualTo(health.Max - enemy.Config.attack.damage).Within(0.01f));
            Assert.That(health.Invulnerable, Is.True, "she is briefly invulnerable after a hit");

            float freeze = Mathf.Max(enemy.Config.attack.hitStop, Player.Move.hurtHitStop);
            yield return Frames(Mathf.CeilToInt((Player.HurtDuration + freeze) / FrameTime) + 2);
            Assert.That(Player.IsHurt, Is.False, "the stagger ends");
        }

        [UnityTest]
        public IEnumerator DuringItsWindUp_AJabDoesNotStopIt_ButAJabAndACrossDo()
        {
            EnemyController enemy = Enemy;
            yield return PlaceAt(enemy.Motor.Distance - 1.5f);

            for (int i = 0; i < 180 && enemy.State != EnemyState.Attack; i++)
                yield return null;
            Assume.That(enemy.State, Is.EqualTo(EnemyState.Attack));

            // Poise: a light blow does not interrupt a committed swing.
            Input.PressAttack();
            for (int i = 0; i < 12; i++)
                yield return null;
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Attack), "it shrugs off a lone jab mid-swing");

            Input.PressAttack();
            for (int i = 0; i < 20 && enemy.State == EnemyState.Attack; i++)
                yield return null;
            Assert.That(enemy.State, Is.EqualTo(EnemyState.Hurt), "the cross on top of it breaks its poise");
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

        /// <summary>Input that asks for the same thing every frame.</summary>
        private sealed class Steady : IInputSource
        {
            public InputFrame Frame;
            public InputFrame Read() => Frame;
        }

        [UnityTest]
        public IEnumerator AttackPressedThroughItsFirstSwing_ChainsTheSecond()
        {
            EnemyController enemy = Enemy;
            yield return PlaceAt(enemy.Motor.Distance - 4f);
            enemy.InputSource = new Steady { Frame = new InputFrame { AttackPressed = true, Face = -1f } };

            bool second = false;
            for (int i = 0; i < 180 && !second; i++)
            {
                yield return null;
                second = enemy.State == EnemyState.Attack && enemy.ComboIndex == 1;
            }
            Assert.That(second, Is.True, "it never followed with its second swing");
            Assert.That(enemy.CurrentStep, Is.SameAs(enemy.Config.attack2));
        }

        [UnityTest]
        public IEnumerator WhenSheIsTooClose_ItBacksAway_StillFacingHer()
        {
            EnemyController enemy = Enemy;
            float start = enemy.Motor.Distance;
            Motor.Teleport(Rail, start - enemy.Config.tooClose * 0.7f, 0.1f);
            RailCamera.Snap();

            bool backed = false;
            for (int i = 0; i < 40 && !backed; i++)
            {
                yield return null;
                backed = enemy.State == EnemyState.WalkBack;
            }
            Assert.That(backed, Is.True, "it never backed off");
            yield return Frames(15);
            Assert.That(enemy.Motor.Distance, Is.GreaterThan(start + 0.1f), "it opened the distance");
            Assert.That(enemy.Motor.Facing, Is.EqualTo(-1), "facing her as it goes");
        }

        [UnityTest]
        public IEnumerator SheStandsOnACrate_ItClimbsUpAfterHer()
        {
            EnemyController enemy = Enemy;
            // The low crate runs from 117 to 119 m and is 1.4 m tall.
            Motor.Teleport(Rail, 118.5f, 1.5f);
            Motor.Facing = -1;
            RailCamera.Snap();

            bool climbed = false;
            for (int i = 0; i < 600 && !climbed; i++)
            {
                yield return null;
                climbed = enemy.State == EnemyState.Climb;
            }
            Assert.That(climbed, Is.True, "it never climbed");
            yield return Frames(Mathf.CeilToInt(enemy.Config.climbDuration / FrameTime) + 10);
            Assert.That(enemy.transform.position.y, Is.GreaterThan(1.3f), "it stands on the crate");
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
                if (enemy.State != EnemyState.Attack || enemy.ComboIndex != 0)
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
            Assert.That(at, Is.InRange(step.startup - 0.06f, step.ActiveEnd + 0.06f),
                $"Its hand is furthest out {at:0.00} s into the swing; the hitbox is live from {step.startup:0.00} to {step.ActiveEnd:0.00} s.");
        }
    }
}
