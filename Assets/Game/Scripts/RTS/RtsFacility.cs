using UnityEngine;

namespace RorType.Gameplay.Rts
{
    [DisallowMultipleComponent]
    public sealed class RtsFacility : MonoBehaviour
    {
        [SerializeField] private RtsFacilityType facilityType = RtsFacilityType.TankFactory;
        [SerializeField] private RtsTeam owner = RtsTeam.Neutral;
        [SerializeField] private string displayName = "Tank Factory";
        [SerializeField, Min(1f)] private float captureRadius = 7f;
        [SerializeField, Min(0.1f)] private float captureDuration = 6f;
        [SerializeField, Min(0f)] private float ammoPerSecond = 12f;
        [SerializeField] private Renderer captureIndicator;

        private RtsTeam capturingTeam = RtsTeam.Neutral;
        private float captureProgress;
        private MaterialPropertyBlock propertyBlock;

        public RtsFacilityType FacilityType => facilityType;
        public RtsTeam Owner => owner;
        public string DisplayName => displayName;
        public float CaptureRadius => captureRadius;
        public float CaptureProgress => captureProgress;
        public RtsTeam CapturingTeam => capturingTeam;
        public bool IsPlayerControlled => owner == RtsTeam.Player;

        private void Awake()
        {
            captureIndicator ??= GetComponentInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            RefreshIndicator();
        }

        private void Update()
        {
            var presence = GetPresence();
            if (presence != RtsTeam.Neutral && presence != owner)
            {
                if (capturingTeam != presence)
                {
                    capturingTeam = presence;
                    captureProgress = 0f;
                }

                captureProgress += Time.deltaTime / captureDuration;
                if (captureProgress >= 1f)
                {
                    owner = capturingTeam;
                    captureProgress = 0f;
                    capturingTeam = RtsTeam.Neutral;
                    RefreshIndicator();
                }
            }
            else if (presence == RtsTeam.Neutral)
            {
                captureProgress = Mathf.Max(0f, captureProgress - Time.deltaTime / captureDuration);
                if (captureProgress <= 0f)
                {
                    capturingTeam = RtsTeam.Neutral;
                }
            }

            if (owner == RtsTeam.Player)
            {
                RefillNearbyPlayerUnits();
            }
        }

        private RtsTeam GetPresence()
        {
            var playerPresent = false;
            var enemyPresent = false;
            var squaredRadius = captureRadius * captureRadius;
            var units = RtsUnit.AllUnits;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || !unit.IsAlive || unit.Team == RtsTeam.Neutral)
                {
                    continue;
                }

                var offset = unit.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > squaredRadius)
                {
                    continue;
                }

                playerPresent |= unit.Team == RtsTeam.Player;
                enemyPresent |= unit.Team == RtsTeam.Enemy;
            }

            if (playerPresent == enemyPresent)
            {
                return RtsTeam.Neutral;
            }

            return playerPresent ? RtsTeam.Player : RtsTeam.Enemy;
        }

        private void RefillNearbyPlayerUnits()
        {
            var squaredRadius = captureRadius * captureRadius;
            var units = RtsUnit.AllUnits;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || !unit.IsAlive || unit.Team != RtsTeam.Player || unit.IsInCombat)
                {
                    continue;
                }

                var offset = unit.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= squaredRadius)
                {
                    unit.RefillAmmo(ammoPerSecond * Time.deltaTime);
                }
            }
        }

        private void RefreshIndicator()
        {
            if (captureIndicator == null)
            {
                return;
            }

            var color = owner == RtsTeam.Player
                ? new Color(0.22f, 0.8f, 0.42f)
                : owner == RtsTeam.Enemy
                    ? new Color(0.83f, 0.25f, 0.22f)
                    : new Color(0.8f, 0.64f, 0.2f);
            captureIndicator.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            captureIndicator.SetPropertyBlock(propertyBlock);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsPlayerControlled ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, captureRadius);
        }
    }
}
