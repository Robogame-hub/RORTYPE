using System;
using UnityEngine;
using UnityEngine.UI;

namespace RorType.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class MinimapController : MonoBehaviour
    {
        [Serializable]
        private struct IconSlot
        {
            public RectTransform root;
            public MinimapMarkerGraphic graphic;
        }

        [SerializeField] private RectTransform mapViewport;
        [SerializeField] private Image mapImage;
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private MinimapMarkerGraphic playerMarkerGraphic;
        [SerializeField] private Vector2 worldSizeMeters = new(500f, 500f);
        [SerializeField] private Vector2 worldCenter;
        [SerializeField] private float iconEdgePadding = 8f;
        [SerializeField] private bool clampIconsToMapBounds = true;
        [SerializeField] private IconSlot[] iconSlots = Array.Empty<IconSlot>();

        private void LateUpdate()
        {
            if (mapViewport == null || mapImage == null)
            {
                return;
            }

            var trackables = MinimapTrackable.RegisteredTrackables;
            var nextSlotIndex = 0;
            MinimapTrackable playerTrackable = null;

            for (var index = 0; index < trackables.Count; index++)
            {
                var trackable = trackables[index];
                if (trackable == null || !trackable.isActiveAndEnabled)
                {
                    continue;
                }

                if (trackable.IconGroup == MinimapIconGroup.Player)
                {
                    playerTrackable ??= trackable;
                    continue;
                }

                if (nextSlotIndex >= iconSlots.Length)
                {
                    continue;
                }

                ApplyIconSlot(iconSlots[nextSlotIndex], trackable);
                nextSlotIndex++;
            }

            for (var index = nextSlotIndex; index < iconSlots.Length; index++)
            {
                SetSlotVisible(iconSlots[index], false);
            }

            ApplyPlayerMarker(playerTrackable);
        }

        private void ApplyPlayerMarker(MinimapTrackable playerTrackable)
        {
            if (playerMarker == null || playerMarkerGraphic == null)
            {
                return;
            }

            if (playerTrackable == null)
            {
                playerMarker.gameObject.SetActive(false);
                return;
            }

            playerMarker.gameObject.SetActive(true);
            playerMarker.sizeDelta = Vector2.one * playerTrackable.MarkerSize;
            playerMarker.anchoredPosition = GetAnchoredPosition(playerTrackable);
            playerMarker.localRotation = playerTrackable.RotateWithWorldYaw
                ? Quaternion.Euler(0f, 0f, -GetYawDegrees(playerTrackable))
                : Quaternion.identity;
            playerMarkerGraphic.SetAppearance(playerTrackable.MarkerShape, playerTrackable.IconColor);
        }

        private void ApplyIconSlot(IconSlot slot, MinimapTrackable trackable)
        {
            if (slot.root == null || slot.graphic == null)
            {
                return;
            }

            slot.root.sizeDelta = Vector2.one * trackable.MarkerSize;
            slot.root.anchoredPosition = GetAnchoredPosition(trackable);
            slot.root.localRotation = trackable.RotateWithWorldYaw
                ? Quaternion.Euler(0f, 0f, -GetYawDegrees(trackable))
                : Quaternion.identity;
            slot.graphic.SetAppearance(trackable.MarkerShape, trackable.IconColor);
            SetSlotVisible(slot, true);
        }

        private void SetSlotVisible(IconSlot slot, bool isVisible)
        {
            if (slot.root != null)
            {
                slot.root.gameObject.SetActive(isVisible);
            }
        }

        private Vector2 GetAnchoredPosition(MinimapTrackable trackable)
        {
            var trackedTransform = trackable.TrackedTransform;
            var worldPosition = trackedTransform.position + trackable.WorldOffset;

            var normalizedX = worldSizeMeters.x > 0.001f
                ? 0.5f + ((worldPosition.x - worldCenter.x) / worldSizeMeters.x)
                : 0.5f;
            var normalizedY = worldSizeMeters.y > 0.001f
                ? 0.5f + ((worldPosition.z - worldCenter.y) / worldSizeMeters.y)
                : 0.5f;

            var rect = mapViewport.rect;
            var anchoredPosition = new Vector2(
                (normalizedX - 0.5f) * rect.width,
                (normalizedY - 0.5f) * rect.height);

            if (!clampIconsToMapBounds)
            {
                return anchoredPosition;
            }

            var halfWidth = Mathf.Max(0f, (rect.width * 0.5f) - iconEdgePadding);
            var halfHeight = Mathf.Max(0f, (rect.height * 0.5f) - iconEdgePadding);

            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, -halfWidth, halfWidth);
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, -halfHeight, halfHeight);
            return anchoredPosition;
        }

        private static float GetYawDegrees(MinimapTrackable trackable)
        {
            return trackable.TrackedTransform.eulerAngles.y;
        }
    }
}
