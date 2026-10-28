using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PX
{
    /// <summary>
    /// Graybox stand-in: tunes the rail camera's view live, with the scene view's mouse controls, and saves it to a file
    /// the scene builder reads on the next rebuild. Right drag orbits, scroll dollies, middle drag raises or lowers the
    /// point the camera looks at, Alt + scroll or [ ] changes the field of view, , . the look-ahead, - = how much the
    /// camera trails her. F5 saves, Backspace goes back to the view the scene started with, F1 hides the panel.
    /// </summary>
    public sealed class CameraTuner : MonoBehaviour
    {
        [SerializeField] private RailCamera railCamera;
        [Tooltip("Where F5 saves, relative to the Assets folder. The scene's builder reads it when it rebuilds the scene.")]
        [SerializeField] private string savePath = "PX/Config/LocomotionCamera.json";
        [SerializeField] private bool showPanel = true;

        [Tooltip("Degrees per pixel of right drag.")]
        [SerializeField] private float orbitSpeed = 0.25f;
        [Tooltip("Fraction of the distance dollied per unit of scroll. macOS reports small values.")]
        [SerializeField] private float dollyPerScroll = 0.05f;
        [Tooltip("Metres the focus moves per pixel of middle drag, per metre of distance.")]
        [SerializeField] private float panSpeed = 0.0015f;

        private CameraTuning started;
        private string message;
        private float messageTime;
        private GUIStyle style;

        /// <summary>The camera's view and follow right now.</summary>
        public CameraTuning Current => new CameraTuning
        {
            view = railCamera.DefaultView,
            followSmoothTime = railCamera.FollowSmoothTime,
            lookAheadSmoothTime = railCamera.LookAheadSmoothTime,
        };

        /// <summary>The full path F5 saves to.</summary>
        public string SaveFile => Path.Combine(Application.dataPath, savePath);

        private void Start()
        {
            if (railCamera != null)
                started = Current;
        }

        private void Update()
        {
            if (railCamera == null)
                return;

            CameraView view = railCamera.DefaultView;
            bool changed = false;

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool alt = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed && delta != Vector2.zero)
                {
                    view.yaw = Mathf.Repeat(view.yaw + delta.x * orbitSpeed + 180f, 360f) - 180f;
                    view.pitch = Mathf.Clamp(view.pitch - delta.y * orbitSpeed, -10f, 89f);
                    changed = true;
                }
                if (mouse.middleButton.isPressed && delta.y != 0f)
                {
                    view.focusHeight -= delta.y * panSpeed * view.distance;
                    changed = true;
                }

                float scroll = Mathf.Clamp(mouse.scroll.ReadValue().y * dollyPerScroll, -0.5f, 0.5f);
                if (scroll != 0f)
                {
                    if (alt)
                        view.fieldOfView = Mathf.Clamp(view.fieldOfView * (1f - scroll), 5f, 100f);
                    else
                        view.distance = Mathf.Max(0.5f, view.distance * (1f - scroll));
                    changed = true;
                }
            }

            if (keyboard != null)
            {
                float dt = Time.unscaledDeltaTime;
                if (keyboard.leftBracketKey.isPressed) { view.fieldOfView = Mathf.Max(5f, view.fieldOfView - 10f * dt); changed = true; }
                if (keyboard.rightBracketKey.isPressed) { view.fieldOfView = Mathf.Min(100f, view.fieldOfView + 10f * dt); changed = true; }
                if (keyboard.commaKey.isPressed) { view.lookAhead = Mathf.Max(0f, view.lookAhead - 1.5f * dt); changed = true; }
                if (keyboard.periodKey.isPressed) { view.lookAhead += 1.5f * dt; changed = true; }
                if (keyboard.minusKey.isPressed) railCamera.FollowSmoothTime -= 0.3f * dt;
                if (keyboard.equalsKey.isPressed) railCamera.FollowSmoothTime += 0.3f * dt;
                if (keyboard.f1Key.wasPressedThisFrame)
                    showPanel = !showPanel;
                if (keyboard.backspaceKey.wasPressedThisFrame && started != null)
                {
                    Apply(started);
                    Say("Back to the scene's view");
                    return;
                }
                if (keyboard.f5Key.wasPressedThisFrame)
                {
                    if (changed)
                        railCamera.SetDefaultViewNow(view);
                    Save();
                    return;
                }
            }

            if (changed)
                railCamera.SetDefaultViewNow(view);
        }

        /// <summary>Puts a saved tuning on the camera.</summary>
        public void Apply(CameraTuning tuning)
        {
            railCamera.SetDefaultViewNow(tuning.view);
            railCamera.FollowSmoothTime = tuning.followSmoothTime;
            railCamera.LookAheadSmoothTime = tuning.lookAheadSmoothTime;
        }

        /// <summary>Writes the camera's view and follow to <see cref="SaveFile"/>, or to <paramref name="path"/> if given.</summary>
        public void Save(string path = null)
        {
            path ??= SaveFile;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Current.ToJson());
                Debug.Log($"Camera saved to {path}:\n{Current.ToJson()}");
                Say($"Saved to {Path.GetFileName(path)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not save the camera to {path}: {e.Message}");
                Say("Save failed: see the console");
            }
        }

        private void Say(string text)
        {
            message = text;
            messageTime = Time.unscaledTime;
        }

        private void OnGUI()
        {
            if (railCamera == null)
                return;
            float scale = Mathf.Max(1f, Screen.height / 720f);
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, padding = new RectOffset(10, 10, 8, 8) };
            style.fontSize = Mathf.RoundToInt(13 * scale);

            if (!showPanel)
            {
                GUI.Label(new Rect(10 * scale, 10 * scale, 300 * scale, 30 * scale), "F1  camera tuner", style);
                return;
            }

            CameraView v = railCamera.DefaultView;
            // Where the camera sits relative to her feet, which is easier to picture than the angles.
            float pitch = v.pitch * Mathf.Deg2Rad;
            float height = v.focusHeight + Mathf.Sin(pitch) * v.distance;
            float across = Mathf.Cos(pitch) * v.distance;
            string text =
                "<b>Camera tuner</b>\n" +
                $"Distance  {v.distance:0.00} m   (to the point it looks at)\n" +
                $"Camera height  {height:0.00} m above her feet,  {across:0.00} m out from her\n" +
                $"Looks at  {v.focusHeight:0.00} m above her feet\n" +
                $"Pitch  {v.pitch:0.0}°   Yaw  {v.yaw:0.0}°   FOV  {v.fieldOfView:0.0}°\n" +
                $"Look-ahead  {v.lookAhead:0.00} m   Trail  {railCamera.FollowSmoothTime:0.00} s\n\n" +
                "Right drag  orbit      Scroll  distance      Middle drag  look higher / lower\n" +
                "Alt + scroll  or  [ ]  FOV      , .  look-ahead      - =  trail\n" +
                "<b>F5  save</b>      Backspace  back to start      F1  hide";
            if (message != null && Time.unscaledTime - messageTime < 3f)
                text += $"\n\n<b>{message}</b>";

            GUI.Box(new Rect(10 * scale, 10 * scale, 560 * scale, 210 * scale), text, style);
        }
    }
}
