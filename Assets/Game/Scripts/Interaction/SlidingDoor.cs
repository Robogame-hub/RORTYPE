using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class SlidingDoor : MonoBehaviour
    {
        [SerializeField] private Transform movingRoot;
        [SerializeField] private Vector3 openLocalOffset = Vector3.down * 5f;
        [SerializeField, Min(0.01f)] private float moveSpeed = 6f;
        [SerializeField] private bool startOpen;

        private Vector3 closedLocalPosition;
        private bool isOpen;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (movingRoot == null)
            {
                movingRoot = transform;
            }

            closedLocalPosition = movingRoot.localPosition;
            isOpen = startOpen;
            movingRoot.localPosition = GetTargetPosition();
        }

        private void Update()
        {
            if (movingRoot == null)
            {
                return;
            }

            var targetPosition = GetTargetPosition();
            movingRoot.localPosition = Vector3.MoveTowards(
                movingRoot.localPosition,
                targetPosition,
                moveSpeed * Time.deltaTime);
        }

        public void SetOpen(bool open)
        {
            isOpen = open;
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
            return closedLocalPosition + (isOpen ? openLocalOffset : Vector3.zero);
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
