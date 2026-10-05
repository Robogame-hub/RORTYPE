using System.Collections.Generic;
using UnityEngine;

namespace RorType.Gameplay.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemyVisionTarget : MonoBehaviour
    {
        [SerializeField] private bool startsVisible = true;
        [SerializeField, Min(0.02f)] private float hiddenRendererRefreshInterval = 0.25f;

        private readonly List<Renderer> renderers = new();
        private readonly Dictionary<Renderer, bool> rendererDefaultEnabled = new();
        private bool isVisible = true;
        private float hiddenRefreshTimer;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            isVisible = startsVisible;
            CaptureRenderers();
            ApplyVisibility();
        }

        private void OnEnable()
        {
            hiddenRefreshTimer = 0f;
            CaptureRenderers();
            ApplyVisibility();
        }

        private void OnDisable()
        {
            SetVisible(true);
        }

        private void LateUpdate()
        {
            if (isVisible)
            {
                return;
            }

            hiddenRefreshTimer -= Time.deltaTime;
            if (hiddenRefreshTimer > 0f)
            {
                return;
            }

            hiddenRefreshTimer = hiddenRendererRefreshInterval;
            CaptureRenderers();
            ApplyVisibility();
        }

        public void SetVisible(bool visible)
        {
            if (isVisible == visible && (visible || hiddenRefreshTimer > 0f))
            {
                return;
            }

            isVisible = visible;
            hiddenRefreshTimer = 0f;
            CaptureRenderers();
            ApplyVisibility();
        }

        private void CaptureRenderers()
        {
            renderers.Clear();
            GetComponentsInChildren(true, renderers);

            for (var i = 0; i < renderers.Count; i++)
            {
                var candidate = renderers[i];
                if (candidate == null || rendererDefaultEnabled.ContainsKey(candidate))
                {
                    continue;
                }

                rendererDefaultEnabled[candidate] = isVisible ? candidate.enabled : true;
            }
        }

        private void ApplyVisibility()
        {
            for (var i = renderers.Count - 1; i >= 0; i--)
            {
                var candidate = renderers[i];
                if (candidate == null)
                {
                    renderers.RemoveAt(i);
                    continue;
                }

                candidate.enabled = isVisible
                    ? rendererDefaultEnabled.TryGetValue(candidate, out var defaultEnabled) && defaultEnabled
                    : false;
            }
        }
    }
}
