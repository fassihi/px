using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PX.Tests
{
    public sealed class EnemyRigTests
    {
        private const string EnemyAPath = "Assets/PX/Art/EnemyA/EnemyA.fbx";

        [TestCase("Idle", 0.5f)]
        [TestCase("Attack", 0.64f)]
        public void EnemyA_Sword_PointsForwardFromItsGrip(string clipName, float time)
        {
            // Tripo modelled the sword hanging from a relaxed hand; in Mixamo's two-handed grip it pointed back over the
            // shoulder until the import seated it (HeldWeapon). In the guard and at the moment the chop lands, the blade
            // should be out in front of the knight.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PX/Prefabs/EnemyA.prefab");
            var config = AssetDatabase.LoadAssetAtPath<PX.EnemyConfig>("Assets/PX/Config/EnemyA.asset");
            AnimationClip clip = clipName == "Idle" ? config.idle : config.attackClip.clip;
            var enemy = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            var graph = PlayableGraph.Create();
            try
            {
                foreach (MonoBehaviour behaviour in enemy.GetComponentsInChildren<MonoBehaviour>())
                    behaviour.enabled = false;
                Animator animator = enemy.GetComponentInChildren<Animator>();
                var playable = AnimationClipPlayable.Create(graph, clip);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                playable.SetTime(time);
                graph.Evaluate(0f);

                var renderer = enemy.GetComponentInChildren<SkinnedMeshRenderer>();
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                int handIndex = System.Array.IndexOf(renderer.bones, hand);
                BoneWeight[] weights = renderer.sharedMesh.boneWeights;
                var posed = new Mesh();
                renderer.BakeMesh(posed, true);
                Vector3[] vertices = posed.vertices;

                // The tip: the vertex held rigidly by the hand that is furthest from it.
                Vector3 tip = hand.position;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (weights[i].boneIndex0 != handIndex || weights[i].weight0 < 0.99f)
                        continue;
                    Vector3 point = renderer.transform.TransformPoint(vertices[i]);
                    if ((point - hand.position).sqrMagnitude > (tip - hand.position).sqrMagnitude)
                        tip = point;
                }

                Vector3 blade = enemy.transform.InverseTransformDirection(tip - hand.position);
                Assert.That(blade.magnitude, Is.GreaterThan(0.6f), "No sword found in the right hand.");
                Assert.That(blade.normalized.z, Is.GreaterThan(0.7f), $"The blade points {blade.normalized} from the hand, not forward.");
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(enemy);
            }
        }

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
