using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Mirror;
using Pantheum.Buildings;
using Pantheum.Network;
using Pantheum.Selection;
using Pantheum.Core;
using Pantheum.UI;
using Pantheum.Units;

namespace Pantheum.Construction
{
    public class ConstructionSite : NetworkBehaviour
    {
        [Header("Defaults")]
        [SerializeField] private GameObject _fallbackBuildingPrefab;
        [SerializeField] private float _maxHealth = 500f;
        [SerializeField] private float _buildRate = 50f;
        [SerializeField] private Vector2Int _gridSize = new(2, 2);

        [Header("Ghost")]
        [SerializeField] private Material _ghostMaterial;
        [SerializeField] private Color _ghostTint = new(0.4f, 0.7f, 1f, 0.5f);

        [Header("UI")]
        [SerializeField] private MonoBehaviour _healthDisplayProvider;
        [SerializeField] private MonoBehaviour _progressDisplayProvider;

        private static readonly int s_baseColorId = Shader.PropertyToID("_BaseColor");

        private GameObject _ghostVisual;
        private GameObject _realVisual;
        private float _pivotToBottom;
        private Renderer[] _originalRenderers;

        private GameObject _buildingPrefab;

        [SyncVar(hook = nameof(OnHealthChanged))]
        private float _currentHealth;

        [SyncVar(hook = nameof(OnBuildProgressChanged))]
        private float _buildProgress;

        [SyncVar(hook = nameof(OnConstructionCompleteChanged))]
        private bool _constructionComplete;

        [SyncVar]
        private int _buildingTypeIndex = -1;

        private bool _initialized;
        private bool _gridOccupied;
        private int _goldCost;
        private IHealthDisplay _healthDisplay;
        private IProgressDisplay _progressDisplay;
        private NetworkFactionSync _networkFactionSync;

        private readonly HashSet<WorkerController> _workers = new();

        public bool IsComplete => _buildProgress >= 1f;
        public float BuildProgress => _buildProgress;
        public Vector2Int GridSize => _gridSize;
        public int GoldCost => _goldCost;

        private void OnHealthChanged(float oldValue, float newValue)
        {
            _healthDisplay?.UpdateHealth(newValue, _maxHealth);
        }

        private void OnBuildProgressChanged(float oldValue, float newValue)
        {
            _progressDisplay?.UpdateProgress(newValue);
            UpdateRealVisual(newValue);
        }

        private void OnConstructionCompleteChanged(bool oldValue, bool newValue)
        {
            if (!newValue) return;

            if (_ghostVisual != null) Destroy(_ghostVisual);
            if (_realVisual != null) Destroy(_realVisual);

            foreach (var r in _originalRenderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(null);
                r.enabled = true;
            }

            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is ConstructionSite) continue;
                if (mb is NetworkBehaviour) continue;
                mb.enabled = true;
            }

            foreach (var bb in GetComponentsInChildren<BuildingBase>(true))
                bb.CompleteConstruction();

            Destroy(this);
        }

        public void Init(GameObject buildingPrefab,
                         Material ghostMaterial = null,
                         float maxHealth = -1f, float buildRate = -1f,
                         int goldCost = 0, int typeIndex = -1)
        {
            _buildingPrefab = buildingPrefab;
            _buildingTypeIndex = typeIndex;

            var bb = buildingPrefab != null ? buildingPrefab.GetComponent<BuildingBase>() : null;
            if (bb != null) _gridSize = bb.GridSize;

            if (ghostMaterial != null) _ghostMaterial = ghostMaterial;
            if (maxHealth > 0f) _maxHealth = maxHealth;
            if (buildRate > 0f) _buildRate = buildRate;

            _goldCost = goldCost;

            GridSystem.Instance?.Occupy(transform.position, _gridSize);
            _gridOccupied = true;
        }

        public void CancelConstruction()
        {
            if (_constructionComplete) return;

            int refund = _goldCost / 2;
            if (refund > 0)
                ResourceManager.Instance?.DepositGold(refund);

            foreach (var worker in _workers)
                worker?.NotifyComplete();
            _workers.Clear();

            Destroy(gameObject);
        }

        private void Awake()
        {
            _healthDisplay = _healthDisplayProvider as IHealthDisplay;
            _progressDisplay = _progressDisplayProvider as IProgressDisplay;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isServer)
            {
                var nfs = GetComponent<NetworkFactionSync>();
                if (nfs == null || !nfs.IsUnderConstruction)
                {
                    Destroy(this);
                    return;
                }
            }
        }

        private void Start()
        {
            _networkFactionSync = GetComponent<NetworkFactionSync>();
            if (_networkFactionSync != null && !_networkFactionSync.IsUnderConstruction)
            {
                Destroy(this);
                return;
            }

            var tempUI = GetComponent<TempWorldUI>();
            if (tempUI == null)
                tempUI = gameObject.AddComponent<TempWorldUI>();

            tempUI.enabled = true;
            _healthDisplay = tempUI;
            _progressDisplay = tempUI;

            if (NetworkClient.active && !NetworkServer.active && _buildingPrefab == null && _buildingTypeIndex >= 0)
                _buildingPrefab = PlayerNetworkController.LocalPlayer?.GetBuildingPrefab(_buildingTypeIndex);

            if (_buildingPrefab == null)
            {
                _buildingPrefab = _fallbackBuildingPrefab;
                if (!_gridOccupied)
                {
                    GridSystem.Instance?.Occupy(transform.position, _gridSize);
                    _gridOccupied = true;
                }
            }

            if (_buildingPrefab == null)
            {
                Debug.LogError("[ConstructionSite] No buildingPrefab. Call Init() or assign _fallbackBuildingPrefab.");
                return;
            }

            _originalRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in _originalRenderers)
                r.enabled = false;

            SpawnVisuals();

            bool isServerSide = NetworkServer.active || !NetworkClient.active;
            if (isServerSide)
            {
                if (!_gridOccupied)
                {
                    GridSystem.Instance?.Occupy(transform.position, _gridSize);
                    _gridOccupied = true;
                }

                SnapToGround();
                RpcSyncPosition(transform.position);

                var obstacle = GetComponent<NavMeshObstacle>() ?? gameObject.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
                obstacle.center = Vector3.zero;

                _currentHealth = 0f;
                _buildProgress = 0f;
            }

            _healthDisplay?.UpdateHealth(0f, _maxHealth);
            _progressDisplay?.UpdateProgress(0f);
            UpdateRealVisual(0f);
            _initialized = true;
        }

        [ClientRpc]
        private void RpcSyncPosition(Vector3 pos)
        {
            if (!isServer) transform.position = pos;
        }

        private void OnDestroy()
        {
            if (!_constructionComplete && _gridOccupied)
                GridSystem.Instance?.Release(transform.position, _gridSize);
        }

        public void AssignWorker(WorkerController worker) => _workers.Add(worker);
        public void RemoveWorker(WorkerController worker) => _workers.Remove(worker);

        public bool IsPrimaryBuilder(WorkerController worker)
        {
            foreach (var w in _workers)
                return w == worker;
            return false;
        }

        public void Tick(float deltaTime)
        {
            if (NetworkClient.active && !NetworkServer.active) return;
            if (!_initialized || IsComplete) return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + _buildRate * deltaTime);
            _buildProgress = _currentHealth / _maxHealth;

            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
            _progressDisplay?.UpdateProgress(_buildProgress);
            UpdateRealVisual(_buildProgress);

            if (IsComplete)
                Complete();
        }

        private void SpawnVisuals()
        {
            _ghostVisual = ClonePrefab(_buildingPrefab);
            _ghostVisual.transform.SetParent(transform, true);
            _ghostVisual.transform.localPosition = Vector3.zero;
            _ghostVisual.transform.localRotation = Quaternion.identity;
            _ghostVisual.transform.localScale = Vector3.one * 0.999f;

            if (_ghostMaterial != null)
                foreach (var r in _ghostVisual.GetComponentsInChildren<Renderer>())
                    r.material = _ghostMaterial;

            var ghostBlock = new MaterialPropertyBlock();
            ghostBlock.SetColor(s_baseColorId, _ghostTint);
            foreach (var r in _ghostVisual.GetComponentsInChildren<Renderer>())
                r.SetPropertyBlock(ghostBlock);

            _realVisual = ClonePrefab(_buildingPrefab);
            _realVisual.transform.SetParent(transform, true);
            _realVisual.transform.localPosition = Vector3.zero;
            _realVisual.transform.localRotation = Quaternion.identity;

            var renderers = _realVisual.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                float minY = float.MaxValue;
                foreach (var r in renderers)
                    minY = Mathf.Min(minY, r.bounds.min.y);
                _pivotToBottom = _realVisual.transform.position.y - minY;
            }
        }

        private static GameObject ClonePrefab(GameObject prefab)
        {
            bool wasActive = prefab.activeSelf;
            prefab.SetActive(false);
            var go = Instantiate(prefab, new Vector3(99999f, 0f, 99999f), Quaternion.identity);
            prefab.SetActive(wasActive);

            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                DestroyImmediate(mb);
            foreach (var ni in go.GetComponentsInChildren<NetworkIdentity>(true))
                DestroyImmediate(ni);
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                DestroyImmediate(rb);

            go.SetActive(true);
            return go;
        }

        private void SnapToGround()
        {
            var ownColliders = GetComponentsInChildren<Collider>();
            foreach (var c in ownColliders) c.enabled = false;

            Ray ray = new Ray(transform.position + Vector3.up * 100f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
                transform.position = new Vector3(transform.position.x, hit.point.y + _pivotToBottom, transform.position.z);

            foreach (var c in ownColliders) c.enabled = true;
        }

        private void UpdateRealVisual(float t)
        {
            if (_realVisual == null) return;

            float scale = Mathf.Max(0.001f, t);
            float localY = _pivotToBottom * (t - 1f);

            _realVisual.transform.localScale = new Vector3(1f, scale, 1f);
            _realVisual.transform.localPosition = new Vector3(0f, localY, 0f);
        }

        private void Complete()
        {
            _constructionComplete = true;

            if (NetworkServer.active && _networkFactionSync != null)
                _networkFactionSync.SetUnderConstruction(false);

            if (!NetworkServer.active)
                OnConstructionCompleteChanged(false, true);

            foreach (var worker in _workers)
                worker?.NotifyComplete();
            _workers.Clear();

            Destroy(this);
        }
    }
}