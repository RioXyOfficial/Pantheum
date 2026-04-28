using UnityEngine;
using Mirror;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Core;
using Pantheum.Network;
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
            public GameObject   buildingPrefab;
        }

        [SerializeField] private BuildingPlacer  _buildingPlacer;
        [SerializeField] private BuildingEntry[] _entries;

        public System.Collections.Generic.IReadOnlyList<BuildingEntry> Entries =>
            _entries ?? System.Array.Empty<BuildingEntry>();

        public int GetEffectiveCost(in BuildingEntry entry)
        {
            if (entry.type == BuildingType.GoldMine
                && BuildingManager.Instance != null
                && BuildingManager.Instance.GetCount(BuildingType.GoldMine) == 0)
                return 0;
            return entry.goldCost;
        }

        public bool CanBuild(in BuildingEntry entry)
        {
            if (BuildingManager.Instance == null) return false;
            if (!BuildingManager.Instance.TierRequirementMet(entry.type)) return false;
            if (!BuildingManager.Instance.CanPlace(entry.type)) return false;
            int gold = NetworkClient.active && PlayerNetworkController.LocalPlayer != null
                ? PlayerNetworkController.LocalPlayer.Gold
                : (ResourceManager.Instance?.Gold ?? 0);
            return gold >= GetEffectiveCost(entry);
        }

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
            _buildingPlacer.BeginPlacement(entry.type, entry.buildingPrefab, GetEffectiveCost(entry),
                                           new[] { worker });
        }
    }
}
