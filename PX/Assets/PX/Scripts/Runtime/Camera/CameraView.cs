using System;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// A way of looking at the heroine, described relative to the rail so it works on any stretch of it.
    /// </summary>
    [Serializable]
    public struct CameraView
    {
        [Tooltip("Degrees around the heroine. 0 is a pure side view, 90 looks along the rail from behind her, -90 from ahead.")]
        [Range(-180f, 180f)] public float yaw;

        [Tooltip("Degrees above the horizon.")]
        [Range(-10f, 89f)] public float pitch;

        [Min(0.5f)] public float distance;

        [Range(5f, 100f)] public float fieldOfView;

        [Tooltip("Height of the point the camera looks at, above her feet.")]
        public float focusHeight;

        [Tooltip("How far the camera leads in the direction she faces.")]
        [Min(0f)] public float lookAhead;

        public static CameraView Side => new CameraView
        {
            yaw = 0f,
            pitch = 8f,
            distance = 14f,
            fieldOfView = 35f,
            focusHeight = 1.6f,
            lookAhead = 2f,
        };

        public static CameraView Lerp(CameraView a, CameraView b, float t)
        {
            return new CameraView
            {
                yaw = Mathf.LerpAngle(a.yaw, b.yaw, t),
                pitch = Mathf.Lerp(a.pitch, b.pitch, t),
                distance = Mathf.Lerp(a.distance, b.distance, t),
                fieldOfView = Mathf.Lerp(a.fieldOfView, b.fieldOfView, t),
                focusHeight = Mathf.Lerp(a.focusHeight, b.focusHeight, t),
                lookAhead = Mathf.Lerp(a.lookAhead, b.lookAhead, t),
            };
        }
    }

    /// <summary>A stretch of rail that is seen through its own <see cref="CameraView"/>.</summary>
    [Serializable]
    public sealed class CameraZone
    {
        public string name;

        [Tooltip("Rail distance where the zone starts, in metres.")]
        public float from;

        [Tooltip("Rail distance where the zone ends, in metres.")]
        public float to;

        public CameraView view = CameraView.Side;

        public bool Contains(float distance) => distance >= from && distance <= to;
    }
}
