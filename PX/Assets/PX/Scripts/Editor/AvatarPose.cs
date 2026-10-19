using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PX.EditorTools
{
    /// <summary>
    /// Puts the reference pose of humanoid avatars into one clean T-pose. A clip is retargeted as changes from its
    /// avatar's reference pose onto changes from the target's, so both have to mean the same standing pose.
    /// They did not: Rana's comes from Tripo's bind pose (leaning forward on bent knees, head down, on tiptoe), and a
    /// Mixamo clip downloaded without skin has no bind pose, so Unity takes the first frame of the animation (mid
    /// stride). Retargeted runs came out hunched with wrong feet. Here both become upright: straight spine and legs,
    /// hip and shoulder lines square, feet forward and flat, arms out with straight wrists, head level.
    /// Her own clips are baked against the same avatar, so they play as before. The pose is stored in each model's
    /// import settings (.meta). A model that is already straight is left alone.
    /// </summary>
    public static class AvatarPose
    {
        // Directions in model space: the character faces +z, her left is -x.
        private static readonly Vector3 NeckUp = new Vector3(0f, 0.96f, 0.27f).normalized;
        private static readonly Vector3 HeadUp = new Vector3(0f, 0.91f, 0.42f).normalized;

        // From the ankle down to the toe joint of a flat foot, below horizontal. It depends on where the rig puts that
        // joint, so each rig has its own: measured from Rana's sole, and from Mixamo clips standing with the toe tip on
        // the floor. With one angle for both, every Mixamo clip put her up on tiptoe by the difference.
        private const float RanaFootPitch = 33f;
        private const float MixamoFootPitch = 61f;

        // A head bone without an end bone on top points out of the face. Level with the ground, the character looks
        // down; tilted up this much, ahead. Heads with an end bone on top (Mixamo, Rana_Oct_14) have that pointed
        // straight up instead.
        private const float HeadPitch = 35f;

        private const float Tolerance = 0.5f;

        private static readonly (string bone, string child, float side)[] Arms =
        {
            ("LeftShoulder", "LeftUpperArm", -1f), ("LeftUpperArm", "LeftLowerArm", -1f), ("LeftLowerArm", "LeftHand", -1f),
            ("RightShoulder", "RightUpperArm", 1f), ("RightUpperArm", "RightLowerArm", 1f), ("RightLowerArm", "RightHand", 1f),
        };

        /// <summary>Rana's body, every Mixamo clip, and the enemies' bodies (Tripo rigs like hers).</summary>
        public static IEnumerable<string> Models()
        {
            yield return RanaImportSettings.BodyPath;
            yield return RanaImportSettings.AnimationsPath;
            foreach (string body in EnemyImportSettings.BodyPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(body) != null)
                    yield return body;
            }
            if (!AssetDatabase.IsValidFolder(RanaImportSettings.MixamoFolder))
                yield break;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { RanaImportSettings.MixamoFolder }))
                yield return AssetDatabase.GUIDToAssetPath(guid);
            foreach (string folder in EnemyImportSettings.MixamoFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                    yield return AssetDatabase.GUIDToAssetPath(guid);
            }
        }

        [MenuItem("PX/Rana/Straighten Avatar Poses")]
        public static void StraightenAll()
        {
            foreach (string path in Models())
                Straighten(path);
        }

        /// <summary>Straightens one model's avatar. Returns false when it was already straight.</summary>
        public static bool Straighten(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            HumanDescription description = importer.humanDescription;
            if (description.human == null || description.human.Length == 0)
                return false;

            var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            string worst = null;
            float worstAngle = Tolerance;
            try
            {
                Transform root = model.transform;
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var transforms = new Dictionary<string, Transform>();
                foreach (Transform t in model.GetComponentsInChildren<Transform>())
                    transforms[t.name] = t;
                foreach (SkeletonBone bone in description.skeleton)
                {
                    if (transforms.TryGetValue(bone.name, out Transform t) && t != root)
                        t.SetLocalPositionAndRotation(bone.position, bone.rotation);
                }

                Transform Bone(string humanName)
                {
                    HumanBone match = description.human.FirstOrDefault(b => b.humanName == humanName);
                    return match.boneName != null && transforms.TryGetValue(match.boneName, out Transform t) ? t : null;
                }

                // Squaring the chest and aiming the shoulders nudge each other, so settle them over a few passes.
                for (int pass = 0; pass < 8; pass++)
                    Pose(Bone, path.Contains("/Mixamo/"));

                for (int i = 0; i < description.skeleton.Length; i++)
                {
                    SkeletonBone bone = description.skeleton[i];
                    if (!transforms.TryGetValue(bone.name, out Transform t) || t == root)
                        continue;
                    float angle = Quaternion.Angle(bone.rotation, t.localRotation);
                    if (angle > worstAngle)
                    {
                        worst = bone.name;
                        worstAngle = angle;
                    }
                    bone.rotation = t.localRotation;
                    description.skeleton[i] = bone;
                }
            }
            finally
            {
                Object.DestroyImmediate(model);
            }

            if (worst == null)
                return false;
            Debug.Log($"Straightened the avatar pose of {Path.GetFileName(path)}. Most turned: {worst}, {worstAngle:0.0} degrees.");
            importer.humanDescription = description;
            importer.SaveAndReimport();
            return true;
        }

        private static void Pose(System.Func<string, Transform> bone, bool mixamo)
        {
            Transform hips = bone("Hips"), chest = bone("UpperChest") ?? bone("Chest"), head = bone("Head");

            Aim(hips, bone("Spine"), Vector3.up);
            Square(hips, bone("LeftUpperLeg"), bone("RightUpperLeg"));
            Aim(bone("Spine"), bone("Chest"), Vector3.up);
            Aim(bone("Chest"), bone("UpperChest"), Vector3.up);
            Square(chest, bone("LeftUpperArm"), bone("RightUpperArm"));
            Aim(chest, bone("Neck"), NeckUp);
            Aim(bone("Neck"), head, HeadUp);

            if (head != null)
            {
                Transform top = head.Cast<Transform>().FirstOrDefault(t => t.name.Contains("Top"));
                if (top != null)
                {
                    Aim(head, top, Vector3.up);
                }
                else
                {
                    Vector3 facing = Flat(head.up);
                    Vector3 lifted = Quaternion.AngleAxis(-HeadPitch, Vector3.Cross(Vector3.up, facing)) * facing;
                    head.rotation = Quaternion.FromToRotation(head.up, lifted) * head.rotation;
                }
                Turn(head, Face(head, chest, top != null, mixamo, bone), Vector3.forward);
            }

            foreach (var (name, child, side) in Arms)
                Aim(bone(name), bone(child), new Vector3(side, 0f, 0f));
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform hand = bone(side + "Hand");
                Aim(hand, bone(side + "MiddleProximal"), new Vector3(side == "Left" ? -1f : 1f, 0f, 0f));
            }

            foreach (string side in new[] { "Left", "Right" })
            {
                Transform upper = bone(side + "UpperLeg"), lower = bone(side + "LowerLeg"), foot = bone(side + "Foot"), toes = bone(side + "Toes");
                Aim(upper, lower, Vector3.down);
                Aim(lower, foot, Vector3.down);
                if (foot == null || toes == null)
                    continue;

                if (mixamo)
                {
                    // A Mixamo clip has no bind pose, so its reference is the first frame, with the ankle turned
                    // however the clip starts. Squaring the leg by where the foot points then gave every clip its own
                    // knee direction, and feet came out twisted. The rig itself says how the leg bends: thigh, shin and
                    // a flat foot all face along their local +z. Square each by that.
                    Roll(upper, lower, Vector3.forward, Vector3.forward);
                    Roll(lower, foot, Vector3.forward, Vector3.forward);
                    Aim(foot, toes, Quaternion.AngleAxis(MixamoFootPitch, Vector3.right) * Vector3.forward);
                    Roll(foot, toes, Vector3.forward, Vector3.up);
                }
                else
                {
                    // Rana's reference is her bind pose: spin the whole leg about its length so the foot points forward.
                    Turn(upper, toes.position - foot.position, Vector3.forward);
                    Aim(foot, toes, Quaternion.AngleAxis(RanaFootPitch, Vector3.right) * Vector3.forward);
                }
                if (toes.childCount > 0)
                    Aim(toes, toes.GetChild(0), Vector3.forward);
            }
        }

        /// <summary>
        /// Which way the face points: between the eyes when there are eye bones. Else a head with an end bone on top
        /// (Mixamo) faces along its forward axis, and one without (Rana) along the bone itself, out of her face.
        /// Squaring Rana's by her forward axis left her face turned about 120 degrees, and every retargeted clip
        /// looked off to her side.
        /// </summary>
        /// <summary>The direction the face points, in world space.</summary>
        private static Vector3 Face(Transform head, Transform chest, bool hasTop, bool mixamo, System.Func<string, Transform> bone)
        {
            Transform left = bone("LeftEye"), right = bone("RightEye");
            if (left != null && right != null)
                return Vector3.Cross(right.position - left.position, Vector3.up);
            if (!hasTop)
                return head.up;
            if (mixamo || chest == null)
                return head.forward;
            // A Tripo rig with Mixamo bone names (Rana_Oct_14) does not point its bones' +z ahead: its spine and head
            // face along their -x. Taking +z turned her head about 100 degrees in every clip. Its head shares the
            // chest's axes, and the chest is already squared to face +z, so the face is the head's version of that axis.
            return head.rotation * (Quaternion.Inverse(chest.rotation) * Vector3.forward);
        }

        /// <summary>Rotates <paramref name="bone"/> so that its child lies in <paramref name="direction"/>.</summary>
        private static void Aim(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null)
                return;
            bone.rotation = Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation;
        }

        /// <summary>Turns <paramref name="bone"/> about the vertical so the line from left to right points along +x.</summary>
        private static void Square(Transform bone, Transform left, Transform right)
        {
            if (bone == null || left == null || right == null)
                return;
            Turn(bone, right.position - left.position, Vector3.right);
        }

        /// <summary>Turns <paramref name="bone"/> about the vertical so <paramref name="from"/>, seen from above, points along <paramref name="to"/>.</summary>
        private static void Turn(Transform bone, Vector3 from, Vector3 to)
        {
            Vector3 flat = Flat(from);
            if (bone == null || flat == Vector3.zero)
                return;
            bone.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(flat, to, Vector3.up), Vector3.up) * bone.rotation;
        }

        /// <summary>
        /// Spins <paramref name="bone"/> about its own length (towards <paramref name="child"/>) so that its local
        /// <paramref name="localAxis"/> points as nearly as it can along <paramref name="worldTarget"/>.
        /// </summary>
        private static void Roll(Transform bone, Transform child, Vector3 localAxis, Vector3 worldTarget)
        {
            if (bone == null || child == null)
                return;
            Vector3 length = (child.position - bone.position).normalized;
            Vector3 current = Vector3.ProjectOnPlane(bone.TransformDirection(localAxis), length);
            Vector3 target = Vector3.ProjectOnPlane(worldTarget, length);
            if (current.sqrMagnitude < 1e-6f || target.sqrMagnitude < 1e-6f)
                return;
            bone.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(current, target, length), length) * bone.rotation;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z).normalized;
    }
}
