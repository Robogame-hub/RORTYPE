using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class TopDownProjectileSphere : MonoBehaviour
    {
        private GameObject environmentImpactPrefab;
        private float environmentImpactScale;
        private TrailRenderer boltTrail;

        public void ConfigureBolterEffects(GameObject impactPrefab, float impactScale, Material trailMaterial)
        {
            environmentImpactPrefab = impactPrefab;
            environmentImpactScale = impactScale;
            if (trailMaterial == null) return;
            var trailObject = new GameObject("Bolt trail");
            trailObject.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
            trailObject.transform.SetParent(transform, true);
            boltTrail = trailObject.AddComponent<TrailRenderer>();
            boltTrail.sharedMaterial = trailMaterial;
            boltTrail.time = 0.12f;
            boltTrail.minVertexDistance = 0.04f;
            boltTrail.startWidth = 0.055f;
            boltTrail.endWidth = 0f;
            boltTrail.startColor = new Color(3f, 1.3f, 0.35f, 1f);
            boltTrail.endColor = new Color(1f, 0.2f, 0.02f, 0f);
            boltTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            boltTrail.receiveShadows = false;
        }

        private void OnDestroy()
        {
            if (boltTrail == null) return;
            boltTrail.emitting = false;
            boltTrail.transform.SetParent(null, true);
            Destroy(boltTrail.gameObject, boltTrail.time);
        }

        private void Impact(Collider other, Vector3 point, Vector3 normal)
        {
            if (isConsumed || other == null || (instigator != null && other.transform.root == instigator.transform.root))
                return;
            var isEnemy = CombatUtility.TryGetDamageable(other, out var target, out _) && target.Team == CombatTeam.Enemy;
            var accepted = TryApplyHit(other, point);
            if (!accepted && other.isTrigger) return;
            isConsumed = true;
            if (!isEnemy)
                PlayerWeaponVfx.Spawn(environmentImpactPrefab, point, Quaternion.LookRotation(normal),
                    environmentImpactScale, null, 2f);
            Destroy(gameObject);
        }

        private Rigidbody body;
        private Vector3 baseScale;
        private float lifetime;
        private float scaleRecoverySharpness;
        private float age;
        private float damage;
        private float impactImpulse;
        private GameObject instigator;
        private CombatTeam sourceTeam;
        private bool isInitialized;
        private bool isConsumed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            var blend = 1f - Mathf.Exp(-scaleRecoverySharpness * Time.deltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, blend);
        }

        public void Initialize(
            Vector3 direction,
            float speed,
            float lifetimeSeconds,
            float stretchMultiplier,
            float squashMultiplier,
            float recoverySharpness,
            float damageAmount,
            float impulse,
            GameObject sourceInstigator,
            CombatTeam team)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            var flightDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

            transform.rotation = Quaternion.LookRotation(flightDirection, Vector3.up);
            baseScale = transform.localScale;
            transform.localScale = new Vector3(
                baseScale.x * squashMultiplier,
                baseScale.y * squashMultiplier,
                baseScale.z * stretchMultiplier);

            lifetime = Mathf.Max(0.01f, lifetimeSeconds);
            scaleRecoverySharpness = Mathf.Max(0.01f, recoverySharpness);
            damage = Mathf.Max(0f, damageAmount);
            impactImpulse = Mathf.Max(0f, impulse);
            instigator = sourceInstigator;
            sourceTeam = team;

            body.useGravity = false;
            body.isKinematic = false;
            body.drag = 0f;
            body.angularDrag = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.velocity = flightDirection * speed;
            body.WakeUp();

            age = 0f;
            isInitialized = true;
            isConsumed = false;
            CombatRuntimeBudget.Register(gameObject, CombatRuntimeObjectKind.PlayerProjectile);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var contact = collision.GetContact(0);
            Impact(collision.collider, contact.point, contact.normal);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            var point = other.ClosestPoint(transform.position);
            var normal = transform.position - point;
            if (normal.sqrMagnitude < 0.0001f) normal = -transform.forward;
            Impact(other, point, normal.normalized);
        }
        private bool TryApplyHit(Collider other, Vector3 hitPoint)
        {
            if (isConsumed || other == null)
            {
                return false;
            }

            if (!CombatUtility.TryGetDamageable(other, out var damageable, out var damageableComponent))
            {
                return false;
            }

            if (!damageable.IsAlive || damageable.Team == sourceTeam || CombatUtility.SharesRoot(instigator, damageableComponent))
            {
                return false;
            }

            isConsumed = true;
            var hitDirection = body != null && body.velocity.sqrMagnitude > 0.0001f
                ? body.velocity.normalized
                : transform.forward;

            return damageable.ReceiveHit(new CombatHitInfo(
                damage,
                hitPoint,
                hitDirection,
                impactImpulse,
                instigator,
                sourceTeam));
        }
    }
}
