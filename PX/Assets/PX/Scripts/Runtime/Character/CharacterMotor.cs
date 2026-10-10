using UnityEngine;

namespace PX
{
    /// <summary>
    /// Moves a character along a <see cref="Rail"/>. Callers think in two axes
    /// (x = along the rail, y = up); the motor turns that into 3D motion with collisions
    /// and keeps the character on the rail's line.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private Rail rail;

        private readonly Collider[] overlapBuffer = new Collider[8];

        private CharacterController controller;
        private int facing = 1;
        private float standingHeight;

        public Rail Rail => rail;

        /// <summary>Distance along the rail, in metres.</summary>
        public float Distance { get; private set; }

        /// <summary>Velocity actually achieved in the last move, after collisions. x = along the rail, y = up.</summary>
        public Vector2 Velocity { get; private set; }

        public bool IsGrounded { get; private set; }

        public bool HitCeiling { get; private set; }

        /// <summary>True while the collider is lowered by <see cref="Crouch"/>.</summary>
        public bool IsCrouched { get; private set; }

        /// <summary>+1 faces toward increasing rail distance, -1 the other way.</summary>
        public int Facing
        {
            get => facing;
            set => facing = value < 0 ? -1 : 1;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            standingHeight = controller.height;
        }

        /// <summary>Lowers the collider to <paramref name="height"/>, keeping her feet where they are.</summary>
        public void Crouch(float height)
        {
            SetHeight(Mathf.Min(height, standingHeight));
            IsCrouched = true;
        }

        /// <summary>Raises the collider back to full height. Returns false, and stays crouched, under a low ceiling.</summary>
        public bool TryStand()
        {
            if (!IsCrouched)
                return true;

            float radius = controller.radius;
            Vector3 bottom = transform.position + Vector3.up * (radius + controller.skinWidth);
            Vector3 top = transform.position + Vector3.up * (standingHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (overlapBuffer[i] != controller && !overlapBuffer[i].transform.IsChildOf(transform))
                    return false;
            }

            SetHeight(standingHeight);
            IsCrouched = false;
            return true;
        }

        private void SetHeight(float height)
        {
            controller.height = height;
            controller.center = new Vector3(controller.center.x, height * 0.5f, controller.center.z);
        }

        private void Start()
        {
            if (rail != null)
            {
                Distance = rail.Project(transform.position);
                ApplyFacing();
            }
        }

        /// <summary>Puts the character on a rail at a distance and height, without sweeping for collisions.</summary>
        public void Teleport(Rail targetRail, float distance, float height)
        {
            rail = targetRail;
            Distance = Mathf.Clamp(distance, 0f, rail.Length);

            Vector3 position = rail.Evaluate(Distance).Position;
            position.y = height;

            // A CharacterController overrides transform changes unless it is disabled while moving it.
            if (controller == null)
                controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;

            Velocity = Vector2.zero;
            IsGrounded = false;
            ApplyFacing();
        }

        /// <summary>Moves by a rail-space velocity for one time step.</summary>
        public void Move(Vector2 velocity, float deltaTime)
        {
            if (rail == null || deltaTime <= 0f)
                return;

            // The rail has two ends. A step never carries her past either of them.
            float step = Mathf.Clamp(velocity.x * deltaTime, -Distance, rail.Length - Distance);

            RailPoint point = rail.Evaluate(Distance);

            // Sideways drift off the rail's line (from curves and from being pushed) is corrected every step.
            Vector3 toRail = point.Position - transform.position;
            toRail.y = 0f;
            Vector3 lateral = toRail - point.Tangent * Vector3.Dot(toRail, point.Tangent);

            Vector3 before = transform.position;
            CollisionFlags flags = controller.Move(
                point.Tangent * step + Vector3.up * (velocity.y * deltaTime) + lateral);
            Vector3 moved = transform.position - before;

            float along = Vector3.Dot(moved, point.Tangent);
            Distance = Mathf.Clamp(Distance + along, 0f, rail.Length);
            Velocity = new Vector2(along / deltaTime, moved.y / deltaTime);
            IsGrounded = (flags & CollisionFlags.Below) != 0;
            HitCeiling = (flags & CollisionFlags.Above) != 0;

            ApplyFacing();
        }

        private void ApplyFacing()
        {
            transform.rotation = Quaternion.LookRotation(rail.Evaluate(Distance).Tangent * facing, Vector3.up);
        }
    }
}
