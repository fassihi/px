using UnityEngine;

namespace PX
{
    /// <summary>
    /// Flashes a fighter's body when a blow lands on it: a white burst that fades in a few frames, so the hit is felt
    /// on the body as well as seen in the sparks. Blocks flash cooler and weaker; a parry flashes the defender gold.
    /// Works with the URP Lit materials of the characters through their emission, which it switches on.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class HitFlash : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Min(0.01f)] private float duration = 0.14f;
        [SerializeField] private Color hitColor = new Color(1f, 0.9f, 0.85f) * 1.1f;
        [SerializeField] private Color blockColor = new Color(0.4f, 0.65f, 1f) * 0.7f;
        [SerializeField] private Color parryColor = new Color(1f, 0.8f, 0.3f) * 1.4f;

        private Health health;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Color color;
        private float remaining;

        private void Awake()
        {
            health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
            renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (Renderer body in renderers)
            {
                // Instanced so the shared material asset is untouched; emission starts black, so nothing changes until a flash.
                foreach (Material material in body.materials)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor(EmissionId, Color.black);
                }
            }
        }

        private void OnEnable()
        {
            Health.AnyStruck += OnStruck;
            PlayerController player = GetComponent<PlayerController>();
            if (player != null)
                player.Parried += OnParried;
        }

        private void OnDisable()
        {
            Health.AnyStruck -= OnStruck;
            PlayerController player = GetComponent<PlayerController>();
            if (player != null)
                player.Parried -= OnParried;
        }

        private void OnStruck(Health target, Hit hit, HitResult result)
        {
            if (target != health || result == HitResult.Parried)
                return;
            Flash(result == HitResult.Blocked ? blockColor : hitColor);
        }

        private void OnParried(GameObject attacker) => Flash(parryColor);

        private void Flash(Color with)
        {
            color = with;
            remaining = duration;
        }

        private void LateUpdate()
        {
            if (remaining <= 0f && remaining > -1f)
            {
                remaining = -1f;
                Apply(Color.black);
                return;
            }
            if (remaining <= 0f)
                return;
            // Real time, so the flash shows through hit-stop.
            remaining -= Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(remaining / duration);
            Apply(color * (t * t));
        }

        private void Apply(Color emission)
        {
            foreach (Renderer body in renderers)
            {
                body.GetPropertyBlock(block);
                block.SetColor(EmissionId, emission);
                body.SetPropertyBlock(block);
            }
        }
    }
}
