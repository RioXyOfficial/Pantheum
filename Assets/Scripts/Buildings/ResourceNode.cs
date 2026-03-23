using UnityEngine;

namespace Pantheum.Buildings
{
    public enum ResourceType { Gold, Mana }

    /// <summary>
    /// Base class for GoldMine and ManaExtractor.
    /// Workers query HarvestAmountPerTrip and HarvestTime from this component.
    /// Harvest amount scales with tier.
    /// </summary>
    public class ResourceNode : BuildingBase
    {
        [Header("Resource")]
        [SerializeField] private ResourceType _resourceType;
        [Tooltip("Harvest amount per trip at each tier (index 0 = T1, 1 = T2, 2 = T3).")]
        [SerializeField] private int[] _harvestPerTier = { 10, 18, 30 };
        [SerializeField] private float _harvestTime = 3f;

        private int _harvestAmountPerTrip;

        public ResourceType ResourceType      => _resourceType;
        public int          HarvestAmountPerTrip => _harvestAmountPerTrip;
        public float        HarvestTime       => _harvestTime;

        protected override void Awake()
        {
            base.Awake();
            _harvestAmountPerTrip = HarvestForTier(CurrentTier);
        }

        protected override void OnTierUpgraded(int newTier)
        {
            _harvestAmountPerTrip = HarvestForTier(newTier);
        }

        private int HarvestForTier(int tier)
        {
            int idx = Mathf.Clamp(tier - 1, 0, _harvestPerTier.Length - 1);
            return _harvestPerTier.Length > 0 ? _harvestPerTier[idx] : 10;
        }
    }
}
