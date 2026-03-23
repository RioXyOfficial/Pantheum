using UnityEngine;
using UnityEngine.AI;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Core;

namespace Pantheum.Units
{
    public enum WorkerState
    {
        Idle,
        MovingToResource,
        Harvesting,
        MovingToDeposit,
        Depositing,
        MovingToBuild,
        Building
    }

    public class WorkerController : UnitBase
    {
        [Header("Worker")]
        [SerializeField] private float _depositPause = 0.5f;

        public Castle      HomeBase => _homeBase;
        public WorkerState State    => _state;

        private Castle            _homeBase;
        private ResourceNode      _targetResource;
        private ConstructionSite  _targetSite;
        private WorkerState       _state = WorkerState.Idle;
        private int               _carryingGold;
        private int               _carryingMana;
        private float             _harvestTimer;
        private float             _depositTimer;

        public bool Initialize(Castle homeBase)
        {
            if (!homeBase.TryRegisterWorker(this))
            {
                Debug.LogWarning("[WorkerController] Castle plein — worker détruit.");
                Destroy(gameObject);
                return false;
            }
            _homeBase = homeBase;
            Debug.Log($"[WorkerController] Initialisé sur {homeBase.name}.");
            return true;
        }

        protected override void OnDeath()
        {
            CancelTask();
            _homeBase?.UnregisterWorker(this);
            base.OnDeath();
        }

        public void AssignToHarvest(ResourceNode node)
        {
            if (node == null) return;
            CancelTask();
            _targetResource = node;
            _state = WorkerState.MovingToResource;
            MoveToBuilding(node.transform.position, node.GridSize);
            Debug.Log($"[WorkerController] → Récolte vers {node.name}.");
        }

        public void AssignToConstruction(ConstructionSite site)
        {
            if (site == null) return;
            CancelTask();
            _targetSite = site;
            site.AssignWorker(this);
            _state = WorkerState.MovingToBuild;
            MoveToBuilding(site.transform.position, site.GridSize);
            Debug.Log($"[WorkerController] → Construction vers {site.name}.");
        }

        public void OrderMove(Vector3 destination)
        {
            CancelTask();
            MoveTo(destination);
        }

        // Finds the closest walkable NavMesh point on the worker's side of the building
        // and navigates directly to it — no going around the other side.
        private const float StoppingDist = 0.3f;

        private void MoveToBuilding(Vector3 center, Vector2Int gridSize)
        {
            _agent.stoppingDistance = StoppingDist;
            MoveTo(AccessPointFor(center, gridSize));
        }

        private Vector3 AccessPointFor(Vector3 center, Vector2Int gridSize)
        {
            float cell       = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float halfExtent = Mathf.Max(gridSize.x, gridSize.y) * cell * 0.5f + 0.1f;

            Vector3 dir = transform.position - center;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();

            // Probe just outside the building edge on the worker's side.
            Vector3 probe = new(center.x + dir.x * halfExtent,
                                center.y,
                                center.z + dir.z * halfExtent);

            return NavMesh.SamplePosition(probe, out NavMeshHit hit, 2f, NavMesh.AllAreas)
                ? hit.position
                : probe;
        }

        // True when the worker is within interaction range of the site —
        // handles the case where another worker is blocking the exact nav destination.
        private bool IsNearSite(ConstructionSite site)
        {
            float cell      = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float halfExtent = Mathf.Max(site.GridSize.x, site.GridSize.y) * cell * 0.5f;
            float threshold = halfExtent + 1.5f;
            Vector3 diff    = transform.position - site.transform.position;
            diff.y = 0f;
            return diff.magnitude <= threshold;
        }

        private void CancelTask()
        {
            if (_targetSite != null)
            {
                _targetSite.RemoveWorker(this);
                _targetSite = null;
            }
            _targetResource = null;
            _state = WorkerState.Idle;
            _agent.stoppingDistance = StoppingDist;
            StopMoving();
        }

        public void NotifyComplete()
        {
            _targetSite = null;
            _state = WorkerState.Idle;
        }

        private void Update()
        {
            switch (_state)
            {
                case WorkerState.MovingToResource:
                    if (_targetResource == null) { _state = WorkerState.Idle; break; }
                    if (HasArrived())
                    {
                        _harvestTimer = _targetResource.HarvestTime;
                        _state = WorkerState.Harvesting;
                        StopMoving();
                    }
                    break;

                case WorkerState.Harvesting:
                    if (_targetResource == null || _homeBase == null) { _state = WorkerState.Idle; break; }
                    _harvestTimer -= Time.deltaTime;
                    if (_harvestTimer <= 0f)
                    {
                        if (_targetResource.ResourceType == ResourceType.Gold)
                            _carryingGold = _targetResource.HarvestAmountPerTrip;
                        else
                            _carryingMana = _targetResource.HarvestAmountPerTrip;

                        _state = WorkerState.MovingToDeposit;
                        MoveToBuilding(_homeBase.transform.position, _homeBase.GridSize);
                    }
                    break;

                case WorkerState.MovingToDeposit:
                    if (_homeBase == null) { _state = WorkerState.Idle; break; }
                    if (HasArrived())
                    {
                        ResourceManager.Instance.DepositGold(_carryingGold);
                        ResourceManager.Instance.DepositMana(_carryingMana);
                        _carryingGold = 0;
                        _carryingMana = 0;
                        _depositTimer = _depositPause;
                        _state = WorkerState.Depositing;
                        StopMoving();
                    }
                    break;

                case WorkerState.Depositing:
                    _depositTimer -= Time.deltaTime;
                    if (_depositTimer <= 0f)
                    {
                        if (_targetResource == null) { _state = WorkerState.Idle; break; }
                        _state = WorkerState.MovingToResource;
                        MoveToBuilding(_targetResource.transform.position, _targetResource.GridSize);
                    }
                    break;

                case WorkerState.MovingToBuild:
                    if (_targetSite == null) { StopMoving(); _state = WorkerState.Idle; break; }
                    if (HasArrived() || IsNearSite(_targetSite))
                    {
                        _state = WorkerState.Building;
                        StopMoving();
                    }
                    break;

                case WorkerState.Building:
                    if (_targetSite == null || _targetSite.IsComplete)
                    {
                        _targetSite?.RemoveWorker(this);
                        _targetSite = null;
                        _agent.stoppingDistance = StoppingDist;
                        _state = WorkerState.Idle;
                    }
                    else
                    {
                        _targetSite.Tick(Time.deltaTime);
                    }
                    break;
            }
        }
    }
}
