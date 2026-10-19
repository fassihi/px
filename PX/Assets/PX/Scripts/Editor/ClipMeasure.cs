using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PX.EditorTools
{
    /// <summary>
    /// Measures Mixamo clips on their own skeletons, so strike windows can be set from numbers: per tenth of a second,
    /// how far the right hand is in front of the hips along the body's facing, how high, and how far to the side. The
    /// impact of a strike is where the hand is furthest out. Logs a report; runs headless too.
    ///
    /// Menu: PX > Rana > Measure Clips. Command line: -executeMethod PX.EditorTools.ClipMeasure.Run
    /// </summary>
    public static class ClipMeasure
    {
        private static readonly string[] Paths =
        {
            RanaImportSettings.MixamoFolder + "/Sabre 1 Horizontal.fbx",
            RanaImportSettings.MixamoFolder + "/Sabre 2 Backhand.fbx",
            RanaImportSettings.MixamoFolder + "/Sabre 3 Downward.fbx",
            RanaImportSettings.MixamoFolder + "/Sabre Spin.fbx",
            RanaImportSettings.MixamoFolder + "/Takedown Stab.fbx",
            RanaImportSettings.MixamoFolder + "/Drink.fbx",
            EnemyImportSettings.EnemyAMixamoFolder + "/Slam.fbx",
            EnemyImportSettings.EnemyAMixamoFolder + "/Spin Attack.fbx",
            EnemyImportSettings.EnemyAMixamoFolder + "/Power Up.fbx",
        };

        [MenuItem("PX/Rana/Measure Clips")]
        public static void Run()
        {
            Debug.Log(Report());
        }

        public static string Report()
        {
            var report = new StringBuilder("ClipMeasure\n");
            foreach (string path in Paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (asset == null || clip == null)
                {
                    report.AppendLine($"{path}: missing");
                    continue;
                }
                GameObject go = Object.Instantiate(asset);
                try
                {
                    Animator animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                    Transform hand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
                    Transform hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                    report.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(path)}: {clip.length:0.00} s");
                    if (hand == null || hips == null)
                    {
                        report.AppendLine("  no humanoid hand or hips");
                        continue;
                    }
                    var line = new StringBuilder("  ");
                    for (float t = 0f; t <= clip.length + 0.001f; t += 0.1f)
                    {
                        clip.SampleAnimation(go, Mathf.Min(t, clip.length));
                        Vector3 offset = hand.position - hips.position;
                        float ahead = Vector3.Dot(offset, go.transform.forward);
                        float side = Vector3.Dot(offset, go.transform.right);
                        line.Append($"{t:0.0}:{ahead:0.00}/{offset.y:0.00}/{side:0.00}  ");
                    }
                    report.AppendLine(line.ToString());
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
            return report.ToString();
        }
    }
}
