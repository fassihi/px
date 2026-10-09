using UnityEngine;
using UnityEngine.InputSystem;

namespace PX
{
    /// <summary>
    /// Graybox stand-in for the Rana viewer scene: drag with the left mouse button to orbit, scroll to zoom.
    /// Turns slowly by itself until the first drag.
    /// </summary>
    public sealed class RanaOrbitView : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 3.5f;
        [SerializeField] private float autoSpinDegreesPerSecond = 20f;
        [SerializeField, Tooltip("Fraction of the distance zoomed per unit of scroll. macOS reports small values.")]
        private float zoomPerScroll = 0.08f;

        private float yaw = 180f;
        private float pitch = 8f;
        private bool dragged;

        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * 0.3f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.3f, -60f, 80f);
                    dragged = true;
                }

                // Proportional to the distance, so close-ups stay controllable; capped so a Windows-sized notch can't jump.
                float zoom = Mathf.Clamp(mouse.scroll.ReadValue().y * zoomPerScroll, -0.5f, 0.5f);
                distance = Mathf.Clamp(distance * (1f - zoom), 0.8f, 10f);
            }

            if (!dragged)
                yaw += autoSpinDegreesPerSecond * Time.deltaTime;

            Vector3 focus = target.position;
            transform.position = focus + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);
            transform.LookAt(focus);
        }
    }
}
