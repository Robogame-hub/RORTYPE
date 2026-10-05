using System.Collections.Generic;
using RorType.Gameplay.Player;
using UnityEngine;

namespace RorType.Gameplay.Rts
{
    [DisallowMultipleComponent]
    public sealed class RtsUnit : MonoBehaviour
    {
        private static readonly List<RtsUnit> ActiveUnits = new List<RtsUnit>();
        private static readonly string[] InfantryMuzzleNames =
        {
            "Muzzle",
            "MuzzlePoint",
            "FirePoint",
            "BarrelEnd",
            "Barrel",
            "Bolter",
            "BoltGun"
        };

        private static readonly int MoveSpeedParameter = Animator.StringToHash("MoveSpeed");
        private static readonly int MoveStrafeParameter = Animator.StringToHash("MoveStrafe");
        private static readonly int FireWeightParameter = Animator.StringToHash("FireWeight");
        private static readonly int UpperBodyFireState = Animator.StringToHash("Upper Body.Fire Overlay");

        [Header("Identity")]
        [SerializeField] private RtsTeam team = RtsTeam.Player;
        [SerializeField] private RtsUnitType unitType = RtsUnitType.Infantry;
        [SerializeField] private string displayName = "Infantry";

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Min(1f)] private float turnSpeed = 360f;
        [SerializeField, Min(0.05f)] private float destinationTolerance = 0.35f;
        [SerializeField, Min(0f)] private float helicopterAltitude = 9f;

        [Header("Local Avoidance")]
        [SerializeField, Min(0.1f)] private float avoidanceRadius = 1.15f;
        [SerializeField, Min(0f)] private float avoidanceStrength = 1.4f;
        [SerializeField, Min(0f)] private float avoidanceSideBias = 0.9f;

        [Header("Combat")]
        [SerializeField, Min(0f)] private float maxHealth = 100f;
        [SerializeField, Min(0.1f)] private float attackRange = 12f;
        [SerializeField, Min(0.1f)] private float sightRange = 20f;
        [SerializeField, Min(0.05f)] private float fireInterval = 0.3f;
        [SerializeField, Min(0f)] private float damagePerShot = 10f;
        [SerializeField, Min(0)] private int maxAmmo = 60;
        [SerializeField] private int ammo = -1;
        [SerializeField, Min(0f)] private float combatGraceSeconds = 3f;
        [SerializeField] private Color shotColor = new Color(1f, 0.7f, 0.18f);

        [Header("Presentation")]
        [SerializeField] private Transform selectionMarker;
        [SerializeField] private Transform visualRoot;

        [Header("Infantry Presentation")]
        [SerializeField] private RuntimeAnimatorController infantryAnimatorController;
        [SerializeField] private GameObject infantryMuzzleFlashPrefab;
        [SerializeField] private GameObject infantryImpactPrefab;
        [SerializeField] private Material infantryBoltTrailMaterial;
        [SerializeField, Min(0.01f)] private float infantryMuzzleFlashScale = 0.4f;
        [SerializeField, Min(0.01f)] private float infantryImpactScale = 0.35f;
        [SerializeField, Min(0.1f)] private float infantryProjectileSpeed = 28f;
        [SerializeField, Min(0.1f)] private float infantryProjectileLifetime = 1.4f;
        [SerializeField, Min(0.01f)] private float infantryProjectileRadius = 0.07f;

        private float health;
        private float lastCombatTime = float.NegativeInfinity;
        private float nextShotTime;
        private Vector3 destination;
        private RtsUnit target;
        private RtsCommandMode commandMode;
        private bool selected;
        private RtsTankVisual tankVisual;
        private RtsHelicopterVisual helicopterVisual;
        private Animator infantryAnimator;
        private Transform infantryMuzzle;
        private Vector3 infantryMoveDirection;
        private Vector3 infantryMovementGoal;
        private float infantryFireAnimationTimer;
        private int infantryUpperBodyLayer = -1;
        private bool infantryIsMoving;

        public static IReadOnlyList<RtsUnit> AllUnits => ActiveUnits;

        public RtsTeam Team => team;
        public RtsUnitType UnitType => unitType;
        public string DisplayName => displayName;
        public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        public float AttackRange => attackRange;
        public int Ammo => ammo;
        public int MaxAmmo => maxAmmo;
        public RtsCommandMode CommandMode => commandMode;
        public bool IsSelected => selected;
        public bool IsAlive => health > 0f && isActiveAndEnabled;
        public bool IsInCombat => Time.time < lastCombatTime + combatGraceSeconds;
        public bool IsControllableByPlayer => team == RtsTeam.Player && unitType != RtsUnitType.Target;
        public RtsUnit CurrentTarget => target;

        private void Awake()
        {
            health = Mathf.Max(1f, maxHealth);
            ammo = Mathf.Clamp(ammo < 0 ? maxAmmo : ammo, 0, maxAmmo);
            destination = transform.position;
            visualRoot ??= transform;
            selectionMarker ??= transform.Find("SelectionMarker");
            if (selectionMarker != null)
            {
                selectionMarker.gameObject.SetActive(false);
            }

            if (unitType == RtsUnitType.Infantry)
            {
                DisableInheritedInfantryPresentation();
                ConfigureInfantryPresentation();
            }

            tankVisual = GetComponent<RtsTankVisual>();
            helicopterVisual = GetComponent<RtsHelicopterVisual>();
            if (unitType == RtsUnitType.Helicopter)
            {
                var position = transform.position;
                position.y = Mathf.Max(position.y, helicopterAltitude);
                transform.position = position;
            }
        }

        private void OnEnable()
        {
            if (!ActiveUnits.Contains(this))
            {
                ActiveUnits.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveUnits.Remove(this);
        }

        private void Update()
        {
            if (!IsAlive || unitType == RtsUnitType.Target)
            {
                return;
            }

            UpdateTarget();
            if (target != null && target.IsAlive)
            {
                UpdateCombatAgainstTarget();
            }
            else if (commandMode == RtsCommandMode.ForcedMove || commandMode == RtsCommandMode.AssaultMove)
            {
                MoveTo(destination, false);
            }

            UpdateInfantryAnimation();
        }

        public void SetSelected(bool value)
        {
            selected = value;
            if (selectionMarker != null)
            {
                selectionMarker.gameObject.SetActive(value);
            }
        }

        public void IssueForcedMove(Vector3 worldPoint)
        {
            destination = KeepCurrentAltitude(worldPoint);
            target = null;
            commandMode = RtsCommandMode.ForcedMove;
        }

        public void IssueAssaultMove(Vector3 worldPoint)
        {
            destination = KeepCurrentAltitude(worldPoint);
            target = null;
            commandMode = RtsCommandMode.AssaultMove;
        }

        public void IssueAttack(RtsUnit enemy)
        {
            if (enemy == null || !IsEnemyOf(enemy))
            {
                return;
            }

            target = enemy;
            commandMode = RtsCommandMode.AttackTarget;
        }

        public void Stop()
        {
            target = null;
            destination = transform.position;
            commandMode = RtsCommandMode.Idle;
            StopInfantryMovement();
        }

        public void ApplyInfantryRootMotion(Vector3 animationDelta)
        {
            if (!IsAlive || unitType != RtsUnitType.Infantry || !infantryIsMoving)
            {
                return;
            }

            var remainingOffset = infantryMovementGoal - transform.position;
            remainingOffset.y = 0f;
            var remainingDistance = remainingOffset.magnitude;
            if (remainingDistance <= destinationTolerance)
            {
                StopInfantryMovement();
                return;
            }

            var rootMotionDistance = new Vector2(animationDelta.x, animationDelta.z).magnitude;
            if (rootMotionDistance <= 0.0001f)
            {
                return;
            }

            var movementDistance = Mathf.Min(rootMotionDistance, remainingDistance);
            transform.position += infantryMoveDirection * movementDistance;
            if (remainingDistance - movementDistance <= destinationTolerance)
            {
                StopInfantryMovement();
            }
        }

        public bool IsEnemyOf(RtsUnit other)
        {
            return other != null && other.team != RtsTeam.Neutral && other.team != team;
        }

        public void RefillAmmo(float amount)
        {
            if (amount <= 0f || ammo >= maxAmmo)
            {
                return;
            }

            ammo = Mathf.Min(maxAmmo, ammo + Mathf.CeilToInt(amount));
        }

        public void TakeDamage(float amount, RtsUnit source)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            health = Mathf.Max(0f, health - amount);
            lastCombatTime = Time.time;
            if (source != null && target == null && IsEnemyOf(source))
            {
                target = source;
                commandMode = RtsCommandMode.AttackTarget;
            }

            if (health <= 0f)
            {
                Destroy(gameObject);
            }
        }

        public RtsAbility[] GetAbilities()
        {
            return GetComponents<RtsAbility>();
        }

        private void UpdateTarget()
        {
            if (target != null && (!target.IsAlive || !IsEnemyOf(target)))
            {
                target = null;
                if (commandMode == RtsCommandMode.AttackTarget)
                {
                    commandMode = RtsCommandMode.Idle;
                }
            }

            if (target != null || commandMode == RtsCommandMode.ForcedMove)
            {
                return;
            }

            target = FindNearestEnemy(sightRange);
        }

        private void UpdateCombatAgainstTarget()
        {
            var horizontalOffset = target.transform.position - transform.position;
            horizontalOffset.y = 0f;
            var distance = horizontalOffset.magnitude;
            if (distance > attackRange)
            {
                if (commandMode == RtsCommandMode.AttackTarget || commandMode == RtsCommandMode.AssaultMove)
                {
                    MoveToCombatRange(target, horizontalOffset, distance);
                }
                return;
            }

            AimAt(horizontalOffset);
            TryFire(target);
            if (unitType == RtsUnitType.Helicopter)
            {
                StrafeAround(target, horizontalOffset, distance);
            }
        }

        private void MoveToCombatRange(RtsUnit enemy, Vector3 offset, float distance)
        {
            if (unitType == RtsUnitType.Helicopter)
            {
                var desiredDistance = Mathf.Max(attackRange * 0.78f, attackRange - 4f);
                var direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
                var orbitPoint = enemy.transform.position - direction * desiredDistance;
                MoveTo(KeepCurrentAltitude(orbitPoint), true);
                return;
            }

            var stopPoint = enemy.transform.position - offset.normalized * (attackRange * 0.92f);
            MoveTo(KeepCurrentAltitude(stopPoint), true);
        }

        private void StrafeAround(RtsUnit enemy, Vector3 offset, float distance)
        {
            var minimumDistance = attackRange * 0.58f;
            var direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
            Vector3 desired;
            if (distance < minimumDistance)
            {
                desired = transform.position + direction * 5f;
            }
            else
            {
                var tangent = Vector3.Cross(Vector3.up, direction).normalized;
                desired = transform.position + tangent * 4f;
            }

            MoveTo(KeepCurrentAltitude(desired), true);
        }

        private void MoveTo(Vector3 targetPosition, bool keepFacingTarget)
        {
            var offset = targetPosition - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= destinationTolerance * destinationTolerance)
            {
                StopInfantryMovement();
                if (commandMode == RtsCommandMode.ForcedMove || (commandMode == RtsCommandMode.AssaultMove && target == null))
                {
                    commandMode = RtsCommandMode.Idle;
                }
                return;
            }

            var direction = ResolveAvoidanceDirection(offset.normalized);
            if (unitType == RtsUnitType.Infantry && infantryAnimator != null)
            {
                infantryMovementGoal = targetPosition;
                infantryMoveDirection = direction;
                infantryIsMoving = true;
                AimAt(direction);
                return;
            }

            transform.position += direction * moveSpeed * Time.deltaTime;
            if (!keepFacingTarget || unitType != RtsUnitType.Tank)
            {
                AimAt(direction);
            }
        }

        private void AimAt(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (tankVisual != null)
            {
                tankVisual.AimAt(direction);
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        private void TryFire(RtsUnit enemy)
        {
            if (ammo <= 0 || Time.time < nextShotTime || enemy == null || !enemy.IsAlive)
            {
                return;
            }

            ammo--;
            nextShotTime = Time.time + fireInterval;
            lastCombatTime = Time.time;

            if (unitType == RtsUnitType.Infantry && infantryAnimator != null)
            {
                FireInfantryProjectile(enemy);
                return;
            }

            enemy.TakeDamage(damagePerShot, this);

            var from = tankVisual != null
                ? tankVisual.MuzzlePosition
                : helicopterVisual != null
                    ? helicopterVisual.MuzzlePosition
                    : transform.position + Vector3.up * 1.2f + transform.forward * 0.7f;
            var to = enemy.transform.position + Vector3.up * 1.1f;
            RtsShotTracer.Spawn(from, to, shotColor);
        }

        private RtsUnit FindNearestEnemy(float range)
        {
            RtsUnit closest = null;
            var shortestSquaredDistance = range * range;
            for (var i = 0; i < ActiveUnits.Count; i++)
            {
                var candidate = ActiveUnits[i];
                if (candidate == null || !candidate.IsAlive || !IsEnemyOf(candidate))
                {
                    continue;
                }

                var offset = candidate.transform.position - transform.position;
                offset.y = 0f;
                var squaredDistance = offset.sqrMagnitude;
                if (squaredDistance < shortestSquaredDistance)
                {
                    shortestSquaredDistance = squaredDistance;
                    closest = candidate;
                }
            }

            return closest;
        }

        private Vector3 KeepCurrentAltitude(Vector3 point)
        {
            point.y = unitType == RtsUnitType.Helicopter ? helicopterAltitude : transform.position.y;
            return point;
        }

        private Vector3 ResolveAvoidanceDirection(Vector3 desiredDirection)
        {
            if (desiredDirection.sqrMagnitude <= 0.0001f || avoidanceStrength <= 0f)
            {
                return desiredDirection;
            }

            var separation = Vector3.zero;
            var personalSpace = ResolvePersonalSpaceRadius();
            for (var i = 0; i < ActiveUnits.Count; i++)
            {
                var other = ActiveUnits[i];
                if (other == null || other == this || !other.IsAlive)
                {
                    continue;
                }

                var offsetFromOther = transform.position - other.transform.position;
                offsetFromOther.y = 0f;
                var distance = offsetFromOther.magnitude;
                var avoidanceDistance = personalSpace + other.ResolvePersonalSpaceRadius();
                if (distance >= avoidanceDistance)
                {
                    continue;
                }

                var away = distance > 0.001f
                    ? offsetFromOther / distance
                    : ResolveOverlapDirection(other);
                var pressure = 1f - Mathf.Clamp01(distance / avoidanceDistance);
                separation += away * (pressure * avoidanceStrength);

                var directionToOther = -away;
                var isAhead = Mathf.Max(0f, Vector3.Dot(desiredDirection, directionToOther));
                if (isAhead <= 0f || avoidanceSideBias <= 0f)
                {
                    continue;
                }

                var lateralDirection = Vector3.Cross(Vector3.up, desiredDirection).normalized;
                var pairKey = Mathf.Abs(GetInstanceID() ^ other.GetInstanceID());
                var sideSign = pairKey % 2 == 0 ? 1f : -1f;
                separation += lateralDirection * (pressure * isAhead * avoidanceSideBias * sideSign);
            }

            var steeredDirection = desiredDirection + separation;
            steeredDirection.y = 0f;
            return steeredDirection.sqrMagnitude > 0.0001f
                ? steeredDirection.normalized
                : desiredDirection;
        }

        private float ResolvePersonalSpaceRadius()
        {
            var typeScale = unitType switch
            {
                RtsUnitType.Tank => 1.7f,
                RtsUnitType.Helicopter => 1.9f,
                RtsUnitType.Target => 1.15f,
                _ => 1f
            };
            return avoidanceRadius * typeScale;
        }

        private Vector3 ResolveOverlapDirection(RtsUnit other)
        {
            var pairKey = GetInstanceID() ^ other.GetInstanceID();
            var axis = (pairKey & 1) == 0 ? Vector3.right : Vector3.forward;
            return GetInstanceID() < other.GetInstanceID() ? axis : -axis;
        }

        private void DisableInheritedInfantryPresentation()
        {
            // CHARACTER.prefab carries landing-smoke particles intended for another game mode.
            selectionMarker = null;
            var particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var particles = particleSystems[i];
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.gameObject.SetActive(false);
            }
        }

        private void ConfigureInfantryPresentation()
        {
            infantryAnimator = GetComponentInChildren<Animator>(true);
            if (infantryAnimator == null)
            {
                return;
            }

            if (infantryAnimatorController != null)
            {
                infantryAnimator.runtimeAnimatorController = infantryAnimatorController;
            }

            if (infantryAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            infantryAnimator.applyRootMotion = true;
            infantryAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            infantryUpperBodyLayer = infantryAnimator.GetLayerIndex("Upper Body");
            if (infantryUpperBodyLayer >= 0)
            {
                infantryAnimator.SetLayerWeight(infantryUpperBodyLayer, 0f);
            }

            infantryMuzzle = ResolveInfantryMuzzle();
        }

        private void UpdateInfantryAnimation()
        {
            if (unitType != RtsUnitType.Infantry || infantryAnimator == null ||
                infantryAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            var movement = Vector2.zero;
            if (infantryIsMoving && infantryMoveDirection.sqrMagnitude > 0.0001f)
            {
                var forward = transform.forward;
                forward.y = 0f;
                forward.Normalize();
                var right = Vector3.Cross(Vector3.up, forward);
                var direction = infantryMoveDirection.normalized;
                movement = new Vector2(
                    Vector3.Dot(direction, right),
                    Vector3.Dot(direction, forward));
            }

            infantryAnimator.SetFloat(MoveSpeedParameter, movement.y, 0.12f, Time.deltaTime);
            infantryAnimator.SetFloat(MoveStrafeParameter, movement.x, 0.12f, Time.deltaTime);
            infantryAnimator.SetFloat(
                FireWeightParameter,
                infantryFireAnimationTimer > 0f ? 1f : 0f,
                0.06f,
                Time.deltaTime);
            if (infantryUpperBodyLayer >= 0)
            {
                // The shared controller keeps locomotion on the base layer only.
                var fireWeight = infantryAnimator.GetFloat(FireWeightParameter);
                infantryAnimator.SetLayerWeight(
                    infantryUpperBodyLayer, fireWeight < 0.001f ? 0f : fireWeight);
            }
            infantryFireAnimationTimer = Mathf.Max(0f, infantryFireAnimationTimer - Time.deltaTime);
        }

        private void FireInfantryProjectile(RtsUnit enemy)
        {
            var muzzle = infantryMuzzle != null ? infantryMuzzle : ResolveInfantryMuzzle();
            if (muzzle != null)
            {
                infantryMuzzle = muzzle;
            }

            var from = muzzle != null
                ? muzzle.position
                : transform.position + Vector3.up * 1.2f + transform.forward * 0.7f;
            var targetPoint = enemy.transform.position + Vector3.up * 1.1f;
            var direction = targetPoint - from;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.forward;
            }
            direction.Normalize();

            TriggerInfantryFireAnimation();
            PlayerWeaponVfx.Spawn(
                infantryMuzzleFlashPrefab,
                from,
                Quaternion.LookRotation(direction, Vector3.up),
                infantryMuzzleFlashScale,
                muzzle,
                0.25f);
            RtsInfantryProjectile.Spawn(
                from,
                direction,
                enemy,
                this,
                damagePerShot,
                infantryProjectileSpeed,
                infantryProjectileLifetime,
                infantryProjectileRadius,
                infantryBoltTrailMaterial,
                infantryImpactPrefab,
                infantryImpactScale);
        }

        private void TriggerInfantryFireAnimation()
        {
            infantryFireAnimationTimer = Mathf.Max(fireInterval, 0.1f);
            if (infantryUpperBodyLayer < 0)
            {
                return;
            }

            infantryAnimator.SetLayerWeight(infantryUpperBodyLayer, 1f);
            infantryAnimator.SetFloat(FireWeightParameter, 1f);
            infantryAnimator.Play(UpperBodyFireState, infantryUpperBodyLayer, 0f);
        }

        private Transform ResolveInfantryMuzzle()
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var nameIndex = 0; nameIndex < InfantryMuzzleNames.Length; nameIndex++)
            {
                for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    if (string.Equals(transforms[transformIndex].name, InfantryMuzzleNames[nameIndex],
                            System.StringComparison.OrdinalIgnoreCase))
                    {
                        return transforms[transformIndex];
                    }
                }
            }

            return null;
        }

        private void StopInfantryMovement()
        {
            infantryIsMoving = false;
            infantryMoveDirection = Vector3.zero;
            infantryMovementGoal = transform.position;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = team == RtsTeam.Player ? Color.cyan : Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
