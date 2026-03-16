using UnityEngine;
using Pantheum.Selection;
using Pantheum.UI;

namespace Pantheum.Buildings
{
    public enum BuildingType
    {
        Castle,
        Barracks,
        Academy,
        Blacksmith,
        House,
        GoldMine,
        ManaExtractor
    }

    public enum CastleTier { T1 = 1, T2 = 2, T3 = 3 }

    /// <summary>
    /// Abstract base for every building. Handles health, death, and
    /// ISelectable delegation to the sibling Selectable component.
    /// Registers/unregisters itself with BuildingManager automatically.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public abstract class BuildingBase : MonoBehaviour, ISelectable
    {
        [Header("Building")]
        [SerializeField] protected BuildingType _buildingType;
        [SerializeField] protected float _maxHealth = 500f;

        [Header("UI")]
        [SerializeField] private MonoBehaviour _healthDisplayProvider;

        protected float _currentHealth;
        protected Selectable _selectable;
        private IHealthDisplay _healthDisplay;

        public BuildingType BuildingType => _buildingType;
        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public bool IsAlive => _currentHealth > 0f;

        protected virtual void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _selectable.OnSelectedEvent += OnSelected;
            _selectable.OnDeselectedEvent += OnDeselected;

            _healthDisplay = _healthDisplayProvider as IHealthDisplay;
            if (_healthDisplay == null)
                foreach (var mb in GetComponents<MonoBehaviour>())
                    if (mb is IHealthDisplay hd) { _healthDisplay = hd; break; }

            _currentHealth = _maxHealth;
            if (BuildingManager.Instance != null)
                BuildingManager.Instance.Register(this);
            else
                Debug.LogError($"[BuildingBase] BuildingManager not found in scene! Add it to the Managers GameObject. ({gameObject.name})");
        }

        protected virtual void OnDestroy()
        {
            if (BuildingManager.Instance != null)
                BuildingManager.Instance.Unregister(this);
        }

        public virtual void TakeDamage(float amount)
        {
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
            if (_currentHealth <= 0f) OnDeath();
        }

        /// <summary>Sets health to max and refreshes the health display.</summary>
        public void SetHealthFull()
        {
            _currentHealth = _maxHealth;
            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
        }

        protected virtual void OnDeath() => Destroy(gameObject);

        public virtual void OnSelected() { }
        public virtual void OnDeselected() { }
    }
}
