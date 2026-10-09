using UnityEngine;
using UnityEngine.InputSystem;

namespace PX
{
    /// <summary>
    /// On-screen controls and live state for the graybox scene, plus debug zoom and time scale.
    /// Those are keys and scroll, not sliders: the left mouse button is the attack button.
    /// </summary>
    public sealed class GrayboxOverlay : MonoBehaviour
    {
        private static readonly float[] TimeScales = { 0f, 0.1f, 0.25f, 0.5f, 1f, 2f };
        private const int NormalTimeScale = 4;

        [SerializeField] private PlayerController player;
        [SerializeField] private CharacterMotor motor;
        [SerializeField] private RailCamera railCamera;

        private GUIStyle style;
        private int timeScaleIndex = NormalTimeScale;

        private void OnDestroy()
        {
            HitStop.BaseTimeScale = 1f;
            Time.timeScale = 1f;
        }

        private void Update()
        {
            HandleDebugKeys();

            // A hit's freeze owns the time scale while it lasts, then HitStop returns to the base.
            HitStop.BaseTimeScale = TimeScales[timeScaleIndex];
            if (!HitStop.IsActive)
                Time.timeScale = HitStop.BaseTimeScale;
        }

        private void HandleDebugKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftBracketKey.wasPressedThisFrame)
                    timeScaleIndex = Mathf.Max(0, timeScaleIndex - 1);
                if (keyboard.rightBracketKey.wasPressedThisFrame)
                    timeScaleIndex = Mathf.Min(TimeScales.Length - 1, timeScaleIndex + 1);
                if (keyboard.minusKey.wasPressedThisFrame && railCamera != null)
                    railCamera.DistanceScale *= 1.15f;
                if (keyboard.equalsKey.wasPressedThisFrame && railCamera != null)
                    railCamera.DistanceScale /= 1.15f;
                if (keyboard.backslashKey.wasPressedThisFrame)
                {
                    timeScaleIndex = NormalTimeScale;
                    if (railCamera != null)
                        railCamera.DistanceScale = 1f;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && railCamera != null)
            {
                // macOS reports small scroll values, so use the sign and a fixed step.
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    railCamera.DistanceScale *= scroll > 0f ? 1f / 1.08f : 1.08f;
            }
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            const string controls =
                "<b>PX graybox</b>\n" +
                "Move  A / D  or  arrows  or  left stick\n" +
                "Jump  Space  or  gamepad south (double jump in the air)\n" +
                "Dash  Left Shift / K  or  gamepad east / right trigger\n" +
                "Attack  J / left mouse  or  gamepad west (press again to chain)\n" +
                "Zoom  scroll  or  - / =        Time scale  [ / ]        Reset  \\";

            string state = player != null && motor != null
                ? $"\n\nstate {player.State}   rail {motor.Distance:0.0} m   speed {motor.Velocity.x:0.0}   " +
                  (motor.IsGrounded ? "grounded" : "airborne")
                : string.Empty;

            string debug = $"\nzoom {(railCamera != null ? railCamera.DistanceScale : 1f):0.00}x   time {TimeScales[timeScaleIndex]:0.##}x";

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(12f, 12f, 520f, 190f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(24f, 20f, 500f, 180f), controls + state + debug, style);
        }
    }
}
