using UnityEngine;
using UnityEngine.AI;
using Pantheum.Core;
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
        [SerializeField] private   Faction _faction = Faction.Player;

        protected float _currentHealth;
        protected NavMeshAgent _agent;
        protected Selectable _selectable;
        private IHealthDisplay _healthDisplay;

        public float   MaxHealth     => _maxHealth;
        public float   CurrentHealth => _currentHealth;
        public bool    IsAlive       => _currentHealth > 0f;
        public Faction Faction       => _faction;

        public void SetFaction(Faction f) => _faction = f;

        protected virtual void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _selectable = GetComponent<Selectable>();
            _selectable.OnSelectedEvent += OnSelected;
            _selectable.OnDeselectedEvent += OnDeselected;

            foreach (var mb in GetComponents<MonoBehaviour>())
                if (mb is IHealthDisplay hd) { _healthDisplay = hd; break; }

            _currentHealth = _maxHealth;
        }

        public virtual void TakeDamage(float amount)
        {
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
            if (_currentHealth <= 0f) OnDeath();
        }

        protected virtual void OnDeath() => Destroy(gameObject);

        public virtual void OnSelected()
        {
            (_healthDisplay as TempWorldUI)?.SetSelected(true);
            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
        }
        public virtual void OnDeselected()
        {
            (_healthDisplay as TempWorldUI)?.SetSelected(false);
        }

        public void MoveTo(Vector3 destination)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        public void StopMoving()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;
            _agent.isStopped = true;
        }

        /// <summary>
        /// True when the agent has reached (or is very close to) its destination,
        /// or when the path is impossible (avoids infinite state lock).
        /// </summary>
        protected bool HasArrived()
        {
            if (_agent.pathPending) return false;
            if (_agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"[UnitBase] {name}: chemin invalide — HasArrived retourne true pour éviter un blocage.");
                return true;
            }
            return _agent.remainingDistance <= _agent.stoppingDistance + 0.1f;
        }
    }
}
