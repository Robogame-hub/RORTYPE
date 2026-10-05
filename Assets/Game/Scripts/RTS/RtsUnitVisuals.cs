using UnityEngine;
using System.Collections.Generic;

namespace RorType.Gameplay.Rts
{
    [DisallowMultipleComponent]
    public sealed class RtsTankVisual : MonoBehaviour
    {
        [SerializeField] private Transform turret;
        [SerializeField] private Transform muzzle;
        [SerializeField, Min(1f)] private float turretTurnSpeed = 360f;
        [SerializeField] private Transform[] trackWheels;

        private Vector3 previousPosition;

        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position + (transform.forward * 1.6f) + Vector3.up;

        private void Awake()
        {
            turret ??= transform.Find("Turret");
            muzzle ??= transform.Find("Turret/Muzzle");
            if (trackWheels == null || trackWheels.Length == 0)
            {
                var wheels = new List<Transform>();
                var transforms = GetComponentsInChildren<Transform>();
                for (var i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name.StartsWith("TrackWheel"))
                    {
                        wheels.Add(transforms[i]);
                    }
                }
                trackWheels = wheels.ToArray();
            }
            previousPosition = transform.position;
        }

        public void AimAt(Vector3 direction)
        {
            direction.y = 0f;
            if (turret == null || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            turret.rotation = Quaternion.RotateTowards(turret.rotation, targetRotation, turretTurnSpeed * Time.deltaTime);
        }

        private void LateUpdate()
        {
            var traveled = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude;
            previousPosition = transform.position;
            if (traveled <= 0.0001f || trackWheels == null)
            {
                return;
            }

            var spin = traveled * 1100f;
            for (var i = 0; i < trackWheels.Length; i++)
            {
                if (trackWheels[i] != null)
                {
                    trackWheels[i].Rotate(Vector3.right, spin, Space.Self);
                }
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class RtsHelicopterVisual : MonoBehaviour
    {
        [SerializeField] private Transform mainRotor;
        [SerializeField] private Transform muzzle;
        [SerializeField, Min(60f)] private float rotorDegreesPerSecond = 1100f;

        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position + (transform.forward * 1.4f);

        private void Awake()
        {
            mainRotor ??= transform.Find("Rotor");
            muzzle ??= transform.Find("Muzzle");
        }

        private void Update()
        {
            if (mainRotor != null)
            {
                mainRotor.Rotate(Vector3.up, rotorDegreesPerSecond * Time.deltaTime, Space.Self);
            }
        }
    }
}
