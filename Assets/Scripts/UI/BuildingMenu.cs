using UnityEngine;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Core;
using Pantheum.Units;

namespace Pantheum.UI
{
    public class BuildingMenu : MonoBehaviour
    {
        [System.Serializable]
        public struct BuildingEntry
        {
            public string       label;
            public BuildingType type;
            public int          goldCost;
            public GameObject   buildingPrefab; // ONE prefab — ghost and site visual are generated at runtime
        }

        [SerializeField] private BuildingPlacer  _buildingPlacer;
        [SerializeField] private BuildingEntry[] _entries;

        public System.Collections.Generic.IReadOnlyList<BuildingEntry> Entries =>
            _entries ?? System.Array.Empty<BuildingEntry>();

        public bool CanBuild(in BuildingEntry entry) =>
            BuildingManager.Instance  != null
            && ResourceManager.Instance != null
            && BuildingManager.Instance.TierRequirementMet(entry.type)
            && BuildingManager.Instance.CanPlace(entry.type)
            && ResourceManager.Instance.Gold >= entry.goldCost;

        public bool TierMet(in BuildingEntry entry) =>
            BuildingManager.Instance != null
            && BuildingManager.Instance.TierRequirementMet(entry.type);

        public void BeginBuild(in BuildingEntry entry, WorkerController worker)
        {
            if (_buildingPlacer == null)
            {
                Debug.LogError("[BuildingMenu] _buildingPlacer non assigné.");
                return;
            }
            _buildingPlacer.BeginPlacement(entry.type, entry.buildingPrefab, entry.goldCost,
                                           new[] { worker });
        }
    }
}
