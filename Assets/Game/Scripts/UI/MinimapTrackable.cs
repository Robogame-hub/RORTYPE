using System.Collections.Generic;
using UnityEngine;

namespace RorType.Gameplay.UI
{
    public enum MinimapIconGroup
    {
        Player = 0,
        Enemy = 1,
        PointOfInterest = 2
    }

    public enum MinimapIconPresentation
    {
        Sprite = 0,
        PlayerArrow = 1
    }

    [DisallowMultipleComponent]
    public sealed class MinimapTrackable : MonoBehaviour
    {
        private static readonly List<MinimapTrackable> ActiveTrackables = new();

        [SerializeField] private Transform trackedTransform;
        [SerializeField] private MinimapIconGroup iconGroup = MinimapIconGroup.PointOfInterest;
        [SerializeField] private MinimapIconPresentation presentation = MinimapIconPresentation.Sprite;
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private Color iconColor = Color.blue;
        [SerializeField] private bool rotateWithWorldYaw;
        [SerializeField] private Vector3 worldOffset;

        public static IReadOnlyList<MinimapTrackable> RegisteredTrackables => ActiveTrackables;

        public Transform TrackedTransform => trackedTransform != null ? trackedTransform : transform;
        public MinimapIconGroup IconGroup => iconGroup;
        public MinimapIconPresentation Presentation => presentation;
        public Sprite IconSprite => iconSprite;
        public Color IconColor => iconColor;
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
