using UnityEngine;
using UnityEngine.AI;
using Pantheum.Selection;
using Pantheum.UI;

namespace Pantheum.Units
{
    /// <summary>
    /// Base class for all units. Wraps NavMeshAgent movement and delegates
    /// selection events to the sibling Selectable component.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Selectable))]
    public abstract class UnitBase : MonoBehaviour, ISelectable
    {
        [Header("Unit")]
        [SerializeField] protected float _maxHealth = 100f;

        [Header("UI")]
        [SerializeField] private MonoBehaviour _healthDisplayProvider;

        protected float _currentHealth;
        protected NavMeshAgent _agent;
        protected Selectable _selectable;
        private IHealthDisplay _healthDisplay;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public bool IsAlive => _currentHealth > 0f;

        protected virtual void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _selectable = GetComponent<Selectable>();
            _selectable.OnSelectedEvent += OnSelected;
            _selectable.OnDeselectedEvent += OnDeselected;

            if (_healthDisplayProvider != null)
                _healthDisplay = _healthDisplayProvider as IHealthDisplay;

            _currentHealth = _maxHealth;
        }

        public virtual void TakeDamage(float amount)
        {
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
            if (_currentHealth <= 0f) OnDeath();
        }

        protected virtual void OnDeath() => Destroy(gameObject);

        public virtual void OnSelected() { }
        public virtual void OnDeselected() { }

        public void MoveTo(Vector3 destination)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        public void StopMoving() => _agent.isStopped = true;

        /// <summary>
        /// True when the agent has reached (or is very close to) its destination.
        /// </summary>
        protected bool HasArrived()
        {
            if (_agent.pathPending) return false;
            return _agent.remainingDistance <= _agent.stoppingDistance + 0.1f;
        }
    }
}
