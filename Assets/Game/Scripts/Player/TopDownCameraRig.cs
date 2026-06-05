using UnityEngine;

namespace RorType.Gameplay.Player
{
    public sealed class TopDownCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 followOffset = new Vector3(-10f, 16f, -10f);
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1f, 0f);
        [SerializeField, Min(0.01f)] private float followSharpness = 8f;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var desiredPosition = target.position + followOffset;
            var blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            transform.rotation = Quaternion.LookRotation((target.position + lookOffset) - transform.position, Vector3.up);
        }
    }
}
