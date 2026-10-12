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

        /// <summary>
        /// Where a sabre held in one hand points in Mixamo's Sword And Shield clips, in the same frame: out along the
        /// forearm, canted slightly toward the thumb. (The two-handed grip above points it back over the shoulder.)
        /// </summary>
        public static readonly Vector3 OneHandedGripDirection = new Vector3(1f, 0.2f, 0f).normalized;

        // As fractions of the model's height. Pieces whose centre lies this close to the blade's line, and which come
        // within the touch distance of the sword, belong to it. The search runs this far back past the blade's base.
        private const float LineRadius = 0.03f;
        private const float TouchDistance = 0.004f;
        private const float HiltReach = 0.25f;

        // A hilt or guard has at most this many times the blade's vertices.
        private const int SwordPieceLimit = 4;

        // A blade is a piece weighted to the hand that reaches further from it than any glove.
        private const float BladeReach = 0.15f;

        /// <summary>
        /// Seats the sword held in <paramref name="side"/> ("Left" or "Right"). With <paramref name="grip"/>, it is also
        /// turned to run that way in the hand's frame; without, it keeps the angle it was modelled at. Returns the
        /// vertices made rigid.
        /// </summary>
        public static int Fix(GameObject model, string side, Vector3? grip = null)
        {
            int moved = 0;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                moved += Fix(renderer, side, grip);
            return moved;
        }

        private static int Fix(SkinnedMeshRenderer renderer, string side, Vector3? grip)
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

            // The hand and its fingers: a grip may be skinned to either.
            var handBones = new HashSet<int>(Enumerable.Range(0, bones.Length).Where(i => bones[i] == bones[hand] || bones[i].IsChildOf(bones[hand])));
            Vector3 handPosition = bones[hand].position;
            // A blade: a piece mostly weighted to the hand that reaches well beyond it. (A glove modelled into the body
            // also reaches past the hand at the fingertips, but the body is mostly weighted elsewhere.)
            var blade = new HashSet<int>();
            var reaching = new HashSet<int>();
            var size = new Dictionary<int, int>();
            var held = new Dictionary<int, int>();
            for (int i = 0; i < weights.Length; i++)
            {
                size[island[i]] = (size.TryGetValue(island[i], out int s) ? s : 0) + 1;
                if (!handBones.Contains(weights[i].boneIndex0))
                    continue;
                held[island[i]] = (held.TryGetValue(island[i], out int h) ? h : 0) + 1;
                if ((world[i] - handPosition).magnitude > BladeReach * height)
                    reaching.Add(island[i]);
            }
            foreach (int piece in reaching)
            {
                if (held[piece] * 2 > size[piece])
                    blade.Add(piece);
            }
            if (blade.Count == 0)
                return 0;

            // The blade's line: its longest axis through its centre, pointing away from the hand.
            List<int> bladeVertices = Enumerable.Range(0, world.Length).Where(i => blade.Contains(island[i])).ToList();
            Vector3 centre = bladeVertices.Aggregate(Vector3.zero, (s, i) => s + world[i]) / bladeVertices.Count;
            Vector3 axis = LongestAxis(bladeVertices.Select(i => world[i] - centre).ToList());
            // Toward the tip: the end furthest from the hand. (The hand can hold a sabre near its middle, so the
            // blade's centre alone does not say which way it points.)
            int tipVertex = bladeVertices.OrderByDescending(i => (world[i] - handPosition).sqrMagnitude).First();
            if (Vector3.Dot(axis, world[tipVertex] - centre) < 0f)
                axis = -axis;
            float baseOfBlade = bladeVertices.Min(i => Vector3.Dot(world[i] - centre, axis));
            float tip = bladeVertices.Max(i => Vector3.Dot(world[i] - centre, axis));

            // Pieces centred on that line, from behind the hand to the tip, that touch the sword: the hilt touches the
            // blade, a pommel the hilt. Studs on the glove lie near the line too, but stand clear of the hilt.
            // The body itself can lie across the blade's line (a sabre held low in front of the coat), so pieces much
            // bigger than the blade are never part of the sword.
            var candidates = new List<List<int>>();
            int largestPart = SwordPieceLimit * bladeVertices.Count;
            foreach (IGrouping<int, int> piece in Enumerable.Range(0, world.Length).GroupBy(i => island[i]))
            {
                if (blade.Contains(piece.Key) || piece.Count() > largestPart)
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
            Quaternion turn = Quaternion.identity;
            if (grip.HasValue)
            {
                Vector3 g = grip.Value.normalized;
                turn = Quaternion.FromToRotation(axis, x * g.x + y * g.y + z * g.z);
            }

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

        /// <summary>
        /// Straps loose gear to the torso: a separate piece of the mesh (a gun slung on the back, say) that the auto-rig
        /// weighted partly to an arm and partly to a leg is made rigid on the nearest torso bone, so it rides on the
        /// back instead of tearing between the limbs. Returns the vertices moved.
        /// </summary>
        public static int StrapToTorso(GameObject model)
        {
            int moved = 0;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Mesh mesh = renderer.sharedMesh;
                Transform[] bones = renderer.bones;
                int[] torso = new[] { "Chest", "UpperChest", "Spine" }.Select(n => System.Array.FindIndex(bones, b => b.name == n)).Where(i => i >= 0).ToArray();
                if (mesh == null || torso.Length == 0)
                    continue;
                bool Arm(int bone) => bones[bone].name.Contains("Arm") || bones[bone].name.Contains("Hand");
                bool Leg(int bone) => bones[bone].name.Contains("Leg") || bones[bone].name.Contains("Foot");

                Vector3[] vertices = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                int[] island = Islands(mesh, vertices);
                int body = island.GroupBy(i => i).OrderByDescending(g => g.Count()).First().Key;
                foreach (IGrouping<int, int> piece in Enumerable.Range(0, vertices.Length).GroupBy(i => island[i]))
                {
                    if (piece.Key == body)
                        continue;
                    int onArm = piece.Count(i => Arm(weights[i].boneIndex0));
                    int onLeg = piece.Count(i => Leg(weights[i].boneIndex0));
                    // At least a tenth of it on each: a stray vertex or two is not enough to call it slung gear.
                    if (onArm * 10 < piece.Count() || onLeg * 10 < piece.Count())
                        continue;
                    Vector3 centre = piece.Aggregate(Vector3.zero, (sum, i) => sum + renderer.transform.TransformPoint(vertices[i])) / piece.Count();
                    int anchor = torso.OrderBy(t => (bones[t].position - centre).sqrMagnitude).First();
                    foreach (int i in piece)
                        weights[i] = new BoneWeight { boneIndex0 = anchor, weight0 = 1f };
                    moved += piece.Count();
                }
                mesh.boneWeights = weights;
            }
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
