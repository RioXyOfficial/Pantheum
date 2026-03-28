using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Pantheum.Buildings;
using Pantheum.Core;
using Pantheum.Network;
using Pantheum.Selection;
using Pantheum.Units;

namespace Pantheum.Construction
{
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

            if (!NetworkClient.active || NetworkServer.active)
            {
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
            }

            CancelPlacement();

            var bb = buildingPrefab.GetComponent<BuildingBase>();
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
                TryPlace(ghostPos);
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
            if (NetworkClient.active && !NetworkServer.active)
            {
                var nc = PlayerNetworkController.LocalPlayer;
                if (_pendingGoldCost > 0 && (nc == null || nc.Gold < _pendingGoldCost))
                {
                    Debug.Log("[BuildingPlacer] Or insuffisant.");
                    return;
                }

                NetworkIdentity workerNI = null;
                if (_pendingWorkers != null && _pendingWorkers.Length > 0 && _pendingWorkers[0] != null)
                    workerNI = _pendingWorkers[0].GetComponent<NetworkIdentity>();

                nc?.CmdPlaceBuilding((int)_pendingType, position, _pendingGoldCost, workerNI);
                CancelPlacement();
                return;
            }

            // Host or solo: spend gold now
            if (NetworkServer.active)
                ResourceManager.ActiveNetworkPlayer = PlayerNetworkController.LocalPlayer;
            if (_pendingGoldCost > 0 && !ResourceManager.Instance.SpendGold(_pendingGoldCost))
            {
                ResourceManager.ActiveNetworkPlayer = null;
                Debug.Log("[BuildingPlacer] Or insuffisant.");
                return;
            }
            ResourceManager.ActiveNetworkPlayer = null;

            var siteGO = Instantiate(_pendingBuildingPrefab, position, Quaternion.identity);

            foreach (var mb in siteGO.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is ConstructionSite) continue;
                if (mb is Mirror.NetworkBehaviour) continue;
                if (mb is Selectable) continue;

                    mb.enabled = false;
            }

            var site = siteGO.GetComponent<ConstructionSite>();
            if (site == null)
            {
                Debug.LogError($"[BuildingPlacer] ConstructionSite manquant sur le prefab '{_pendingBuildingPrefab.name}'. Ajoute le composant (désactivé) sur chaque prefab de bâtiment.");
                Destroy(siteGO);
                return;
            }
            site.enabled = true;
            site.Init(_pendingBuildingPrefab, _ghostMaterial, goldCost: _pendingGoldCost);

            if (NetworkServer.active)
            {
                Faction hostFaction = PlayerNetworkController.LocalPlayer?.Faction ?? Faction.Player;
                var factionSync = siteGO.GetComponent<NetworkFactionSync>();
                if (factionSync != null)
                {
                    factionSync.SetNetworkFaction(hostFaction);
                    factionSync.SetUnderConstruction(true);
                }
                else
                {
                    foreach (var b in siteGO.GetComponentsInChildren<BuildingBase>())
                        b.SetFaction(hostFaction);
                }
                NetworkServer.Spawn(siteGO, NetworkServer.localConnection);
                var ni = siteGO.GetComponent<NetworkIdentity>();
                if (ni != null) PlayerNetworkController.BroadcastFaction(ni, hostFaction);
            }

            Debug.Log($"[BuildingPlacer] {_pendingType} placé en {position}.");

            if (_pendingWorkers != null)
                foreach (var w in _pendingWorkers)
                    if (w != null) w.AssignToConstruction(site);

            CancelPlacement();
        }

        private GameObject CreateGhost(GameObject buildingPrefab)
        {
            bool wasActive = buildingPrefab.activeSelf;
            buildingPrefab.SetActive(false);

            var ghost = Instantiate(buildingPrefab, new Vector3(99999f, 0f, 99999f), Quaternion.identity);

            buildingPrefab.SetActive(wasActive);

            foreach (var nb in ghost.GetComponentsInChildren<NetworkBehaviour>(true))
                DestroyImmediate(nb);
            foreach (var ni in ghost.GetComponentsInChildren<NetworkIdentity>(true))
                DestroyImmediate(ni);

            ghost.SetActive(true);

            foreach (var bb in ghost.GetComponentsInChildren<BuildingBase>(true))
                bb.CancelRegistration();

            foreach (var col in ghost.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            foreach (var obs in ghost.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
                DestroyImmediate(obs);

            foreach (var rb in ghost.GetComponentsInChildren<Rigidbody>(true))
                DestroyImmediate(rb);

            foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                mb.enabled = false;

            var rend = ghost.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var mf = rend.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    float belowPivot = -mf.sharedMesh.bounds.min.y * ghost.transform.localScale.y;
                    _ghostPivotToBottom = Mathf.Max(0f, belowPivot);
                }
                else
                {
                    _ghostPivotToBottom = 0f;
                }
            }
            else
            {
                _ghostPivotToBottom = 0f;
            }

            if (_ghostMaterial == null)
            {
                Debug.LogWarning("[BuildingPlacer] _ghostMaterial non assigné.");
                return ghost;
            }

            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
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
