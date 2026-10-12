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
        /// <summary>
        /// Which way to face along the rail, whatever Move says: -1, +1, or 0 to face where Move goes. Lets an AI back away
        /// while still facing its opponent. The heroine ignores it.
        /// </summary>
        public float Face;
        /// <summary>Move at a run, not a walk. Only enemies use it.</summary>
        public bool Run;
    }

    /// <summary>Anything that can drive a character: a device, a test script, later an AI.</summary>
    public interface IInputSource
    {
        InputFrame Read();
    }
}
