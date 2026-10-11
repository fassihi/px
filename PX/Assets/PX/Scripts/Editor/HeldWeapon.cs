using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PX.EditorTools
{
    /// <summary>
    /// Seats a sword modelled into a Tripo character so it sits in the hand the way Mixamo's sword clips expect.
    /// Tripo models the sword hanging from a relaxed hand, and its auto-rig weights the hilt to the forearm, thumb and
    /// hips, so in a two-handed Mixamo grip the blade points back over the shoulder and the hilt tears loose. Here the
    /// blade and every piece along its line (hilt, guard, pommel) are weighted rigidly to the hand, then turned about
    /// the grip so the sword runs along <see cref="GripDirection"/>.
    /// </summary>
    public static class HeldWeapon
    {
        /// <summary>
        /// Where the sword points, pommel to tip, in the hand's own frame: x from the wrist to the middle knuckle, y toward
        /// the thumb, z the rest. Measured from Mixamo's Great Sword clips on Enemy A: in its idle, walk and strike the
        /// line from the left palm to the right palm holds this direction within a few degrees, with the palms 0.19 m apart.
        /// </summary>
        public static readonly Vector3 GripDirection = new Vector3(0.71f, 0.32f, -0.62f).normalized;

        // As fractions of the model's height. Pieces whose centre lies this close to the blade's line, and which come
        // within the touch distance of the sword, belong to it. The search runs this far back past the blade's base.
        private const float LineRadius = 0.03f;
        private const float TouchDistance = 0.004f;
        private const float HiltReach = 0.25f;

        // A blade is a piece weighted to the hand that reaches further from it than any glove.
        private const float BladeReach = 0.15f;

        /// <summary>Seats the sword held in <paramref name="side"/> ("Left" or "Right"). Returns the vertices moved.</summary>
        public static int Fix(GameObject model, string side)
        {
            int moved = 0;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                moved += Fix(renderer, side);
            return moved;
        }

        private static int Fix(SkinnedMeshRenderer renderer, string side)
        {
            Mesh mesh = renderer.sharedMesh;
            Transform[] bones = renderer.bones;
            int hand = System.Array.FindIndex(bones, b => b.name == side + "_Hand");
            Transform knuckle = bones.FirstOrDefault(b => b.name == side + "_MiddleProximal");
            Transform thumb = bones.FirstOrDefault(b => b.name == side + "_ThumbProximal");
            if (mesh == null || hand < 0 || knuckle == null || thumb == null)
                return 0;

            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            Vector3[] world = vertices.Select(v => renderer.transform.TransformPoint(v)).ToArray();
            float height = world.Max(p => p.y) - world.Min(p => p.y);
            int[] island = Islands(mesh, vertices);

            Vector3 handPosition = bones[hand].position;
            var blade = new HashSet<int>();
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i].boneIndex0 == hand && (world[i] - handPosition).magnitude > BladeReach * height)
                    blade.Add(island[i]);
            }
            if (blade.Count == 0)
                return 0;

            // The blade's line: its longest axis through its centre, pointing away from the hand.
            List<int> bladeVertices = Enumerable.Range(0, world.Length).Where(i => blade.Contains(island[i])).ToList();
            Vector3 centre = bladeVertices.Aggregate(Vector3.zero, (s, i) => s + world[i]) / bladeVertices.Count;
            Vector3 axis = LongestAxis(bladeVertices.Select(i => world[i] - centre).ToList());
            if (Vector3.Dot(axis, centre - handPosition) < 0f)
                axis = -axis;
            float baseOfBlade = bladeVertices.Min(i => Vector3.Dot(world[i] - centre, axis));
            float tip = bladeVertices.Max(i => Vector3.Dot(world[i] - centre, axis));

            // Pieces centred on that line, from behind the hand to the tip, that touch the sword: the hilt touches the
            // blade, a pommel the hilt. Studs on the glove lie near the line too, but stand clear of the hilt.
            var candidates = new List<List<int>>();
            foreach (IGrouping<int, int> piece in Enumerable.Range(0, world.Length).GroupBy(i => island[i]))
            {
                if (blade.Contains(piece.Key))
                    continue;
                Vector3 pieceCentre = piece.Aggregate(Vector3.zero, (s, i) => s + world[i]) / piece.Count();
                float along = Vector3.Dot(pieceCentre - centre, axis);
                if (along >= baseOfBlade - HiltReach * height && along <= tip && OffLine(pieceCentre, centre, axis) < LineRadius * height)
                    candidates.Add(piece.ToList());
            }
            var sword = new HashSet<int>(blade);
            var swordPoints = new List<Vector3>(bladeVertices.Select(i => world[i]));
            float touch = TouchDistance * height;
            for (bool grew = true; grew;)
            {
                grew = false;
                foreach (List<int> piece in candidates)
                {
                    if (sword.Contains(island[piece[0]]) || !piece.Any(i => swordPoints.Any(p => (p - world[i]).sqrMagnitude < touch * touch)))
                        continue;
                    sword.Add(island[piece[0]]);
                    swordPoints.AddRange(piece.Select(i => world[i]));
                    grew = true;
                }
            }

            // Turn it about the point on its line nearest the palm, so the hilt stays in the fist.
            Vector3 palm = (handPosition + knuckle.position) * 0.5f;
            Vector3 pivot = centre + axis * Vector3.Dot(palm - centre, axis);
            Vector3 x = (knuckle.position - handPosition).normalized;
            Vector3 y = Vector3.ProjectOnPlane(thumb.position - handPosition, x).normalized;
            Vector3 z = Vector3.Cross(x, y);
            Vector3 wanted = x * GripDirection.x + y * GripDirection.y + z * GripDirection.z;
            Quaternion turn = Quaternion.FromToRotation(axis, wanted);

            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            Quaternion toLocal = Quaternion.Inverse(renderer.transform.rotation);
            Quaternion localTurn = toLocal * turn * renderer.transform.rotation;
            int moved = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!sword.Contains(island[i]))
                    continue;
                vertices[i] = renderer.transform.InverseTransformPoint(pivot + turn * (world[i] - pivot));
                if (normals.Length == vertices.Length)
                    normals[i] = localTurn * normals[i];
                if (tangents.Length == vertices.Length)
                {
                    Vector3 t = localTurn * (Vector3)tangents[i];
                    tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
                }
                weights[i] = new BoneWeight { boneIndex0 = hand, weight0 = 1f };
                moved++;
            }

            mesh.vertices = vertices;
            if (normals.Length == vertices.Length)
                mesh.normals = normals;
            if (tangents.Length == vertices.Length)
                mesh.tangents = tangents;
            mesh.boneWeights = weights;
            mesh.RecalculateBounds();
            return moved;
        }

        private static float OffLine(Vector3 point, Vector3 origin, Vector3 axis)
        {
            Vector3 offset = point - origin;
            return (offset - axis * Vector3.Dot(offset, axis)).magnitude;
        }

        /// <summary>The direction of greatest spread, by power iteration on the covariance.</summary>
        private static Vector3 LongestAxis(List<Vector3> offsets)
        {
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (Vector3 o in offsets)
            {
                xx += o.x * o.x; xy += o.x * o.y; xz += o.x * o.z;
                yy += o.y * o.y; yz += o.y * o.z; zz += o.z * o.z;
            }
            Vector3 v = new Vector3(0.577f, 0.577f, 0.577f);
            for (int k = 0; k < 64; k++)
            {
                v = new Vector3(xx * v.x + xy * v.y + xz * v.z, xy * v.x + yy * v.y + yz * v.z, xz * v.x + yz * v.y + zz * v.z);
                v = v.sqrMagnitude > 0f ? v.normalized : Vector3.forward;
            }
            return v;
        }

        /// <summary>Connected pieces of the mesh, joining vertices that share a triangle or a position.</summary>
        private static int[] Islands(Mesh mesh, Vector3[] vertices)
        {
            int[] parent = Enumerable.Range(0, vertices.Length).ToArray();
            int Find(int a)
            {
                while (parent[a] != a)
                    a = parent[a] = parent[parent[a]];
                return a;
            }

            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                parent[Find(triangles[i])] = Find(triangles[i + 1]);
                parent[Find(triangles[i + 1])] = Find(triangles[i + 2]);
            }
            var seen = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 10000f);
                if (seen.TryGetValue(key, out int other))
                    parent[Find(i)] = Find(other);
                else
                    seen[key] = i;
            }
            return Enumerable.Range(0, vertices.Length).Select(Find).ToArray();
        }
    }
}
