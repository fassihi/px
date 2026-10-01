using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PX.Tests
{
    public sealed class RailTests
    {
        private GameObject host;
        private Rail rail;

        // 10 m along +X, then a quarter circle of radius 5 turning toward +Z, then 10 m along +Z.
        private const float Straight = 10f;
        private const float Radius = 5f;
        private static readonly float ExpectedLength = Straight * 2f + Mathf.PI * Radius * 0.5f;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Rail under test");
            rail = host.AddComponent<Rail>();

            float arc = Radius * 0.5523f;
            Spline spline = host.GetComponent<SplineContainer>().Spline;
            spline.Clear();
            spline.Add(new BezierKnot(new float3(0f, 0f, 0f), new float3(-3f, 0f, 0f), new float3(3f, 0f, 0f)), TangentMode.Broken);
            spline.Add(new BezierKnot(new float3(Straight, 0f, 0f), new float3(-3f, 0f, 0f), new float3(arc, 0f, 0f)), TangentMode.Broken);
            spline.Add(new BezierKnot(new float3(Straight + Radius, 0f, Radius), new float3(0f, 0f, -arc), new float3(0f, 0f, 3f)), TangentMode.Broken);
            spline.Add(new BezierKnot(new float3(Straight + Radius, 0f, Radius + Straight), new float3(0f, 0f, -3f), new float3(0f, 0f, 3f)), TangentMode.Broken);
            rail.Rebuild();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Length_MatchesTheAuthoredPath()
        {
            Assert.That(rail.Length, Is.EqualTo(ExpectedLength).Within(0.05f));
        }

        [Test]
        public void Evaluate_OnTheFirstStraight_IsMetresAlongX()
        {
            RailPoint point = rail.Evaluate(4f);

            Assert.That(point.Position.x, Is.EqualTo(4f).Within(0.01f));
            Assert.That(point.Position.z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(Vector3.Dot(point.Tangent, Vector3.right), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Evaluate_AfterTheTurn_RunsAlongZ()
        {
            RailPoint point = rail.Evaluate(ExpectedLength - 4f);

            Assert.That(point.Position.x, Is.EqualTo(Straight + Radius).Within(0.02f));
            Assert.That(point.Position.z, Is.EqualTo(Radius + Straight - 4f).Within(0.05f));
            Assert.That(Vector3.Dot(point.Tangent, Vector3.forward), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void Evaluate_ClampsBeyondTheEnds()
        {
            Assert.That(rail.Evaluate(-5f).Position, Is.EqualTo(rail.Evaluate(0f).Position));
            Assert.That(rail.Evaluate(rail.Length + 5f).Position, Is.EqualTo(rail.Evaluate(rail.Length).Position));
        }

        [Test]
        public void Tangent_IsAlwaysHorizontalAndUnitLength()
        {
            for (float d = 0f; d <= rail.Length; d += 0.5f)
            {
                Vector3 tangent = rail.Evaluate(d).Tangent;
                Assert.That(tangent.y, Is.EqualTo(0f).Within(1e-5f), $"at {d} m");
                Assert.That(tangent.magnitude, Is.EqualTo(1f).Within(1e-4f), $"at {d} m");
            }
        }

        [Test]
        public void Side_OnTheFirstStraight_FacesTheSideViewCamera()
        {
            // Travelling along +X, the camera sits toward -Z so that +X reads as "right" on screen.
            Vector3 side = rail.Evaluate(4f).Side;
            Assert.That(Vector3.Dot(side, Vector3.back), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Project_IsTheInverseOfEvaluate_EvenOffTheRailAndOffTheGround()
        {
            for (float d = 1f; d < rail.Length; d += 1.7f)
            {
                RailPoint point = rail.Evaluate(d);
                Vector3 offRail = point.Position + point.Side * 1.5f + Vector3.up * 3f;
                Assert.That(rail.Project(offRail), Is.EqualTo(d).Within(0.05f), $"at {d} m");
            }
        }
    }
}
