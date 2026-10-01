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
