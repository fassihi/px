using UnityEngine;

namespace PX
{
    /// <summary>
    /// A sentinel's shot: a slow glowing bolt that travels in rail space. It hurts her when it reaches her, and her
    /// guard decides what becomes of it: blocked it fizzles, parried it turns round and flies back at whatever fired
    /// it, which it can kill. It dies against level geometry and after a few seconds in the air.
    /// </summary>
    public sealed class Bolt : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float radius = 0.3f;
        [SerializeField, Min(0.1f)] private float lifetime = 4f;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Light glow;

        private Rail rail;
        private float distance;
        private float height;
        private Vector2 velocity;
        private float damage;
        private GameObject source;
        private float age;
        private bool reflected;
        private readonly Collider[] overlap = new Collider[8];

        public bool Reflected => reflected;

        public Vector2 Velocity => velocity;

        public float Distance => distance;

        public void Launch(Rail onRail, float atDistance, float atHeight, Vector2 speed, float hurt, GameObject from)
        {
            rail = onRail;
            distance = atDistance;
            height = atHeight;
            velocity = speed;
            damage = hurt;
            source = from;
            Place();
            if (trail != null)
                trail.Clear();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || rail == null)
                return;
            age += dt;
            if (age > lifetime || distance <= 0f || distance >= rail.Length)
            {
                Destroy(gameObject);
                return;
            }

            distance += velocity.x * dt;
            height += velocity.y * dt;
            Place();

            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = overlap[i];
                if (other.transform.IsChildOf(transform))
                    continue;
                if (source != null && other.transform.IsChildOf(source.transform) && !reflected)
                    continue;
                Health target = other.GetComponentInParent<Health>();
                if (target != null)
                {
                    bool enemy = target.GetComponent<EnemyController>() != null || target.GetComponent<Sentinel>() != null;
                    if (enemy && !reflected)
                        continue;
                    // Turned round, it is hers: it passes through her on its way back.
                    if (reflected && target.GetComponent<PlayerController>() != null)
                        continue;
                    if (target.IsDead)
                        continue;
                    Vector3 direction = rail.Evaluate(distance).Tangent * Mathf.Sign(velocity.x);
                    HitResult result = target.TakeHit(new Hit(damage, direction, source, 4f, transform.position));
                    if (result == HitResult.Ignored)
                        continue;
                    if (result == HitResult.Parried)
                    {
                        // Turned round, it belongs to her now, and flies back the way it came, a little faster.
                        reflected = true;
                        velocity = new Vector2(-velocity.x * 1.3f, 0f);
                        damage *= 3f;
                        age = 0f;
                        var player = target.GetComponent<PlayerController>();
                        source = player != null ? player.gameObject : source;
                        continue;
                    }
                    Destroy(gameObject);
                    return;
                }
                if (other is CharacterController)
                    continue;
                // Level geometry.
                Destroy(gameObject);
                return;
            }
        }

        private void Place()
        {
            RailPoint point = rail.Evaluate(distance);
            transform.position = new Vector3(point.Position.x, height, point.Position.z);
        }
    }
}
