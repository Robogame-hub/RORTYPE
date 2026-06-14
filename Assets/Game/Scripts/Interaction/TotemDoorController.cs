using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class TotemDoorController : MonoBehaviour
    {
        [SerializeField] private SlidingDoor linkedDoor;
        [SerializeField] private TotemPedestal[] pedestals = System.Array.Empty<TotemPedestal>();

        private void Awake()
        {
            EnsureReferences();
            BindPedestals();
            UpdateDoorState();
        }

        public void NotifyPedestalChanged()
        {
            UpdateDoorState();
        }

        private void UpdateDoorState()
        {
            if (linkedDoor == null)
            {
                return;
            }

            linkedDoor.SetOpen(AreAllPedestalsFilled());
        }

        private bool AreAllPedestalsFilled()
        {
            EnsureReferences();
            if (pedestals == null || pedestals.Length == 0)
            {
                return false;
            }

            for (var index = 0; index < pedestals.Length; index++)
            {
                var pedestal = pedestals[index];
                if (pedestal == null || !pedestal.HasTotem)
                {
                    return false;
                }
            }

            return true;
        }

        private void EnsureReferences()
        {
            if (linkedDoor == null)
            {
                linkedDoor = GetComponentInChildren<SlidingDoor>(true);
            }

            pedestals = GetComponentsInChildren<TotemPedestal>(true);
        }

        private void BindPedestals()
        {
            if (pedestals == null)
            {
                return;
            }

            for (var index = 0; index < pedestals.Length; index++)
            {
                var pedestal = pedestals[index];
                if (pedestal != null)
                {
                    pedestal.Bind(this);
                }
            }
        }

        private void OnValidate()
        {
            EnsureReferences();
            BindPedestals();
        }
    }
}
