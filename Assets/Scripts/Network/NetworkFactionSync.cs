using Mirror;
using UnityEngine;
using Pantheum.Core;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Units;
using Pantheum.Selection;

namespace Pantheum.Network
{
    public class NetworkFactionSync : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnFactionChanged))]
        private Faction _faction;

        [SyncVar(hook = nameof(OnConstructionStateChanged))]
        private bool _underConstruction;

        public Faction Faction            => _faction;
        public bool    IsUnderConstruction => _underConstruction;

        [SyncVar(hook = nameof(OnWorkerCountChanged))]
        private int _workerCount;

        [Server]
        public void SetWorkerCount(int count) => _workerCount = count;

        [SyncVar(hook = nameof(OnTierChanged))]
        private int _buildingTier = 1;

        [Server]
        public void SetBuildingTier(int tier) => _buildingTier = tier;

        [SyncVar] private int   _syncWorkerQueue;
        [SyncVar] private bool  _syncWorkerProducing;
        [SyncVar] private float _syncWorkerTimer;

        public int   SyncWorkerQueue     => _syncWorkerQueue;
        public bool  SyncWorkerProducing => _syncWorkerProducing;
        public float SyncWorkerTimer     => _syncWorkerTimer;

        public int WorkerCountSynced => _workerCount;

        [Server]
        public void SetWorkerProductionState(int queue, bool producing, float timer)
        {
            _syncWorkerQueue     = queue;
            _syncWorkerProducing = producing;
            _syncWorkerTimer     = timer;
        }

        [SyncVar] private int   _syncUnitQueue;
        [SyncVar] private bool  _syncUnitProducing;
        [SyncVar] private float _syncUnitTimer;
        [SyncVar] private int   _syncUnitMaxQueue;

        public int   SyncUnitQueue     => _syncUnitQueue;
        public bool  SyncUnitProducing => _syncUnitProducing;
        public float SyncUnitTimer     => _syncUnitTimer;
        public int   SyncUnitMaxQueue  => _syncUnitMaxQueue;

        [Server]
        public void SetUnitProductionState(int queue, bool producing, float timer, int maxQueue)
        {
            _syncUnitQueue     = queue;
            _syncUnitProducing = producing;
            _syncUnitTimer     = timer;
            _syncUnitMaxQueue  = maxQueue;
        }

        private void OnTierChanged(int oldVal, int newVal)
        {
            var bb = GetComponent<BuildingBase>();
            if (bb != null) bb.ApplyTierFromNetwork(newVal);
        }

        private void OnWorkerCountChanged(int oldVal, int newVal)
        {
            GetComponent<Castle>()?.ApplyWorkerCountDisplay(newVal);
        }

        public void SetUnderConstruction(bool value)
        {
            _underConstruction = value;
        }

        private void OnConstructionStateChanged(bool oldVal, bool newVal)
        {
            foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is NetworkBehaviour) continue;
                if (mb is Selection.Selectable) continue;
                if (mb is ConstructionSite) continue;
                if (mb is UI.TempWorldUI) continue;
                mb.enabled = !newVal;
            }

            if (newVal)
            {
                var cs = GetComponent<ConstructionSite>();
                if (cs != null && !cs.enabled) cs.enabled = true;
            }
        }

        public override void OnStartClient()
        {
            if (_underConstruction)
            {
                OnConstructionStateChanged(false, true);
                var cs = GetComponent<ConstructionSite>();
                if (cs != null && !cs.enabled) cs.enabled = true;
            }

            ApplyFaction(_faction);

            if (_workerCount > 0)
                GetComponent<Castle>()?.ApplyWorkerCountDisplay(_workerCount);

            BuildingManager.Instance?.RebuildCastleTierLists();

            if (_buildingTier > 1)
                OnTierChanged(1, _buildingTier);
        }

        [Server]
        public void SetNetworkFaction(Faction f)
        {
            _faction = f;
            ApplyFaction(f);
        }

        private void OnFactionChanged(Faction old, Faction nw) => ApplyFaction(nw);

        private void ApplyFaction(Faction f)
        {
            foreach (var bb in GetComponentsInChildren<BuildingBase>())
            {
                if (bb is Castle castle)
                    BuildingManager.Instance?.RemoveCastleFromTierLists(castle);
                bb.SetFaction(f);
                BuildingManager.Instance?.RegisterCastleTier(bb);
            }
            foreach (var ub in GetComponentsInChildren<UnitBase>())
                ub.SetFaction(f);
        }
    }
}
