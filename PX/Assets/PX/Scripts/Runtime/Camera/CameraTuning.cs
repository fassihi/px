using System;
using System.IO;
using UnityEngine;

namespace PX
{
    /// <summary>A rail camera's view and follow, as saved by <see cref="CameraTuner"/> and read back by the scene builders.</summary>
    [Serializable]
    public sealed class CameraTuning
    {
        public CameraView view;
        public float followSmoothTime;
        public float lookAheadSmoothTime;

        public string ToJson() => JsonUtility.ToJson(this, true);

        /// <summary>The tuning saved at <paramref name="path"/>, or null if there is none.</summary>
        public static CameraTuning Load(string path)
        {
            return File.Exists(path) ? JsonUtility.FromJson<CameraTuning>(File.ReadAllText(path)) : null;
        }
    }
}
