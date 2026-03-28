using UnityEngine;
using UnityEngine.AI;
using Mirror;
using Pantheum.Core;
using Pantheum.Selection;
using Pantheum.UI;

namespace Pantheum.Units
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Selectable))]
    public abstract class UnitBase : NetworkBehaviour, ISelectable
    {
        [Header("Unit")]
        [SerializeField] protected float _maxHealth = 100f;

        [SerializeField] private Faction _faction = Faction.Player;

        [SyncVar(hook = nameof(OnHealthChanged))]
        protected float _currentHealth;

        protected NavMeshAgent  _agent;
        protected Selectable    _selectable;
        private   IHealthDisplay _healthDisplay;

        public float   MaxHealth     => _maxHealth;
        public float   CurrentHealth => _currentHealth;
        public bool    IsAlive       => _currentHealth > 0f;
        public Faction Faction       => _faction;

        protected static bool IsClientOnly => NetworkClient.active && !NetworkServer.active;

        public void SetFaction(Faction f) => _faction = f;

        protected virtual void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null)
                _agent.enabled = false;

            var nt = GetComponent<Mirror.NetworkTransformBase>();
            if (nt != null) nt.syncInterval = 0f;

            _selectable = GetComponent<Selectable>();
            _selectable.OnSelectedEvent   += OnSelected;
            _selectable.OnDeselectedEvent += OnDeselected;

            foreach (var mb in GetComponents<MonoBehaviour>())
                if (mb is IHealthDisplay hd) { _healthDisplay = hd; break; }

            _currentHealth = _maxHealth;
        }

        protected virtual void Start()
        {
            if (!NetworkClient.active && !NetworkServer.active && _agent != null)
                _agent.enabled = true;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_agent != null) _agent.enabled = true;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!isServer && _agent != null) _agent.enabled = false;
        }

        private void OnHealthChanged(float oldHP, float newHP)
        {
            _healthDisplay?.UpdateHealth(newHP, _maxHealth);
        }

        [Server]
        public virtual void TakeDamage(float amount)
        {
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            if (_currentHealth <= 0f) OnDeath();
        }

        [Server]
        protected virtual void OnDeath()
        {
            NetworkServer.Destroy(gameObject);
        }

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

        protected void RestoreHealth(float amount)
        {
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        }

        protected bool HasArrived()
        {
            if (_agent.pathPending) return false;
            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
            return _agent.remainingDistance <= _agent.stoppingDistance + 0.1f;
        }
    }
}
