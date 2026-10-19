using UnityEngine;

namespace PX
{
    /// <summary>Rocks an object slowly about its forward axis, like a banner in a draught.</summary>
    public sealed class Sway : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float degrees = 4f;
        [SerializeField, Min(0.1f)] private float period = 4f;

        private Quaternion rest;
        private float phase;

        private void Awake()
        {
            rest = transform.localRotation;
            phase = (transform.position.x * 0.37f + transform.position.z * 0.53f) % (Mathf.PI * 2f);
        }

        private void Update()
        {
            float t = Time.time * Mathf.PI * 2f / period + phase;
            transform.localRotation = rest * Quaternion.AngleAxis(Mathf.Sin(t) * degrees, Vector3.forward);
        }
    }
}
