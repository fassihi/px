using System.Collections.Generic;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// Follows a character along its rail. The view is defined relative to the rail's direction,
    /// so a side view stays a side view around corners, and any stretch of rail can be given
    /// a different angle through a <see cref="CameraZone"/> without touching gameplay.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class RailCamera : MonoBehaviour
    {
        [SerializeField] private CharacterMotor target;
        [SerializeField] private CameraView defaultView = CameraView.Side;
        [SerializeField] private List<CameraZone> zones = new List<CameraZone>();

        [Tooltip("Seconds for the view to mostly settle after entering or leaving a zone.")]
        [SerializeField, Min(0.01f)] private float viewBlendTime = 0.6f;
        [SerializeField, Min(0f)] private float followSmoothTime = 0.12f;
        [SerializeField, Min(0.01f)] private float lookAheadSmoothTime = 0.35f;

        private Camera cam;
        private CameraView current;
        private Vector3 focus;
        private Vector3 focusVelocity;
        private float lead;
        private float leadVelocity;
        private bool initialized;

        public CharacterMotor Target
        {
            get => target;
            set => target = value;
        }

        public CameraView DefaultView
        {
            get => defaultView;
            set => defaultView = value;
        }

        public List<CameraZone> Zones => zones;

        /// <summary>The view currently in effect, mid-blend included.</summary>
        public CameraView CurrentView => current;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null || target.Rail == null)
                return;

            if (!initialized)
            {
                Snap();
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            current = CameraView.Lerp(current, ViewAt(target.Distance), 1f - Mathf.Exp(-3f * dt / viewBlendTime));
            lead = Mathf.SmoothDamp(lead, target.Facing * current.lookAhead, ref leadVelocity, lookAheadSmoothTime);
            focus = Vector3.SmoothDamp(focus, FocusTarget(), ref focusVelocity, followSmoothTime);
            Apply();
        }

        /// <summary>Jumps straight to where the camera wants to be, with no blending. Use after teleports and cuts.</summary>
        public void Snap()
        {
            if (cam == null)
                cam = GetComponent<Camera>();
            if (target == null || target.Rail == null)
                return;

            current = ViewAt(target.Distance);
            lead = target.Facing * current.lookAhead;
            leadVelocity = 0f;
            focus = FocusTarget();
            focusVelocity = Vector3.zero;
            initialized = true;
            Apply();
        }

        /// <summary>The view that applies at a rail distance. Later zones in the list win where zones overlap.</summary>
        public CameraView ViewAt(float distance)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                if (zones[i].Contains(distance))
                    return zones[i].view;
            }

            return defaultView;
        }

        private Vector3 FocusTarget()
        {
            RailPoint point = target.Rail.Evaluate(target.Distance);
            return target.transform.position + Vector3.up * current.focusHeight + point.Tangent * lead;
        }

        private void Apply()
        {
            RailPoint point = target.Rail.Evaluate(target.Distance);

            // Start on the rail's camera side, swing around the heroine by yaw, then lift by pitch.
            Vector3 horizontal = Quaternion.AngleAxis(current.yaw, Vector3.up) * point.Side;
            float pitch = current.pitch * Mathf.Deg2Rad;
            Vector3 offset = horizontal * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);

            transform.position = focus + offset * current.distance;
            transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
            cam.fieldOfView = current.fieldOfView;
        }
    }
}
