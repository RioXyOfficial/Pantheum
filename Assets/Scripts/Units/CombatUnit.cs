using UnityEngine;
using Pantheum.Buildings;
using Pantheum.Core;
using Pantheum.Selection;

namespace Pantheum.Units
{
    public class CombatUnit : UnitBase
    {
        [Header("Combat")]
        [SerializeField] private float _attackDamage        = 10f;
        [SerializeField] private float _attackRange         = 2f;
        [SerializeField] private float _detectionRange      = 8f;
        [SerializeField] private float _attackCooldown      = 1f;
        [SerializeField] private int   _supplyCost          = 1;
        [SerializeField] private float _bonusPerAttackLevel = 5f;
        [SerializeField] private float _armorPerLevel       = 2f;

        protected float TotalAttack => _attackDamage + Blacksmith.AttackLevel * _bonusPerAttackLevel;
        protected float AttackRange => _attackRange;

        private UnitBase     _target;
        private BuildingBase _buildingTarget;
        private float        _attackTimer;
        private float        _scanTimer;
        private Vector3      _lastKnownTargetPos;

        private bool    _isAttackMove;
        private Vector3 _attackMoveDestination;

        private const float MoveThresholdSq = 0.25f;
        private const float ScanInterval    = 0.5f;

        public int SupplyCost => _supplyCost;

        protected override void Awake()
        {
            base.Awake();
            _attackTimer = _attackCooldown;
            _scanTimer   = Random.Range(0f, ScanInterval);
        }

        public override void TakeDamage(float amount)
        {
            if (Faction == Faction.Player)
                amount = Mathf.Max(0f, amount - Blacksmith.ArmorLevel * _armorPerLevel);
            base.TakeDamage(amount);
        }

        protected override void OnDeath()
        {
            SupplyManager.Instance?.ReleaseSupply(_supplyCost);
            base.OnDeath();
        }

        public void OrderMove(Vector3 destination)
        {
            _target         = null;
            _buildingTarget = null;
            _isAttackMove   = false;
            MoveTo(destination);
        }

        public void OrderAttackMove(Vector3 destination)
        {
            _target                = null;
            _buildingTarget        = null;
            _isAttackMove          = true;
            _attackMoveDestination = destination;
            _scanTimer             = 0f;
            MoveTo(destination);
        }

        public void SetTarget(UnitBase target)
        {
            if (target == this) return;
            _buildingTarget = null;
            _target = target;
        }

        public void SetBuildingTarget(BuildingBase building)
        {
            _target         = null;
            _isAttackMove   = false;
            _buildingTarget = building;
            if (building != null)
                MoveTo(building.transform.position);
        }

        protected virtual void PerformAttack(UnitBase target)
        {
            target.TakeDamage(TotalAttack);
        }

        protected virtual void PerformAttackOnBuilding(BuildingBase building)
        {
            building.TakeDamage(TotalAttack);
        }

        private UnitBase ScanForTarget()
        {
            Faction enemy     = Faction == Faction.Player ? Faction.Enemy : Faction.Player;
            UnitBase nearest  = null;
            float nearestDist = _detectionRange;

            foreach (var sel in Selectable.All)
            {
                var unit = sel.GetComponent<UnitBase>();
                if (unit == null || !unit.IsAlive || unit.Faction != enemy) continue;
                float dist = Vector3.Distance(transform.position, unit.transform.position);
                if (dist < nearestDist) { nearestDist = dist; nearest = unit; }
            }
            return nearest;
        }

        private BuildingBase ScanForBuildingTarget()
        {
            Faction enemy        = Faction == Faction.Player ? Faction.Enemy : Faction.Player;
            BuildingBase nearest = null;
            float nearestDist    = _detectionRange;

            foreach (var sel in Selectable.All)
            {
                var building = sel.GetComponent<BuildingBase>();
                if (building == null || !building.IsAlive || building.Faction != enemy) continue;
                float dist = Vector3.Distance(transform.position, building.transform.position);
                if (dist < nearestDist) { nearestDist = dist; nearest = building; }
            }
            return nearest;
        }

        private void Update()
        {
            if (IsClientOnly) return;

            _attackTimer -= Time.deltaTime;
            _scanTimer   -= Time.deltaTime;

            if (_isAttackMove && _scanTimer <= 0f)
            {
                if (_target == null || !_target.IsAlive)
                    _target = ScanForTarget();
                if (_target == null && (_buildingTarget == null || !_buildingTarget.IsAlive))
                    _buildingTarget = ScanForBuildingTarget();
                _scanTimer = ScanInterval;
            }

            if (_target != null && _target.IsAlive)
            {
                float dist = Vector3.Distance(transform.position, _target.transform.position);
                if (dist > _attackRange)
                {
                    Vector3 tp = _target.transform.position;
                    if (Vector3.SqrMagnitude(tp - _lastKnownTargetPos) > MoveThresholdSq)
                    {
                        _lastKnownTargetPos = tp;
                        MoveTo(tp);
                    }
                }
                else
                {
                    StopMoving();
                    if (_attackTimer <= 0f)
                    {
                        PerformAttack(_target);
                        _attackTimer = _attackCooldown;
                    }
                }
            }
            else if (_buildingTarget != null && _buildingTarget.IsAlive)
            {
                _target = null;
                float dist = Vector3.Distance(transform.position, _buildingTarget.transform.position);
                if (dist > _attackRange)
                {
                    Vector3 bp = _buildingTarget.transform.position;
                    if (Vector3.SqrMagnitude(bp - _lastKnownTargetPos) > MoveThresholdSq)
                    {
                        _lastKnownTargetPos = bp;
                        MoveTo(bp);
                    }
                }
                else
                {
                    StopMoving();
                    if (_attackTimer <= 0f)
                    {
                        PerformAttackOnBuilding(_buildingTarget);
                        _attackTimer = _attackCooldown;
                    }
                }
            }
            else
            {
                _target         = null;
                _buildingTarget = null;
                if (_isAttackMove)
                {
                    if (_agent.isStopped || !_agent.hasPath ||
                        Vector3.SqrMagnitude(_agent.destination - _attackMoveDestination) > 0.25f)
                        MoveTo(_attackMoveDestination);
                }
            }
        }
    }
}
