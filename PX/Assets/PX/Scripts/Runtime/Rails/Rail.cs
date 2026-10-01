using System;
using UnityEngine;
using UnityEngine.Splines;

namespace PX
{
    /// <summary>
    /// A point on a <see cref="Rail"/>: where it is and which way the rail runs there.
    /// </summary>
    public readonly struct RailPoint
    {
        public readonly Vector3 Position;

        /// <summary>Horizontal unit vector pointing toward increasing distance.</summary>
        public readonly Vector3 Tangent;

        public RailPoint(Vector3 position, Vector3 tangent)
        {
            Position = position;
            Tangent = tangent;
        }

        /// <summary>
        /// Horizontal unit vector perpendicular to the rail, on the side where the default
        /// side-view camera sits. Moving along <see cref="Tangent"/> reads as left-to-right from there.
        /// </summary>
        public Vector3 Side => Vector3.Cross(Vector3.up, Tangent);
    }

    /// <summary>
    /// The path the game is played on. Gameplay is 2D (distance along the rail, height),
    /// but the rail itself can curve freely through the 3D world.
    ///
    /// Authored as a spline, then baked into an evenly spaced polyline so that
    /// "distance" means real horizontal metres everywhere on the rail.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SplineContainer))]
    public sealed class Rail : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float sampleSpacing = 0.25f;

        private Vector3[] points;
        private Vector3[] tangents;
        private float[] distances;
        private float length;
        private bool baked;

        public float Length
        {
            get
            {
                EnsureBaked();
                return length;
            }
        }

        private void OnEnable()
        {
            Spline.Changed += OnSplineChanged;
            baked = false;
        }

        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
        }

        private void OnValidate()
        {
            baked = false;
        }

        private void OnSplineChanged(Spline spline, int knot, SplineModification modification)
        {
            var container = GetComponent<SplineContainer>();
            if (container != null && container.Spline == spline)
                baked = false;
        }

        /// <summary>Samples the spline again. Called automatically when the spline is edited.</summary>
        public void Rebuild()
        {
            var container = GetComponent<SplineContainer>();
            float splineLength = container.CalculateLength();
            int count = Mathf.Max(2, Mathf.CeilToInt(splineLength / sampleSpacing) + 1);

            points = new Vector3[count];
            tangents = new Vector3[count];
            distances = new float[count];

            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (count - 1f);
                points[i] = container.EvaluatePosition(t);
                if (i > 0)
                    total += Flatten(points[i] - points[i - 1]).magnitude;
                distances[i] = total;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Flatten(points[Mathf.Min(i + 1, count - 1)] - points[Mathf.Max(i - 1, 0)]);
                tangents[i] = direction.sqrMagnitude > 1e-10f ? direction.normalized : Vector3.forward;
            }

            length = total;
            baked = true;
        }

        /// <summary>The rail at a distance from its start, in metres. Clamped to the rail's ends.</summary>
        public RailPoint Evaluate(float distance)
        {
            EnsureBaked();
            distance = Mathf.Clamp(distance, 0f, length);

            int upper = Array.BinarySearch(distances, distance);
            if (upper < 0)
                upper = ~upper;
            upper = Mathf.Clamp(upper, 1, distances.Length - 1);
            int lower = upper - 1;

            float span = distances[upper] - distances[lower];
            float t = span > 1e-6f ? (distance - distances[lower]) / span : 0f;

            Vector3 tangent = Vector3.Lerp(tangents[lower], tangents[upper], t);
            tangent = tangent.sqrMagnitude > 1e-10f ? tangent.normalized : tangents[lower];
            return new RailPoint(Vector3.Lerp(points[lower], points[upper], t), tangent);
        }

        /// <summary>The distance along the rail closest to a world position, ignoring height.</summary>
        public float Project(Vector3 worldPosition)
        {
            EnsureBaked();
            Vector3 target = Flatten(worldPosition);
            float best = float.MaxValue;
            float bestDistance = 0f;

            for (int i = 1; i < points.Length; i++)
            {
                Vector3 a = Flatten(points[i - 1]);
                Vector3 segment = Flatten(points[i]) - a;
                float segmentLengthSq = segment.sqrMagnitude;
                float t = segmentLengthSq > 1e-10f
                    ? Mathf.Clamp01(Vector3.Dot(target - a, segment) / segmentLengthSq)
                    : 0f;

                float sq = (a + segment * t - target).sqrMagnitude;
                if (sq < best)
                {
                    best = sq;
                    bestDistance = Mathf.Lerp(distances[i - 1], distances[i], t);
                }
            }

            return bestDistance;
        }

        private void EnsureBaked()
        {
            if (!baked || points == null)
                Rebuild();
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            EnsureBaked();

            Gizmos.color = new Color(1f, 0.78f, 0.2f);
            for (int i = 1; i < points.Length; i++)
                Gizmos.DrawLine(points[i - 1], points[i]);

            // A tick and a label every 5 m, so camera zones and encounters can be placed by distance.
            const float tickSpacing = 5f;
            for (float d = 0f; d <= length; d += tickSpacing)
            {
                RailPoint p = Evaluate(d);
                Gizmos.DrawLine(p.Position - p.Side * 0.5f, p.Position + p.Side * 0.5f);
                UnityEditor.Handles.Label(p.Position + Vector3.up * 0.3f, d.ToString("0"));
            }
        }
#endif
    }
}
