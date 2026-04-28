using UnityEngine;
using UnityEngine.AI;
using Pantheum.Core;
using Pantheum.Selection;
using Pantheum.UI;
using Pantheum.Network;


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

    [System.Serializable]
    public struct TierVisualData
    {
        public Mesh        mesh;
        public Material[]  materials;
    }

    [RequireComponent(typeof(Selectable))]
    public abstract class BuildingBase : MonoBehaviour, ISelectable
    {
        [Header("Building")]
        [SerializeField] protected BuildingType _buildingType;
        [SerializeField] protected float        _maxHealth = 500f;
        [SerializeField] private   Vector2Int   _gridSize  = new(2, 2);
        [SerializeField] private   Faction      _faction   = Faction.Player;

        [Header("Tier Upgrade")]
        [SerializeField] private int _maxTier           = 1;
        [SerializeField] private int _upgradeCost       = 300;
        [Tooltip("Castle tier required to upgrade this building to tier 2. 0 = no requirement.")]
        [SerializeField] private int _castleReqForTier2 = 0;
        [Tooltip("Castle tier required to upgrade this building to tier 3. 0 = no requirement.")]
        [SerializeField] private int _castleReqForTier3 = 0;
        [Tooltip("One entry per tier (index 0 = T1, 1 = T2, 2 = T3). Leave empty to keep the same visual.")]
        [SerializeField] private TierVisualData[] _tierVisuals = System.Array.Empty<TierVisualData>();

        protected float        _currentHealth;
        protected Selectable   _selectable;
        protected bool         _registeredInManagers;
        private   IHealthDisplay _healthDisplay;

        public BuildingType BuildingType  => _buildingType;
        public float        MaxHealth     => _maxHealth;
        public float        CurrentHealth => _currentHealth;
        public bool         IsAlive       => _currentHealth > 0f;

        public int  CurrentTier { get; private set; } = 1;
        public int  MaxTier     => _maxTier;
        public int  UpgradeCost => _upgradeCost;

        public int NextTierCastleReq => CurrentTier == 1 ? _castleReqForTier2 : _castleReqForTier3;

        public bool UpgradeCastleReqMet =>
            NextTierCastleReq == 0
            || (BuildingManager.Instance?.GetCastleCount(NextTierCastleReq, Faction) ?? 0) > 0;

        public bool IsRegistered => _registeredInManagers;

        public bool CanUpgrade
        {
            get
            {
                var localFaction = PlayerNetworkController.LocalPlayer?.Faction;
                Debug.Log($"[CanUpgrade] {name} | MyFaction={Faction} | LocalFaction={localFaction} | Tier={CurrentTier} | MaxTier={_maxTier} | NextReq={NextTierCastleReq} | ReqMet={UpgradeCastleReqMet} | CastleCount={BuildingManager.Instance?.GetCastleCount(NextTierCastleReq)}");
                return CurrentTier < _maxTier && UpgradeCastleReqMet;
            }
        }
        public Vector2Int   GridSize      => _gridSize;
        public Faction      Faction       => _faction;

        public void SetFaction(Faction f) => _faction = f;

        protected virtual void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _selectable.OnSelectedEvent   += OnSelected;
            _selectable.OnDeselectedEvent += OnDeselected;

            foreach (var mb in GetComponents<MonoBehaviour>())
                if (mb is IHealthDisplay hd) { _healthDisplay = hd; break; }

            _currentHealth = _maxHealth;
            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
            ApplyTierVisual(CurrentTier);

            if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.Register(this);
                _registeredInManagers = true;
            }
            else
            {
                Debug.LogError($"[BuildingBase] BuildingManager introuvable. ({name})");
            }

            GridSystem.Instance?.Occupy(transform.position, _gridSize);

            var obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape   = NavMeshObstacleShape.Box;
            obstacle.size    = Vector3.one;
            obstacle.center  = Vector3.zero;
        }

        protected virtual void OnDestroy()
        {
            if (!_registeredInManagers) return;
            BuildingManager.Instance?.Unregister(this);
            GridSystem.Instance?.Release(transform.position, _gridSize);
        }

        public void ApplyTierFromNetwork(int tier)
        {
            CurrentTier = tier;
            ApplyTierVisual(tier);
            OnTierUpgraded(tier);
        }

        public virtual void StartConstruction()
        {
            if (_selectable != null)
            {
                _selectable.OnSelectedEvent   -= OnSelected;
                _selectable.OnDeselectedEvent -= OnDeselected;
            }
            GridSystem.Instance?.Release(transform.position, _gridSize);
            if (_registeredInManagers && this is Castle)
                BuildingManager.Instance?.RemoveCastleFromTierLists((Castle)this);
        }

        public virtual void CompleteConstruction()
        {
            if (_selectable != null)
            {
                _selectable.OnSelectedEvent   -= OnSelected;
                _selectable.OnDeselectedEvent -= OnDeselected;
                _selectable.OnSelectedEvent   += OnSelected;
                _selectable.OnDeselectedEvent += OnDeselected;
            }

            _healthDisplay = null;
            foreach (var mb in GetComponents<MonoBehaviour>())
                if (mb is IHealthDisplay hd) { _healthDisplay = hd; break; }

            if (_registeredInManagers)
            {
                BuildingManager.Instance?.RegisterCastleTier(this);
            }
            else if (BuildingManager.Instance != null)
            {
                BuildingManager.Instance.Register(this);
                _registeredInManagers = true;
            }

            SetHealthFull();
        }

        public virtual void CancelRegistration()
        {
            if (_selectable != null)
            {
                _selectable.OnSelectedEvent   -= OnSelected;
                _selectable.OnDeselectedEvent -= OnDeselected;
            }
            if (!_registeredInManagers) return;
            BuildingManager.Instance?.Unregister(this);
            GridSystem.Instance?.Release(transform.position, _gridSize);
            _registeredInManagers = false;
        }

        public void Upgrade()
        {
            if (!CanUpgrade) return;
            if (!ResourceManager.Instance.SpendGold(_upgradeCost)) return;
            CurrentTier++;
            ApplyTierVisual(CurrentTier);
            OnTierUpgraded(CurrentTier);
            Debug.Log($"[BuildingBase] {name} upgraded to tier {CurrentTier}.");

            if (Mirror.NetworkServer.active)
                GetComponent<NetworkFactionSync>()?.SetBuildingTier(CurrentTier);
        }

        protected virtual void OnTierUpgraded(int newTier) { }

        private void ApplyTierVisual(int tier)
        {
            if (_tierVisuals == null || _tierVisuals.Length == 0) return;
            int idx = tier - 1;
            if (idx < 0 || idx >= _tierVisuals.Length) return;
            var data = _tierVisuals[idx];

            var mf = GetComponentInChildren<MeshFilter>();
            var mr = GetComponentInChildren<MeshRenderer>();

            if (mf != null && data.mesh != null)
                mf.mesh = data.mesh;
            if (mr != null && data.materials != null && data.materials.Length > 0)
                mr.materials = data.materials;
        }

        public void Demolish() => OnDeath();

        public virtual void TakeDamage(float amount)
        {
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
            if (_currentHealth <= 0f) OnDeath();
        }

        public void SetHealthFull()
        {
            _currentHealth = _maxHealth;
            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
        }

        protected virtual void OnDeath()
        {
            Debug.Log($"[BuildingBase] {name} détruit.");
            if (Mirror.NetworkServer.active)
                Mirror.NetworkServer.Destroy(gameObject);
            else
                Destroy(gameObject);
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
    }
}
