using System.Collections;
using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.Environment
{
    [DisallowMultipleComponent]
    public sealed class ExplosiveBarrel : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int hitsToExplode = 3;
        [SerializeField, Min(1)] private int warningFlashCount = 3;
        [SerializeField, Min(0.01f)] private float warningFlashInterval = 0.14f;
        [SerializeField, Min(0.1f)] private float explosionRadius = 5f;
        [SerializeField, Min(0f)] private float explosionDamage = 3f;
        [SerializeField, Min(0.05f)] private float explosionVisualLifetime = 0.22f;
        [SerializeField] private Color baseColor = new Color(1f, 0.45f, 0.42f);
        [SerializeField] private Color warningColor = Color.red;

        private Renderer visualRenderer;
        private Collider barrelCollider;
        private int hitCount;
        private bool exploding;
        private readonly Collider[] overlapBuffer = new Collider[32];
        private readonly Component[] uniqueDamageables = new Component[32];

        public CombatTeam Team => CombatTeam.Neutral;
        public bool IsAlive => !exploding;

        private void Awake()
        {
            ResolveAuthoredVisual();
            ApplyColor(baseColor);
        }

        public bool ReceiveHit(in CombatHitInfo hitInfo)
        {
            if (exploding || hitInfo.Team != CombatTeam.Player)
            {
                return false;
            }

            hitCount++;
            FloatingWorldText.Spawn(hitInfo.Point + Vector3.up * 0.45f, $"{hitCount}/{hitsToExplode}", warningColor, 0.1f);
            if (hitCount >= hitsToExplode)
            {
                StartCoroutine(ExplodeAfterWarning());
            }

            return true;
        }

        private IEnumerator ExplodeAfterWarning()
        {
            exploding = true;
            if (barrelCollider != null)
            {
                barrelCollider.enabled = false;
            }

            for (var i = 0; i < warningFlashCount; i++)
            {
                ApplyColor(warningColor);
                yield return new WaitForSeconds(warningFlashInterval);
                ApplyColor(baseColor);
                yield return new WaitForSeconds(warningFlashInterval);
            }

            ApplyExplosionDamage();
            SpawnExplosionVisual();
            Destroy(gameObject);
        }

        private void ApplyExplosionDamage()
        {
            var center = transform.position + Vector3.up;
            var hitCount = Physics.OverlapSphereNonAlloc(
                center,
                explosionRadius,
                overlapBuffer,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            var uniqueCount = 0;

            for (var i = 0; i < hitCount && uniqueCount < uniqueDamageables.Length; i++)
            {
                var hitCollider = overlapBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                if (!CombatUtility.TryGetDamageable(hitCollider, out var damageable, out var damageableComponent))
                {
                    continue;
                }

                if (!damageable.IsAlive || !IsExplosionDamageTarget(damageable, damageableComponent))
                {
                    continue;
                }

                var alreadyHit = false;
                for (var uniqueIndex = 0; uniqueIndex < uniqueCount; uniqueIndex++)
                {
                    if (uniqueDamageables[uniqueIndex] == damageableComponent)
                    {
                        alreadyHit = true;
                        break;
                    }
                }

                if (alreadyHit)
                {
                    continue;
                }

                uniqueDamageables[uniqueCount] = damageableComponent;
                uniqueCount++;

                var hitPoint = hitCollider.ClosestPoint(center);
                var hitDirection = damageableComponent.transform.position - center;
                hitDirection.y = 0f;
                if (hitDirection.sqrMagnitude <= 0.0001f)
                {
                    hitDirection = transform.forward;
                }

                var explosionHit = new CombatHitInfo(
                    explosionDamage,
                    hitPoint,
                    hitDirection,
                    0f,
                    gameObject,
                    CombatTeam.Neutral);

                if (damageable is DestructibleCover destructibleCover)
                {
                    destructibleCover.DestroyImmediately(explosionHit);
                    continue;
                }

                damageable.ReceiveHit(explosionHit);
            }

            for (var i = 0; i < hitCount && i < overlapBuffer.Length; i++)
            {
                overlapBuffer[i] = null;
            }

            for (var i = 0; i < uniqueCount; i++)
            {
                uniqueDamageables[i] = null;
            }
        }

        private bool IsExplosionDamageTarget(IDamageable damageable, Component damageableComponent)
        {
            if (damageableComponent == this)
            {
                return false;
            }

            return damageable.Team == CombatTeam.Enemy || damageable.Team == CombatTeam.Neutral;
        }

        private void SpawnExplosionVisual()
        {
            var explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            explosion.name = "BarrelExplosion";
            explosion.transform.position = transform.position + Vector3.up;
            explosion.transform.localScale = Vector3.one * 0.1f;

            var collider = explosion.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            var renderer = explosion.GetComponent<Renderer>();
            if (renderer != null)
            {
                RuntimeRendererUtility.SetColor(renderer, warningColor);
            }

            var effect = explosion.AddComponent<TransientScaleEffect>();
            effect.Initialize(Vector3.one * 0.1f, Vector3.one * (explosionRadius * 2f), explosionVisualLifetime);
        }

        private void ResolveAuthoredVisual()
        {
            visualRenderer = GetComponentInChildren<Renderer>();
            barrelCollider = GetComponentInChildren<Collider>();
            if (visualRenderer != null && barrelCollider != null)
            {
                return;
            }

            Debug.LogWarning($"{nameof(ExplosiveBarrel)} on {name} is missing authored visual renderer or collider in the prefab.", this);
        }

        private void ApplyColor(Color color)
        {
            if (visualRenderer == null)
            {
                return;
            }

            RuntimeRendererUtility.SetColor(visualRenderer, color);
        }

        private void OnValidate()
        {
            hitsToExplode = Mathf.Max(1, hitsToExplode);
            warningFlashCount = Mathf.Max(1, warningFlashCount);
            warningFlashInterval = Mathf.Max(0.01f, warningFlashInterval);
            explosionRadius = Mathf.Max(0.1f, explosionRadius);
            explosionDamage = Mathf.Max(0f, explosionDamage);
            explosionVisualLifetime = Mathf.Max(0.05f, explosionVisualLifetime);
        }
    }
}
