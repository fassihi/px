using UnityEngine;

namespace PX
{
    /// <summary>
    /// Keeps her sabre in her right hand. The blade is built as a visual under the player and moved into the hand bone
    /// once the rig is posed, pointed out along the hand the way a one-handed sword sits in Mixamo's clips: from the
    /// wrist through the middle knuckle, canted a little toward the thumb. Hidden while she has no weapon drawn.
    /// </summary>
    public sealed class HeldSabre : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform blade;
        [Tooltip("Where the blade points in the hand's frame: x from the wrist to the middle knuckle, y toward the thumb.")]
        [SerializeField] private Vector3 grip = new Vector3(1f, 0.25f, 0f);
        [Tooltip("How far back from the palm the pommel sits, in metres.")]
        [SerializeField] private float pommel = 0.06f;
        [Tooltip("Extra turn of the blade about its own length, in degrees, to set which way the edge faces.")]
        [SerializeField] private float roll = 0f;

        private bool seated;

        public Transform Blade => blade;

        private void LateUpdate()
        {
            if (seated || animator == null || blade == null || !animator.isHuman)
                return;
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform knuckle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform thumb = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
            if (hand == null || knuckle == null || thumb == null)
                return;

            Vector3 x = (knuckle.position - hand.position).normalized;
            Vector3 y = Vector3.ProjectOnPlane(thumb.position - hand.position, x).normalized;
            Vector3 z = Vector3.Cross(x, y);
            Vector3 g = grip.normalized;
            Vector3 along = (x * g.x + y * g.y + z * g.z).normalized;
            Vector3 palm = (hand.position + knuckle.position) * 0.5f;

            blade.SetParent(hand, true);
            blade.position = palm - along * pommel;
            blade.rotation = Quaternion.LookRotation(along, y) * Quaternion.Euler(0f, 0f, roll);
            seated = true;
        }
    }
}
