using System;
using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemyOverheadPresentation : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField, Min(0f)] private float heightOffset = 1.7f;
        [SerializeField, Min(0.01f)] private float healthBarWidth = 0.9f;
        [SerializeField, Min(0.01f)] private float healthBarHeight = 0.16f;
        [SerializeField, Min(0.01f)] private float healthTextScale = 0.075f;
        [SerializeField, Min(0.01f)] private float damageTextScale = 0.09f;

        [Header("Colors")]
        [SerializeField] private Color healthFillColor = new Color(0.92f, 0.16f, 0.16f);
        [SerializeField] private Color healthBackgroundColor = new Color(0.08f, 0.08f, 0.08f);
        [SerializeField] private Color healthTextColor = Color.white;
        [SerializeField] private Color damageTextColor = Color.white;

        private Transform overheadRoot;
        private Transform billboardRoot;
        private Transform healthFillTransform;
        private TextMesh healthTextMesh;
        private Font builtInFont;

        private void Awake()
        {
            EnsurePresentationBuilt();
        }

        public void SetHealth(float currentHealth, float maxHealth)
        {
            EnsurePresentationBuilt();

            var clampedMax = Mathf.Max(1f, maxHealth);
            var ratio = Mathf.Clamp01(currentHealth / clampedMax);

            if (healthFillTransform != null)
            {
                healthFillTransform.localScale = new Vector3(healthBarWidth * ratio, healthBarHeight, healthBarHeight);
                healthFillTransform.localPosition = new Vector3((-healthBarWidth * 0.5f) + (healthFillTransform.localScale.x * 0.5f), 0f, 0f);
            }

            if (healthTextMesh != null)
            {
                healthTextMesh.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(clampedMax)}";
            }
        }

        public void SpawnDamageNumber(float damage, Vector3 worldPosition)
        {
            var damageTextObject = new GameObject("DamageNumber");
            damageTextObject.transform.position = worldPosition + new Vector3(
                UnityEngine.Random.Range(-0.12f, 0.12f),
                0.9f,
                UnityEngine.Random.Range(-0.12f, 0.12f));

            var textMesh = damageTextObject.AddComponent<TextMesh>();
            ConfigureTextMesh(textMesh, damageTextColor, TextAnchor.MiddleCenter);
            textMesh.alignment = TextAlignment.Center;

            damageTextObject.AddComponent<WorldBillboard>();
            var floatingText = damageTextObject.AddComponent<FloatingWorldText>();
            floatingText.Initialize(Mathf.RoundToInt(damage).ToString(), damageTextColor, damageTextScale);
        }

        private void EnsurePresentationBuilt()
        {
            if (overheadRoot != null)
            {
                return;
            }

            builtInFont = ResolveBuiltInFont();

            overheadRoot = new GameObject("Overhead").transform;
            overheadRoot.SetParent(transform, false);
            overheadRoot.localPosition = new Vector3(0f, heightOffset, 0f);

            billboardRoot = new GameObject("Billboard").transform;
            billboardRoot.SetParent(overheadRoot, false);
            billboardRoot.localPosition = Vector3.zero;
            billboardRoot.gameObject.AddComponent<WorldBillboard>();

            var background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "HealthBarBackground";
            background.transform.SetParent(billboardRoot, false);
            background.transform.localPosition = Vector3.zero;
            background.transform.localScale = new Vector3(healthBarWidth, healthBarHeight, healthBarHeight);
            RemoveCollider(background);
            var backgroundRenderer = background.GetComponent<MeshRenderer>();
            if (backgroundRenderer != null)
            {
                backgroundRenderer.material.color = healthBackgroundColor;
            }

            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "HealthBarFill";
            fill.transform.SetParent(billboardRoot, false);
            fill.transform.localPosition = Vector3.zero;
            fill.transform.localScale = new Vector3(healthBarWidth, healthBarHeight, healthBarHeight);
            RemoveCollider(fill);
            healthFillTransform = fill.transform;
            var fillRenderer = fill.GetComponent<MeshRenderer>();
            if (fillRenderer != null)
            {
                fillRenderer.material.color = healthFillColor;
            }

            var healthTextObject = new GameObject("HealthText");
            healthTextObject.transform.SetParent(billboardRoot, false);
            healthTextObject.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            healthTextObject.transform.localScale = Vector3.one * healthTextScale;
            healthTextMesh = healthTextObject.AddComponent<TextMesh>();
            ConfigureTextMesh(healthTextMesh, healthTextColor, TextAnchor.MiddleCenter);
            healthTextMesh.alignment = TextAlignment.Center;
        }

        private void ConfigureTextMesh(TextMesh textMesh, Color color, TextAnchor anchor)
        {
            if (textMesh == null)
            {
                return;
            }

            textMesh.anchor = anchor;
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.1f;
            textMesh.color = color;

            if (builtInFont != null)
            {
                textMesh.font = builtInFont;
                var renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = builtInFont.material;
                    renderer.material.color = color;
                }
            }
        }

        private static Font ResolveBuiltInFont()
        {
            try
            {
                var legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (legacyFont != null)
                {
                    return legacyFont;
                }
            }
            catch (ArgumentException)
            {
            }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }
    }
}
