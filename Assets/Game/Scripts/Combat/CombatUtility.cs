using UnityEngine;

namespace RorType.Gameplay.Combat
{
    public static class CombatUtility
    {
        public static bool TryGetDamageable(Component source, out IDamageable damageable, out Component damageableComponent)
        {
            damageable = null;
            damageableComponent = null;

            if (source == null)
            {
                return false;
            }

            var current = source.transform;
            while (current != null)
            {
                var behaviours = current.GetComponents<MonoBehaviour>();
                for (var i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is not IDamageable candidate)
                    {
                        continue;
                    }

                    damageable = candidate;
                    damageableComponent = behaviours[i];
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        public static bool SharesRoot(GameObject gameObject, Component component)
        {
            if (gameObject == null || component == null)
            {
                return false;
            }

            return gameObject.transform.root == component.transform.root;
        }
    }
}
