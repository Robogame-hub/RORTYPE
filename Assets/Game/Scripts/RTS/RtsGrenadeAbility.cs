using UnityEngine;

namespace RorType.Gameplay.Rts
{
    [DisallowMultipleComponent]
    public sealed class RtsGrenadeAbility : RtsAbility
    {
        [SerializeField, Min(0.1f)] private float throwRange = 13f;
        [SerializeField, Min(0.1f)] private float blastRadius = 3.5f;
        [SerializeField, Min(0f)] private float blastDamage = 45f;

        protected override bool CanActivate(RtsUnit owner, Vector3 worldPoint)
        {
            var offset = worldPoint - owner.transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= throwRange * throwRange;
        }

        protected override void Activate(RtsUnit owner, Vector3 worldPoint)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "RtsGrenadeBlast";
            visual.transform.position = worldPoint + Vector3.up * 0.15f;
            visual.transform.localScale = Vector3.one * (blastRadius * 2f);
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.38f, 0.1f, 0.45f);
            }

            Destroy(visual, 0.12f);
            var units = RtsUnit.AllUnits;
            var squaredRadius = blastRadius * blastRadius;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null || !candidate.IsAlive || !owner.IsEnemyOf(candidate))
                {
                    continue;
                }

                var offset = candidate.transform.position - worldPoint;
                offset.y = 0f;
                if (offset.sqrMagnitude <= squaredRadius)
                {
                    candidate.TakeDamage(blastDamage, owner);
                }
            }
        }
    }
}
