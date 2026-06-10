using System.Collections.Generic;
using UnityEngine;

namespace RorType.Gameplay.UI
{
    public enum MinimapIconGroup
    {
        Player = 0,
        Enemy = 1,
        PointOfInterest = 2,
        Loot = 3
    }

    public enum MinimapMarkerShape
    {
        Cross = 0,
        Square = 1,
        Triangle = 2,
        Circle = 3,
        Arrow = 4
    }

    [DisallowMultipleComponent]
    public sealed class MinimapTrackable : MonoBehaviour
    {
        private static readonly List<MinimapTrackable> ActiveTrackables = new();

        [SerializeField] private Transform trackedTransform;
        [SerializeField] private MinimapIconGroup iconGroup = MinimapIconGroup.PointOfInterest;
        [SerializeField] private MinimapMarkerShape markerShape = MinimapMarkerShape.Square;
        [SerializeField] private Color iconColor = Color.blue;
        [SerializeField, Min(4f)] private float markerSize = 14f;
        [SerializeField] private bool rotateWithWorldYaw;
        [SerializeField] private Vector3 worldOffset;

        public static IReadOnlyList<MinimapTrackable> RegisteredTrackables => ActiveTrackables;

        public Transform TrackedTransform => trackedTransform != null ? trackedTransform : transform;
        public MinimapIconGroup IconGroup => iconGroup;
        public MinimapMarkerShape MarkerShape => markerShape;
        public Color IconColor => iconColor;
        public float MarkerSize => Mathf.Max(4f, markerSize);
        public bool RotateWithWorldYaw => rotateWithWorldYaw;
        public Vector3 WorldOffset => worldOffset;

        private void Reset()
        {
            trackedTransform = transform;
        }

        private void OnValidate()
        {
            if (trackedTransform == null)
            {
                trackedTransform = transform;
            }

            markerSize = Mathf.Max(4f, markerSize);
        }

        private void OnEnable()
        {
            if (!ActiveTrackables.Contains(this))
            {
                ActiveTrackables.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveTrackables.Remove(this);
        }
    }
}
