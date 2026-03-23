using UnityEngine;
using UnityEngine.InputSystem;
using Pantheum.Buildings;
using Pantheum.Core;
using Pantheum.Selection;
using Pantheum.Units;

namespace Pantheum.Construction
{
    // Manages ghost-preview grid placement and spawns a ConstructionSite on confirm.
    //
    // Setup in Inspector:
    //   _ghostMaterial : ONE transparent material (URP Lit, Surface = Transparent)
    //   _groundLayer   : "Ground" layer mask
    //
    // NO construction site prefab needed — the site is built at runtime from the building prefab.
    // ExecutionOrder > 0 ensures SelectionManager (order 0) runs first, so its !IsActive check
    // still sees IsActive=true when the player right-clicks to cancel placement.
    [DefaultExecutionOrder(10)]
    public class BuildingPlacer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Material _ghostMaterial;

        [Header("Placement")]
        [SerializeField] private LayerMask _groundLayer;

        private static readonly Color _ghostTint = new(0.4f, 0.7f, 1f, 0.5f);

        public static bool IsActive { get; private set; }

        private static readonly int s_baseColorId = Shader.PropertyToID("_BaseColor");

        private BuildingType       _pendingType;
        private Vector2Int         _pendingGridSize;
        private GameObject         _pendingBuildingPrefab;
        private int                _pendingGoldCost;
        private WorkerController[] _pendingWorkers;
        private GameObject         _ghostInstance;
        private Renderer[]         _ghostRenderers;
        private MaterialPropertyBlock _propBlock;
        private float              _ghostPivotToBottom;
        private bool               _isPlacing;
        private Camera             _cam;

        private void Awake()
        {
            _cam = Camera.main;
            _propBlock = new MaterialPropertyBlock();
        }

        public void BeginPlacement(BuildingType type,
                                   GameObject buildingPrefab, int goldCost = 0,
                                   WorkerController[] workers = null)
        {
            if (buildingPrefab == null)
            {
                Debug.LogError($"[BuildingPlacer] buildingPrefab null pour {type}.");
                return;
            }
            if (!BuildingManager.Instance.TierRequirementMet(type))
            {
                Debug.Log($"[BuildingPlacer] {type}: tier requis non atteint.");
                return;
            }
            if (!BuildingManager.Instance.CanPlace(type))
            {
                Debug.Log($"[BuildingPlacer] {type}: limite atteinte.");
                return;
            }

            CancelPlacement();

            var bb = buildingPrefab.GetComponent<Pantheum.Buildings.BuildingBase>();
            _pendingType           = type;
            _pendingGridSize       = bb != null ? bb.GridSize : new Vector2Int(2, 2);
            _pendingBuildingPrefab = buildingPrefab;
            _pendingGoldCost       = goldCost;
            _pendingWorkers        = workers;
            _ghostInstance         = CreateGhost(buildingPrefab);
            _ghostRenderers        = _ghostInstance.GetComponentsInChildren<Renderer>();
            SetGhostColor(true);
            _isPlacing             = true;
            IsActive               = true;
        }

        public void CancelPlacement()
        {
            _isPlacing             = false;
            IsActive               = false;
            _pendingWorkers        = null;
            _pendingBuildingPrefab = null;
            if (_ghostInstance != null) Destroy(_ghostInstance);
            _ghostInstance  = null;
            _ghostRenderers = null;
            GridSystem.Instance?.ClearPlacementPreview();
        }

        private void Update()
        {
            if (!_isPlacing) return;

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame ||
                (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false))
            {
                CancelPlacement();
                return;
            }

            Ray ray = _cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _groundLayer)) return;

            var grid    = GridSystem.Instance;
            Vector3 snapped  = grid != null ? grid.Snap(hit.point, _pendingGridSize) : hit.point;
            Vector3 checkPos = new(snapped.x, hit.point.y, snapped.z);
            Vector3 ghostPos = new(snapped.x, hit.point.y + _ghostPivotToBottom, snapped.z);

            bool valid = IsValidPlacement(checkPos);
            _ghostInstance.transform.position = ghostPos;
            SetGhostColor(valid);

            if (grid != null)
                grid.SetPlacementPreview(checkPos, _pendingGridSize, valid);

            if (mouse.leftButton.wasPressedThisFrame && valid)
                TryPlace(checkPos);
        }

        private bool IsValidPlacement(Vector3 position)
        {
            if (GridSystem.Instance == null)
            {
                Debug.LogError("[BuildingPlacer] GridSystem introuvable — ajoutez un GameObject GridSystem dans la scène.");
                return false;
            }
            if (!GridSystem.Instance.CanPlace(position, _pendingGridSize))
            {
                Debug.Log($"[BuildingPlacer] Placement invalide : cellule(s) occupée(s) en {position}.");
                return false;
            }
            return true;
        }

        private void TryPlace(Vector3 position)
        {
            if (!BuildingManager.Instance.TierRequirementMet(_pendingType) ||
                !BuildingManager.Instance.CanPlace(_pendingType))
            {
                Debug.Log($"[BuildingPlacer] {_pendingType}: conditions changées depuis le preview.");
                return;
            }
            if (_pendingGoldCost > 0 && !ResourceManager.Instance.SpendGold(_pendingGoldCost))
            {
                Debug.Log("[BuildingPlacer] Or insuffisant.");
                return;
            }

            // Instantiate the building prefab directly as the construction host.
            var siteGO = Instantiate(_pendingBuildingPrefab, position, Quaternion.identity);

            // Keep the building count registered (limits apply immediately on placement)
            // but release grid and remove castle from tier lists until construction completes.
            foreach (var bb in siteGO.GetComponentsInChildren<BuildingBase>())
                bb.StartConstruction();

            // Hide original renderers — ConstructionSite spawns its own visuals.
            foreach (var r in siteGO.GetComponentsInChildren<Renderer>())
                r.enabled = false;

            // Disable all scripts, then re-enable Selectable so workers can be
            // right-click assigned to this site.
            foreach (var mb in siteGO.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;
            foreach (var sel in siteGO.GetComponentsInChildren<Selectable>())
                sel.enabled = true;

            // AddComponent triggers Awake immediately; Init() must be called before Start().
            var site = siteGO.AddComponent<ConstructionSite>();
            site.Init(_pendingBuildingPrefab, _ghostMaterial, goldCost: _pendingGoldCost);

            Debug.Log($"[BuildingPlacer] {_pendingType} placé en {position}.");

            if (_pendingWorkers != null)
                foreach (var w in _pendingWorkers)
                    if (w != null) w.AssignToConstruction(site);

            CancelPlacement();
        }

        // Clones the building prefab and strips all logic, leaving only renderers.
        private GameObject CreateGhost(GameObject buildingPrefab)
        {
            // Instantiate far away so BuildingBase.Awake() occupies/releases cells at an
            // unused coordinate — never at the real placement position or any existing building.
            var ghost = Instantiate(buildingPrefab, new Vector3(99999f, 0f, 99999f), Quaternion.identity);

            foreach (var bb in ghost.GetComponentsInChildren<BuildingBase>())
                bb.CancelRegistration();

            // Colliders off — ghost must not interact with units physically.
            foreach (var col in ghost.GetComponentsInChildren<Collider>())
                col.enabled = false;

            // NavMeshObstacle must be destroyed (not just disabled) — even a disabled
            // obstacle can still carve the NavMesh in some Unity versions, which causes
            // units to path around the ghost as if it were a real building.
            foreach (var obs in ghost.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>())
                Destroy(obs);

            // Rigidbodies would cause physics interactions with units.
            foreach (var rb in ghost.GetComponentsInChildren<Rigidbody>())
                Destroy(rb);

            foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;

            var rend = ghost.GetComponentInChildren<Renderer>();
            _ghostPivotToBottom = rend != null ? ghost.transform.position.y - rend.bounds.min.y : 0f;

            if (_ghostMaterial == null)
            {
                Debug.LogWarning("[BuildingPlacer] _ghostMaterial non assigné — fantôme non transparent.");
                return ghost;
            }

            // Remplace TOUS les slots de chaque renderer par le ghost material.
            // r.material ne touche que le slot 0 — un prefab avec plusieurs slots
            // (ex: corps + fenêtres) laisserait les autres slots avec leurs matériaux
            // d'origine (blancs), d'où l'apparence blanche du ghost.
            foreach (var r in ghost.GetComponentsInChildren<Renderer>())
            {
                var slots = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < slots.Length; i++) slots[i] = _ghostMaterial;
                r.materials = slots;
            }

            return ghost;
        }

        private void SetGhostColor(bool valid)
        {
            if (_ghostRenderers == null) return;
            _propBlock.SetColor(s_baseColorId, _ghostTint);
            foreach (var r in _ghostRenderers)
                r.SetPropertyBlock(_propBlock);
        }
    }
}
