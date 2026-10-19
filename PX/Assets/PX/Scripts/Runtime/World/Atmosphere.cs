using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PX
{
    /// <summary>
    /// The air of the level, keyed by rail distance: fog colour and density and the ambient light blend from one
    /// act's palette to the next as she moves between the keys, so the gate halls give way to the face shrine
    /// without a cut.
    /// </summary>
    public sealed class Atmosphere : MonoBehaviour
    {
        [Serializable]
        public struct Key
        {
            public float distance;
            public Color fog;
            public float fogDensity;
            public Color sky;
            public Color equator;
            public Color ground;
        }

        [SerializeField] private CharacterMotor target;
        [Tooltip("The camera whose background follows the fog colour, so the dark has no horizon.")]
        [SerializeField] private Camera background;
        [SerializeField] private Key[] keys = Array.Empty<Key>();
        [Tooltip("Seconds the air takes to settle after a change, so a reveal does not snap the colours.")]
        [SerializeField, Min(0.01f)] private float settleTime = 1.5f;

        private Key current;
        private bool initialized;

        private void LateUpdate()
        {
            if (keys.Length == 0)
                return;
            Key wanted = At(target != null ? target.Distance : 0f);
            if (!initialized)
            {
                current = wanted;
                initialized = true;
            }
            else
            {
                float t = 1f - Mathf.Exp(-Time.deltaTime / settleTime);
                current = Lerp(current, wanted, t);
            }
            Apply(current);
            if (background != null)
                background.backgroundColor = current.fog;
        }

        /// <summary>The air at a rail distance: the keys either side of it, blended.</summary>
        public Key At(float distance)
        {
            if (distance <= keys[0].distance)
                return keys[0];
            for (int i = 1; i < keys.Length; i++)
            {
                if (distance <= keys[i].distance)
                {
                    float span = keys[i].distance - keys[i - 1].distance;
                    float t = span > 0f ? Mathf.SmoothStep(0f, 1f, (distance - keys[i - 1].distance) / span) : 1f;
                    return Lerp(keys[i - 1], keys[i], t);
                }
            }
            return keys[keys.Length - 1];
        }

        private static Key Lerp(Key a, Key b, float t)
        {
            return new Key
            {
                distance = Mathf.Lerp(a.distance, b.distance, t),
                fog = Color.Lerp(a.fog, b.fog, t),
                fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
                sky = Color.Lerp(a.sky, b.sky, t),
                equator = Color.Lerp(a.equator, b.equator, t),
                ground = Color.Lerp(a.ground, b.ground, t),
            };
        }

        private static void Apply(Key key)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = key.fog;
            RenderSettings.fogDensity = key.fogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = key.sky;
            RenderSettings.ambientEquatorColor = key.equator;
            RenderSettings.ambientGroundColor = key.ground;
        }
    }
}
