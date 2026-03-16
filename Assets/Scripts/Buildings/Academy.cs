using UnityEngine;
using Pantheum.Units;

namespace Pantheum.Buildings
{
    /// <summary>
    /// Produces Mage (T1) and Valkyrie (T2) via UnitProduction component.
    /// </summary>
    [RequireComponent(typeof(UnitProduction))]
    public class Academy : BuildingBase
    {
        [Header("Production")]
        [SerializeField] private GameObject _magePrefab;
        [SerializeField] private GameObject _valkyriePrefab;
        [SerializeField] private int _mageGoldCost = 100;
        [SerializeField] private int _valkyrieGoldCost = 150;

        private UnitProduction _production;

        protected override void Awake()
        {
            base.Awake();
            _production = GetComponent<UnitProduction>();
        }

        public void TrainMage()
        {
            if (_magePrefab == null) return;
            _production.TryEnqueue(new ProductionEntry
            {
                prefab = _magePrefab,
                goldCost = _mageGoldCost,
                isCombatUnit = true,
                supplyCost = 1
            });
        }

        public void TrainValkyrie()
        {
            if (_valkyriePrefab == null) return;
            _production.TryEnqueue(new ProductionEntry
            {
                prefab = _valkyriePrefab,
                goldCost = _valkyrieGoldCost,
                isCombatUnit = true,
                supplyCost = 1
            });
        }
    }
}
