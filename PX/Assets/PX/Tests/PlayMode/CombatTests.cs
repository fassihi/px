using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    public sealed class CombatTests : GrayboxFixture
    {
        private Health NearestDummy()
        {
            return Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None)
                .OrderBy(d => (d.transform.position - Motor.transform.position).sqrMagnitude)
                .First()
                .GetComponent<Health>();
        }

        [UnityTest]
        public IEnumerator Attack_HitsTheDummyInFrontOfHer_Once()
        {
            // The first dummy stands at 62 m.
            yield return PlaceAt(60.6f);
            Health dummy = NearestDummy();
            int hits = 0;
            Player.HitLanded += _ => hits++;

            Input.PressAttack();
            yield return Frames(40);

            float expected = dummy.Max - Player.Combo.steps[0].damage;
            Assert.That(dummy.Current, Is.EqualTo(expected).Within(0.01f), "one swing is one hit, however many frames it overlaps");
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
        }

        [UnityTest]
        public IEnumerator AHitThatLands_ShakesTheCamera_AndAMissDoesNot()
        {
            yield return PlaceAt(85f);
            Input.PressAttack();
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                Assert.That(CameraShake.IsActive, Is.False, "a swing at nothing shook the camera");
            }

            yield return PlaceAt(60.6f);
            bool shook = false;
            Input.PressAttack();
            for (int i = 0; i < 30 && !shook; i++)
            {
                yield return null;
                shook = CameraShake.IsActive;
            }
            Assert.That(shook, Is.True);
        }

        [UnityTest]
        public IEnumerator TheNextStrike_ComesEarlierOnAHit_ThanOnAWhiff()
        {
            // On a whiff the next strike waits for the cancel point; on a hit it can follow as soon as the hitbox closes.
            AttackStep first = Player.Combo.steps[0];
            yield return PlaceAt(85f);
            float whiff = 0f;
            yield return MashUntilSecondStrike(t => whiff = t);

            yield return PlaceAt(60.6f);
            float hit = 0f;
            yield return MashUntilSecondStrike(t => hit = t);

            Assert.That(whiff, Is.GreaterThanOrEqualTo(first.CancelFrom - 0.001f));
            Assert.That(hit, Is.LessThan(first.CancelFrom - 0.001f));
            Assert.That(hit, Is.GreaterThanOrEqualTo(first.ActiveEnd - 0.001f));
        }

        private IEnumerator MashUntilSecondStrike(System.Action<float> startedAt)
        {
            float last = 0f;
            for (int i = 0; i < 60; i++)
            {
                Input.PressAttack();
                if (Player.State == PlayerState.Attack && Player.ComboIndex == 0)
                    last = Player.StateTime;
                yield return null;
                if (Player.State == PlayerState.Attack && Player.ComboIndex == 1)
                {
                    startedAt(last + FrameTime);
                    break;
                }
            }
            yield return Frames(60);
        }

        [UnityTest]
        public IEnumerator AHit_KnocksTheDummyBack_AndThrowsSparks()
        {
            yield return PlaceAt(60.6f);
            Health dummy = NearestDummy();
            Vector3 before = dummy.transform.position;
            var effects = Object.FindFirstObjectByType<HitEffects>();
            Assert.That(effects, Is.Not.Null, "The scene has no hit effects. Rebuild the graybox scene.");
            ParticleSystem sparks = effects.transform.Find("Sparks").GetComponent<ParticleSystem>();

            Input.PressAttack();
            int most = 0;
            float pushed = 0f;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                most = Mathf.Max(most, sparks.particleCount);
                pushed = Mathf.Max(pushed, Vector3.Dot(dummy.transform.position - before, Player.transform.forward));
            }

            Assert.That(most, Is.GreaterThan(0), "no sparks");
            Assert.That(pushed, Is.GreaterThan(0.03f), "the dummy was not pushed back");
        }

        [UnityTest]
        public IEnumerator Attack_FacingAway_Misses()
        {
            yield return PlaceAt(60.6f);
            Health dummy = NearestDummy();

            // Turn around first, then swing.
            Input.Move = -1f;
            yield return Frames(2);
            Input.Move = 0f;
            yield return Frames(20);
            Input.PressAttack();
            yield return Frames(40);

            Assert.That(dummy.Current, Is.EqualTo(dummy.Max));
        }

        [UnityTest]
        public IEnumerator Attack_PressedRepeatedly_ChainsThroughTheWholeCombo()
        {
            // Open floor, nothing to hit.
            yield return PlaceAt(85f);
            int steps = Player.Combo.steps.Length;

            int highest = -1;
            for (int i = 0; i < 90; i++)
            {
                if (i % 5 == 0)
                    Input.PressAttack();
                yield return null;
                if (Player.State == PlayerState.Attack)
                    highest = Mathf.Max(highest, Player.ComboIndex);
            }

            Assert.That(highest, Is.EqualTo(steps - 1));
        }

        [UnityTest]
        public IEnumerator Attack_PressedOnce_DoesOnlyTheFirstSwing()
        {
            yield return PlaceAt(85f);

            Input.PressAttack();
            int highest = -1;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (Player.State == PlayerState.Attack)
                    highest = Mathf.Max(highest, Player.ComboIndex);
            }

            Assert.That(highest, Is.EqualTo(0));
            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
        }

        [UnityTest]
        public IEnumerator Combo_KillsADummy_AndItStandsBackUp()
        {
            yield return PlaceAt(60.6f);
            Health dummy = NearestDummy();
            bool died = false;
            dummy.Died += () => died = true;

            for (int i = 0; i < 600 && !died; i++)
            {
                if (i % 5 == 0)
                    Input.PressAttack();
                yield return null;
            }

            Assert.That(died, Is.True, "the dummy should go down within ten seconds of mashing attack");

            yield return Frames(150);
            Assert.That(dummy.IsDead, Is.False, "it restores itself after a moment");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "hit-stop must always release time");
        }
    }
}
