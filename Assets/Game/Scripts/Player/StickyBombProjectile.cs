using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class StickyBombProjectile : MonoBehaviour
    {
        private const int ExplosionHitBufferSize = 32;

        private readonly Collider[] explosionHitBuffer = new Collider[ExplosionHitBufferSize];
        private readonly Component[] explosionUniqueHitBuffer = new Component[ExplosionHitBufferSize];

        private Rigidbody body;
        private float lifetime;
        private float fuseDuration;
        private float explosionVisualRadius;
        private float explosionDamageRadius;
        private float explosionDamage;
        private float explosionImpulse;
        private float explosionVisualLifetime;
        private Color explosionColor;
        private GameObject instigator;
        private Transform instigatorRoot;
        private CombatTeam sourceTeam;
        private float age;
        private float stuckTimer;
        private bool isInitialized;
        private bool isStuck;
        private bool hasExploded;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public void Initialize(
            Vector3 direction,
            float speed,
            float lifetimeSeconds,
            float fuseSeconds,
            float visualRadius,
            float damageRadius,
            float damageAmount,
            float impulse,
            float visualLifetime,
            Color color,
            GameObject sourceInstigator,
            CombatTeam team)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            var flightDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            transform.rotation = Quaternion.LookRotation(flightDirection, Vector3.up);

            lifetime = Mathf.Max(0.01f, lifetimeSeconds);
            fuseDuration = Mathf.Max(0.01f, fuseSeconds);
            explosionVisualRadius = Mathf.Max(0.1f, visualRadius);
            explosionDamageRadius = Mathf.Max(0.1f, damageRadius);
            explosionDamage = Mathf.Max(0f, damageAmount);
            explosionImpulse = Mathf.Max(0f, impulse);
            explosionVisualLifetime = Mathf.Max(0.05f, visualLifetime);
            explosionColor = color;
            instigator = sourceInstigator;
            instigatorRoot = sourceInstigator != null ? sourceInstigator.transform.root : null;
            sourceTeam = team;

            body.useGravity = false;
            body.isKinematic = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = flightDirection * speed;
            body.WakeUp();

            age = 0f;
            stuckTimer = 0f;
            isInitialized = true;
            isStuck = false;
            hasExploded = false;
            CombatRuntimeBudget.Register(gameObject, CombatRuntimeObjectKind.PlayerProjectile);
        }

        private void Update()
        {
            if (!isInitialized || hasExploded)
            {
                return;
            }

            if (isStuck)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= fuseDuration)
                {
                    Explode();
                }

                return;
            }

            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isInitialized || isStuck || hasExploded || collision == null)
            {
                return;
            }

            var hitCollider = collision.collider;
            var contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            TryStick(hitCollider, contactPoint);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isInitialized || isStuck || hasExploded || other == null)
            {
                return;
            }

            if (!CombatUtility.TryGetDamageable(other, out var damageable, out _) && other.isTrigger)
            {
                return;
            }

            TryStick(other, other.ClosestPoint(transform.position));
        }

        private void TryStick(Collider hitCollider, Vector3 hitPoint)
        {
            if (hitCollider == null)
            {
                return;
            }

            if (instigatorRoot != null && hitCollider.transform.root == instigatorRoot)
            {
                return;
            }

            if (hitCollider.GetComponentInParent<StickyBombProjectile>() != null)
            {
                return;
            }

            if (CombatUtility.TryGetDamageable(hitCollider, out var damageable, out var damageableComponent)
                && damageable.Team == sourceTeam)
            {
                return;
            }

            isStuck = true;
            stuckTimer = 0f;
            transform.position = hitPoint;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            if (CombatUtility.TryGetDamageable(hitCollider, out _, out var targetComponent))
            {
                transform.SetParent(targetComponent.transform, true);
            }
            else if (!hitCollider.isTrigger)
            {
                transform.SetParent(hitCollider.transform, true);
            }
        }

        private void Explode()
        {
            if (hasExploded)
            {
                return;
            }

            hasExploded = true;
            ApplyExplosionDamage();
            SpawnExplosionVisual();
            Destroy(gameObject);
        }

        private void ApplyExplosionDamage()
        {
            var center = transform.position;
            var hitCount = Physics.OverlapSphereNonAlloc(
                center,
                explosionDamageRadius,
                explosionHitBuffer,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            var uniqueHitCount = 0;

            for (var i = 0; i < hitCount && uniqueHitCount < explosionUniqueHitBuffer.Length; i++)
            {
                var hitCollider = explosionHitBuffer[i];
                if (hitCollider == null)
                {
                    continue;
                }

                if (!CombatUtility.TryGetDamageable(hitCollider, out var damageable, out var damageableComponent))
                {
                    continue;
                }

                if (!damageable.IsAlive || damageable.Team == sourceTeam || CombatUtility.SharesRoot(instigator, damageableComponent))
                {
                    continue;
                }

                var alreadyHit = false;
                for (var uniqueIndex = 0; uniqueIndex < uniqueHitCount; uniqueIndex++)
                {
                    if (explosionUniqueHitBuffer[uniqueIndex] == damageableComponent)
                    {
                        alreadyHit = true;
                        break;
                    }
                }

                if (alreadyHit)
                {
                    continue;
                }

                explosionUniqueHitBuffer[uniqueHitCount] = damageableComponent;
                uniqueHitCount++;

                var hitPoint = hitCollider.ClosestPoint(center);
                var hitDirection = damageableComponent.transform.position - center;
                hitDirection.y = 0f;
                if (hitDirection.sqrMagnitude <= 0.0001f)
                {
                    hitDirection = transform.forward;
                }

                damageable.ReceiveHit(new CombatHitInfo(
                    explosionDamage,
                    hitPoint,
                    hitDirection,
                    explosionImpulse,
                    instigator,
                    sourceTeam));
            }

            for (var i = 0; i < hitCount && i < explosionHitBuffer.Length; i++)
            {
                explosionHitBuffer[i] = null;
            }

            for (var i = 0; i < uniqueHitCount; i++)
            {
                explosionUniqueHitBuffer[i] = null;
            }
        }

        private void SpawnExplosionVisual()
        {
            var explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            explosion.name = "PlayerStickyBombExplosion";
            explosion.transform.position = transform.position;
            explosion.transform.localScale = Vector3.one * 0.1f;

            var explosionCollider = explosion.GetComponent<Collider>();
            if (explosionCollider != null)
            {
                explosionCollider.enabled = false;
                Destroy(explosionCollider);
            }

            var renderer = explosion.GetComponent<Renderer>();
            if (renderer != null)
            {
                RuntimeRendererUtility.SetColor(renderer, explosionColor);
            }

            var effect = explosion.AddComponent<TransientScaleEffect>();
            effect.Initialize(
                Vector3.one * 0.1f,
                Vector3.one * explosionVisualRadius,
                explosionVisualLifetime);
        }
    }
}
