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
        [SerializeField] private bool scaleMarkerToWorldBounds;
        [SerializeField] private Vector2 markerWorldSizeMeters;
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

        public bool TryGetMarkerWorldSize(out Vector2 worldSize)
        {
            if (markerWorldSizeMeters.x > 0f && markerWorldSizeMeters.y > 0f)
            {
                worldSize = markerWorldSizeMeters;
                return true;
            }

            if (!scaleMarkerToWorldBounds)
            {
                worldSize = default;
                return false;
            }

            return TryCalculateLocalBoundsSize(out worldSize);
        }

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
            markerWorldSizeMeters.x = Mathf.Max(0f, markerWorldSizeMeters.x);
            markerWorldSizeMeters.y = Mathf.Max(0f, markerWorldSizeMeters.y);
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

        private bool TryCalculateLocalBoundsSize(out Vector2 worldSize)
        {
            var reference = TrackedTransform;
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            var hasBounds = false;

            var renderers = reference.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                EncapsulateWorldBounds(renderers[i].bounds, reference, ref hasBounds, ref min, ref max);
            }

            var colliders = reference.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                EncapsulateWorldBounds(colliders[i].bounds, reference, ref hasBounds, ref min, ref max);
            }

            if (!hasBounds)
            {
                worldSize = default;
                return false;
            }

            var size = max - min;
            worldSize = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.z));
            return worldSize.x > 0.001f && worldSize.y > 0.001f;
        }

        private static void EncapsulateWorldBounds(
            Bounds bounds,
            Transform reference,
            ref bool hasBounds,
            ref Vector3 min,
            ref Vector3 max)
        {
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var worldCorner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        var localCorner = reference.InverseTransformPoint(worldCorner);
                        min = hasBounds ? Vector3.Min(min, localCorner) : localCorner;
                        max = hasBounds ? Vector3.Max(max, localCorner) : localCorner;
                        hasBounds = true;
                    }
                }
            }
        }
    }
}
