using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class TopDownProjectileSphere : MonoBehaviour
    {
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
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = flightDirection * speed;
            body.WakeUp();

            age = 0f;
            isInitialized = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (TryApplyHit(collision.collider, collision.GetContact(0).point))
            {
                Destroy(gameObject);
                return;
            }

            Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
            {
                return;
            }

            if (TryApplyHit(other, other.ClosestPoint(transform.position)))
            {
                Destroy(gameObject);
                return;
            }

            if (other.isTrigger)
            {
                return;
            }

            Destroy(gameObject);
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
            var hitDirection = body != null && body.linearVelocity.sqrMagnitude > 0.0001f
                ? body.linearVelocity.normalized
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
