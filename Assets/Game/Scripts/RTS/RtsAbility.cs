using UnityEngine;

namespace RorType.Gameplay.Rts
{
    public abstract class RtsAbility : MonoBehaviour
    {
        [SerializeField] private string displayName = "Ability";
        [SerializeField, Min(0.1f)] private float cooldown = 5f;

        private float readyAt;

        public string DisplayName => displayName;
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
        public bool IsReady => Time.time >= readyAt;

        public bool TryActivate(RtsUnit owner, Vector3 worldPoint)
        {
            if (owner == null || !owner.IsAlive || !IsReady || !CanActivate(owner, worldPoint))
            {
                return false;
            }

            Activate(owner, worldPoint);
            readyAt = Time.time + cooldown;
            return true;
        }

        protected virtual bool CanActivate(RtsUnit owner, Vector3 worldPoint)
        {
            return true;
        }

        protected abstract void Activate(RtsUnit owner, Vector3 worldPoint);
    }
}
