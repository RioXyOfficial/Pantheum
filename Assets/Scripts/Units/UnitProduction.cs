using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Pantheum.Buildings;
using Pantheum.Core;
using Pantheum.Network;

namespace Pantheum.Units
{
    [System.Serializable]
    public struct ProductionEntry
    {
        public GameObject prefab;
        public int goldCost;
        public bool isCombatUnit;
        public int supplyCost;
    }

    public class UnitProduction : MonoBehaviour
    {
        [SerializeField] private float _productionTime = 10f;
        [SerializeField] private int _maxQueueSize = 5;

        private readonly Queue<ProductionEntry> _queue = new();
        private float _timer;
        private bool _isProducing;

        private NetworkFactionSync _factionSync;
        private BuildingBase _buildingBase;

        public int QueueCount => _queue.Count;
        public int MaxQueueSize => _maxQueueSize;
        public bool IsProducing => _isProducing;
        public float TimeRemaining => _isProducing ? Mathf.Max(0f, _timer) : 0f;

        private void SyncProductionState()
        {
            if (!Mirror.NetworkServer.active) return;
            if (_factionSync == null) return;

            _factionSync.SetUnitProductionState(
                _queue.Count,
                _isProducing,
                _isProducing ? Mathf.Max(0f, _timer) : 0f,
                _maxQueueSize
            );
        }

        private void Awake()
        {
            _factionSync = GetComponent<NetworkFactionSync>();
            _buildingBase = GetComponent<BuildingBase>();
        }

        private void Start()
        {
            SyncProductionState();
        }

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

            if (!_isProducing)
                StartNext();
            else
                SyncProductionState();

            return true;
        }

        private void StartNext()
        {
            if (_queue.Count == 0)
            {
                _isProducing = false;
                _timer = 0f;
                SyncProductionState();
                return;
            }

            _timer = _productionTime;
            _isProducing = true;
            SyncProductionState();
        }

        private void OnDestroy()
        {
            foreach (var entry in _queue)
                if (entry.isCombatUnit)
                    SupplyManager.Instance?.ReleaseSupply(entry.supplyCost);

            _queue.Clear();
        }

        private void Update()
        {
            if (Mirror.NetworkClient.active && !Mirror.NetworkServer.active) return;
            if (!_isProducing) return;

            _timer -= Time.deltaTime;

            if (_timer <= 0f)
            {
                SpawnUnit(_queue.Dequeue());
                StartNext();
            }
            else
            {
                SyncProductionState();
            }
        }

        private void SpawnUnit(ProductionEntry entry)
        {
            Vector2Int gridSize = _buildingBase != null ? _buildingBase.GridSize : new Vector2Int(2, 2);
            Vector3 pos = FindSpawnPosition(transform.position, gridSize);

            var agentOnPrefab = entry.prefab != null ? entry.prefab.GetComponent<NavMeshAgent>() : null;
            if (agentOnPrefab != null)
                pos.y += agentOnPrefab.baseOffset;

            var spawnedGo = Instantiate(entry.prefab, pos, Quaternion.identity);

            if (Mirror.NetworkServer.active)
            {
                var factionSync = spawnedGo.GetComponent<NetworkFactionSync>();
                Pantheum.Core.Faction spawnFaction = _buildingBase != null ? _buildingBase.Faction : Pantheum.Core.Faction.Player;

                if (factionSync != null)
                    factionSync.SetNetworkFaction(spawnFaction);

                Mirror.NetworkServer.Spawn(spawnedGo);

                var ni = spawnedGo.GetComponent<Mirror.NetworkIdentity>();
                PlayerNetworkController.BroadcastFaction(ni, spawnFaction);
            }
        }

        internal static Vector3 FindSpawnPosition(Vector3 center, Vector2Int gridSize)
        {
            float cell = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float minRadius = Mathf.Max(gridSize.x, gridSize.y) * cell * 0.5f + 0.8f;

            for (int attempt = 0; attempt < 16; attempt++)
            {
                float r = minRadius + attempt * 0.3f;
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 probe = new(center.x + Mathf.Cos(angle) * r, center.y, center.z + Mathf.Sin(angle) * r);

                if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                    return hit.position;
            }

            if (NavMesh.SamplePosition(center, out NavMeshHit fallback, 15f, NavMesh.AllAreas))
                return fallback.position;

            return center;
        }
    }
}