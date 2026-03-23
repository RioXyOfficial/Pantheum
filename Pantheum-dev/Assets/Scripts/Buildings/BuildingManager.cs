using System.Collections.Generic;
using UnityEngine;

namespace Pantheum.Buildings
{
    /// <summary>
    /// Singleton that tracks all placed buildings and enforces per-type limits
    /// that scale with the number of Castles of the required tier.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        private readonly Dictionary<BuildingType, int> _counts = new();

        // Index 0 = T1 castles, 1 = T2, 2 = T3
        private readonly List<Castle>[] _castlesByTier = { new(), new(), new() };

        // Maximum buildings of this type per Castle of the required tier.
        // 0 means unlimited (int.MaxValue returned by GetLimit).
        private static readonly Dictionary<BuildingType, int> BaseLimits = new()
        {
            { BuildingType.Castle,        0 },
            { BuildingType.Barracks,      1 },
            { BuildingType.Academy,       1 },
            { BuildingType.Blacksmith,    1 },
            { BuildingType.House,         4 },
            { BuildingType.GoldMine,      2 },
            { BuildingType.ManaExtractor, 2 },
        };

        // Which Castle tier must exist before this building type can be placed.
        private static readonly Dictionary<BuildingType, int> RequiredTier = new()
        {
            { BuildingType.Castle,        1 },
            { BuildingType.Barracks,      1 },
            { BuildingType.Academy,       1 },
            { BuildingType.Blacksmith,    1 },
            { BuildingType.House,         1 },
            { BuildingType.GoldMine,      1 },
            { BuildingType.ManaExtractor, 2 }, // T2 building — gated on T2 Castle
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Register(BuildingBase b)
        {
            _counts.TryGetValue(b.BuildingType, out int c);
            _counts[b.BuildingType] = c + 1;

            if (b is Castle castle)
                _castlesByTier[(int)castle.Tier - 1].Add(castle);
        }

        public void Unregister(BuildingBase b)
        {
            if (_counts.TryGetValue(b.BuildingType, out int c))
                _counts[b.BuildingType] = Mathf.Max(0, c - 1);

            if (b is Castle castle)
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
        /// Returns how many of <paramref name="type"/> can be placed given
        /// the current Castle tier counts. Castles themselves are unlimited.
        /// </summary>
        public int GetLimit(BuildingType type)
        {
            if (BaseLimits[type] == 0) return int.MaxValue;
            return BaseLimits[type] * GetCastleCount(RequiredTier[type]);
        }

        public bool CanPlace(BuildingType type)
        {
            _counts.TryGetValue(type, out int c);
            return c < GetLimit(type);
        }

        public bool TierRequirementMet(BuildingType type) =>
            GetCastleCount(RequiredTier[type]) > 0;
    }
}
