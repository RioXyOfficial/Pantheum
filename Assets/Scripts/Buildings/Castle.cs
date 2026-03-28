using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Pantheum.Core;
using Pantheum.Network;
using Pantheum.UI;
using Pantheum.Units;

namespace Pantheum.Buildings
{
    public class Castle : BuildingBase
    {
        public const int MaxWorkers = 15;

        [Header("Worker Production")]
        [SerializeField] private GameObject _workerPrefab;
        [SerializeField] private int _workerGoldCost = 50;
        [SerializeField] private float _workerProductionTime = 10f;
        [SerializeField] private int _maxWorkerQueueSize = 5;

        [Header("Worker UI")]
        [SerializeField] private MonoBehaviour _workerCountDisplayProvider;

        private readonly List<WorkerController> _workers = new();
        private IWorkerCountDisplay _workerCountDisplay;

        private int _workerQueueCount;
        private float _workerTimer;
        private bool _isProducingWorker;

        public CastleTier Tier          => (CastleTier)CurrentTier;
        public int  WorkerCount         => _workers.Count;
        public int  WorkerQueueCount    => _workerQueueCount;
        public int  WorkerGoldCost      => _workerGoldCost;
        public int  MaxWorkerQueueSize  => _maxWorkerQueueSize;
        public bool IsProducingWorker   => _isProducingWorker;
        public float WorkerTimeRemaining => _isProducingWorker ? Mathf.Max(0f, _workerTimer) : 0f;

        protected override void Awake()
        {
            base.Awake();

            _workerCountDisplay = _workerCountDisplayProvider as IWorkerCountDisplay;
            if (_workerCountDisplay == null)
                foreach (var mb in GetComponents<MonoBehaviour>())
                    if (mb is IWorkerCountDisplay wcd) { _workerCountDisplay = wcd; break; }

            _workerCountDisplay?.UpdateWorkerCount(0, MaxWorkers);
        }

        public int DisplayWorkerCount
        {
            get
            {
                if (Mirror.NetworkClient.active && !Mirror.NetworkServer.active)
                {
                    var nfs = GetComponent<NetworkFactionSync>();
                    return nfs != null ? nfs.WorkerCountSynced : 0;
                }
                return _workers.Count;
            }
        }

        protected override void OnTierUpgraded(int newTier)
        {
            BuildingManager.Instance?.UpdateCastleTier(this, (CastleTier)(newTier - 1));
        }

        private void SyncProductionState()
        {
            if (Mirror.NetworkServer.active)
                GetComponent<NetworkFactionSync>()?.SetWorkerProductionState(
                    _workerQueueCount, _isProducingWorker, _workerTimer);
        }

        private void Update()
        {
            if (Mirror.NetworkClient.active && !Mirror.NetworkServer.active) return;
            if (!_isProducingWorker) return;
            _workerTimer -= Time.deltaTime;
            if (_workerTimer <= 0f) FinishWorkerSpawn();
            SyncProductionState();
        }

        public bool SpawnWorker()
        {
            if (_workerPrefab == null)
            {
                Debug.LogWarning("[Castle] _workerPrefab not assigned.");
                return false;
            }
            if (_workers.Count + _workerQueueCount >= MaxWorkers)
            {
                Debug.Log("[Castle] Worker cap reached.");
                return false;
            }
            if (_workerQueueCount >= _maxWorkerQueueSize)
            {
                Debug.Log("[Castle] Worker queue full.");
                return false;
            }
            if (!ResourceManager.Instance.SpendGold(_workerGoldCost))
            {
                Debug.Log("[Castle] Not enough gold.");
                return false;
            }

            _workerQueueCount++;
            if (!_isProducingWorker)
            {
                _isProducingWorker = true;
                _workerTimer = _workerProductionTime;
            }
            return true;
        }

        private void FinishWorkerSpawn()
        {
            _workerQueueCount--;

            Vector3 pos = UnitProduction.FindSpawnPosition(transform.position, GridSize);
            pos = ApplyAgentBaseOffset(_workerPrefab, pos);
            var go = Instantiate(_workerPrefab, pos, Quaternion.identity);
            var worker = go.GetComponent<WorkerController>();
            if (worker != null && !worker.Initialize(this))
            {
                ResourceManager.Instance?.DepositGold(_workerGoldCost);
                Debug.Log("[Castle] Worker rejeté après spawn — or remboursé.");
            }

            if (NetworkServer.active)
            {
                var factionSync = go.GetComponent<NetworkFactionSync>();
                if (factionSync != null) factionSync.SetNetworkFaction(Faction);
                NetworkServer.Spawn(go);
                PlayerNetworkController.BroadcastFaction(go.GetComponent<NetworkIdentity>(), Faction);
            }

            if (_workerQueueCount > 0)
                _workerTimer = _workerProductionTime;
            else
                _isProducingWorker = false;
        }

        protected override void OnDeath()
        {
            _workerQueueCount = 0;
            _isProducingWorker = false;
            foreach (var w in _workers)
                w?.NotifyHomeDestroyed();
            _workers.Clear();
            base.OnDeath();
        }

        public void SpawnWorkerImmediate()
        {
            if (_workerPrefab == null)
            {
                Debug.LogWarning("[Castle] _workerPrefab not assigned.");
                return;
            }

            Vector3 pos = UnitProduction.FindSpawnPosition(transform.position, GridSize);
            pos = ApplyAgentBaseOffset(_workerPrefab, pos);
            var go = Instantiate(_workerPrefab, pos, Quaternion.identity);
            var worker = go.GetComponent<WorkerController>();
            if (worker != null)
                worker.Initialize(this);

            if (NetworkServer.active)
            {
                var factionSync = go.GetComponent<NetworkFactionSync>();
                if (factionSync != null) factionSync.SetNetworkFaction(Faction);
                NetworkServer.Spawn(go);
                PlayerNetworkController.BroadcastFaction(go.GetComponent<NetworkIdentity>(), Faction);
            }
        }

        public bool TryRegisterWorker(WorkerController worker)
        {
            if (_workers.Count >= MaxWorkers) return false;
            _workers.Add(worker);
            NotifyWorkerCountChanged();
            return true;
        }

        public void UnregisterWorker(WorkerController worker)
        {
            _workers.Remove(worker);
            NotifyWorkerCountChanged();
        }

        private void NotifyWorkerCountChanged()
        {
            _workerCountDisplay?.UpdateWorkerCount(_workers.Count, MaxWorkers);
            if (NetworkServer.active)
                GetComponent<NetworkFactionSync>()?.SetWorkerCount(_workers.Count);
        }

        public void ApplyWorkerCountDisplay(int count)
        {
            _workerCountDisplay?.UpdateWorkerCount(count, MaxWorkers);
        }

        private static Vector3 ApplyAgentBaseOffset(GameObject prefab, Vector3 pos)
        {
            var agent = prefab != null ? prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
            return agent != null ? new Vector3(pos.x, pos.y + agent.baseOffset, pos.z) : pos;
        }
    }
}
