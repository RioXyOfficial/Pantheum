using UnityEngine;
using Pantheum.Units;

namespace Pantheum.Buildings
{
    /// <summary>
    /// Produces Knight (T1) and Archer (T2) via UnitProduction component.
    /// </summary>
    [RequireComponent(typeof(UnitProduction))]
    public class Barracks : BuildingBase
    {
        [Header("Production")]
        [SerializeField] private GameObject _knightPrefab;
        [SerializeField] private GameObject _archerPrefab;
        [SerializeField] private int _knightGoldCost = 75;
        [SerializeField] private int _archerGoldCost = 100;

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
