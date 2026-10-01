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
    }

    /// <summary>Anything that can drive a character: a device, a test script, later an AI.</summary>
    public interface IInputSource
    {
        InputFrame Read();
    }
}
