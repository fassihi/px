using UnityEngine;

namespace PX
{
    /// <summary>
    /// Graybox stand-in for animation and effects: tints the body by state and shows the live hitbox.
    /// Delete once the heroine has real animation.
    /// </summary>
    public sealed class PlayerDebugView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private PlayerController player;
        [SerializeField] private Renderer body;
        [SerializeField] private Transform hitboxVisual;
        [SerializeField] private Color dashColor = new Color(0.55f, 0.9f, 1f);
        [SerializeField] private Color attackColor = new Color(1f, 0.85f, 0.45f);

        private MaterialPropertyBlock block;
        private Color baseColor;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (body != null)
                baseColor = body.sharedMaterial.GetColor(BaseColorId);
        }

        private void LateUpdate()
        {
            if (player == null)
                return;

            if (body != null)
            {
                Color color = player.State switch
                {
                    PlayerState.Dash => dashColor,
                    PlayerState.Attack => attackColor,
                    _ => baseColor,
                };
                block.SetColor(BaseColorId, color);
                body.SetPropertyBlock(block);
            }

            if (hitboxVisual != null)
            {
                bool show = player.State == PlayerState.Attack && player.IsHitboxActive;
                if (show)
                {
                    player.GetHitbox(player.Combo.steps[player.ComboIndex], out Vector3 center, out Vector3 halfExtents);
                    hitboxVisual.SetPositionAndRotation(center, player.transform.rotation);
                    // Drawn thin across the rail: reach and height are what matter when tuning from the side.
                    hitboxVisual.localScale = new Vector3(0.08f, halfExtents.y * 2f, halfExtents.z * 2f);
                }

                if (hitboxVisual.gameObject.activeSelf != show)
                    hitboxVisual.gameObject.SetActive(show);
            }
        }
    }
}
