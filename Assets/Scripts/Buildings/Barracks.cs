using UnityEngine;
using Pantheum.Units;

namespace Pantheum.Buildings
{
    [RequireComponent(typeof(UnitProduction))]
    public class Barracks : BuildingBase
    {
        [Header("Production")]
        [SerializeField] private GameObject _knightPrefab;
        [SerializeField] private GameObject _archerPrefab;
        [SerializeField] private int _knightGoldCost = 75;
        [SerializeField] private int _archerGoldCost = 100;

        public int KnightGoldCost => _knightGoldCost;
        public int ArcherGoldCost  => _archerGoldCost;

        private UnitProduction _production;

        protected override void Awake()
        {
            base.Awake();
            _production = GetComponent<UnitProduction>();
        }

        public void TrainKnight()
        {
            if (_knightPrefab == null) return;
            _production.TryEnqueue(new ProductionEntry
            {
                prefab = _knightPrefab,
                goldCost = _knightGoldCost,
                isCombatUnit = true,
                supplyCost = 1
            });
        }

        public void TrainArcher()
        {
            if (_archerPrefab == null) return;
            if (CurrentTier < 2)
            {
                Debug.Log("[Barracks] Archer requires tier 2 Barracks.");
                return;
            }
            _production.TryEnqueue(new ProductionEntry
            {
                prefab = _archerPrefab,
                goldCost = _archerGoldCost,
                isCombatUnit = true,
                supplyCost = 1
            });
        }
    }
}
