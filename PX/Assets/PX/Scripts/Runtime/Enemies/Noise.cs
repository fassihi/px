using System;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// Sound in the world, as far as guards are concerned: a blow landing, a hard landing, a body falling. Anything
    /// that makes a noise raises it here with where and how far it carries; brains listen.
    /// </summary>
    public static class Noise
    {
        /// <summary>Where the noise was made, how far it carries in metres, and who made it.</summary>
        public static event Action<Vector3, float, GameObject> Emitted;

        public static void Emit(Vector3 position, float radius, GameObject source)
        {
            if (radius <= 0f)
                return;
            Emitted?.Invoke(position, radius, source);
        }
    }
}
