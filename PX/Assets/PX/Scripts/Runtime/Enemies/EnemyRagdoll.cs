using System.Collections.Generic;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// The physical body an enemy dies into. Built once from the humanoid rig while it stands in its bind pose: a
    /// capsule and a rigidbody per major bone, joined by character joints with limits (knees and elbows bend one way).
    /// It sleeps as kinematic bodies with their colliders off while the enemy lives and is animated. On death the
    /// animator is switched off and the bodies are set free with the velocity of the killing blow; on revival they are
    /// frozen again, the bones go back to their bind pose, and the animator takes over.
    /// </summary>
    public sealed class EnemyRagdoll : MonoBehaviour
    {
        private struct Part
        {
            public Rigidbody Body;
            public Collider Collider;
        }

        // Shares of the character's height.
        private const float HipsRadius = 0.075f;
        private const float SpineRadius = 0.07f;
        private const float HeadRadius = 0.065f;
        private const float UpperArmRadius = 0.03f;
        private const float LowerArmRadius = 0.025f;
        private const float UpperLegRadius = 0.045f;
        private const float LowerLegRadius = 0.035f;

        [SerializeField] private Animator animator;

        private readonly List<Part> parts = new List<Part>();
        private readonly Dictionary<Transform, (Vector3 position, Quaternion rotation)> bindPose =
            new Dictionary<Transform, (Vector3, Quaternion)>();
        private SkinnedMeshRenderer[] renderers;
        private PhysicsMaterial grip;
        private Transform hips;
        private float height;

        /// <summary>True from death until revival: the bones belong to the physics.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Where the body's lowest point rests, in world space, and where its hips are.</summary>
        public Vector3 HipsPosition => hips != null ? hips.position : transform.position;

        public float LowestPoint
        {
            get
            {
                float lowest = float.MaxValue;
                foreach (Part part in parts)
                    lowest = Mathf.Min(lowest, part.Collider.bounds.min.y);
                return parts.Count > 0 ? lowest : transform.position.y;
            }
        }

        public int BodyCount => parts.Count;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            // A body does not slide across the floor like ice.
            grip = new PhysicsMaterial("Ragdoll")
            {
                dynamicFriction = 0.9f, staticFriction = 0.9f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
            };
            Build();
        }

        private void Build()
        {
            if (animator == null || !animator.isHuman)
                return;

            foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
                bindPose[bone] = (bone.localPosition, bone.localRotation);

            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || head == null)
                return;
            height = Mathf.Max(0.5f, head.position.y - transform.position.y + 0.15f);

            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? spine;
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? head;

            // Bones are made parent first, so a joint can find the body above it.
            AddLimb(hips, spine ?? chest, HipsRadius, 14f, Hinge.None, 20f, 25f);
            if (spine != null && chest != spine)
                AddLimb(spine, chest, SpineRadius, 9f, Hinge.None, 20f, 25f);
            AddLimb(chest, neck, SpineRadius, 9f, Hinge.None, 20f, 25f);
            AddHead(head);

            foreach (string side in new[] { "Left", "Right" })
            {
                bool left = side == "Left";
                HumanBodyBones upperArm = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
                HumanBodyBones lowerArm = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
                HumanBodyBones hand = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                HumanBodyBones upperLeg = left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
                HumanBodyBones lowerLeg = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
                HumanBodyBones foot = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;

                AddLimb(animator.GetBoneTransform(upperArm), animator.GetBoneTransform(lowerArm), UpperArmRadius, 2.5f, Hinge.None, 40f, 85f);
                AddLimb(animator.GetBoneTransform(lowerArm), animator.GetBoneTransform(hand), LowerArmRadius, 1.5f, Hinge.Elbow, 0f, 0f);
                AddLimb(animator.GetBoneTransform(upperLeg), animator.GetBoneTransform(lowerLeg), UpperLegRadius, 7f, Hinge.None, 30f, 70f);
                AddLimb(animator.GetBoneTransform(lowerLeg), animator.GetBoneTransform(foot), LowerLegRadius, 4f, Hinge.Knee, 0f, 0f);
            }

            // Its own pieces never collide with each other: crossed limbs would only jitter.
            for (int i = 0; i < parts.Count; i++)
            {
                for (int j = i + 1; j < parts.Count; j++)
                    Physics.IgnoreCollision(parts[i].Collider, parts[j].Collider);
            }

            SetPhysics(false);
        }

        private enum Hinge { None, Knee, Elbow }

        private void AddLimb(Transform bone, Transform child, float radius, float mass, Hinge hinge, float twist, float swing)
        {
            if (bone == null || child == null || bone == child)
                return;

            Vector3 local = bone.InverseTransformPoint(child.position);
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            int axis = AxisOf(local);
            capsule.direction = axis;
            capsule.center = local * 0.5f;
            capsule.height = local.magnitude;
            capsule.radius = radius * height / Mathf.Max(0.0001f, bone.lossyScale.x);
            AddBody(bone, capsule, mass, hinge, twist, swing, local);
        }

        private void AddHead(Transform head)
        {
            var collider = head.gameObject.AddComponent<SphereCollider>();
            collider.center = head.InverseTransformPoint(head.position + Vector3.up * height * 0.055f);
            collider.radius = HeadRadius * height / Mathf.Max(0.0001f, head.lossyScale.x);
            AddBody(head, collider, 4f, Hinge.None, 30f, 40f, head.InverseTransformDirection(Vector3.up));
        }

        private void AddBody(Transform bone, Collider collider, float mass, Hinge hinge, float twist, float swing, Vector3 localAxis)
        {
            collider.sharedMaterial = grip;
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.2f;
            body.angularDamping = 1.5f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 12;
            body.maxDepenetrationVelocity = 4f;

            Rigidbody parent = ParentBody(bone);
            if (parent != null)
            {
                var joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parent;
                joint.anchor = Vector3.zero;
                joint.enableProjection = true;
                joint.enablePreprocessing = false;

                if (hinge == Hinge.None)
                {
                    Vector3 twistAxis = localAxis.sqrMagnitude > 1e-6f ? localAxis.normalized : Vector3.up;
                    joint.axis = twistAxis;
                    joint.swingAxis = Perpendicular(twistAxis);
                    joint.lowTwistLimit = new SoftJointLimit { limit = -twist };
                    joint.highTwistLimit = new SoftJointLimit { limit = twist };
                    joint.swing1Limit = new SoftJointLimit { limit = swing };
                    joint.swing2Limit = new SoftJointLimit { limit = swing };
                }
                else
                {
                    // A hinge about her side-to-side axis, with the other two held nearly still. Bending the shin so the
                    // foot goes backward, and the forearm so the hand goes forward, is a turn about +/- that axis.
                    Vector3 side = bone.InverseTransformDirection(transform.right).normalized;
                    joint.axis = side;
                    joint.swingAxis = Perpendicular(side);
                    joint.lowTwistLimit = new SoftJointLimit { limit = hinge == Hinge.Knee ? -5f : -135f };
                    joint.highTwistLimit = new SoftJointLimit { limit = hinge == Hinge.Knee ? 135f : 5f };
                    joint.swing1Limit = new SoftJointLimit { limit = 4f };
                    joint.swing2Limit = new SoftJointLimit { limit = 4f };
                }
            }

            parts.Add(new Part { Body = body, Collider = collider });
        }

        private Rigidbody ParentBody(Transform bone)
        {
            for (Transform up = bone.parent; up != null && up != transform; up = up.parent)
            {
                Rigidbody body = up.GetComponent<Rigidbody>();
                if (body != null)
                    return body;
            }
            return null;
        }

        private static int AxisOf(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
        }

        private static Vector3 Perpendicular(Vector3 v)
        {
            Vector3 other = Mathf.Abs(Vector3.Dot(v.normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            return Vector3.Cross(v, other).normalized;
        }

        private void SetPhysics(bool on)
        {
            foreach (Part part in parts)
            {
                if (!on)
                {
                    part.Body.isKinematic = true;
                    part.Collider.enabled = false;
                }
                else
                {
                    part.Collider.enabled = true;
                    part.Body.isKinematic = false;
                    part.Body.detectCollisions = true;
                }
            }
        }

        /// <summary>Keeps the pieces from colliding with something, such as the character the enemy was fighting.</summary>
        public void IgnoreCollisionsWith(Collider other)
        {
            if (other == null)
                return;
            foreach (Part part in parts)
                Physics.IgnoreCollision(part.Collider, other);
        }

        /// <summary>Lets go of the animation and sets the body free, thrown with <paramref name="velocity"/>.</summary>
        public void Activate(Vector3 velocity)
        {
            if (IsActive || parts.Count == 0)
                return;
            IsActive = true;

            if (animator != null)
                animator.enabled = false;
            foreach (SkinnedMeshRenderer renderer in renderers)
                renderer.updateWhenOffscreen = true;

            SetPhysics(true);
            foreach (Part part in parts)
            {
                part.Body.linearVelocity = velocity;
                part.Body.angularVelocity = Vector3.zero;
            }
            // A blow lands high on the body: the head and chest are thrown further than the legs, so it tumbles.
            if (hips != null)
            {
                Rigidbody top = hips.GetComponentInChildren<Rigidbody>();
                foreach (Part part in parts)
                {
                    float up = Mathf.Clamp01((part.Body.position.y - hips.position.y) / (height * 0.4f));
                    part.Body.AddForce(velocity * 0.35f * up, ForceMode.VelocityChange);
                }
                if (top != null)
                    top.AddTorque(Vector3.Cross(Vector3.up, velocity) * 0.1f, ForceMode.VelocityChange);
            }
        }

        /// <summary>Freezes the body and puts every bone back in its bind pose, ready for the animator to take over.</summary>
        public void Deactivate()
        {
            if (!IsActive)
                return;
            IsActive = false;

            foreach (Part part in parts)
            {
                part.Body.linearVelocity = Vector3.zero;
                part.Body.angularVelocity = Vector3.zero;
            }
            SetPhysics(false);
            foreach (KeyValuePair<Transform, (Vector3 position, Quaternion rotation)> pair in bindPose)
            {
                pair.Key.localPosition = pair.Value.position;
                pair.Key.localRotation = pair.Value.rotation;
            }
            foreach (SkinnedMeshRenderer renderer in renderers)
                renderer.updateWhenOffscreen = false;
            if (animator != null)
                animator.enabled = true;
        }
    }
}
