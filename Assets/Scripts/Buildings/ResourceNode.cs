using UnityEngine;

namespace Pantheum.Buildings
{
    public enum ResourceType { Gold, Mana }

    /// <summary>
    /// Base class for GoldMine and ManaExtractor.
    /// Workers query HarvestAmountPerTrip and HarvestTime from this component.
    /// </summary>
    public class ResourceNode : BuildingBase
    {
        [Header("Resource")]
        [SerializeField] private ResourceType _resourceType;
        [SerializeField] private int _harvestAmountPerTrip = 10;
        [SerializeField] private float _harvestTime = 3f;

        public ResourceType ResourceType => _resourceType;
        public int HarvestAmountPerTrip => _harvestAmountPerTrip;
        public float HarvestTime => _harvestTime;
    }
}
