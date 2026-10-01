using UnityEngine;
using UnityEngine.InputSystem;

namespace PX
{
    /// <summary>
    /// Keyboard and gamepad input for the heroine.
    /// Bindings are defined in code for now; move them to an input actions asset when rebinding is needed.
    /// </summary>
    public sealed class PlayerInputSource : MonoBehaviour, IInputSource
    {
        private const float StickDeadzone = 0.25f;

        private InputAction move;
        private InputAction jump;
        private InputAction dash;
        private InputAction attack;

        private void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            move.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            move.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/dpad/left")
                .With("Positive", "<Gamepad>/dpad/right");
            move.AddBinding("<Gamepad>/leftStick/x");

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            dash = new InputAction("Dash", InputActionType.Button);
            dash.AddBinding("<Keyboard>/leftShift");
            dash.AddBinding("<Keyboard>/k");
            dash.AddBinding("<Gamepad>/buttonEast");
            dash.AddBinding("<Gamepad>/rightTrigger");

            attack = new InputAction("Attack", InputActionType.Button);
            attack.AddBinding("<Keyboard>/j");
            attack.AddBinding("<Mouse>/leftButton");
            attack.AddBinding("<Gamepad>/buttonWest");
        }

        private void OnEnable()
        {
            move.Enable();
            jump.Enable();
            dash.Enable();
            attack.Enable();
        }

        private void OnDisable()
        {
            move.Disable();
            jump.Disable();
            dash.Disable();
            attack.Disable();
        }

        private void OnDestroy()
        {
            move.Dispose();
            jump.Dispose();
            dash.Dispose();
            attack.Dispose();
        }

        public InputFrame Read()
        {
            float axis = move.ReadValue<float>();
            return new InputFrame
            {
                Move = Mathf.Abs(axis) < StickDeadzone ? 0f : Mathf.Clamp(axis, -1f, 1f),
                JumpPressed = jump.WasPressedThisFrame(),
                JumpHeld = jump.IsPressed(),
                DashPressed = dash.WasPressedThisFrame(),
                AttackPressed = attack.WasPressedThisFrame(),
            };
        }
    }
}
