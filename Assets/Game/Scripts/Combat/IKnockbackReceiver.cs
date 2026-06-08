using UnityEngine;

namespace RorType.Gameplay.Combat
{
    public interface IKnockbackReceiver
    {
        void ApplyKnockback(Vector3 direction, float force);
    }
}
