using UnityEngine;
using Pantheum.Core;

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
        [SerializeField] private float _attackDamage = 10f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackCooldown = 1f;
        [SerializeField] private int _supplyCost = 1;

        private UnitBase _target;
        private float _attackTimer;

        public int SupplyCost => _supplyCost;

        protected override void OnDeath()
        {
            SupplyManager.Instance?.ReleaseSupply(_supplyCost);
            base.OnDeath();
        }

        public void SetTarget(UnitBase target) => _target = target;

        private void Update()
        {
            _attackTimer -= Time.deltaTime;

            if (_target == null || !_target.IsAlive)
            {
                _target = null;
                return;
            }

            float dist = Vector3.Distance(transform.position, _target.transform.position);
            if (dist > _attackRange)
            {
                MoveTo(_target.transform.position);
            }
            else
            {
                StopMoving();
                if (_attackTimer <= 0f)
                {
                    _target.TakeDamage(_attackDamage);
                    _attackTimer = _attackCooldown;
                }
            }
        }
    }
}
