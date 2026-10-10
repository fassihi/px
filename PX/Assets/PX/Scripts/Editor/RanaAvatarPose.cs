using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PX.EditorTools
{
    /// <summary>
    /// Straightens the reference pose of Rana's humanoid avatar. Unity builds it from Tripo's bind pose, which is
    /// not a T-pose: she leans forward on bent knees, head down, on tiptoe. Clips from other rigs (Mixamo, the
    /// animation library) are retargeted relative to that pose, so they came out hunched with pointed feet. Her own
    /// clips are baked against the same avatar, so they play as before. The pose is stored in her model's import
    /// settings (.meta). Running it again changes nothing.
    /// </summary>
    public static class RanaAvatarPose
    {
        // Directions in her model space: she faces +z, her left is -x.
        private static readonly Vector3 SpineUp = Vector3.up;
        private static readonly Vector3 NeckUp = new Vector3(0f, 0.96f, 0.27f).normalized;
        private static readonly Vector3 HeadUp = new Vector3(0f, 0.91f, 0.42f).normalized;
        private static readonly Vector3 LegDown = Vector3.down;

        // Her head bone points out of her face. Level with the ground, she looks down; tilted up this much, ahead.
        private const float HeadPitch = 25f;

        // From the ankle down to the ball of a flat foot, about 33 degrees below horizontal.
        private const float FootPitch = 33f;

        private static readonly (string bone, string child)[] ArmChains =
        {
            ("LeftShoulder", "LeftUpperArm"), ("LeftUpperArm", "LeftLowerArm"), ("LeftLowerArm", "LeftHand"),
            ("RightShoulder", "RightUpperArm"), ("RightUpperArm", "RightLowerArm"), ("RightLowerArm", "RightHand"),
        };

        [MenuItem("PX/Rana/Straighten Avatar Pose")]
        public static void Straighten()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(RanaImportSettings.BodyPath);
            HumanDescription description = importer.humanDescription;
            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RanaImportSettings.BodyPath));
            try
            {
                Dictionary<string, Transform> transforms = model.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                Transform root = model.transform;
                foreach (SkeletonBone bone in description.skeleton)
                {
                    if (transforms.TryGetValue(bone.name, out Transform t) && t != root)
                    {
                        t.localPosition = bone.position;
                        t.localRotation = bone.rotation;
                    }
                }

                Transform Bone(string humanName)
                {
                    HumanBone match = description.human.FirstOrDefault(b => b.humanName == humanName);
                    return match.boneName != null && transforms.TryGetValue(match.boneName, out Transform t) ? t : null;
                }

                // The arms and hands were already where they belong. Keep their directions while the spine moves.
                var keepDirection = ArmChains
                    .Select(pair => (bone: Bone(pair.bone), child: Bone(pair.child)))
                    .Where(pair => pair.bone != null && pair.child != null)
                    .Select(pair => (pair.bone, pair.child, direction: pair.child.position - pair.bone.position))
                    .ToArray();
                var keepRotation = new[] { "Head", "LeftHand", "RightHand" }
                    .Select(Bone).Where(t => t != null)
                    .Select(t => (bone: t, rotation: t.rotation))
                    .ToArray();

                Aim(Bone("Hips"), Bone("Spine"), SpineUp);
                Aim(Bone("Spine"), Bone("Chest"), SpineUp);
                Aim(Bone("Chest"), Bone("UpperChest"), SpineUp);
                Aim(Bone("UpperChest") ?? Bone("Chest"), Bone("Neck"), NeckUp);
                Aim(Bone("Neck"), Bone("Head"), HeadUp);

                foreach (var (bone, child, direction) in keepDirection)
                    Aim(bone, child, direction);
                foreach (var (bone, rotation) in keepRotation)
                    bone.rotation = rotation;

                Transform head = Bone("Head");
                if (head != null)
                {
                    Vector3 facing = Flat(head.up);
                    Vector3 lifted = Quaternion.AngleAxis(-HeadPitch, Vector3.Cross(Vector3.up, facing)) * facing;
                    head.rotation = Quaternion.FromToRotation(head.up, lifted) * head.rotation;
                }

                foreach (string side in new[] { "Left", "Right" })
                {
                    Aim(Bone(side + "UpperLeg"), Bone(side + "LowerLeg"), LegDown);
                    Aim(Bone(side + "LowerLeg"), Bone(side + "Foot"), LegDown);

                    Transform foot = Bone(side + "Foot"), toes = Bone(side + "Toes");
                    if (foot == null || toes == null)
                        continue;
                    Vector3 heading = Flat(toes.position - foot.position);
                    Aim(foot, toes, Quaternion.AngleAxis(FootPitch, Vector3.Cross(Vector3.up, heading)) * heading);
                    if (toes.childCount > 0)
                    {
                        Transform tip = toes.GetChild(0);
                        Aim(toes, tip, Flat(tip.position - toes.position));
                    }
                }

                description.skeleton = description.skeleton
                    .Select(bone =>
                    {
                        if (transforms.TryGetValue(bone.name, out Transform t) && t != root)
                        {
                            bone.position = t.localPosition;
                            bone.rotation = t.localRotation;
                        }
                        return bone;
                    })
                    .ToArray();
            }
            finally
            {
                Object.DestroyImmediate(model);
            }

            importer.humanDescription = description;
            importer.SaveAndReimport();
        }

        /// <summary>Rotates <paramref name="bone"/> so that its child lies in <paramref name="direction"/>.</summary>
        private static void Aim(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null)
                return;
            bone.rotation = Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z).normalized;
    }
}
