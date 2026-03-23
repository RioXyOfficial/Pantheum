using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Pantheum.Buildings;
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
    /// Units spawn at the nearest free NavMesh position around the building perimeter.
    /// Gold and supply are consumed at enqueue time; supply is released on unit death (CombatUnit).
    /// </summary>
    public class UnitProduction : MonoBehaviour
    {
        [SerializeField] private float _productionTime = 10f;
        [SerializeField] private int _maxQueueSize = 5;

        private readonly Queue<ProductionEntry> _queue = new();
        private float _timer;
        private bool _isProducing;

        public int QueueCount   => _queue.Count;
        public int MaxQueueSize => _maxQueueSize;
        public bool IsProducing => _isProducing;
        public float TimeRemaining => _isProducing ? Mathf.Max(0f, _timer) : 0f;

        public bool TryEnqueue(ProductionEntry entry)
        {
            if (_queue.Count >= _maxQueueSize) return false;
            if (!ResourceManager.Instance.SpendGold(entry.goldCost)) return false;

            if (entry.isCombatUnit && !SupplyManager.Instance.TryUseSupply(entry.supplyCost))
            {
                ResourceManager.Instance.DepositGold(entry.goldCost);
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

        private void OnDestroy()
        {
            // No gold refund — resources are lost if the building is destroyed.
            // Supply must still be released so the cap doesn't get permanently stuck.
            foreach (var entry in _queue)
                if (entry.isCombatUnit)
                    SupplyManager.Instance?.ReleaseSupply(entry.supplyCost);
            _queue.Clear();
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
            var bb = GetComponent<BuildingBase>();
            Vector2Int gridSize = bb != null ? bb.GridSize : new Vector2Int(2, 2);
            Vector3 pos = FindSpawnPosition(transform.position, gridSize);
            Instantiate(entry.prefab, pos, Quaternion.identity);
        }

        // Finds a walkable NavMesh position around the building perimeter.
        internal static Vector3 FindSpawnPosition(Vector3 center, Vector2Int gridSize)
        {
            float cell      = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float minRadius = Mathf.Max(gridSize.x, gridSize.y) * cell * 0.5f + 0.8f;

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float r     = minRadius + attempt * 0.3f;
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 probe = new(center.x + Mathf.Cos(angle) * r,
                                    center.y,
                                    center.z + Mathf.Sin(angle) * r);
                if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                    return hit.position;
            }

            if (NavMesh.SamplePosition(center, out NavMeshHit fallback, 15f, NavMesh.AllAreas))
                return fallback.position;
            return center;
        }
    }
}
