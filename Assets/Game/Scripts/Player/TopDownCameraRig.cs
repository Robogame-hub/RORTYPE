using UnityEngine;

namespace RorType.Gameplay.Player
{
    public sealed class TopDownCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 followOffset = new Vector3(-10f, 16f, -10f);
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1f, 0f);
        [SerializeField, Min(0.01f)] private float followSharpness = 8f;

        private Vector3 smoothedTargetPosition;
        private bool hasSmoothedTargetPosition;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            hasSmoothedTargetPosition = false;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            if (!hasSmoothedTargetPosition)
            {
                smoothedTargetPosition = target.position;
                hasSmoothedTargetPosition = true;
            }
            else
            {
                smoothedTargetPosition = Vector3.Lerp(smoothedTargetPosition, target.position, blend);
            }

            transform.position = smoothedTargetPosition + followOffset;
            transform.rotation = Quaternion.LookRotation((smoothedTargetPosition + lookOffset) - transform.position, Vector3.up);
        }
    }
}
