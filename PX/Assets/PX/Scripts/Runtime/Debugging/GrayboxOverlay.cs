using UnityEngine;

namespace PX
{
    /// <summary>On-screen controls and live state for the graybox scene.</summary>
    public sealed class GrayboxOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private CharacterMotor motor;

        private GUIStyle style;

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            const string controls =
                "<b>PX graybox</b>\n" +
                "Move  A / D  or  arrows  or  left stick\n" +
                "Jump  Space  or  gamepad south (double jump in the air)\n" +
                "Dash  Left Shift / K  or  gamepad east / right trigger\n" +
                "Attack  J / left mouse  or  gamepad west (press again to chain)";

            string state = player != null && motor != null
                ? $"\n\nstate {player.State}   rail {motor.Distance:0.0} m   speed {motor.Velocity.x:0.0}   " +
                  (motor.IsGrounded ? "grounded" : "airborne")
                : string.Empty;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(12f, 12f, 520f, 140f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(24f, 20f, 500f, 130f), controls + state, style);
        }
    }
}
