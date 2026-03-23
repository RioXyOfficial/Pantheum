using System.Collections.Generic;
using UnityEngine;
using Pantheum.Core;

namespace Pantheum.Buildings
{
    /// <summary>
    /// Singleton that tracks all placed buildings and enforces per-type limits
    /// that scale with the total castle weight (T1=1, T2=2, T3=3).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BuildingManager : MonoBehaviour
    {
        /// <summary>
        /// Per-building-type limit table configurable in the Inspector.
        /// Each castle contributes limitPerCastleTier[its tier - 1] to the total limit.
        /// Total limit = sum of all castles' individual contributions.
        /// Example: GoldMine [2, 3, 4] + 1xT3 castle + 1xT2 castle = 4+3 = 7.
        /// </summary>
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

        private readonly Dictionary<BuildingType, int> _counts = new();

        // Index 0 = T1 castles, 1 = T2, 2 = T3
        private readonly List<Castle>[] _castlesByTier = { new(), new(), new() };

        // Which Castle tier must exist before this building type can be placed.
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

        public void Register(BuildingBase b)
        {
            _counts.TryGetValue(b.BuildingType, out int c);
            _counts[b.BuildingType] = c + 1;

            if (b is Castle castle && castle.Faction == Faction.Player)
                _castlesByTier[(int)castle.Tier - 1].Add(castle);
        }

        /// <summary>
        /// Moves a Castle from its old tier list to its new tier list after an upgrade.
        /// </summary>
        public void UpdateCastleTier(Castle castle, CastleTier oldTier)
        {
            _castlesByTier[(int)oldTier - 1].Remove(castle);
            _castlesByTier[(int)castle.Tier - 1].Add(castle);
        }

        public void Unregister(BuildingBase b)
        {
            if (_counts.TryGetValue(b.BuildingType, out int c))
                _counts[b.BuildingType] = Mathf.Max(0, c - 1);

            if (b is Castle castle && castle.Faction == Faction.Player)
                _castlesByTier[(int)castle.Tier - 1].Remove(castle);
        }

        /// <summary>
        /// Returns the count of Castles at <paramref name="tier"/> or higher.
        /// A T2 Castle therefore also satisfies a T1 requirement.
        /// </summary>
        public int GetCastleCount(int tier)
        {
            int count = 0;
            for (int i = tier - 1; i < _castlesByTier.Length; i++)
                count += _castlesByTier[i].Count;
            return count;
        }

/// <summary>
        /// Returns how many of <paramref name="type"/> can be placed.
        /// Looks up the limit table configured in the Inspector using the current castle weight.
        /// Castles themselves are always unlimited.
        /// </summary>
        public int GetLimit(BuildingType type)
        {
            if (type == BuildingType.Castle) return int.MaxValue;
            if (_limits == null || !_limits.TryGetValue(type, out var entry))
                return int.MaxValue;

            // Each castle contributes based on its own tier.
            return _castlesByTier[0].Count * entry.t1
                 + _castlesByTier[1].Count * entry.t2
                 + _castlesByTier[2].Count * entry.t3;
        }

        public bool CanPlace(BuildingType type)
        {
            _counts.TryGetValue(type, out int c);
            return c < GetLimit(type);
        }

        public bool TierRequirementMet(BuildingType type) =>
            GetCastleCount(RequiredTier[type]) > 0;

        /// <summary>
        /// Adds a completed building's castle to tier tracking (if applicable).
        /// Called by CompleteConstruction() for buildings placed via StartConstruction().
        /// </summary>
        public void RegisterCastleTier(BuildingBase b)
        {
            if (b is Castle castle && castle.Faction == Faction.Player)
            {
                var list = _castlesByTier[(int)castle.Tier - 1];
                if (!list.Contains(castle))
                    list.Add(castle);
            }
        }

        /// <summary>
        /// Removes a castle from all tier tracking lists.
        /// Call after changing a castle's faction to non-Player.
        /// </summary>
        public void RemoveCastleFromTierLists(Castle castle)
        {
            foreach (var list in _castlesByTier)
                list.Remove(castle);
        }

        /// <summary>Returns the number of currently placed buildings of <paramref name="type"/>.</summary>
        public int GetCount(BuildingType type)
        {
            _counts.TryGetValue(type, out int c);
            return c;
        }
    }
}
