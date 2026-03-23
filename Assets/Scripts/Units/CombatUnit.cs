using UnityEngine;
using Pantheum.Core;
using Pantheum.Buildings;

namespace Pantheum.Units
{
    /// <summary>
    /// A unit that attacks enemy targets and consumes supply.
    /// Supply is claimed at production time (UnitProduction.TryEnqueue) and
    /// released on death here.
    /// </summary>
    public class CombatUnit : UnitBase
    {
        [Header("Combat")]
        [SerializeField] private float _attackDamage    = 10f;
        [SerializeField] private float _attackRange     = 2f;
        [SerializeField] private float _attackCooldown  = 1f;
        [SerializeField] private int   _supplyCost      = 1;
        [SerializeField] private float _bonusPerAttackLevel = 5f;
        [SerializeField] private float _armorPerLevel       = 2f;

        private float TotalAttack => _attackDamage + Blacksmith.AttackLevel * _bonusPerAttackLevel;

        private UnitBase _target;
        private float _attackTimer;
        private Vector3 _lastKnownTargetPos;
        private const float MoveThresholdSq = 0.25f;

        public int SupplyCost => _supplyCost;

        protected override void Awake()
        {
            base.Awake();
            _attackTimer = _attackCooldown;
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

        public void SetTarget(UnitBase target)
        {
            if (target == this) return;
            _target = target;
        }

        private void Update()
        {
            _attackTimer -= Time.deltaTime;

            if (_target == null || !_target.IsAlive)
            {
                _target = null;
                StopMoving();
                return;
            }

            float dist = Vector3.Distance(transform.position, _target.transform.position);
            if (dist > _attackRange)
            {
                Vector3 targetPos = _target.transform.position;
                if (Vector3.SqrMagnitude(targetPos - _lastKnownTargetPos) > MoveThresholdSq)
                {
                    _lastKnownTargetPos = targetPos;
                    MoveTo(targetPos);
                }
            }
            else
            {
                StopMoving();
                if (_attackTimer <= 0f)
                {
                    _target.TakeDamage(TotalAttack);
                    _attackTimer = _attackCooldown;
                }
            }
        }
    }
}
