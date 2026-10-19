using UnityEngine;

namespace PX
{
    /// <summary>The ribbon of the tether: a line of light from her hand to the lamp while she is pulled, lingering a moment after.</summary>
    public sealed class TetherEffects : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Animator animator;
        [SerializeField] private LineRenderer ribbon;
        [SerializeField] private Color color = new Color(1f, 0.55f, 0.15f);
        [SerializeField, Min(0f)] private float lingerTime = 0.25f;

        private float linger;
        private Vector3 lastEnd;
        private Transform hand;

        private void OnEnable()
        {
            if (player != null)
                player.Tethered += OnTethered;
            if (ribbon != null)
            {
                ribbon.positionCount = 2;
                ribbon.enabled = false;
            }
        }

        private void OnDisable()
        {
            if (player != null)
                player.Tethered -= OnTethered;
        }

        private void OnTethered(TetherAnchor anchor)
        {
            lastEnd = anchor.transform.position;
            linger = lingerTime;
        }

        private void LateUpdate()
        {
            if (ribbon == null || player == null)
                return;
            if (hand == null && animator != null && animator.isHuman)
                hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            bool pulling = player.State == PlayerState.Tether && player.Tether != null;
            if (pulling)
            {
                lastEnd = player.Tether.transform.position;
                linger = lingerTime;
            }
            else
                linger -= Time.deltaTime;

            bool show = pulling || linger > 0f;
            ribbon.enabled = show;
            if (!show)
                return;
            Vector3 start = hand != null ? hand.position : player.transform.position + Vector3.up * 1.3f;
            // A slight sag, pulled taut: the middle hangs a little below the line.
            ribbon.positionCount = 6;
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                Vector3 p = Vector3.Lerp(start, lastEnd, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * (pulling ? 0.12f : 0.4f);
                ribbon.SetPosition(i, p);
            }
            float alpha = pulling ? 1f : Mathf.Clamp01(linger / lingerTime);
            Color c = color;
            c.a = alpha;
            ribbon.startColor = c;
            ribbon.endColor = c;
        }
    }
}
