using UnityEngine;

namespace RorType.Gameplay.Rts
{
    [DisallowMultipleComponent]
    public sealed class RtsCameraController : MonoBehaviour
    {
        [SerializeField] private Vector3 focusPoint = new Vector3(0f, 0f, 0f);
        [SerializeField, Min(1f)] private float distance = 42f;
        [SerializeField, Min(1f)] private float minDistance = 16f;
        [SerializeField, Min(1f)] private float maxDistance = 68f;
        [SerializeField, Range(20f, 85f)] private float pitch = 58f;
        [SerializeField] private float yaw = 0f;
        [SerializeField, Min(0.1f)] private float panSpeed = 28f;
        [SerializeField, Min(0.01f)] private float rotateSpeed = 0.16f;
        [SerializeField, Min(0.01f)] private float zoomSpeed = 5f;
        [SerializeField, Min(0.01f)] private float rotationSmoothTime = 0.12f;
        [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.1f;
        [SerializeField] private Vector2 mapHalfExtents = new Vector2(48f, 48f);

        private Vector3 middleMouseStart;
        private float targetDistance;
        private float targetPitch;
        private float targetYaw;
        private float distanceVelocity;
        private float pitchVelocity;
        private float yawVelocity;

        private void Start()
        {
            targetDistance = distance;
            targetPitch = pitch;
            targetYaw = yaw;
            ApplyCameraTransform();
        }

        private void Update()
        {
            HandlePan();
            HandleRotation();
            HandleZoom();
            ApplyCameraTransform();
        }

        private void HandlePan()
        {
            var horizontal = Input.GetAxisRaw("Horizontal");
            var vertical = Input.GetAxisRaw("Vertical");
            if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f))
            {
                return;
            }

            var flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var move = (transform.right * horizontal + flatForward * vertical).normalized;
            focusPoint += move * panSpeed * Time.deltaTime;
            focusPoint.x = Mathf.Clamp(focusPoint.x, -mapHalfExtents.x, mapHalfExtents.x);
            focusPoint.z = Mathf.Clamp(focusPoint.z, -mapHalfExtents.y, mapHalfExtents.y);
        }

        private void HandleRotation()
        {
            if (Input.GetMouseButtonDown(2))
            {
                middleMouseStart = Input.mousePosition;
            }

            if (!Input.GetMouseButton(2))
            {
                return;
            }

            var delta = (Vector3)Input.mousePosition - middleMouseStart;
            middleMouseStart = Input.mousePosition;
            targetYaw += delta.x * rotateSpeed;
            targetPitch = Mathf.Clamp(targetPitch - delta.y * rotateSpeed * 0.55f, 35f, 78f);
        }

        private void HandleZoom()
        {
            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSpeed, minDistance, maxDistance);
            }
        }

        private void ApplyCameraTransform()
        {
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, rotationSmoothTime);
            pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref pitchVelocity, rotationSmoothTime);
            distance = Mathf.SmoothDamp(distance, targetDistance, ref distanceVelocity, zoomSmoothTime);
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = focusPoint + rotation * new Vector3(0f, 0f, -distance);
            transform.rotation = rotation;
        }
    }
}
