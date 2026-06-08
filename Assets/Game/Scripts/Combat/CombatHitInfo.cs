using UnityEngine;

namespace RorType.Gameplay.Combat
{
    public enum CombatTeam
    {
        Neutral = 0,
        Player = 1,
        Enemy = 2
    }

    public readonly struct CombatHitInfo
    {
        public CombatHitInfo(
            float damage,
            Vector3 point,
            Vector3 direction,
            float impulse,
            GameObject instigator,
            CombatTeam team)
        {
            Damage = Mathf.Max(0f, damage);
            Point = point;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            Impulse = Mathf.Max(0f, impulse);
            Instigator = instigator;
            Team = team;
        }

        public float Damage { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public float Impulse { get; }
        public GameObject Instigator { get; }
        public CombatTeam Team { get; }
    }

    public interface IDamageable
    {
        CombatTeam Team { get; }
        bool IsAlive { get; }
        bool ReceiveHit(in CombatHitInfo hitInfo);
    }
}
