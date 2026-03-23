using System.Collections.Generic;
using UnityEngine;
using Pantheum.Core;

namespace Pantheum.Units
{
    [System.Serializable]
    public struct ProductionEntry
    {
        public GameObject prefab;
        public int goldCost;
        public bool isCombatUnit; // false = Worker (no supply cost or supply check)
        public int supplyCost;
    }

    /// <summary>
    /// Reusable production queue used by Barracks and Academy.
    /// Attach alongside the building component and wire up _spawnPoint in the Inspector.
    /// Gold and supply are consumed at enqueue time; supply is released on unit death (CombatUnit).
    /// </summary>
    public class UnitProduction : MonoBehaviour
    {
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private float _productionTime = 10f;
        [SerializeField] private int _maxQueueSize = 5;

        private readonly Queue<ProductionEntry> _queue = new();
        private float _timer;
        private bool _isProducing;

        public int QueueCount => _queue.Count;
        public bool IsProducing => _isProducing;

        /// <summary>
        /// Attempts to add a unit to the queue.
        /// Deducts gold (and supply for combat units) immediately.
        /// Returns false if the queue is full, resources are insufficient, or supply is capped.
        /// </summary>
        public bool TryEnqueue(ProductionEntry entry)
        {
            if (_queue.Count >= _maxQueueSize) return false;
            if (!ResourceManager.Instance.SpendGold(entry.goldCost)) return false;

            if (entry.isCombatUnit && !SupplyManager.Instance.TryUseSupply(entry.supplyCost))
            {
                ResourceManager.Instance.DepositGold(entry.goldCost); // refund
                return false;
            }

            _queue.Enqueue(entry);
            if (!_isProducing) StartNext();
            return true;
        }

        private void StartNext()
        {
            if (_queue.Count == 0) { _isProducing = false; return; }
            _timer = _productionTime;
            _isProducing = true;
        }

        private void Update()
        {
            if (!_isProducing) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                SpawnUnit(_queue.Dequeue());
                StartNext();
            }
        }

        private void SpawnUnit(ProductionEntry entry)
        {
            Vector3 pos = _spawnPoint != null ? _spawnPoint.position : transform.position;
            Instantiate(entry.prefab, pos, Quaternion.identity);
        }
    }
}
