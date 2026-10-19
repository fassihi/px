using System;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// A shrine: a lantern on a ring of light in the floor. Walking up to it lights it and makes it the place she gets
    /// up at. Resting at it (Interact) fills her health, stamina and flasks, and brings the regular enemies back to
    /// their posts; it is also where light is spent.
    /// </summary>
    public sealed class Shrine : MonoBehaviour
    {
        [SerializeField] private float distance;
        [SerializeField] private float height;
        [SerializeField] private string areaName = "Shrine";
        [Tooltip("What lights up: the flame and its light, kept off until she comes.")]
        [SerializeField] private GameObject flame;

        public float Distance => distance;

        public float Height => height;

        public string AreaName => areaName;

        public bool IsLit { get; private set; }

        public event Action Lit;

        private void Awake()
        {
            if (flame != null)
                flame.SetActive(false);
        }

        /// <summary>Lights the shrine: from now on she gets up here.</summary>
        public void Light()
        {
            if (IsLit)
                return;
            IsLit = true;
            if (flame != null)
                flame.SetActive(true);
            Lit?.Invoke();
        }
    }
}
