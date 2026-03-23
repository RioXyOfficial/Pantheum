using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Pantheum.Core;
using Pantheum.UI;
using Pantheum.Units;

namespace Pantheum.Buildings
{
    /// <summary>
    /// Castle owns a Worker registry (hard cap: 15).
    /// Always displays "X / 15" via IWorkerCountDisplay — not gated on selection.
    /// Worker production uses an internal queue + timer, mirroring UnitProduction
    /// for Barracks/Academy, but with Castle-specific cap and Initialize() call.
    /// </summary>
    public class Castle : BuildingBase
    {
        public const int MaxWorkers = 15;

        [Header("Worker Production")]
        [SerializeField] private GameObject _workerPrefab;
        [SerializeField] private Transform _spawnPoint;
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
        /// <summary>Secondes restantes pour le Worker en cours de production (0 si idle).</summary>
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

        protected override void OnTierUpgraded(int newTier)
        {
            // CurrentTier is already newTier here; old tier is newTier - 1.
            BuildingManager.Instance?.UpdateCastleTier(this, (CastleTier)(newTier - 1));
        }

        private void Update()
        {
            if (!_isProducingWorker) return;
            _workerTimer -= Time.deltaTime;
            if (_workerTimer <= 0f) FinishWorkerSpawn();
        }

        /// <summary>
        /// Enqueues a Worker for production. Deducts gold immediately.
        /// Returns false if queue full, cap reached, or not enough gold.
        /// </summary>
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

            Vector3 pos = _spawnPoint != null
                ? _spawnPoint.position
                : transform.position + transform.forward * 2f;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            pos += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.2f;

            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                pos = hit.position;

            var go = Instantiate(_workerPrefab, pos, Quaternion.identity);
            var worker = go.GetComponent<WorkerController>();
            if (worker != null && !worker.Initialize(this))
            {
                // Castle plein entre-temps (worker mort pendant la queue) — remboursement
                ResourceManager.Instance?.DepositGold(_workerGoldCost);
                Debug.Log("[Castle] Worker rejeté après spawn — or remboursé.");
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
            base.OnDeath();
        }

        /// <summary>
        /// Spawns a Worker instantly with no gold cost and no queue.
        /// Used at game start to give the player their initial workers.
        /// </summary>
        public void SpawnWorkerImmediate()
        {
            if (_workerPrefab == null)
            {
                Debug.LogWarning("[Castle] _workerPrefab not assigned.");
                return;
            }

            Vector3 pos = _spawnPoint != null
                ? _spawnPoint.position
                : transform.position + transform.forward * 2f;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            pos += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.2f;

            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                pos = hit.position;

            var go = Instantiate(_workerPrefab, pos, Quaternion.identity);
            var worker = go.GetComponent<WorkerController>();
            if (worker != null)
                worker.Initialize(this);
        }

        public bool TryRegisterWorker(WorkerController worker)
        {
            if (_workers.Count >= MaxWorkers) return false;
            _workers.Add(worker);
            _workerCountDisplay?.UpdateWorkerCount(_workers.Count, MaxWorkers);
            return true;
        }

        public void UnregisterWorker(WorkerController worker)
        {
            _workers.Remove(worker);
            _workerCountDisplay?.UpdateWorkerCount(_workers.Count, MaxWorkers);
        }
    }
}
