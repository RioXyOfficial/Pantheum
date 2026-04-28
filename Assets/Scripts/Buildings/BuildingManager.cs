using System.Collections.Generic;
using UnityEngine;
using Pantheum.Core;
using Pantheum.Network;

namespace Pantheum.Buildings
{
    [DefaultExecutionOrder(-100)]
    public class BuildingManager : MonoBehaviour
    {
        [System.Serializable]
        public struct BuildingLimitEntry
        {
            public BuildingType type;
            [Tooltip("Slots unlocked per castle of that tier. Index 0=T1, 1=T2, 2=T3.")]
            public int t1;
            public int t2;
            public int t3;
        }

        public static BuildingManager Instance { get; private set; }

        [Header("Building Limits (slots unlocked per castle tier)")]
        [SerializeField] private BuildingLimitEntry[] _limitEntries = new BuildingLimitEntry[]
        {
            new() { type = BuildingType.Barracks,      t1 = 1, t2 = 2, t3 = 2 },
            new() { type = BuildingType.Academy,       t1 = 0, t2 = 1, t3 = 2 },
            new() { type = BuildingType.Blacksmith,    t1 = 0, t2 = 1, t3 = 1 },
            new() { type = BuildingType.House,         t1 = 3, t2 = 5, t3 = 7 },
            new() { type = BuildingType.GoldMine,      t1 = 2, t2 = 3, t3 = 4 },
            new() { type = BuildingType.ManaExtractor, t1 = 2, t2 = 3, t3 = 4 },
        };

        private Dictionary<BuildingType, BuildingLimitEntry> _limits;

        private readonly List<Castle>[] _castlesByTier = { new(), new(), new() };

        private static readonly Dictionary<BuildingType, int> RequiredTier = new()
        {
            { BuildingType.Castle,        1 },
            { BuildingType.Barracks,      1 },
            { BuildingType.Academy,       2 },
            { BuildingType.Blacksmith,    2 },
            { BuildingType.House,         1 },
            { BuildingType.GoldMine,      1 },
            { BuildingType.ManaExtractor, 1 },
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _limits = new Dictionary<BuildingType, BuildingLimitEntry>();
            foreach (var entry in _limitEntries)
                _limits[entry.type] = entry;
        }

        private static Faction GetLocalFaction() =>
            PlayerNetworkController.LocalPlayer?.Faction ?? Faction.Player;

        public void Register(BuildingBase b)
        {
            if (b is Castle castle && castle.Faction == GetLocalFaction())
                _castlesByTier[(int)castle.Tier - 1].Add(castle);
        }

        public void RebuildCastleTierLists()
        {
            foreach (var list in _castlesByTier) list.Clear();
            Faction localFaction = GetLocalFaction();
            foreach (var castle in Object.FindObjectsByType<Castle>(FindObjectsSortMode.None))
                if (castle.Faction == localFaction)
                    _castlesByTier[(int)castle.Tier - 1].Add(castle);
        }

        public void UpdateCastleTier(Castle castle, CastleTier oldTier)
        {
            if (castle.Faction != GetLocalFaction()) return;
            _castlesByTier[(int)oldTier - 1].Remove(castle);
            _castlesByTier[(int)castle.Tier - 1].Add(castle);
        }

        public int GetCastleCount(int tier, Faction faction)
        {
            int count = 0;
            foreach (var castle in Object.FindObjectsByType<Castle>(FindObjectsSortMode.None))
            {
                if (castle.Faction != faction) continue;
                if ((int)castle.Tier >= tier)
                    count++;
            }
            return count;
        }

        public int GetCount(BuildingType type, Faction faction)
        {
            int count = 0;
            foreach (var bb in Object.FindObjectsByType<BuildingBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (bb.IsRegistered && bb.BuildingType == type && bb.Faction == faction)
                    count++;
            return count;
        }

        public int GetLimit(BuildingType type, Faction faction)
        {
            if (type == BuildingType.Castle) return int.MaxValue;
            if (_limits == null || !_limits.TryGetValue(type, out var entry))
                return int.MaxValue;

            int t1 = 0, t2 = 0, t3 = 0;
            foreach (var castle in Object.FindObjectsByType<Castle>(FindObjectsSortMode.None))
            {
                if (castle.Faction != faction) continue;
                switch (castle.Tier)
                {
                    case CastleTier.T1: t1++; break;
                    case CastleTier.T2: t2++; break;
                    case CastleTier.T3: t3++; break;
                }
            }

            return t1 * entry.t1 + t2 * entry.t2 + t3 * entry.t3;
        }

        public bool CanPlace(BuildingType type, Faction faction)
        {
            return GetCount(type, faction) < GetLimit(type, faction);
        }

        public bool TierRequirementMet(BuildingType type, Faction faction)
        {
            return GetCastleCount(RequiredTier[type], faction) > 0;
        }

        public void Unregister(BuildingBase b)
        {
            if (b is Castle castle && castle.Faction == GetLocalFaction())
                _castlesByTier[(int)castle.Tier - 1].Remove(castle);
        }

        public int GetCastleCount(int tier)
        {
            int count = 0;
            for (int i = tier - 1; i < _castlesByTier.Length; i++)
                count += _castlesByTier[i].Count;
            return count;
        }

        public int GetLimit(BuildingType type)
        {
            if (type == BuildingType.Castle) return int.MaxValue;
            if (_limits == null || !_limits.TryGetValue(type, out var entry))
                return int.MaxValue;

            return _castlesByTier[0].Count * entry.t1
                 + _castlesByTier[1].Count * entry.t2
                 + _castlesByTier[2].Count * entry.t3;
        }

        public int GetCount(BuildingType type)
        {
            Faction local = GetLocalFaction();
            int count = 0;
            foreach (var bb in Object.FindObjectsByType<BuildingBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (bb.IsRegistered && bb.BuildingType == type && bb.Faction == local)
                    count++;
            return count;
        }

        public bool CanPlace(BuildingType type)
        {
            return GetCount(type) < GetLimit(type);
        }

        public bool TierRequirementMet(BuildingType type) =>
            GetCastleCount(RequiredTier[type]) > 0;

        public void RegisterCastleTier(BuildingBase b)
        {
            if (b is Castle castle && castle.Faction == GetLocalFaction())
            {
                var list = _castlesByTier[(int)castle.Tier - 1];
                if (!list.Contains(castle))
                    list.Add(castle);
            }
        }

        public void RemoveCastleFromTierLists(Castle castle)
        {
            foreach (var list in _castlesByTier)
                list.Remove(castle);
        }
    }
}
