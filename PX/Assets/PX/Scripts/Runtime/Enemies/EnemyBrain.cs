using UnityEngine;

namespace PX
{
    /// <summary>What a guard knows about her.</summary>
    public enum Awareness
    {
        /// <summary>At its post or on its patrol. It has not seen her.</summary>
        Unaware,
        /// <summary>It saw or heard something and is looking into it.</summary>
        Suspicious,
        /// <summary>It knows she is here and fights.</summary>
        Alert,
    }

    /// <summary>
    /// A melee AI, written as an input source so the enemy is driven the same way the player is. With stealth on, it
    /// patrols between two posts and sees only in front of it: a glimpse makes it suspicious and it comes to look,
    /// a good look makes it sure, a blow landing nearby brings it running. Crouched behind cover she is hidden from it.
    /// Once alert it follows her, climbing what is in its way; in reach it swings, and follows the first swing with a
    /// second only some of the time, so she cannot learn one rhythm. Too close, or sometimes after attacking, it backs
    /// off, still facing her, to set its distance again. A boss also charges from far off and slams from the middle
    /// distance. Without stealth it notices her at its aggro range from any side, as the graybox enemies do.
    /// </summary>
    public sealed class EnemyBrain : MonoBehaviour, IInputSource
    {
        // Blows and landings this close are heard even by a guard whose hearing range is short.
        private const float TouchDistance = 1.3f;

        [SerializeField] private EnemyController body;
        [SerializeField] private CharacterMotor target;
        [Tooltip("On: it sees only in front of it and patrols. Off: it notices her at its aggro range from any side.")]
        [SerializeField] private bool stealth;
        [Tooltip("Rail distances it walks between while unaware. The same number twice, or both zero: it stands at its post.")]
        [SerializeField] private float patrolFrom;
        [SerializeField] private float patrolTo;

        private bool engaged;
        private bool chain;
        private float retreatTimer;
        private EnemyState lastState;
        private int lastCombo;
        private Awareness awareness;
        private float seenTime;
        private float investigateTimer;
        private float investigateTarget;
        private bool investigateArrived;
        private float lastKnown;
        private int patrolHeading = 1;
        private float waitTimer;
        private int restFacing;
        private bool restFacingKnown;
        private int special;
        private float specialRange;
        private float decisionTimer;

        public CharacterMotor Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>True once it has noticed her and is following her.</summary>
        public bool Engaged => engaged;

        public Awareness Awareness => awareness;

        public bool Stealth
        {
            get => stealth;
            set => stealth = value;
        }

        /// <summary>Rail distance of where it last saw or heard her.</summary>
        public float LastKnown => lastKnown;

        private void OnEnable()
        {
            Noise.Emitted += OnNoise;
        }

        private void OnDisable()
        {
            Noise.Emitted -= OnNoise;
        }

        /// <summary>Roused by the level: it knows she is here, wherever she is. For ambushes and bosses.</summary>
        public void Alarm()
        {
            if (target != null)
                lastKnown = target.Distance;
            Become(Awareness.Alert);
            if (body != null)
                body.Mood = Mood.Alert;
        }

        /// <summary>Forgets her: back to unaware, as when she rests or dies.</summary>
        public void Forget()
        {
            engaged = false;
            awareness = Awareness.Unaware;
            seenTime = 0f;
            investigateTimer = 0f;
            waitTimer = 0f;
            special = 0;
            if (body != null)
                body.Mood = Mood.Calm;
        }

        public InputFrame Read()
        {
            var frame = new InputFrame();
            if (body == null || target == null || body.Config == null)
                return frame;
            EnemyConfig config = body.Config;
            float dt = Time.deltaTime;

            if (!restFacingKnown)
            {
                restFacing = body.Motor.Facing;
                restFacingKnown = true;
            }

            Remember();

            Health targetHealth = target.GetComponent<Health>();
            if (targetHealth != null && targetHealth.IsDead)
            {
                Forget();
                return frame;
            }

            float offset = target.Distance - body.Motor.Distance;
            float distance = Mathf.Abs(offset);
            float height = Mathf.Abs(target.transform.position.y - body.transform.position.y);

            if (stealth)
                Sense(config, offset, distance, height, dt);
            else
            {
                if (!engaged && distance <= config.aggroRange && height <= config.aggroHeight)
                    engaged = true;
                else if (engaged && distance > config.chaseRange)
                    engaged = false;
                awareness = engaged ? Awareness.Alert : Awareness.Unaware;
            }
            body.Mood = awareness == Awareness.Alert ? Mood.Alert : awareness == Awareness.Suspicious ? Mood.Suspicious : Mood.Calm;

            if (awareness == Awareness.Unaware)
                return Patrol(config, frame, dt);
            if (awareness == Awareness.Suspicious)
                return Investigate(config, frame, dt);

            float toward = Mathf.Sign(offset);
            frame.Face = toward;

            if (body.State == EnemyState.Attack)
            {
                frame.AttackPressed = chain;
                return frame;
            }
            if (body.State != EnemyState.Idle && body.State != EnemyState.Walk && body.State != EnemyState.Run && body.State != EnemyState.WalkBack)
                return frame;

            retreatTimer -= dt;
            if (config.isBoss && Boss(config, frame, toward, distance, height, dt))
                return frame;

            if (retreatTimer > 0f || distance < config.tooClose)
                frame.Move = -toward;
            else if (distance > config.attackRange || height > 1f)
            {
                frame.Move = toward;
                frame.Run = distance > config.runDistance;
            }
            else
                frame.AttackPressed = true;
            return frame;
        }

        // ---------------------------------------------------------------- senses

        /// <summary>
        /// Sight and memory. It sees her in front of it, within range and height, unless she is crouched behind cover;
        /// right next to it she is noticed whatever she does. Seeing her long enough makes it alert; a glimpse makes it
        /// suspicious of where she was. Out of sight long enough, an alert guard goes back to suspicious, then calm.
        /// </summary>
        private void Sense(EnemyConfig config, float offset, float distance, float height, float dt)
        {
            // Behind it she is never seen, however close: that is what a takedown is. In front, right up close, she is
            // noticed at once whatever she does.
            bool inFront = Mathf.Sign(offset) == body.Motor.Facing;
            bool inRange = awareness == Awareness.Alert ? distance <= config.chaseRange : distance <= config.sightRange && height <= config.sightHeight;
            bool sees = inFront && inRange && !Hidden(distance);

            if (sees)
            {
                lastKnown = target.Distance;
                float notice = awareness == Awareness.Suspicious ? config.noticeTime * 0.5f : config.noticeTime;
                seenTime += dt;
                if (distance < TouchDistance)
                    seenTime = notice;
                if (seenTime >= notice)
                    Become(Awareness.Alert);
                else if (awareness == Awareness.Unaware)
                    Suspect(target.Distance);
                return;
            }

            seenTime = Mathf.Max(0f, seenTime - dt * 2f);
            if (awareness == Awareness.Alert && !inRange)
                Suspect(lastKnown);
        }

        /// <summary>
        /// Crouched behind something, she is hidden: a line from its eyes to her chest meets level geometry. Standing,
        /// or crouched in the open, she is not.
        /// </summary>
        private bool Hidden(float distance)
        {
            var player = target.GetComponent<PlayerController>();
            bool crouched = player != null && (player.State == PlayerState.Crouch || target.IsCrouched);
            if (!crouched || distance < TouchDistance)
                return false;
            Vector3 eyes = body.transform.position + Vector3.up * (body.Motor.Radius > 0f ? 1.6f : 1.6f);
            Vector3 chest = target.transform.position + Vector3.up * 0.55f;
            Vector3 line = chest - eyes;
            int count = Physics.RaycastNonAlloc(eyes, line.normalized, hits, line.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i].collider;
                if (hit is CharacterController || hit.transform.IsChildOf(body.transform) || hit.transform.IsChildOf(target.transform))
                    continue;
                if (hit.GetComponentInParent<EnemyController>() != null || hit.GetComponentInParent<Sentinel>() != null)
                    continue;
                return true;
            }
            return false;
        }

        private readonly RaycastHit[] hits = new RaycastHit[8];

        private void OnNoise(Vector3 position, float radius, GameObject source)
        {
            if (!stealth || body == null || source == body.gameObject || awareness == Awareness.Alert)
                return;
            if (body.Health.IsDead)
                return;
            float reach = Mathf.Min(radius, body.Config.hearingRange);
            float distance = Vector3.Distance(position, body.transform.position);
            if (distance > reach && distance > TouchDistance)
                return;
            float where = body.Motor.Rail != null ? body.Motor.Rail.Project(position) : body.Motor.Distance;
            // A second noise while it is already looking makes it sure.
            if (awareness == Awareness.Suspicious && distance <= reach * 0.6f && target != null)
            {
                lastKnown = target.Distance;
                Become(Awareness.Alert);
                return;
            }
            Suspect(where);
        }

        private void Suspect(float where)
        {
            investigateTarget = where;
            investigateTimer = body.Config.investigateTime;
            investigateArrived = false;
            if (awareness != Awareness.Suspicious)
                Become(Awareness.Suspicious);
        }

        private void Become(Awareness next)
        {
            awareness = next;
            engaged = next == Awareness.Alert;
            if (next != Awareness.Alert)
                seenTime = 0f;
            if (next == Awareness.Unaware)
                waitTimer = body.Config.patrolWait * 0.5f;
        }

        // --------------------------------------------------------- unaware, suspicious

        /// <summary>Walks between its two posts, waiting at each; or stands facing the way it was placed.</summary>
        private InputFrame Patrol(EnemyConfig config, InputFrame frame, float dt)
        {
            bool patrols = Mathf.Abs(patrolTo - patrolFrom) > 0.5f;
            if (!patrols)
            {
                if (body.State == EnemyState.Idle)
                    frame.Face = restFacing;
                return frame;
            }

            waitTimer -= dt;
            if (waitTimer > 0f)
                return frame;
            float goal = patrolHeading > 0 ? Mathf.Max(patrolFrom, patrolTo) : Mathf.Min(patrolFrom, patrolTo);
            float offset = goal - body.Motor.Distance;
            if (Mathf.Abs(offset) < 0.3f)
            {
                patrolHeading = -patrolHeading;
                waitTimer = config.patrolWait;
                return frame;
            }
            frame.Move = Mathf.Sign(offset);
            return frame;
        }

        /// <summary>Walks to where it saw or heard something, looks about for a while, then goes back to its rounds.</summary>
        private InputFrame Investigate(EnemyConfig config, InputFrame frame, float dt)
        {
            float offset = investigateTarget - body.Motor.Distance;
            if (!investigateArrived && Mathf.Abs(offset) > 1.2f)
            {
                frame.Move = Mathf.Sign(offset);
                return frame;
            }
            investigateArrived = true;
            investigateTimer -= dt;
            if (investigateTimer <= 0f)
                Become(Awareness.Unaware);
            return frame;
        }

        // ------------------------------------------------------------------ boss

        /// <summary>
        /// The boss's extra choices, rolled every half second: from far off it charges, spinning its blade, from the
        /// middle distance it leaps into a slam, otherwise it fights like the others.
        /// </summary>
        private bool Boss(EnemyConfig config, InputFrame frame, float toward, float distance, float height, float dt)
        {
            if (special != 0)
            {
                // Keep running at her until the charge goes off, then let the body take it.
                frame.Special = special;
                frame.SpecialRange = specialRange;
                frame.Move = toward;
                frame.Run = special == 2;
                if (body.State == EnemyState.Attack)
                    special = 0;
                if (special == 2 && distance > 3.5f)
                    frame.Special = 0;
                return true;
            }
            decisionTimer -= dt;
            if (decisionTimer > 0f || height > 1.5f)
                return false;
            decisionTimer = 0.5f;
            if (distance > config.chargeDistance && Random.value < config.chargeChance)
            {
                special = 2;
                specialRange = distance;
                return true;
            }
            if (distance > config.attackRange + 0.5f && distance <= config.chargeDistance + 1f && Random.value < config.slamChance)
            {
                special = 1;
                specialRange = distance - 0.9f;
                frame.Special = 1;
                frame.SpecialRange = specialRange;
                return true;
            }
            return false;
        }

        /// <summary>Rolls the dice as each combo starts and ends: a second swing or not, backing off after it or not.</summary>
        private void Remember()
        {
            EnemyState state = body.State;
            bool startedCombo = state == EnemyState.Attack && body.ComboIndex == 0 && (lastState != EnemyState.Attack || lastCombo != 0);
            if (startedCombo)
                chain = Random.value < body.Config.comboChance;
            if (lastState == EnemyState.Attack && state != EnemyState.Attack && state != EnemyState.Dead)
            {
                retreatTimer = Random.value < body.Config.retreatChance ? body.Config.retreatTime : 0f;
                special = 0;
            }
            lastState = state;
            lastCombo = body.ComboIndex;
        }
    }
}
