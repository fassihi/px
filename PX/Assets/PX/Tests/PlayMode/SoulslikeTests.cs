using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    /// <summary>The soulslike layer: stamina, the dodge back, guard, parry and counter, death and getting back up.</summary>
    public sealed class SoulslikeTests : GrayboxFixture
    {
        // Clear floor for a few metres either way, between the ledge at 74-79 m and Enemy A.
        private const float OpenStretch = 90f;

        private static EnemyController EnemyA()
        {
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy.name.StartsWith("EnemyA"))
                    return enemy;
            }
            return null;
        }

        /// <summary>Stands her in front of Enemy A and waits until its first swing is this close to going live.</summary>
        private IEnumerator WaitForSwing(EnemyController enemy, float beforeLive)
        {
            yield return PlaceAt(enemy.Motor.Distance - 1.5f);
            float live = enemy.Config.attack.startup;
            for (int i = 0; i < 300; i++)
            {
                if (enemy.State == EnemyState.Attack && enemy.ComboIndex == 0 && enemy.StateTime >= live - beforeLive)
                    yield break;
                yield return null;
            }
            Assert.Fail("Enemy A never swung at her.");
        }

        [UnityTest]
        public IEnumerator Strikes_CostStamina_AndItComesBackAfterAPause()
        {
            yield return PlaceAt(OpenStretch);
            Stamina stamina = Player.Stamina;
            Assert.That(stamina, Is.Not.Null, "She has no stamina. Delete Player.prefab and rebuild the scene.");
            Assume.That(stamina.Current, Is.EqualTo(stamina.Max));

            Input.PressAttack();
            yield return Frames(2);
            Assert.That(stamina.Current, Is.EqualTo(stamina.Max - Player.Combo.steps[0].staminaCost).Within(0.5f));

            yield return Frames(Mathf.CeilToInt((Player.Combat.staminaRegenDelay + 0.5f) / FrameTime));
            Assert.That(stamina.Current, Is.EqualTo(stamina.Max).Within(0.01f), "it came back");
        }

        [UnityTest]
        public IEnumerator OutOfStamina_SheCannotAttack()
        {
            yield return PlaceAt(OpenStretch);
            Player.Stamina.Spend(Player.Stamina.Max);
            Input.PressAttack();
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion), "an empty stamina bar does not swing");
        }

        [UnityTest]
        public IEnumerator DashWithNoDirection_IsADodgeBack_ThatBlowsPassThrough()
        {
            yield return PlaceAt(OpenStretch);
            float start = Motor.Distance;
            Assume.That(Motor.Facing, Is.EqualTo(1));
            Health health = Player.GetComponent<Health>();

            Input.PressDash();
            yield return Frames(2);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Dodge));
            Assert.That(health.Invulnerable, Is.True, "early in the dodge, blows pass through her");
            Assert.That(health.TakeHit(new Hit(10f, Vector3.back, null)), Is.EqualTo(HitResult.Ignored));

            yield return Frames(Mathf.CeilToInt(Player.Move.dodgeDuration / FrameTime) + 2);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
            Assert.That(Motor.Distance, Is.LessThan(start - 1f), "she hopped back");
            Assert.That(Motor.Facing, Is.EqualTo(1), "still facing the way she was");
            Assert.That(health.Current, Is.EqualTo(health.Max));
            Assert.That(Player.Stamina.Current, Is.LessThan(Player.Stamina.Max), "the dodge costs stamina");
        }

        [UnityTest]
        public IEnumerator GuardPressedJustBeforeTheBlow_ParriesIt_AndAttackThenCounters()
        {
            EnemyController enemy = EnemyA();
            Health health = Player.GetComponent<Health>();
            yield return WaitForSwing(enemy, 0.08f);

            Input.PressGuard();
            for (int i = 0; i < 40 && enemy.State != EnemyState.Parried; i++)
                yield return null;

            Assert.That(enemy.State, Is.EqualTo(EnemyState.Parried), "its blade was turned aside");
            Assert.That(health.Current, Is.EqualTo(health.Max), "a parry takes no damage");
            Assert.That(Player.IsHurt, Is.False);
            Assert.That(Player.CanCounter, Is.True, "a counter is on offer");

            float before = enemy.Health.Current;
            Input.GuardHeld = false;
            Input.PressAttack();
            // The parry freezes the world for a moment; the press is held over until it ends.
            for (int i = 0; i < 30 && HitStop.IsActive; i++)
                yield return null;
            yield return Frames(2);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Attack));
            Assert.That(Player.IsCounter, Is.True, "Attack after a parry is the counter");
            for (int i = 0; i < 40 && enemy.Health.Current >= before; i++)
                yield return null;
            Assert.That(before - enemy.Health.Current, Is.EqualTo(Player.Combo.counterAttack.damage).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator GuardHeldThroughTheBlow_BlocksIt_ForStaminaAndALittleDamage()
        {
            EnemyController enemy = EnemyA();
            Health health = Player.GetComponent<Health>();
            yield return WaitForSwing(enemy, 0.45f);

            Input.PressGuard();
            for (int i = 0; i < 60 && health.Current >= health.Max; i++)
                yield return null;

            float damage = enemy.Config.attack.damage;
            Assert.That(enemy.State, Is.Not.EqualTo(EnemyState.Parried), "too early for a parry");
            Assert.That(health.Current, Is.EqualTo(health.Max - damage * Player.Combat.blockDamageScale).Within(0.01f));
            Assert.That(Player.State, Is.EqualTo(PlayerState.Guard), "a block does not stagger her");
            Assert.That(Player.Stamina.Current, Is.LessThan(Player.Stamina.Max - damage), "and it costs stamina");
        }

        [UnityTest]
        public IEnumerator BlockingWithoutStamina_BreaksHerGuard()
        {
            EnemyController enemy = EnemyA();
            Health health = Player.GetComponent<Health>();
            yield return WaitForSwing(enemy, 0.45f);

            Input.PressGuard();
            Player.Stamina.Spend(Player.Stamina.Max - 1f);
            for (int i = 0; i < 60 && !Player.IsHurt; i++)
                yield return null;

            Assert.That(Player.IsHurt, Is.True, "her guard broke");
            Assert.That(health.Current, Is.EqualTo(health.Max - enemy.Config.attack.damage).Within(0.01f), "the blow landed in full");
            Assert.That(Player.HurtDuration, Is.GreaterThan(Player.Move.hurtStun), "and staggers her for longer");
        }

        [UnityTest]
        public IEnumerator HerStrikes_ReachNoFurtherThanHerFistsAndFeet()
        {
            yield return PlaceAt(OpenStretch);
            ComboConfig combo = Player.Combo;
            Assert.That(combo.steps.Length, Is.EqualTo(5), "jab, cross, side kick, roundhouse, spinning back kick");
            foreach (AttackStep step in combo.steps)
                Assert.That(step.hitboxCenter.x + step.hitboxSize.x * 0.5f, Is.LessThanOrEqualTo(1.6f), "a strike reaches past her limb");
            Assert.That(combo.hitboxDepth, Is.LessThanOrEqualTo(0.7f), "a strike reaches far off the rail");
        }

        [UnityTest]
        public IEnumerator WhenSheFalls_SheGetsUpAtTheLastCheckpoint_WithFullHealth()
        {
            yield return PlaceAt(100f);
            Health health = Player.GetComponent<Health>();
            var respawn = Object.FindFirstObjectByType<PlayerRespawn>();
            Assert.That(respawn, Is.Not.Null, "The scene has no respawn. Rebuild the graybox scene.");
            yield return Frames(5);
            float checkpoint = respawn.Checkpoint;
            Assume.That(checkpoint, Is.GreaterThan(50f), "she passed a checkpoint before Enemy A");

            health.TakeHit(new Hit(health.Max * 2f, Vector3.back, null, 3f));
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Dead));

            yield return Frames(Mathf.CeilToInt(Player.Combat.respawnDelay / FrameTime) + 5);
            Assert.That(Player.State, Is.Not.EqualTo(PlayerState.Dead));
            Assert.That(health.Current, Is.EqualTo(health.Max));
            Assert.That(Motor.Distance, Is.EqualTo(checkpoint).Within(0.5f));
        }
    }
}
