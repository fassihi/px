namespace PX
{
    /// <summary>What the player asked for this frame, independent of the device it came from.</summary>
    public struct InputFrame
    {
        /// <summary>-1..1 along the rail. Positive is toward increasing rail distance.</summary>
        public float Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool DashPressed;
        public bool AttackPressed;
        /// <summary>Down is held: she crouches on the ground, or lets go of a ledge.</summary>
        public bool CrouchHeld;
        /// <summary>Guard went down this frame: the moment that can parry.</summary>
        public bool GuardPressed;
        /// <summary>Guard is held: she blocks.</summary>
        public bool GuardHeld;
        /// <summary>Interact went down: rest at a shrine, pull a lever, pick something up, or throw the tether.</summary>
        public bool InteractPressed;
        /// <summary>Flask went down: drink to heal.</summary>
        public bool FlaskPressed;
        /// <summary>
        /// Which way to face along the rail, whatever Move says: -1, +1, or 0 to face where Move goes. Lets an AI back away
        /// while still facing its opponent. The heroine ignores it.
        /// </summary>
        public float Face;
        /// <summary>Move at a run, not a walk. Only enemies use it.</summary>
        public bool Run;
        /// <summary>A special move an AI asks its body for: 0 none, 1 the slam, 2 the spinning charge. Only bosses use it.</summary>
        public int Special;
        /// <summary>How far along the rail the special move should carry it, in metres.</summary>
        public float SpecialRange;
    }

    /// <summary>Anything that can drive a character: a device, a test script, later an AI.</summary>
    public interface IInputSource
    {
        InputFrame Read();
    }
}
