using System.Collections.Generic;
using UnityEngine;

namespace RorType.Gameplay.Combat
{
    public enum CombatRuntimeObjectKind
    {
        PlayerProjectile = 0,
        EnemyProjectile = 1,
        ResourcePickup = 2,
        FloatingText = 3,
        ExplosionEffect = 4
    }

    public static class CombatRuntimeBudget
    {
        private sealed class RuntimeObjectEntry
        {
            public GameObject GameObject;
        }

        private const int MaxPlayerProjectiles = 48;
        private const int MaxEnemyProjectiles = 96;
        private const int MaxResourcePickups = 80;
        private const int MaxFloatingTexts = 60;
        private const int MaxExplosionEffects = 16;

        private static readonly List<RuntimeObjectEntry> PlayerProjectiles = new();
        private static readonly List<RuntimeObjectEntry> EnemyProjectiles = new();
        private static readonly List<RuntimeObjectEntry> ResourcePickups = new();
        private static readonly List<RuntimeObjectEntry> FloatingTexts = new();
        private static readonly List<RuntimeObjectEntry> ExplosionEffects = new();

        public static void Register(GameObject gameObject, CombatRuntimeObjectKind kind)
        {
            if (gameObject == null)
            {
                return;
            }

            var entries = GetEntries(kind);
            CleanupDestroyed(entries);

            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index].GameObject == gameObject)
                {
                    return;
                }
            }

            entries.Add(new RuntimeObjectEntry { GameObject = gameObject });
            EnforceLimit(entries, GetLimit(kind));
        }

        private static List<RuntimeObjectEntry> GetEntries(CombatRuntimeObjectKind kind)
        {
            switch (kind)
            {
                case CombatRuntimeObjectKind.PlayerProjectile:
                    return PlayerProjectiles;
                case CombatRuntimeObjectKind.EnemyProjectile:
                    return EnemyProjectiles;
                case CombatRuntimeObjectKind.ResourcePickup:
                    return ResourcePickups;
                case CombatRuntimeObjectKind.FloatingText:
                    return FloatingTexts;
                case CombatRuntimeObjectKind.ExplosionEffect:
                    return ExplosionEffects;
                default:
                    return FloatingTexts;
            }
        }

        private static int GetLimit(CombatRuntimeObjectKind kind)
        {
            switch (kind)
            {
                case CombatRuntimeObjectKind.PlayerProjectile:
                    return MaxPlayerProjectiles;
                case CombatRuntimeObjectKind.EnemyProjectile:
                    return MaxEnemyProjectiles;
                case CombatRuntimeObjectKind.ResourcePickup:
                    return MaxResourcePickups;
                case CombatRuntimeObjectKind.FloatingText:
                    return MaxFloatingTexts;
                case CombatRuntimeObjectKind.ExplosionEffect:
                    return MaxExplosionEffects;
                default:
                    return MaxFloatingTexts;
            }
        }

        private static void CleanupDestroyed(List<RuntimeObjectEntry> entries)
        {
            for (var index = entries.Count - 1; index >= 0; index--)
            {
                if (entries[index].GameObject == null)
                {
                    entries.RemoveAt(index);
                }
            }
        }

        private static void EnforceLimit(List<RuntimeObjectEntry> entries, int limit)
        {
            while (entries.Count > limit)
            {
                var oldest = entries[0].GameObject;
                entries.RemoveAt(0);
                if (oldest != null)
                {
                    Object.Destroy(oldest);
                }
            }
        }
    }
}
