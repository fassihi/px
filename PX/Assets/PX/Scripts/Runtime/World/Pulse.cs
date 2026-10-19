using System.Collections.Generic;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// Breathes the light of an object: the emission of every glowing renderer under it rises and falls, and so can
    /// its scale. A flicker adds a little noise, for neon. Renderers without emission are left alone.
    /// </summary>
    public sealed class Pulse : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Min(0.1f)] private float period = 4f;
        [Tooltip("How far the emission swings either side of its base, as a share of it.")]
        [SerializeField, Range(0f, 1f)] private float swing = 0.3f;
        [Tooltip("Scale swing, as a share of the object's scale. 0 keeps the scale.")]
        [SerializeField, Range(0f, 1f)] private float scaleSwing = 0f;
        [Tooltip("Random flicker added to the emission, as a share of it.")]
        [SerializeField, Range(0f, 1f)] private float flicker = 0f;

        private readonly List<(Renderer renderer, Color emission)> glowing = new List<(Renderer, Color)>();
        private MaterialPropertyBlock block;
        private Vector3 restScale;
        private float phase;

        /// <summary>Multiplies the emission on top of the breathing. Lets the tether brighten lamps in reach.</summary>
        public float Boost { get; set; } = 1f;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            restScale = transform.localScale;
            phase = (transform.position.x * 0.43f + transform.position.y * 0.71f + transform.position.z * 0.17f) % (Mathf.PI * 2f);
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(EmissionId))
                    continue;
                Color emission = material.GetColor(EmissionId);
                if (emission.maxColorComponent <= 0.01f)
                    continue;
                glowing.Add((renderer, emission));
            }
        }

        private void Update()
        {
            float t = Time.time * Mathf.PI * 2f / period + phase;
            float wave = 1f + Mathf.Sin(t) * swing;
            if (flicker > 0f)
                wave += (Mathf.PerlinNoise(Time.time * 9f, phase) - 0.5f) * 2f * flicker;
            wave = Mathf.Max(0.05f, wave) * Boost;
            foreach ((Renderer renderer, Color emission) in glowing)
            {
                renderer.GetPropertyBlock(block);
                block.SetColor(EmissionId, emission * wave);
                renderer.SetPropertyBlock(block);
            }
            if (scaleSwing > 0f)
                transform.localScale = restScale * (1f + Mathf.Sin(t) * scaleSwing);
        }
    }
}
