using UnityEngine;

namespace RorType.Gameplay.Environment
{
    public enum GearRotationAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    [DisallowMultipleComponent]
    public sealed class GearRotator : MonoBehaviour
    {
        [SerializeField] private GearRotationAxis axis = GearRotationAxis.Y;
        [SerializeField] private float degreesPerSecond = 90f;

        private void Update()
        {
            transform.Rotate(GetAxisVector() * (degreesPerSecond * Time.deltaTime), Space.Self);
        }

        private Vector3 GetAxisVector()
        {
            return axis switch
            {
                GearRotationAxis.X => Vector3.right,
                GearRotationAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
        }
    }
}
