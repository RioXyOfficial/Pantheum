using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Pantheum.Buildings;
using Pantheum.Selection;
using Pantheum.Core;
using Pantheum.UI;
using Pantheum.Units;

namespace Pantheum.Construction
{
    // Attach this to a minimal prefab (Collider + Selectable + TempWorldUI).
    // One shared prefab for all building types.
    // Call Init() immediately after Instantiate, before the first Update.
    public class ConstructionSite : MonoBehaviour
    {
        [Header("Defaults (fallback when Init() is not called)")]
        [SerializeField] private GameObject _fallbackBuildingPrefab;
        [SerializeField] private float      _maxHealth = 500f;
        [SerializeField] private float      _buildRate = 50f;
        [SerializeField] private Vector2Int _gridSize  = new(2, 2);

        [Header("Ghost outline material (same as BuildingPlacer._ghostMaterial)")]
        [SerializeField] private Material _ghostMaterial;
        [SerializeField] private Color    _ghostTint = new(0.4f, 0.7f, 1f, 0.5f);

        [Header("UI")]
        [SerializeField] private MonoBehaviour _healthDisplayProvider;
        [SerializeField] private MonoBehaviour _progressDisplayProvider;

        private static readonly int s_baseColorId = Shader.PropertyToID("_BaseColor");

        private GameObject _ghostVisual;      // full-size blue transparent outline — always visible
        private GameObject _realVisual;       // real material, grows from bottom up
        private float      _pivotToBottom;
        private Renderer[] _originalRenderers; // siteGO renderers captured before SpawnVisuals

        private GameObject  _buildingPrefab;
        private float       _currentHealth;
        private float       _buildProgress;
        private bool        _initialized;
        private bool        _constructionComplete;
        private bool        _gridOccupied;
        private int         _goldCost;
        private IHealthDisplay   _healthDisplay;
        private IProgressDisplay _progressDisplay;

        private readonly HashSet<WorkerController> _workers = new();

        public bool      IsComplete    => _buildProgress >= 1f;
        public float     BuildProgress => _buildProgress;
        public Vector2Int GridSize     => _gridSize;
        public int       GoldCost      => _goldCost;

        // Called by BuildingPlacer right after Instantiate, before Start().
        public void Init(GameObject buildingPrefab,
                         Material ghostMaterial = null,
                         float maxHealth = -1f, float buildRate = -1f,
                         int goldCost = 0)
        {
            _buildingPrefab = buildingPrefab;
            var bb = buildingPrefab != null ? buildingPrefab.GetComponent<BuildingBase>() : null;
            if (bb != null) _gridSize = bb.GridSize;
            if (ghostMaterial != null) _ghostMaterial = ghostMaterial;
            if (maxHealth > 0f) _maxHealth = maxHealth;
            if (buildRate  > 0f) _buildRate  = buildRate;
            _goldCost = goldCost;

            GridSystem.Instance?.Occupy(transform.position, _gridSize);
            _gridOccupied = true;
        }

        /// <summary>
        /// Cancels construction, refunds half the gold cost, and destroys the site.
        /// </summary>
        public void CancelConstruction()
        {
            if (_constructionComplete) return;

            int refund = _goldCost / 2;
            if (refund > 0)
                ResourceManager.Instance?.DepositGold(refund);

            foreach (var worker in _workers)
                worker?.NotifyComplete(); // workers return to idle
            _workers.Clear();

            Destroy(gameObject);
        }

        private void Awake()
        {
            _healthDisplay   = _healthDisplayProvider   as IHealthDisplay;
            _progressDisplay = _progressDisplayProvider as IProgressDisplay;

            // Only pick components that are actually enabled — the host building prefab
            // may have a disabled TempWorldUI that must not be used.
            if (_healthDisplay == null || _progressDisplay == null)
                foreach (var mb in GetComponents<MonoBehaviour>())
                {
                    if (!mb.enabled) continue;
                    if (_healthDisplay   == null && mb is IHealthDisplay   hd) _healthDisplay   = hd;
                    if (_progressDisplay == null && mb is IProgressDisplay pd) _progressDisplay = pd;
                }

            // Still nothing found — add a fresh TempWorldUI.
            if (_healthDisplay == null || _progressDisplay == null)
            {
                var ui = gameObject.AddComponent<TempWorldUI>();
                _healthDisplay   ??= ui;
                _progressDisplay ??= ui;
            }
        }

        private void Start()
        {
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
                Debug.LogError("[ConstructionSite] Aucun buildingPrefab. Appelle Init() ou assigne _fallbackBuildingPrefab.");
                return;
            }

            // Capture les renderers AVANT SpawnVisuals — après, GetComponentsInChildren
            // retournerait aussi les renderers de _ghostVisual et _realVisual.
            _originalRenderers = GetComponentsInChildren<Renderer>(true);

            SpawnVisuals();
            // Safety re-occupy: SpawnVisuals() instantiates clones that each run Awake+CancelRegistration.
            // In rare cases (prefab stored at same X/Z as placement) those Release calls could remove our cells.
            GridSystem.Instance?.Occupy(transform.position, _gridSize);
            SnapToGround();

            var obstacle = GetComponent<NavMeshObstacle>() ?? gameObject.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape   = NavMeshObstacleShape.Box;
            obstacle.size    = Vector3.one;
            obstacle.center  = Vector3.zero;
            _currentHealth = 0f;
            _buildProgress = 0f;
            _healthDisplay?.UpdateHealth(0f, _maxHealth);
            _progressDisplay?.UpdateProgress(0f);
            UpdateRealVisual(0f);
            _initialized = true;

            Debug.Log($"[ConstructionSite] Chantier démarré → {_buildingPrefab.name} en {transform.position}.");
        }

        private void OnDestroy()
        {
            if (!_constructionComplete && _gridOccupied)
            {
                GridSystem.Instance?.Release(transform.position, _gridSize);
                Debug.Log("[ConstructionSite] Détruit avant complétion — cellules libérées.");
            }
        }

        public void AssignWorker(WorkerController worker) => _workers.Add(worker);
        public void RemoveWorker(WorkerController worker) => _workers.Remove(worker);

        public void Tick(float deltaTime)
        {
            if (!_initialized || IsComplete) return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + _buildRate * deltaTime);
            _buildProgress = _currentHealth / _maxHealth;

            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
            _progressDisplay?.UpdateProgress(_buildProgress);
            UpdateRealVisual(_buildProgress);

            if (IsComplete) Complete();
        }

        private void SpawnVisuals()
        {
            // ── Ghost outline — full size, blue transparent, always visible ──────
            _ghostVisual = ClonePrefab(_buildingPrefab);
            _ghostVisual.transform.SetParent(transform, worldPositionStays: true);
            _ghostVisual.transform.localPosition = Vector3.zero;
            _ghostVisual.transform.localRotation = Quaternion.identity;
            _ghostVisual.transform.localScale     = Vector3.one * 0.999f; // évite le z-fighting avec le real visual

            if (_ghostMaterial != null)
                foreach (var r in _ghostVisual.GetComponentsInChildren<Renderer>())
                    r.material = _ghostMaterial;

            var ghostBlock = new MaterialPropertyBlock();
            ghostBlock.SetColor(s_baseColorId, _ghostTint);
            foreach (var r in _ghostVisual.GetComponentsInChildren<Renderer>())
                r.SetPropertyBlock(ghostBlock);

            // ── Real visual — starts invisible, grows from bottom up ─────────────
            _realVisual = ClonePrefab(_buildingPrefab);
            _realVisual.transform.SetParent(transform, worldPositionStays: true);
            _realVisual.transform.localPosition = Vector3.zero;
            _realVisual.transform.localRotation = Quaternion.identity;

            // Measure pivot-to-bottom on the real visual for the grow formula.
            var rend = _realVisual.GetComponentInChildren<Renderer>();
            _pivotToBottom = rend != null ? transform.position.y - rend.bounds.min.y : 0f;
        }

        // Returns a clone of prefab with all logic stripped (Colliders off, MonoBehaviours off).
        private static GameObject ClonePrefab(GameObject prefab)
        {
            // Instantiate far away so BuildingBase.Awake() occupies/releases cells at an
            // unused coordinate — never at the real placement position or any existing building.
            var go = Instantiate(prefab, new Vector3(99999f, 0f, 99999f), Quaternion.identity);
            foreach (var bb in go.GetComponentsInChildren<BuildingBase>())
                bb.CancelRegistration();
            foreach (var col in go.GetComponentsInChildren<Collider>())
                col.enabled = false;
            // Destroy NavMeshObstacles added by BuildingBase.Awake() — even a disabled obstacle
            // can carve the NavMesh in some Unity versions, blocking workers.
            foreach (var obs in go.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>())
                UnityEngine.Object.Destroy(obs);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;
            return go;
        }

        // Snaps the site root so the building bottom sits on the ground.
        private void SnapToGround()
        {
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas)) return;
            transform.position = new Vector3(transform.position.x,
                                             hit.position.y + _pivotToBottom,
                                             transform.position.z);
        }

        // Grows _realVisual from the ground up as t goes 0 → 1.
        // The ghost outline stays full size the whole time.
        private void UpdateRealVisual(float t)
        {
            if (_realVisual == null) return;
            float scale  = Mathf.Max(0.001f, t);
            float localY = _pivotToBottom * (t - 1f); // keeps bottom flush with ground
            _realVisual.transform.localScale    = new Vector3(1f, scale, 1f);
            _realVisual.transform.localPosition = new Vector3(0f, localY, 0f);
        }

        private void Complete()
        {
            _constructionComplete = true;

            foreach (var worker in _workers)
                worker?.NotifyComplete();
            _workers.Clear();

            // ── Supprime les visuels de construction ──────────────────────
            // DestroyImmediate évite que Destroy() différé fasse apparaître le ghost
            // pendant encore un frame après la complétion.
            if (_ghostVisual != null) DestroyImmediate(_ghostVisual);
            if (_realVisual  != null) DestroyImmediate(_realVisual);

            // ── Réactive UNIQUEMENT les renderers originaux du prefab ─────
            // _originalRenderers a été capturé avant SpawnVisuals, donc il ne
            // contient pas les renderers du ghost ni du realVisual.
            foreach (var r in _originalRenderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(null); // vide tout MPB résiduel
                r.enabled = true;
            }

            // ── Réactive tous les scripts du bâtiment ─────────────────────
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is ConstructionSite) continue; // on se détruit en dernier
                mb.enabled = true;
            }

            // ── Ré-enregistre le bâtiment dans les managers ───────────────
            foreach (var bb in GetComponentsInChildren<BuildingBase>(true))
                bb.CompleteConstruction();

            Debug.Log($"[ConstructionSite] Terminé → {gameObject.name} converti en bâtiment fini.");

            // ── Détruit uniquement le composant ConstructionSite ──────────
            // Le GameObject lui-même survit en tant que bâtiment fonctionnel.
            Destroy(this);
        }
    }
}
