using UnityEngine;
using UnityEngine.InputSystem;

namespace PX
{
    /// <summary>
    /// Graybox stand-in for the Rana viewer scene: drag with the left mouse button to orbit, scroll or use the
    /// panel on the right to zoom, and set the time scale there to slow the animation down.
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
        private bool autoSpin = true;
        private float timeScale = 1f;

        private const float MinDistance = 0.8f;
        private const float MaxDistance = 10f;

        private void OnDestroy() => Time.timeScale = 1f;

        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                // While a slider or button on the panels is held, the drag belongs to it, not to the camera.
                if (mouse.leftButton.isPressed && GUIUtility.hotControl == 0)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * 0.3f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.3f, -60f, 80f);
                    dragged = true;
                }

                // Proportional to the distance, so close-ups stay controllable; capped so a Windows-sized notch can't jump.
                float zoom = Mathf.Clamp(mouse.scroll.ReadValue().y * zoomPerScroll, -0.5f, 0.5f);
                distance = Mathf.Clamp(distance * (1f - zoom), MinDistance, MaxDistance);
            }

            // Unscaled, so the camera keeps turning when the animation is slowed down or stopped.
            if (autoSpin && !dragged)
                yaw += autoSpinDegreesPerSecond * Time.unscaledDeltaTime;

            Vector3 focus = target.position;
            transform.position = focus + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);
            transform.LookAt(focus);
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            const float width = 220f;
            GUILayout.BeginArea(new Rect(Screen.width / scale - width - 12f, 12f, width, 165f), GUI.skin.box);

            GUILayout.Label($"Zoom: {distance:0.0} m away");
            // The slider runs far to near, so dragging right zooms in.
            distance = MaxDistance + MinDistance - GUILayout.HorizontalSlider(MaxDistance + MinDistance - distance, MinDistance, MaxDistance);

            GUILayout.Space(8f);
            GUILayout.Label($"Time scale: {timeScale:0.00}x");
            timeScale = GUILayout.HorizontalSlider(timeScale, 0f, 2f);
            GUILayout.BeginHorizontal();
            foreach (float preset in new[] { 0f, 0.1f, 0.25f, 0.5f, 1f })
            {
                if (GUILayout.Button(preset.ToString("0.##")))
                    timeScale = preset;
            }

            GUILayout.EndHorizontal();
            Time.timeScale = timeScale;

            GUILayout.Space(8f);
            autoSpin = GUILayout.Toggle(autoSpin, "Auto-spin");
            GUILayout.EndArea();
        }
    }
}
