using UnityEngine;

namespace RorType.Gameplay.Combat
{
    public sealed class WorldBillboard : MonoBehaviour
    {
        [SerializeField] private bool keepUpright = true;

        private void LateUpdate()
        {
            var currentCamera = Camera.main;
            if (currentCamera == null)
            {
                return;
            }

            var forward = currentCamera.transform.forward;
            if (keepUpright)
            {
                forward.y = 0f;
                if (forward.sqrMagnitude <= 0.0001f)
                {
                    forward = Vector3.forward;
                }
            }

            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
