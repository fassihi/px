using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    public sealed class DebugControlTests : GrayboxFixture
    {
        [TearDown]
        public void RestoreTimeScale()
        {
            HitStop.BaseTimeScale = 1f;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator Zooming_ScalesTheCameraDistance()
        {
            yield return PlaceAt(20f);
            yield return Frames(60);
            float normal = Vector3.Distance(RailCamera.transform.position, Player.transform.position);

            RailCamera.DistanceScale = 0.5f;
            yield return Frames(60);
            float zoomedIn = Vector3.Distance(RailCamera.transform.position, Player.transform.position);

            Assert.That(zoomedIn, Is.LessThan(normal * 0.6f), "Zooming in to 0.5x should bring the camera about half as far away.");
            RailCamera.DistanceScale = 1f;
        }

        [UnityTest]
        public IEnumerator AfterAHit_TimeReturnsToTheDebugScale()
        {
            yield return PlaceAt(20f);
            // The overlay owns the base scale in the scene, so take it out of the way and drive HitStop directly.
            Object.FindFirstObjectByType<GrayboxOverlay>().enabled = false;
            HitStop.BaseTimeScale = 0.5f;

            HitStop.Trigger(0.05f);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "A hit freezes time.");

            yield return Frames(10);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f), "After the freeze, time returns to the debug scale, not to 1.");
        }
    }
}
