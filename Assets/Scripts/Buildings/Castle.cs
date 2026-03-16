using System.Collections.Generic;
using UnityEngine;
using Pantheum.UI;
using Pantheum.Units;

namespace Pantheum.Buildings
{
    /// <summary>
    /// Castle owns a Worker registry (hard cap: 15).
    /// Always displays "X / 15" via IWorkerCountDisplay — not gated on selection.
    /// </summary>
    public class Castle : BuildingBase
    {
        public const int MaxWorkers = 15;

        [Header("Castle")]
        [SerializeField] private CastleTier _tier = CastleTier.T1;

        [Header("Worker UI")]
        [SerializeField] private MonoBehaviour _workerCountDisplayProvider;

        private readonly List<WorkerController> _workers = new();
        private IWorkerCountDisplay _workerCountDisplay;

        public CastleTier Tier => _tier;
        public int WorkerCount => _workers.Count;

        protected override void Awake()
        {
            base.Awake();

            _workerCountDisplay = _workerCountDisplayProvider as IWorkerCountDisplay;
            if (_workerCountDisplay == null)
                foreach (var mb in GetComponents<MonoBehaviour>())
                    if (mb is IWorkerCountDisplay wcd) { _workerCountDisplay = wcd; break; }

            // Render initial "0 / 15" unconditionally
            _workerCountDisplay?.UpdateWorkerCount(0, MaxWorkers);
        }

        /// <summary>
        /// Called at Worker spawn. Returns false if the castle is full (15/15).
        /// The caller must abort spawning the Worker when false is returned.
        /// </summary>
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
