using UnityEngine;

namespace PX
{
    /// <summary>Floats an object up and down about where it was placed, like a hanging lamp or a hovering craft.</summary>
    public sealed class Bob : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float amplitude = 0.1f;
        [SerializeField, Min(0.1f)] private float period = 3f;

        private Vector3 rest;
        private float phase;

        /// <summary>How far above its rest position the object is right now, in metres.</summary>
        public float Offset { get; private set; }

        private void Awake()
        {
            rest = transform.localPosition;
            phase = (transform.position.x * 0.61f + transform.position.z * 0.29f) % (Mathf.PI * 2f);
        }

        private void Update()
        {
            Offset = Mathf.Sin(Time.time * Mathf.PI * 2f / period + phase) * amplitude;
            transform.localPosition = rest + Vector3.up * Offset;
        }
    }
}
