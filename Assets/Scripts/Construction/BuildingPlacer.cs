using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Pantheum.Buildings;

namespace Pantheum.Construction
{
    /// <summary>
    /// Manages ghost-preview placement and spawns a ConstructionSite on confirm.
    /// Checks BuildingManager limits and NavMesh validity before placing.
    ///
    /// Usage: call BeginPlacement() from a UI button or hotkey handler.
    /// Right-click or ESC cancels. Left-click confirms.
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float _navMeshSampleDistance = 2f;

        private BuildingType _pendingType;
        private GameObject _ghostInstance;
        private GameObject _sitePrefab;
        private bool _isPlacing;
        private Camera _cam;

        private void Awake() => _cam = Camera.main;

        public void BeginPlacement(BuildingType type, GameObject ghostPrefab, GameObject sitePrefab)
        {
            if (!BuildingManager.Instance.TierRequirementMet(type))
            {
                Debug.Log($"[BuildingPlacer] Cannot place {type}: tier requirement not met.");
                return;
            }
            if (!BuildingManager.Instance.CanPlace(type))
            {
                Debug.Log($"[BuildingPlacer] Cannot place {type}: limit reached.");
                return;
            }

            CancelPlacement();
            _pendingType = type;
            _sitePrefab = sitePrefab;
            _ghostInstance = Instantiate(ghostPrefab);
            _isPlacing = true;
        }

        public void CancelPlacement()
        {
            _isPlacing = false;
            if (_ghostInstance != null) Destroy(_ghostInstance);
            _ghostInstance = null;
        }

        private void Update()
        {
            if (!_isPlacing) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // Cancel
            if (mouse.rightButton.wasPressedThisFrame ||
                (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false))
            {
                CancelPlacement();
                return;
            }

            // Move ghost to cursor
            Ray ray = _cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _groundLayer))
            {
                _ghostInstance.transform.position = hit.point;

                if (mouse.leftButton.wasPressedThisFrame)
                    TryPlace(hit.point);
            }
        }

        private void TryPlace(Vector3 position)
        {
            if (!NavMesh.SamplePosition(position, out NavMeshHit _, _navMeshSampleDistance, NavMesh.AllAreas))
            {
                Debug.Log("[BuildingPlacer] Invalid placement: not near NavMesh.");
                return;
            }

            Instantiate(_sitePrefab, position, Quaternion.identity);
            CancelPlacement();
        }
    }
}
