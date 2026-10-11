using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PX.EditorTools
{
    /// <summary>
    /// Fixes cloth skinned to the arms. Tripo's auto-rig weights by distance in the bind pose, where the arms hang
    /// beside the coat, its straps and its tassels, so they follow the hands: when the knight raises its arm, the cloth
    /// is dragged up with it. Here, any vertex weighted to an arm but not part of the arm (further from every arm bone
    /// than an armoured sleeve reaches) hands that weight to the nearest torso bone. Separate mesh pieces held in a
    /// hand, like a sword, are left alone.
    /// </summary>
    public static class CoatWeights
    {
        // As fractions of the model's height, so the rule does not depend on its units.
        // How far the arm's own mesh reaches from each kind of bone; beyond it, a vertex is cloth. An armoured sleeve
        // stands well off its bone, a glove hugs the hand, and a finger hugs its finger. The coat hangs right by the
        // fingertips in the bind pose, so fingers need the tightest radius.
        private const float SleeveRadius = 0.08f;
        private const float GloveRadius = 0.06f;
        private const float FingerRadius = 0.03f;
        private const float HeldReach = 0.3f;

        private static readonly string[] Torso = { "Hips", "Spine", "Chest", "UpperChest" };

        public static int Fix(GameObject model)
        {
            int moved = 0;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                moved += Fix(renderer);
            return moved;
        }

        private static int Fix(SkinnedMeshRenderer renderer)
        {
            Mesh mesh = renderer.sharedMesh;
            Transform[] bones = renderer.bones;
            int[] torso = Torso.Select(n => System.Array.FindIndex(bones, b => b.name == n)).Where(i => i >= 0).ToArray();
            if (mesh == null || torso.Length == 0)
                return 0;

            var arm = new HashSet<int>();
            for (int i = 0; i < bones.Length; i++)
            {
                string name = bones[i].name;
                bool side = name.StartsWith("Left_") || name.StartsWith("Right_");
                bool armPart = name.Contains("Arm") || name.Contains("Hand") || name.Contains("Thumb") || name.Contains("Index")
                    || name.Contains("Middle") || name.Contains("Ring") || name.Contains("Little") || name.Contains("Pinky");
                if (side && armPart)
                    arm.Add(i);
            }

            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            var world = vertices.Select(v => renderer.transform.TransformPoint(v)).ToArray();
            float height = world.Max(p => p.y) - world.Min(p => p.y);
            var segments = arm.Select(i => (
                from: bones[i].position,
                to: bones[i].childCount > 0 ? bones[i].GetChild(0).position : bones[i].position,
                radius: (bones[i].name.Contains("Arm") ? SleeveRadius : bones[i].name.EndsWith("_Hand") ? GloveRadius : FingerRadius) * height)).ToList();
            int[] island = Islands(mesh, vertices);
            HashSet<int> held = HeldIslands(weights, world, island, arm, bones, height);

            int moved = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (held.Contains(island[i]))
                    continue;
                BoneWeight w = weights[i];
                if (ArmWeight(w, arm) <= 0f)
                    continue;
                Vector3 point = world[i];
                if (segments.Any(s => DistanceToSegment(point, s.from, s.to) < s.radius))
                    continue;
                Vector3 p = world[i];
                int nearestTorso = torso.OrderBy(t => (bones[t].position - p).sqrMagnitude).First();
                weights[i] = MoveToBone(w, arm, nearestTorso);
                moved++;
            }

            moved += FixByConnection(mesh, vertices, world, weights, island, held, arm, bones, torso, segments, height);
            mesh.boneWeights = weights;
            return moved;
        }

        /// <summary>
        /// Distance alone keeps coat that touches the fingers in the bind pose. Along the mesh, though, a glove is
        /// joined to the arm through the wrist, and the coat only to the rest of the coat. So each vertex still
        /// weighted to an arm goes to whichever is nearer along the surface: the core of a sleeve, or vertices with no
        /// arm weight at all. Those nearer the body hand their arm weight to the torso.
        /// </summary>
        private static int FixByConnection(Mesh mesh, Vector3[] vertices, Vector3[] world, BoneWeight[] weights,
            int[] island, HashSet<int> held, HashSet<int> arm, Transform[] bones, int[] torso,
            List<(Vector3 from, Vector3 to, float radius)> segments, float height)
        {
            int n = vertices.Length;
            List<(int to, float length)>[] edges = Graph(mesh, vertices, world);

            // Start points: sleeve cores (never coat) and vertices with no arm weight (never arm).
            var dist = new float[n];
            var fromArm = new bool[n];
            var queue = new SortedSet<(float d, int v)>();
            for (int i = 0; i < n; i++)
            {
                dist[i] = float.MaxValue;
                float armWeight = ArmWeight(weights[i], arm);
                bool body = armWeight <= 0f;
                bool core = armWeight >= 0.95f && IsSleeveCore(world[i], weights[i], bones);
                if (!body && !core)
                    continue;
                dist[i] = 0f;
                fromArm[i] = core;
                queue.Add((0f, i));
            }

            while (queue.Count > 0)
            {
                var (d, v) = queue.Min;
                queue.Remove(queue.Min);
                if (d > dist[v])
                    continue;
                foreach (var (to, length) in edges[v])
                {
                    float next = d + length;
                    if (next >= dist[to])
                        continue;
                    if (dist[to] < float.MaxValue)
                        queue.Remove((dist[to], to));
                    dist[to] = next;
                    fromArm[to] = fromArm[v];
                    queue.Add((next, to));
                }
            }

            int moved = 0;
            for (int i = 0; i < n; i++)
            {
                if (held.Contains(island[i]) || fromArm[i] || dist[i] == float.MaxValue || ArmWeight(weights[i], arm) <= 0f)
                    continue;
                Vector3 p = world[i];
                int nearestTorso = torso.OrderBy(t => (bones[t].position - p).sqrMagnitude).First();
                weights[i] = MoveToBone(weights[i], arm, nearestTorso);
                moved++;
            }
            return moved;
        }

        /// <summary>True for a vertex mostly weighted to an upper or lower arm and close around that bone.</summary>
        private static bool IsSleeveCore(Vector3 point, BoneWeight w, Transform[] bones)
        {
            Transform bone = bones[w.boneIndex0];
            if (!bone.name.Contains("UpperArm") && !bone.name.Contains("LowerArm") || bone.childCount == 0)
                return false;
            return w.weight0 >= 0.6f && DistanceToSegment(point, bone.position, bone.GetChild(0).position)
                < (bone.position - bone.GetChild(0).position).magnitude * 0.6f;
        }

        /// <summary>Edges of the mesh, with vertices at the same position (UV seams) joined at zero length.</summary>
        private static List<(int to, float length)>[] Graph(Mesh mesh, Vector3[] vertices, Vector3[] world)
        {
            var edges = new List<(int to, float length)>[vertices.Length];
            for (int i = 0; i < edges.Length; i++)
                edges[i] = new List<(int, float)>();
            int[] triangles = mesh.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = triangles[t + k], b = triangles[t + (k + 1) % 3];
                    float length = (world[a] - world[b]).magnitude;
                    edges[a].Add((b, length));
                    edges[b].Add((a, length));
                }
            }
            var seen = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 10000f);
                if (seen.TryGetValue(key, out int other))
                {
                    edges[i].Add((other, 0f));
                    edges[other].Add((i, 0f));
                }
                else
                {
                    seen[key] = i;
                }
            }
            return edges;
        }

        private static float ArmWeight(BoneWeight w, HashSet<int> arm)
        {
            return (arm.Contains(w.boneIndex0) ? w.weight0 : 0f) + (arm.Contains(w.boneIndex1) ? w.weight1 : 0f)
                + (arm.Contains(w.boneIndex2) ? w.weight2 : 0f) + (arm.Contains(w.boneIndex3) ? w.weight3 : 0f);
        }

        /// <summary>The same weights with every arm bone's share given to <paramref name="target"/>, heaviest first.</summary>
        private static BoneWeight MoveToBone(BoneWeight w, HashSet<int> arm, int target)
        {
            var shares = new Dictionary<int, float>();
            void Add(int bone, float weight)
            {
                if (weight <= 0f)
                    return;
                int to = arm.Contains(bone) ? target : bone;
                shares[to] = (shares.TryGetValue(to, out float s) ? s : 0f) + weight;
            }
            Add(w.boneIndex0, w.weight0);
            Add(w.boneIndex1, w.weight1);
            Add(w.boneIndex2, w.weight2);
            Add(w.boneIndex3, w.weight3);

            var sorted = shares.OrderByDescending(kv => kv.Value).Take(4).ToList();
            float total = sorted.Sum(kv => kv.Value);
            var result = new BoneWeight();
            for (int k = 0; k < sorted.Count; k++)
            {
                float value = sorted[k].Value / total;
                switch (k)
                {
                    case 0: result.boneIndex0 = sorted[k].Key; result.weight0 = value; break;
                    case 1: result.boneIndex1 = sorted[k].Key; result.weight1 = value; break;
                    case 2: result.boneIndex2 = sorted[k].Key; result.weight2 = value; break;
                    case 3: result.boneIndex3 = sorted[k].Key; result.weight3 = value; break;
                }
            }
            return result;
        }

        /// <summary>Mesh pieces carried in a hand: islands with a hand-weighted vertex well beyond the hand.</summary>
        private static HashSet<int> HeldIslands(BoneWeight[] weights, Vector3[] world, int[] island, HashSet<int> arm,
            Transform[] bones, float height)
        {
            var held = new HashSet<int>();
            for (int i = 0; i < weights.Length; i++)
            {
                int bone = weights[i].boneIndex0;
                if (!arm.Contains(bone) || !bones[bone].name.Contains("Hand"))
                    continue;
                if ((world[i] - bones[bone].position).magnitude > HeldReach * height * 0.5f)
                    held.Add(island[i]);
            }
            return held;
        }

        /// <summary>Connected pieces of the mesh, joining vertices that share a triangle or a position.</summary>
        private static int[] Islands(Mesh mesh, Vector3[] vertices)
        {
            int[] parent = Enumerable.Range(0, vertices.Length).ToArray();
            int Find(int x)
            {
                while (parent[x] != x)
                    x = parent[x] = parent[parent[x]];
                return x;
            }
            void Union(int a, int b) => parent[Find(a)] = Find(b);

            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Union(triangles[i], triangles[i + 1]);
                Union(triangles[i + 1], triangles[i + 2]);
            }
            var seen = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 10000f);
                if (seen.TryGetValue(key, out int other))
                    Union(i, other);
                else
                    seen[key] = i;
            }
            return Enumerable.Range(0, vertices.Length).Select(Find).ToArray();
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
