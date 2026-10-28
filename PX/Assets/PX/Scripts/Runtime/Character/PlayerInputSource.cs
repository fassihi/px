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

        // The stick has to be pushed well down to crouch, so running with a sloppy stick does not.
        private const float CrouchStickThreshold = 0.6f;

        [Tooltip("Only moving, running, jumping and crouching: no dash, strikes, guard, interact or flask. Left Shift " +
                 "runs instead of dashing.")]
        [SerializeField] private bool locomotionOnly;

        private InputAction move;
        private InputAction run;
        private InputAction jump;
        private InputAction dash;
        private InputAction attack;
        private InputAction crouch;
        private InputAction crouchStick;
        private InputAction guard;
        private InputAction interact;
        private InputAction flask;

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

            // Run is held. It only matters when her move config has a walk speed; otherwise she always runs.
            run = new InputAction("Run", InputActionType.Button);
            run.AddBinding("<Keyboard>/leftShift");
            run.AddBinding("<Gamepad>/leftStickPress");
            run.AddBinding("<Gamepad>/rightTrigger");

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

            crouch = new InputAction("Crouch", InputActionType.Button);
            crouch.AddBinding("<Keyboard>/s");
            crouch.AddBinding("<Keyboard>/downArrow");
            crouch.AddBinding("<Gamepad>/dpad/down");

            crouchStick = new InputAction("Crouch Stick", InputActionType.Value);
            crouchStick.AddBinding("<Gamepad>/leftStick/y");

            guard = new InputAction("Guard", InputActionType.Button);
            guard.AddBinding("<Keyboard>/l");
            guard.AddBinding("<Mouse>/rightButton");
            guard.AddBinding("<Gamepad>/leftShoulder");

            interact = new InputAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Keyboard>/upArrow");
            interact.AddBinding("<Keyboard>/w");
            interact.AddBinding("<Gamepad>/buttonNorth");
            interact.AddBinding("<Gamepad>/rightShoulder");

            flask = new InputAction("Flask", InputActionType.Button);
            flask.AddBinding("<Keyboard>/q");
            flask.AddBinding("<Gamepad>/dpad/up");
            flask.AddBinding("<Gamepad>/leftTrigger");
        }

        private void OnEnable()
        {
            move.Enable();
            run.Enable();
            jump.Enable();
            dash.Enable();
            attack.Enable();
            crouch.Enable();
            crouchStick.Enable();
            guard.Enable();
            interact.Enable();
            flask.Enable();
        }

        private void OnDisable()
        {
            move.Disable();
            run.Disable();
            jump.Disable();
            dash.Disable();
            attack.Disable();
            crouch.Disable();
            crouchStick.Disable();
            guard.Disable();
            interact.Disable();
            flask.Disable();
        }

        private void OnDestroy()
        {
            move.Dispose();
            run.Dispose();
            jump.Dispose();
            dash.Dispose();
            attack.Dispose();
            crouch.Dispose();
            crouchStick.Dispose();
            guard.Dispose();
            interact.Dispose();
            flask.Dispose();
        }

        public InputFrame Read()
        {
            float axis = move.ReadValue<float>();
            var frame = new InputFrame
            {
                Move = Mathf.Abs(axis) < StickDeadzone ? 0f : Mathf.Clamp(axis, -1f, 1f),
                Run = run.IsPressed(),
                JumpPressed = jump.WasPressedThisFrame(),
                JumpHeld = jump.IsPressed(),
                CrouchHeld = crouch.IsPressed() || crouchStick.ReadValue<float>() < -CrouchStickThreshold,
            };
            if (locomotionOnly)
                return frame;

            frame.DashPressed = dash.WasPressedThisFrame();
            frame.AttackPressed = attack.WasPressedThisFrame();
            frame.GuardPressed = guard.WasPressedThisFrame();
            frame.GuardHeld = guard.IsPressed();
            frame.InteractPressed = interact.WasPressedThisFrame();
            frame.FlaskPressed = flask.WasPressedThisFrame();
            return frame;
        }
    }
}
