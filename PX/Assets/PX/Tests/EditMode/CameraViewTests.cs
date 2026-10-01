using NUnit.Framework;

namespace PX.Tests
{
    public sealed class CameraViewTests
    {
        [Test]
        public void Lerp_BlendsEveryField()
        {
            var a = new CameraView { yaw = 0f, pitch = 0f, distance = 10f, fieldOfView = 30f, focusHeight = 1f, lookAhead = 0f };
            var b = new CameraView { yaw = 90f, pitch = 20f, distance = 20f, fieldOfView = 50f, focusHeight = 2f, lookAhead = 4f };

            CameraView mid = CameraView.Lerp(a, b, 0.5f);

            Assert.That(mid.yaw, Is.EqualTo(45f).Within(1e-4f));
            Assert.That(mid.pitch, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(mid.distance, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(mid.fieldOfView, Is.EqualTo(40f).Within(1e-4f));
            Assert.That(mid.focusHeight, Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(mid.lookAhead, Is.EqualTo(2f).Within(1e-4f));
        }

        [Test]
        public void Lerp_TakesTheShortWayAroundForYaw()
        {
            var a = new CameraView { yaw = 170f };
            var b = new CameraView { yaw = -170f };

            // 170 -> -170 is 20 degrees through 180, not 340 degrees through 0.
            Assert.That(CameraView.Lerp(a, b, 0.5f).yaw, Is.EqualTo(180f).Within(1e-3f));
        }

        [Test]
        public void Zone_ContainsItsEnds()
        {
            var zone = new CameraZone { from = 10f, to = 20f };

            Assert.That(zone.Contains(10f), Is.True);
            Assert.That(zone.Contains(20f), Is.True);
            Assert.That(zone.Contains(9.99f), Is.False);
            Assert.That(zone.Contains(20.01f), Is.False);
        }
    }
}
