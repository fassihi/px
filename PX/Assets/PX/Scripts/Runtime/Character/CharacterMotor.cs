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

        /// <summary>
        /// Puts the character at a rail distance and height on its own rail, keeping its facing, without sweeping for
        /// collisions. For moves the game animates by hand, like hanging from a ledge and climbing onto it.
        /// </summary>
        public void Place(float distance, float height)
        {
            Teleport(rail, distance, height);
        }

        /// <summary>The radius of the character's collider, in metres.</summary>
        public float Radius => controller != null ? controller.radius : GetComponent<CharacterController>().radius;

        /// <summary>
        /// Looks for a ledge in front of the character: a wall whose top is between <paramref name="minHeight"/> and
        /// <paramref name="maxHeight"/> above its feet, within <paramref name="reach"/> of its body, with room to stand
        /// on top. Returns the top's height and the rail distance of the wall's face.
        /// </summary>
        public bool FindLedge(float minHeight, float maxHeight, float reach, out float top, out float wallDistance)
        {
            top = 0f;
            wallDistance = 0f;
            if (rail == null)
                return false;

            RailPoint point = rail.Evaluate(Distance);
            Vector3 forward = point.Tangent * facing;
            Vector3 feet = transform.position;
            float radius = Radius;

            // Down from above the highest reach, just past the body: the first floor it meets is the ledge's top.
            Vector3 probe = feet + forward * (radius + reach * 0.5f) + Vector3.up * (maxHeight + 0.05f);
            if (!Physics.SphereCast(probe, 0.05f, Vector3.down, out RaycastHit down, maxHeight - minHeight + 0.05f, ~0, QueryTriggerInteraction.Ignore)
                || down.normal.y < 0.7f || IsIgnored(down.collider))
                return false;
            top = down.point.y;
            float above = top - feet.y;
            if (above < minHeight || above > maxHeight)
                return false;

            // The wall under it: straight ahead, a little below the top.
            Vector3 chest = new Vector3(feet.x, top - 0.15f, feet.z);
            if (!Physics.Raycast(chest, forward, out RaycastHit wall, radius + reach, ~0, QueryTriggerInteraction.Ignore)
                || Mathf.Abs(wall.normal.y) > 0.3f || IsIgnored(wall.collider))
                return false;
            wallDistance = Distance + Vector3.Dot(wall.point - feet, point.Tangent);

            // Room to stand on top, a body's width past the edge.
            float standing = standingHeight > 0f ? standingHeight : controller.height;
            Vector3 onTop = new Vector3(wall.point.x, top, wall.point.z) + forward * (radius + 0.1f);
            Vector3 bottom = onTop + Vector3.up * (radius + 0.05f);
            Vector3 head = onTop + Vector3.up * (standing - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, head, radius * 0.9f, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!IsIgnored(overlapBuffer[i]))
                    return false;
            }
            return true;
        }

        // Characters are not ledges: only level geometry counts.
        private bool IsIgnored(Collider other) => other == controller || other.transform.IsChildOf(transform) || other is CharacterController;

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
