using System;
using System.Collections.Generic;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// A fight the level watches: a set of enemies in a stretch of rail. When she comes into it the gates close and the
    /// guards are roused; when the last of them falls the gates open again and the level hears about it (and the
    /// camera cuts to the kill). Her death puts everything back.
    /// </summary>
    public sealed class EncounterGroup : MonoBehaviour
    {
        [SerializeField] private string encounterName = "Encounter";
        [SerializeField] private CharacterMotor target;
        [SerializeField] private EnemyController[] enemies = Array.Empty<EnemyController>();
        [SerializeField] private Sentinel[] sentinels = Array.Empty<Sentinel>();
        [SerializeField] private Door[] gates = Array.Empty<Door>();
        [Tooltip("The stretch of rail that is the arena. Entering it with any of its enemies alive starts the encounter.")]
        [SerializeField] private float from;
        [SerializeField] private float to;
        [Tooltip("On: entering rouses every enemy at once (a boss, an ambush). Off: they notice her as they would anyway.")]
        [SerializeField] private bool rouseOnEntry;
        [Tooltip("On: the gates only close when the fight starts and open when it ends. Off: no gates.")]
        [SerializeField] private bool lockGates = true;

        private readonly HashSet<Health> dead = new HashSet<Health>();
        private bool engaged;
        private bool cleared;

        public string Name => encounterName;

        public bool IsEngaged => engaged;

        public bool IsCleared => cleared;

        public float From => from;

        public float To => to;

        public IReadOnlyList<EnemyController> Enemies => enemies;

        public IReadOnlyList<Sentinel> Sentinels => sentinels;

        /// <summary>Raised when the fight starts.</summary>
        public event Action<EncounterGroup> Engaged;

        /// <summary>Raised when the last of them dies, with what died: the camera's cue.</summary>
        public event Action<EncounterGroup, Transform> LastKill;

        /// <summary>Raised a moment after the last kill, when the gates open.</summary>
        public event Action<EncounterGroup> Cleared;

        private void OnEnable()
        {
            foreach (EnemyController enemy in enemies)
            {
                if (enemy != null)
                    enemy.Health.Died += () => OnDied(enemy.Health, enemy.transform);
            }
            foreach (Sentinel sentinel in sentinels)
            {
                if (sentinel != null)
                    sentinel.Health.Died += () => OnDied(sentinel.Health, sentinel.transform);
            }
        }

        private void Update()
        {
            if (engaged || cleared || target == null)
                return;
            if (target.Distance < from || target.Distance > to)
                return;
            if (AliveCount() == 0)
            {
                cleared = true;
                return;
            }
            Engage();
        }

        private int AliveCount()
        {
            int alive = 0;
            foreach (EnemyController enemy in enemies)
            {
                if (enemy != null && !enemy.Health.IsDead)
                    alive++;
            }
            foreach (Sentinel sentinel in sentinels)
            {
                if (sentinel != null && !sentinel.Health.IsDead)
                    alive++;
            }
            return alive;
        }

        private void Engage()
        {
            engaged = true;
            if (lockGates)
            {
                foreach (Door gate in gates)
                {
                    if (gate != null)
                        gate.Close();
                }
            }
            if (rouseOnEntry)
            {
                foreach (EnemyController enemy in enemies)
                {
                    if (enemy != null && enemy.InputSource is EnemyBrain brain)
                        brain.Alarm();
                }
            }
            Engaged?.Invoke(this);
        }

        private void OnDied(Health health, Transform body)
        {
            if (!dead.Add(health))
                return;
            if (AliveCount() > 0)
                return;
            cleared = true;
            LastKill?.Invoke(this, body);
            foreach (Door gate in gates)
            {
                if (gate != null)
                    gate.Open();
            }
            Cleared?.Invoke(this);
        }

        /// <summary>Her death: the fight is off, the gates open so she can come back in, the dead are forgotten.</summary>
        public void Reset()
        {
            engaged = false;
            cleared = AliveCount() == 0;
            dead.Clear();
            foreach (Door gate in gates)
            {
                if (gate != null)
                    gate.Open();
            }
        }
    }
}
