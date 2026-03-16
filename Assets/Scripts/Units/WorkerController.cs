using UnityEngine;
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

    /// <summary>
    /// FSM-driven worker. homeBase is assigned once at spawn and never changes.
    /// Workers do NOT consume supply.
    /// </summary>
    public class WorkerController : UnitBase
    {
        [Header("Worker")]
        [SerializeField] private float _depositPause = 0.5f;

        // Set once at spawn via Initialize(); never reassigned
        public Castle HomeBase { get; private set; }

        private ResourceNode _targetResource;
        private ConstructionSite _targetSite;
        private WorkerState _state = WorkerState.Idle;

        private int _carryingGold;
        private int _carryingMana;
        private float _harvestTimer;
        private float _depositTimer;

        public WorkerState State => _state;

        /// <summary>
        /// Must be called immediately after instantiation.
        /// Registers this worker with the Castle; destroys self if Castle is full.
        /// </summary>
        public void Initialize(Castle homeBase)
        {
            if (!homeBase.TryRegisterWorker(this))
            {
                Destroy(gameObject);
                return;
            }
            HomeBase = homeBase;
        }

        protected override void OnDeath()
        {
            HomeBase?.UnregisterWorker(this);
            base.OnDeath();
        }

        public void AssignToHarvest(ResourceNode node)
        {
            _targetResource = node;
            _targetSite = null;
            _state = WorkerState.MovingToResource;
            MoveTo(node.transform.position);
        }

        public void AssignToConstruction(ConstructionSite site)
        {
            _targetSite = site;
            _targetResource = null;
            site.AssignWorker(this);
            _state = WorkerState.MovingToBuild;
            MoveTo(site.transform.position);
        }

        private void Update()
        {
            switch (_state)
            {
                case WorkerState.MovingToResource:
                    if (HasArrived())
                    {
                        _harvestTimer = _targetResource.HarvestTime;
                        _state = WorkerState.Harvesting;
                        StopMoving();
                    }
                    break;

                case WorkerState.Harvesting:
                    _harvestTimer -= Time.deltaTime;
                    if (_harvestTimer <= 0f)
                    {
                        if (_targetResource.ResourceType == ResourceType.Gold)
                            _carryingGold = _targetResource.HarvestAmountPerTrip;
                        else
                            _carryingMana = _targetResource.HarvestAmountPerTrip;

                        _state = WorkerState.MovingToDeposit;
                        MoveTo(HomeBase.transform.position);
                    }
                    break;

                case WorkerState.MovingToDeposit:
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
                        _state = WorkerState.MovingToResource;
                        MoveTo(_targetResource.transform.position);
                    }
                    break;

                case WorkerState.MovingToBuild:
                    if (HasArrived())
                    {
                        _state = WorkerState.Building;
                        StopMoving();
                    }
                    break;

                case WorkerState.Building:
                    // _targetSite becomes Unity-null once the site destroys itself on completion
                    if (_targetSite == null || _targetSite.IsComplete)
                    {
                        _state = WorkerState.Idle;
                        _targetSite = null;
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
