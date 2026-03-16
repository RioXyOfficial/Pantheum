using System.Collections.Generic;
using UnityEngine;
using Pantheum.UI;
using Pantheum.Units;

namespace Pantheum.Construction
{
    /// <summary>
    /// Tracks parallel health and build progress while Workers are assigned.
    /// Both values advance at the same rate; both bars are visible simultaneously.
    ///
    /// Each assigned Worker calls Tick(deltaTime) once per frame from its own Update,
    /// so build speed scales linearly with worker count.
    ///
    /// On completion: spawns the finished building prefab at full health and destroys self.
    /// Workers detect completion via the IsComplete property (Unity null-check) and
    /// transition themselves to Idle.
    /// </summary>
    public class ConstructionSite : MonoBehaviour
    {
        [Header("Construction")]
        [SerializeField] private GameObject _finishedBuildingPrefab;
        [SerializeField] private float _maxHealth = 500f;
        [SerializeField] private float _buildRate = 50f; // health gained per second per worker

        [Header("UI")]
        [SerializeField] private MonoBehaviour _healthDisplayProvider;
        [SerializeField] private MonoBehaviour _progressDisplayProvider;

        private float _currentHealth;
        private float _buildProgress; // 0..1
        private readonly List<WorkerController> _workers = new();
        private IHealthDisplay _healthDisplay;
        private IProgressDisplay _progressDisplay;

        public bool IsComplete => _buildProgress >= 1f;
        public float BuildProgress => _buildProgress;

        private void Awake()
        {
            _healthDisplay = _healthDisplayProvider as IHealthDisplay;
            _progressDisplay = _progressDisplayProvider as IProgressDisplay;
            if (_healthDisplay == null || _progressDisplay == null)
                foreach (var mb in GetComponents<MonoBehaviour>())
                {
                    if (_healthDisplay == null && mb is IHealthDisplay hd)   _healthDisplay   = hd;
                    if (_progressDisplay == null && mb is IProgressDisplay pd) _progressDisplay = pd;
                }

            _currentHealth = 0f;
            _buildProgress = 0f;
            _healthDisplay?.UpdateHealth(0f, _maxHealth);
            _progressDisplay?.UpdateProgress(0f);
        }

        public void AssignWorker(WorkerController worker)
        {
            if (!_workers.Contains(worker))
                _workers.Add(worker);
        }

        public void RemoveWorker(WorkerController worker) => _workers.Remove(worker);

        /// <summary>
        /// Called each frame by each Worker in Building state.
        /// Returns immediately if already complete.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (IsComplete) return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + _buildRate * deltaTime);
            _buildProgress = _currentHealth / _maxHealth;

            _healthDisplay?.UpdateHealth(_currentHealth, _maxHealth);
            _progressDisplay?.UpdateProgress(_buildProgress);

            if (IsComplete) Complete();
        }

        private void Complete()
        {
            _workers.Clear();

            if (_finishedBuildingPrefab != null)
                Instantiate(_finishedBuildingPrefab, transform.position, transform.rotation);
            // BuildingBase.Awake on the spawned prefab calls SetHealthFull() via _currentHealth = _maxHealth

            Destroy(gameObject);
        }
    }
}
