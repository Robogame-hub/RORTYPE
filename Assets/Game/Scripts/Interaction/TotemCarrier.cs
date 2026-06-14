using System.Collections.Generic;
using UnityEngine;

namespace RorType.Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class TotemCarrier : MonoBehaviour
    {
        [SerializeField, Min(0.25f)] private float orbitRadius = 1.35f;
        [SerializeField, Min(0f)] private float orbitHeight = 1.65f;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.18f;
        [SerializeField, Min(0f)] private float bobSpeed = 2.4f;
        [SerializeField, Min(0f)] private float followSharpness = 18f;
        [SerializeField, Min(0f)] private float orbitDegreesPerSecond = 80f;

        private readonly List<TotemPickup> carriedTotems = new();

        public int CarriedCount => carriedTotems.Count;

        private void LateUpdate()
        {
            for (var index = carriedTotems.Count - 1; index >= 0; index--)
            {
                if (carriedTotems[index] == null)
                {
                    carriedTotems.RemoveAt(index);
                }
            }

            var count = carriedTotems.Count;
            if (count == 0)
            {
                return;
            }

            var baseAngle = Time.time * orbitDegreesPerSecond;
            for (var index = 0; index < count; index++)
            {
                var totem = carriedTotems[index];
                if (totem == null)
                {
                    continue;
                }

                var angle = (baseAngle + (360f * index / count)) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius;
                offset.y = orbitHeight + Mathf.Sin(Time.time * bobSpeed + index) * bobAmplitude;
                totem.MoveAsCarried(transform.position + offset, followSharpness);
            }
        }

        public bool Add(TotemPickup totem)
        {
            if (totem == null || carriedTotems.Contains(totem))
            {
                return false;
            }

            carriedTotems.Add(totem);
            return true;
        }

        public TotemPickup RemoveForPlacement()
        {
            if (carriedTotems.Count == 0)
            {
                return null;
            }

            var lastIndex = carriedTotems.Count - 1;
            var totem = carriedTotems[lastIndex];
            carriedTotems.RemoveAt(lastIndex);
            return totem;
        }
    }
}
