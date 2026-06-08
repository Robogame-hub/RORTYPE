using RorType.Gameplay.Combat;
using UnityEngine;

namespace RorType.Gameplay.AI
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class EnemyProjectileSphere : MonoBehaviour
    {
        private Rigidbody body;
        private Vector3 baseScale;
        private float lifetime;
        private float scaleRecoverySharpness;
        private float knockbackForce;
        private float age;
        private bool isInitialized;
        private bool isConsumed;
        private Transform instigatorRoot;

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
            float knockbackImpulse,
            Transform instigator)
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
            knockbackForce = Mathf.Max(0f, knockbackImpulse);
            instigatorRoot = instigator;

            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = flightDirection * speed;
            body.WakeUp();

            age = 0f;
            isInitialized = true;
            isConsumed = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            ConsumeImpact(collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            ConsumeImpact(other);
        }

        private void ConsumeImpact(Collider other)
        {
            if (isConsumed)
            {
                return;
            }

            if (other != null && instigatorRoot != null && other.transform.root == instigatorRoot)
            {
                return;
            }

            isConsumed = true;

            if (other != null)
            {
                var knockbackReceiver = other.GetComponentInParent<IKnockbackReceiver>();
                if (knockbackReceiver != null)
                {
                    var impactDirection = body != null && body.linearVelocity.sqrMagnitude > 0.0001f
                        ? body.linearVelocity.normalized
                        : transform.forward;
                    knockbackReceiver.ApplyKnockback(impactDirection, knockbackForce);
                }
            }

            Destroy(gameObject);
        }
    }
}
