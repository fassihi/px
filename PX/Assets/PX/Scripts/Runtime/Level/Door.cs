using System;
using UnityEngine;

namespace PX
{
    /// <summary>
    /// A block that bars the way until something opens it: a lever, an encounter's end, a pickup. It slides out of
    /// the way over a moment and its collider goes with it. Arena gates close again when a fight starts.
    /// </summary>
    public sealed class Door : MonoBehaviour
    {
        [Tooltip("Where the block goes when open, relative to where it was placed.")]
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 6f, 0f);
        [SerializeField, Min(0.05f)] private float duration = 1.2f;
        [SerializeField] private bool startsOpen;

        private Vector3 closedPosition;
        private float progress;
        private bool open;
        private Collider[] colliders;

        public bool IsOpen => open;

        /// <summary>True while it is still moving.</summary>
        public bool IsMoving => Mathf.Abs(progress - (open ? 1f : 0f)) > 0.001f;

        public event Action Opened;

        public event Action Closed;

        private void Awake()
        {
            closedPosition = transform.position;
            colliders = GetComponentsInChildren<Collider>();
            open = startsOpen;
            progress = open ? 1f : 0f;
            Apply();
        }

        public void Open()
        {
            if (open)
                return;
            open = true;
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!open)
                return;
            open = false;
            Closed?.Invoke();
        }

        private void Update()
        {
            float target = open ? 1f : 0f;
            if (Mathf.Abs(progress - target) < 0.0001f)
                return;
            progress = Mathf.MoveTowards(progress, target, Time.deltaTime / duration);
            Apply();
        }

        private void Apply()
        {
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = closedPosition + openOffset * eased;
            foreach (Collider collider in colliders)
                collider.enabled = progress < 0.95f;
        }
    }
}
