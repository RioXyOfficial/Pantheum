using UnityEngine;
using UnityEngine.AI;
using Mirror;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Core;
using Pantheum.Network;

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

        [SyncVar]
        private WorkerState _syncedState = WorkerState.Idle;

        public Castle HomeBase => _homeBase;
        public WorkerState State => Mirror.NetworkClient.active && !Mirror.NetworkServer.active ? _syncedState : _state;

        private Castle _homeBase;
        private ResourceNode _targetResource;
        private ConstructionSite _targetSite;
        private float _siteProximityThreshold;
        private WorkerState _state = WorkerState.Idle;
        private int _carryingGold;
        private int _carryingMana;
        private float _harvestTimer;
        private float _depositTimer;
        private NavMeshPath _navPath;

        private const float StoppingDist = 0.3f;

        protected override void Awake()
        {
            base.Awake();
            _navPath = new NavMeshPath();
        }

        public bool Initialize(Castle homeBase)
        {
            if (!homeBase.TryRegisterWorker(this))
            {
                Debug.LogWarning("[WorkerController] Castle full — worker destroyed.");
                Destroy(gameObject);
                return false;
            }

            _homeBase = homeBase;
            return true;
        }

        private void SetState(WorkerState newState)
        {
            _state = newState;
            if (Mirror.NetworkServer.active)
                _syncedState = newState;
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
            SetState(WorkerState.MovingToResource);
            MoveToBuilding(node.transform.position, node.GridSize);
        }

        public void AssignToConstruction(ConstructionSite site)
        {
            if (site == null) return;

            CancelTask();
            _targetSite = site;
            site.AssignWorker(this);

            float cell = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            _siteProximityThreshold = Mathf.Max(site.GridSize.x, site.GridSize.y) * cell * 0.5f + 1.5f;

            SetState(WorkerState.MovingToBuild);
            MoveToBuilding(site.transform.position, site.GridSize);
        }

        public void OrderMove(Vector3 destination)
        {
            CancelTask();
            MoveTo(destination);
        }

        private void MoveToBuilding(Vector3 center, Vector2Int gridSize)
        {
            _agent.stoppingDistance = StoppingDist;
            MoveTo(AccessPointFor(center, gridSize));
        }

        private Vector3 AccessPointFor(Vector3 center, Vector2Int gridSize)
        {
            float cell = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float halfExtent = Mathf.Max(gridSize.x, gridSize.y) * cell * 0.5f + 0.5f;
            const int attempts = 8;

            Vector3 toWorker = transform.position - center;
            toWorker.y = 0f;
            if (toWorker.sqrMagnitude < 0.001f) toWorker = Vector3.forward;
            float startAngle = Mathf.Atan2(toWorker.z, toWorker.x);

            for (int i = 0; i < attempts; i++)
            {
                float a = startAngle + (i / (float)attempts) * Mathf.PI * 2f;
                Vector3 probe = new(center.x + Mathf.Cos(a) * halfExtent,
                                    center.y,
                                    center.z + Mathf.Sin(a) * halfExtent);

                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    continue;

                if (NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, _navPath)
                    && _navPath.status == NavMeshPathStatus.PathComplete)
                    return hit.position;
            }

            Vector3 fallbackProbe = center + toWorker.normalized * halfExtent;
            return NavMesh.SamplePosition(fallbackProbe, out NavMeshHit fb, 5f, NavMesh.AllAreas)
                ? fb.position
                : fallbackProbe;
        }

        private bool IsNearSite(ConstructionSite site)
        {
            Vector3 diff = transform.position - site.transform.position;
            diff.y = 0f;
            return diff.magnitude <= _siteProximityThreshold;
        }

        private void CancelTask()
        {
            if (_targetSite != null)
            {
                _targetSite.RemoveWorker(this);
                _targetSite = null;
            }

            _targetResource = null;
            SetState(WorkerState.Idle);
            _agent.stoppingDistance = StoppingDist;
            StopMoving();
        }

        public void NotifyComplete()
        {
            _targetSite = null;
            SetState(WorkerState.Idle);
        }

        public void NotifyHomeDestroyed()
        {
            _homeBase = null;
            CancelTask();
        }

        private void Update()
        {
            if (IsClientOnly) return;

            switch (_state)
            {
                case WorkerState.MovingToResource:
                    if (_targetResource == null)
                    {
                        SetState(WorkerState.Idle);
                        break;
                    }

                    if (HasArrived())
                    {
                        _harvestTimer = _targetResource.HarvestTime;
                        SetState(WorkerState.Harvesting);
                        StopMoving();
                    }
                    break;

                case WorkerState.Harvesting:
                    if (_targetResource == null || _homeBase == null)
                    {
                        SetState(WorkerState.Idle);
                        break;
                    }

                    _harvestTimer -= Time.deltaTime;
                    if (_harvestTimer <= 0f)
                    {
                        if (_targetResource.ResourceType == ResourceType.Gold)
                            _carryingGold = _targetResource.HarvestAmountPerTrip;
                        else
                            _carryingMana = _targetResource.HarvestAmountPerTrip;

                        SetState(WorkerState.MovingToDeposit);
                        MoveToBuilding(_homeBase.transform.position, _homeBase.GridSize);
                    }
                    break;

                case WorkerState.MovingToDeposit:
                    if (_homeBase == null)
                    {
                        SetState(WorkerState.Idle);
                        break;
                    }

                    if (HasArrived())
                    {
                        if (NetworkServer.active)
                        {
                            var ctrl = PlayerNetworkController.FindForFaction(Faction);
                            if (ctrl != null)
                            {
                                ctrl.DepositGold(_carryingGold);
                                ctrl.DepositMana(_carryingMana);
                            }
                        }
                        else
                        {
                            ResourceManager.Instance.DepositGold(_carryingGold);
                            ResourceManager.Instance.DepositMana(_carryingMana);
                        }

                        _carryingGold = 0;
                        _carryingMana = 0;
                        _depositTimer = _depositPause;
                        SetState(WorkerState.Depositing);
                        StopMoving();
                    }
                    break;

                case WorkerState.Depositing:
                    _depositTimer -= Time.deltaTime;
                    if (_depositTimer <= 0f)
                    {
                        if (_targetResource == null)
                        {
                            SetState(WorkerState.Idle);
                            break;
                        }

                        SetState(WorkerState.MovingToResource);
                        MoveToBuilding(_targetResource.transform.position, _targetResource.GridSize);
                    }
                    break;

                case WorkerState.MovingToBuild:
                    if (_targetSite == null)
                    {
                        StopMoving();
                        SetState(WorkerState.Idle);
                        break;
                    }

                    if (HasArrived() || IsNearSite(_targetSite))
                    {
                        SetState(WorkerState.Building);
                        StopMoving();
                    }
                    break;

                case WorkerState.Building:
                    if (_targetSite == null || _targetSite.IsComplete)
                    {
                        _targetSite?.RemoveWorker(this);
                        _targetSite = null;
                        _agent.stoppingDistance = StoppingDist;
                        SetState(WorkerState.Idle);
                    }
                    else if (_targetSite.IsPrimaryBuilder(this))
                    {
                        _targetSite.Tick(Time.deltaTime);
                    }
                    break;
            }
        }
    }
}