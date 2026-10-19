using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PX
{
    /// <summary>
    /// The level's conductor, the soulslike loop around the fights: shrines that light as she passes and that she rests
    /// at; the light the enemies leave, dropped where she dies and recovered once; what Interact does where she stands;
    /// pickups (the tether, more flasks, caches of light); the levers and doors; the encounters and the boss; and the
    /// end of the level. The HUD reads from here.
    /// </summary>
    public sealed class LevelFlow : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerRespawn respawn;
        [SerializeField] private CameraDirector director;
        [SerializeField] private KillPlane killPlane;
        [SerializeField] private Shrine[] shrines = Array.Empty<Shrine>();
        [SerializeField] private EnemyController[] enemies = Array.Empty<EnemyController>();
        [SerializeField] private Sentinel[] sentinels = Array.Empty<Sentinel>();
        [SerializeField] private Pickup[] pickups = Array.Empty<Pickup>();
        [SerializeField] private Lever[] levers = Array.Empty<Lever>();
        [SerializeField] private EncounterGroup[] encounters = Array.Empty<EncounterGroup>();
        [SerializeField] private EnemyController boss;
        [SerializeField] private EncounterGroup bossArena;
        [SerializeField] private string bossName = "The Captain";
        [SerializeField] private Material droppedLightMaterial;
        [SerializeField] private Color lightColor = new Color(1f, 0.6f, 0.2f);
        [Tooltip("The view of the lamps when she picks up the tether.")]
        [SerializeField] private CameraView tetherRevealView = new CameraView { yaw = 20f, pitch = 28f, distance = 16f, fieldOfView = 40f, focusHeight = 4f, lookAhead = 0f };
        [SerializeField, Min(0f)] private float reach = 1.6f;

        private CharacterMotor motor;
        private float toastTime = float.MaxValue;
        private bool complete;
        private float completeTime;

        /// <summary>The light she carries: what the enemies leave behind, spent at shrines.</summary>
        public float Light { get; private set; }

        /// <summary>The light she dropped when she last fell, if it is still out there.</summary>
        public DroppedLight Dropped { get; private set; }

        /// <summary>What Interact or Attack would do where she stands, for the HUD. Empty when nothing.</summary>
        public string Prompt { get; private set; } = string.Empty;

        /// <summary>The shrine menu is up: light can be spent.</summary>
        public bool MenuOpen { get; private set; }

        public Shrine MenuShrine { get; private set; }

        /// <summary>A short message for the HUD and how long ago it was set.</summary>
        public string Toast { get; private set; } = string.Empty;

        public float ToastAge => Time.unscaledTime - toastTime;

        public EnemyController Boss => boss;

        public string BossName => bossName;

        /// <summary>True while the boss fight is on.</summary>
        public bool BossActive => bossArena != null && bossArena.IsEngaged && boss != null && !boss.Health.IsDead;

        /// <summary>True once the boss has fallen.</summary>
        public bool IsComplete => complete;

        public float CompleteAge => complete ? Time.unscaledTime - completeTime : 0f;

        public PlayerController Player => player;

        public IReadOnlyList<Shrine> Shrines => shrines;

        public IReadOnlyList<EncounterGroup> Encounters => encounters;

        public event Action<Shrine> Rested;

        public event Action<float> LightChanged;

        private void Awake()
        {
            motor = player != null ? player.GetComponent<CharacterMotor>() : null;
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.Died += OnDied;
                player.Interaction = OnInteract;
            }
            if (respawn != null)
                respawn.Respawned += OnRespawned;
            foreach (EnemyController enemy in enemies)
            {
                if (enemy == null)
                    continue;
                EnemyController e = enemy;
                e.Health.Died += () => OnEnemyDied(e);
            }
            foreach (Sentinel sentinel in sentinels)
            {
                if (sentinel == null)
                    continue;
                Sentinel s = sentinel;
                s.Health.Died += () => OnSentinelDied(s);
            }
            foreach (Pickup pickup in pickups)
            {
                if (pickup != null)
                    pickup.Collected += OnCollected;
            }
            foreach (EncounterGroup encounter in encounters)
            {
                if (encounter != null)
                    encounter.LastKill += OnLastKill;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.Died -= OnDied;
                if (player.Interaction == (Func<bool>)OnInteract)
                    player.Interaction = null;
            }
            if (respawn != null)
                respawn.Respawned -= OnRespawned;
            foreach (Pickup pickup in pickups)
            {
                if (pickup != null)
                    pickup.Collected -= OnCollected;
            }
            foreach (EncounterGroup encounter in encounters)
            {
                if (encounter != null)
                    encounter.LastKill -= OnLastKill;
            }
        }

        private void Update()
        {
            if (player == null || motor == null)
                return;

            // A shrine lights as she comes to it, and becomes the place she gets up.
            Shrine near = NearestShrine();
            if (near != null && !near.IsLit)
            {
                near.Light();
                respawn?.SetCheckpoint(near.Distance, near.Height + 0.1f);
                Say($"{near.AreaName}: shrine lit");
            }
            if (MenuOpen && (near != MenuShrine || player.State == PlayerState.Dead))
                CloseMenu();
            if (MenuOpen)
                ReadMenuKeys();

            Prompt = ComputePrompt(near);
        }

        // ------------------------------------------------------------ interact

        private bool OnInteract()
        {
            if (MenuOpen)
            {
                CloseMenu();
                return true;
            }
            Shrine shrine = NearestShrine();
            if (shrine != null && player.State != PlayerState.Dead && motor.IsGrounded)
            {
                Rest(shrine);
                return true;
            }
            Lever lever = NearestLever();
            if (lever != null)
            {
                lever.Pull();
                Say("The way opens");
                return true;
            }
            return false;
        }

        /// <summary>
        /// A rest: her health, stamina and flasks back in full, the regular enemies back at their posts alive, the
        /// shrine lit and the menu opened.
        /// </summary>
        public void Rest(Shrine shrine)
        {
            shrine.Light();
            respawn?.SetCheckpoint(shrine.Distance, shrine.Height + 0.1f);
            player.RestoreAll();
            foreach (EnemyController enemy in enemies)
            {
                if (enemy != null && !(enemy.Config != null && enemy.Config.isBoss))
                    enemy.Reset();
            }
            foreach (Sentinel sentinel in sentinels)
            {
                if (sentinel != null)
                    sentinel.Reset();
            }
            foreach (EncounterGroup encounter in encounters)
            {
                if (encounter != null && encounter != bossArena)
                    encounter.Reset();
            }
            MenuOpen = true;
            MenuShrine = shrine;
            Say("Rested");
            Rested?.Invoke(shrine);
        }

        public void CloseMenu()
        {
            MenuOpen = false;
            MenuShrine = null;
        }

        private void ReadMenuKeys()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool one = (keyboard != null && keyboard.digit1Key.wasPressedThisFrame) || (pad != null && pad.dpad.left.wasPressedThisFrame);
            bool two = (keyboard != null && keyboard.digit2Key.wasPressedThisFrame) || (pad != null && pad.dpad.right.wasPressedThisFrame);
            if (one)
                BuyFlaskSlot();
            else if (two)
                BuyHealth();
        }

        /// <summary>Spends light on one more flask. Returns false when she cannot afford it.</summary>
        public bool BuyFlaskSlot()
        {
            float cost = player.Combat != null ? player.Combat.flaskUpgradeCost : 300f;
            if (Light < cost)
            {
                Say("Not enough light");
                return false;
            }
            SetLight(Light - cost);
            player.AddFlaskSlot();
            Say("One more flask");
            return true;
        }

        /// <summary>Spends light on more health. Returns false when she cannot afford it.</summary>
        public bool BuyHealth()
        {
            float cost = player.Combat != null ? player.Combat.healthUpgradeCost : 200f;
            if (Light < cost)
            {
                Say("Not enough light");
                return false;
            }
            SetLight(Light - cost);
            player.GetComponent<Health>().RaiseMax(player.Combat != null ? player.Combat.healthUpgrade : 20f);
            Say("Stronger");
            return true;
        }

        private Shrine NearestShrine()
        {
            Shrine best = null;
            float bestDistance = reach;
            foreach (Shrine shrine in shrines)
            {
                if (shrine == null)
                    continue;
                float d = Mathf.Abs(shrine.Distance - motor.Distance);
                if (d <= bestDistance && Mathf.Abs(shrine.Height - motor.transform.position.y) < 1.5f)
                {
                    bestDistance = d;
                    best = shrine;
                }
            }
            return best;
        }

        private Lever NearestLever()
        {
            foreach (Lever lever in levers)
            {
                if (lever == null || lever.IsPulled)
                    continue;
                if (Mathf.Abs(lever.Distance - motor.Distance) <= reach && Mathf.Abs(lever.Height - motor.transform.position.y) < 1.5f)
                    return lever;
            }
            return null;
        }

        private string ComputePrompt(Shrine near)
        {
            if (player.State == PlayerState.Dead || player.State == PlayerState.Finisher || MenuOpen)
                return string.Empty;
            EnemyController finisher = player.FindFinisherTarget(out bool execution);
            if (finisher != null && motor.IsGrounded)
                return execution ? "Execute   (Attack)" : "Take down   (Attack)";
            if (near != null && motor.IsGrounded)
                return "Rest   (E)";
            if (NearestLever() != null)
                return "Pull   (E)";
            if (player.HasTether && player.State != PlayerState.Tether && player.FindAnchor() != null)
                return "Tether   (E)";
            return string.Empty;
        }

        // --------------------------------------------------------------- light

        private void OnEnemyDied(EnemyController enemy)
        {
            float value = enemy.Config != null ? enemy.Config.lightValue : 0f;
            SetLight(Light + value);
            if (enemy == boss)
                OnBossFell();
        }

        private void OnSentinelDied(Sentinel sentinel)
        {
            SetLight(Light + sentinel.LightValue);
        }

        private void SetLight(float value)
        {
            Light = Mathf.Max(0f, value);
            LightChanged?.Invoke(Light);
        }

        private void OnDied()
        {
            // What she carried stays where she fell, or where she last stood if she fell into the water. A second fall
            // before she gets it back loses it for good.
            if (Dropped != null)
            {
                Destroy(Dropped.gameObject);
                Dropped = null;
                Say("The light you dropped is lost");
            }
            if (Light > 0f)
            {
                bool fell = killPlane != null && killPlane.Fell;
                float where = fell ? killPlane.LastSafe.x : motor.Distance;
                float height = fell ? killPlane.LastSafe.y : motor.transform.position.y;
                Dropped = MakeDroppedLight(where, height, Light);
                SetLight(0f);
            }
            CloseMenu();
            foreach (EncounterGroup encounter in encounters)
            {
                if (encounter != null)
                    encounter.Reset();
            }
        }

        private DroppedLight MakeDroppedLight(float distance, float height, float amount)
        {
            var go = new GameObject("Dropped light");
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(orb.GetComponent<Collider>());
            orb.transform.SetParent(go.transform, false);
            orb.transform.localScale = Vector3.one * 0.35f;
            if (droppedLightMaterial != null)
                orb.GetComponent<Renderer>().sharedMaterial = droppedLightMaterial;
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Point;
            light.color = lightColor;
            light.range = 5f;
            light.intensity = 3f;
            var dropped = go.AddComponent<DroppedLight>();
            dropped.Place(motor, distance, height, amount, motor.Rail);
            dropped.Recovered += OnRecovered;
            // Added after the orb is placed: Bob remembers where it rests when it wakes.
            go.AddComponent<Pulse>();
            go.AddComponent<Bob>();
            return dropped;
        }

        private void OnRecovered(DroppedLight dropped)
        {
            SetLight(Light + dropped.Amount);
            if (Dropped == dropped)
                Dropped = null;
            Say("Light recovered");
        }

        private void OnRespawned()
        {
            foreach (Sentinel sentinel in sentinels)
            {
                if (sentinel != null)
                    sentinel.ReturnToPost();
            }
        }

        // ------------------------------------------------------------- pickups

        private void OnCollected(Pickup pickup)
        {
            switch (pickup.Kind)
            {
                case PickupKind.Tether:
                    player.HasTether = true;
                    Say("The Tether: throw it at a lamp  (E)");
                    director?.PlayReveal("The Tether", tetherRevealView, 2.6f);
                    break;
                case PickupKind.FlaskSlot:
                    player.AddFlaskSlot();
                    Say("One more flask");
                    break;
                case PickupKind.Light:
                    SetLight(Light + pickup.Amount);
                    Say($"Light: {pickup.Amount:0}");
                    break;
            }
        }

        // ---------------------------------------------------------- encounters

        private void OnLastKill(EncounterGroup encounter, Transform victim)
        {
            if (encounter == bossArena)
                return;
            // The last kill of a fight gets its moment, unless a finisher already has the camera.
            if (player.State != PlayerState.Finisher)
                director?.PlayLastKill(victim);
        }

        private void OnBossFell()
        {
            complete = true;
            completeTime = Time.unscaledTime;
            director?.PlayBossFall(boss.transform);
            Say(string.Empty);
        }

        private void Say(string text)
        {
            Toast = text;
            toastTime = Time.unscaledTime;
        }
    }
}
