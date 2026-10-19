using System.Collections.Generic;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// A lamp the tether can catch: a point in rail space she is pulled to. Glows brighter while she could reach it,
    /// so the lamps that matter read from across the hall.
    /// </summary>
    public sealed class TetherAnchor : MonoBehaviour
    {
        public static readonly List<TetherAnchor> All = new List<TetherAnchor>();

        [SerializeField] private float distance;
        [SerializeField] private float height;
        [SerializeField] private PlayerController player;

        private Pulse pulse;

        /// <summary>Rail distance of the lamp.</summary>
        public float Distance => distance;

        /// <summary>Height of the lamp above the floor's zero, in metres.</summary>
        public float Height => height;

        /// <summary>True while she could throw the tether at this lamp from where she is.</summary>
        public bool InReach { get; private set; }

        private void Awake()
        {
            pulse = GetComponent<Pulse>();
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Update()
        {
            InReach = player != null && player.HasTether && player.FindAnchor() == this;
            if (pulse != null)
                pulse.Boost = Mathf.MoveTowards(pulse.Boost, InReach ? 2.2f : 1f, Time.deltaTime * 4f);
        }
    }
}
