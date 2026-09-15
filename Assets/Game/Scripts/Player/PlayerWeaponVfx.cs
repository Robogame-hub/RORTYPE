using UnityEngine;

namespace RorType.Gameplay.Player
{
    public static class PlayerWeaponVfx
    {
        public static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation,
            float scale, Transform parent, float lifetime)
        {
            if (prefab == null) return;
            var effect = Object.Instantiate(prefab, position, rotation);
            effect.transform.localScale = Vector3.one * scale;
            // Keep world scale independent of imported weapon bones (often scaled by 100).
            if (parent != null) effect.transform.SetParent(parent, true);
            Object.Destroy(effect, lifetime);
        }
    }
}