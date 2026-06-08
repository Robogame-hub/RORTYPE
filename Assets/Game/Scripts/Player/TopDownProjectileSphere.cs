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
        private bool isInitialized;

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
            float recoverySharpness)
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
        }

        private void OnCollisionEnter(Collision collision)
        {
            Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            Destroy(gameObject);
        }
    }
}
