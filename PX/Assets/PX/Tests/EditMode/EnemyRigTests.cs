using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PX.Tests
{
    public sealed class EnemyRigTests
    {
        private const string EnemyAPath = "Assets/PX/Art/EnemyA/EnemyA.fbx";

        [Test]
        public void EnemyA_RaisingBothArms_DoesNotDragItsCoat()
        {
            // The auto-rig skinned the coat, its straps and tassels to the arms, so a raised arm dragged a long band of
            // cloth up with it: about 300 triangles stretched to more than 2.5 times their size. The import fixes the
            // weights (CoatWeights); what stretches now is around the shoulders, where some stretch is expected.
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyAPath);
            Assert.That(asset, Is.Not.Null, $"No model at {EnemyAPath}.");
            var model = Object.Instantiate(asset);
            try
            {
                var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
                Transform[] bones = renderer.bones;
                int[] triangles = renderer.sharedMesh.triangles;

                var before = new Mesh();
                renderer.BakeMesh(before, true);
                foreach (string side in new[] { "Left", "Right" })
                {
                    Transform upperArm = System.Array.Find(bones, b => b.name == side + "_UpperArm");
                    upperArm.rotation = Quaternion.AngleAxis(side == "Left" ? 150f : -150f, Vector3.forward) * upperArm.rotation;
                }
                var after = new Mesh();
                renderer.BakeMesh(after, true);

                Vector3[] a = before.vertices, b = after.vertices;
                int stretched = 0;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    float worst = 0f;
                    for (int k = 0; k < 3; k++)
                    {
                        int i = triangles[t + k], j = triangles[t + (k + 1) % 3];
                        float rest = (a[i] - a[j]).magnitude;
                        if (rest > 1e-5f)
                            worst = Mathf.Max(worst, (b[i] - b[j]).magnitude / rest);
                    }
                    if (worst > 2.5f)
                        stretched++;
                }

                Assert.That(stretched, Is.LessThan(200), $"{stretched} triangles stretch more than 2.5 times with the arms raised.");
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }
    }
}
