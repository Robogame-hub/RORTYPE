using RorType.Gameplay.UI;
using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class SlidingDoor : MonoBehaviour
    {
        [SerializeField] private Transform movingRoot;
        [SerializeField] private Vector3 openLocalOffset = Vector3.down * 5f;
        [SerializeField, Min(0.01f)] private float moveSpeed = 6f;
        [SerializeField, Min(0.01f)] private float closeSpeed = 12f;
        [SerializeField] private bool lockOpenWhenFullyOpen = true;
        [SerializeField] private bool startOpen;

        private Vector3 closedLocalPosition;
        private bool isOpen;
        private bool isLockedOpen;
        private MinimapTrackable minimapTrackable;

        public bool IsOpen => isOpen || isLockedOpen;
        public bool IsLockedOpen => isLockedOpen;

        private void Awake()
        {
            if (movingRoot == null)
            {
                movingRoot = transform;
            }

            closedLocalPosition = movingRoot.localPosition;
            isOpen = startOpen;
            isLockedOpen = startOpen && lockOpenWhenFullyOpen;
            movingRoot.localPosition = GetTargetPosition();
            minimapTrackable = GetComponent<MinimapTrackable>();
            RefreshMinimapMarker();
        }

        private void Update()
        {
            if (movingRoot == null)
            {
                return;
            }

            var wantsOpen = IsOpen;
            var targetPosition = GetTargetPosition();
            movingRoot.localPosition = Vector3.MoveTowards(
                movingRoot.localPosition,
                targetPosition,
                (wantsOpen ? moveSpeed : closeSpeed) * Time.deltaTime);

            if (!isLockedOpen &&
                lockOpenWhenFullyOpen &&
                wantsOpen &&
                Vector3.SqrMagnitude(movingRoot.localPosition - targetPosition) <= 0.0001f)
            {
                isLockedOpen = true;
                isOpen = true;
                RefreshMinimapMarker();
            }
        }

        public void SetOpen(bool open)
        {
            if (isLockedOpen && !open)
            {
                return;
            }

            isOpen = open;
            RefreshMinimapMarker();
        }

        public void Open()
        {
            SetOpen(true);
        }

        public void Close()
        {
            SetOpen(false);
        }

        private Vector3 GetTargetPosition()
        {
            return closedLocalPosition + (IsOpen ? openLocalOffset : Vector3.zero);
        }

        private void RefreshMinimapMarker()
        {
            if (minimapTrackable != null)
            {
                minimapTrackable.enabled = !isLockedOpen;
            }
        }

        private void OnValidate()
        {
            if (movingRoot == null)
            {
                movingRoot = transform;
            }
        }
    }
}
