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
        private float distanceScale = 1f;

        // A shot: a view that overrides the zones for a while, with its own focus. Finishers and reveals use it.
        private bool shotActive;
        private CameraView shotView;
        private Transform shotFocusA;
        private Transform shotFocusB;
        private float shotTime;
        private float shotDuration;
        private float shotBlendIn;
        private float shotBlendOut;
        private float sinceShot = float.MaxValue;

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

        /// <summary>Multiplies every view's distance: below 1 zooms in, above 1 zooms out. A debug aid, not a camera setting.</summary>
        public float DistanceScale
        {
            get => distanceScale;
            set => distanceScale = Mathf.Clamp(value, 0.2f, 4f);
        }

        /// <summary>The view currently in effect, mid-blend included.</summary>
        public CameraView CurrentView => current;

        /// <summary>True while a shot overrides the zones.</summary>
        public bool IsShotActive => shotActive;

        /// <summary>
        /// Overrides the zones with a view for a while: a cut for a finisher, a sweep for a reveal. The focus is the
        /// midpoint of the two transforms (or the one given), at the view's focus height; without any, it stays on her.
        /// </summary>
        public void PlayShot(CameraView view, float duration, float blendIn = 0.15f, float blendOut = 0.7f, Transform focusA = null, Transform focusB = null)
        {
            shotActive = true;
            shotView = view;
            shotDuration = duration;
            shotBlendIn = Mathf.Max(0.01f, blendIn);
            shotBlendOut = Mathf.Max(0.01f, blendOut);
            shotFocusA = focusA;
            shotFocusB = focusB;
            shotTime = 0f;
        }

        /// <summary>Ends the shot early, blending back to the zones.</summary>
        public void EndShot()
        {
            if (!shotActive)
                return;
            shotActive = false;
            sinceShot = 0f;
        }

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
            float realDt = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            if (shotActive)
            {
                shotTime += realDt;
                if (shotTime >= shotDuration)
                    EndShot();
            }
            else
                sinceShot += realDt;
            if (dt <= 0f && !shotActive)
            {
                // Hit-stop freezes the follow, but the shake that comes with it still shows.
                if (CameraShake.IsActive)
                    Apply();
                return;
            }

            // A shot blends in fast and out slowly, on real time, so slow motion does not stretch the cut.
            float blendTime = shotActive ? shotBlendIn : sinceShot < shotBlendOut * 2f ? shotBlendOut : viewBlendTime;
            float blendDt = shotActive || sinceShot < shotBlendOut * 2f ? realDt : dt;
            CameraView wanted = shotActive ? shotView : ViewAt(target.Distance, target.transform.position.y);
            current = CameraView.Lerp(current, wanted, 1f - Mathf.Exp(-3f * blendDt / blendTime));
            lead = Mathf.SmoothDamp(lead, shotActive ? 0f : target.Facing * current.lookAhead, ref leadVelocity, lookAheadSmoothTime, float.MaxValue, blendDt);
            focus = Vector3.SmoothDamp(focus, FocusTarget(), ref focusVelocity, shotActive ? followSmoothTime * 0.5f : followSmoothTime, float.MaxValue, blendDt);
            Apply();
        }

        /// <summary>Jumps straight to where the camera wants to be, with no blending. Use after teleports and cuts.</summary>
        public void Snap()
        {
            if (cam == null)
                cam = GetComponent<Camera>();
            if (target == null || target.Rail == null)
                return;

            shotActive = false;
            current = ViewAt(target.Distance, target.transform.position.y);
            lead = target.Facing * current.lookAhead;
            leadVelocity = 0f;
            focus = FocusTarget();
            focusVelocity = Vector3.zero;
            initialized = true;
            Apply();
        }

        /// <summary>The view that applies at a rail distance, at her current height. Later zones in the list win where zones overlap.</summary>
        public CameraView ViewAt(float distance) => ViewAt(distance, target != null ? target.transform.position.y : 0f);

        /// <summary>The view that applies at a rail distance and height. Later zones in the list win where zones overlap.</summary>
        public CameraView ViewAt(float distance, float height)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                if (zones[i].Contains(distance, height))
                    return zones[i].view;
            }

            return defaultView;
        }

        private Vector3 FocusTarget()
        {
            RailPoint point = target.Rail.Evaluate(target.Distance);
            if (shotActive && (shotFocusA != null || shotFocusB != null))
            {
                Vector3 a = shotFocusA != null ? shotFocusA.position : shotFocusB.position;
                Vector3 b = shotFocusB != null ? shotFocusB.position : a;
                Vector3 mid = (a + b) * 0.5f;
                return new Vector3(mid.x, Mathf.Min(a.y, b.y), mid.z) + Vector3.up * current.focusHeight;
            }
            return target.transform.position + Vector3.up * current.focusHeight + point.Tangent * lead;
        }

        private void Apply()
        {
            RailPoint point = target.Rail.Evaluate(target.Distance);

            // Start on the rail's camera side, swing around the heroine by yaw, then lift by pitch.
            Vector3 horizontal = Quaternion.AngleAxis(current.yaw, Vector3.up) * point.Side;
            float pitch = current.pitch * Mathf.Deg2Rad;
            Vector3 offset = horizontal * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);

            transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
            Vector2 shake = CameraShake.Sample();
            transform.position = focus + offset * (current.distance * distanceScale)
                + transform.right * shake.x + transform.up * shake.y;
            cam.fieldOfView = current.fieldOfView;
        }
    }
}
