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

                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * 0.002f, 0.8f, 10f);
            }

            if (!dragged)
                yaw += autoSpinDegreesPerSecond * Time.deltaTime;

            Vector3 focus = target.position;
            transform.position = focus + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);
            transform.LookAt(focus);
        }
    }
}
