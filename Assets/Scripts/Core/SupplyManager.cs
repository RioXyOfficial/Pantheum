using UnityEngine;

namespace Pantheum.Core
{
    /// <summary>
    /// Tracks global supply for combat units only.
    /// Workers never interact with this manager.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SupplyManager : MonoBehaviour
    {
        public static SupplyManager Instance { get; private set; }

        private int _usedSupply;
        private int _totalSupply;

        public int UsedSupply => _usedSupply;
        public int TotalSupply => _totalSupply;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Called by House on placement.</summary>
        public void AddCapacity(int amount) => _totalSupply += amount;

        /// <summary>Called by House on destruction.</summary>
        public void RemoveCapacity(int amount) => _totalSupply = Mathf.Max(0, _totalSupply - amount);

        /// <summary>
        /// Reserves supply for a combat unit spawn.
        /// Returns false if not enough supply — caller should abort spawn.
        /// </summary>
        public bool TryUseSupply(int amount)
        {
            if (_usedSupply + amount > _totalSupply) return false;
            _usedSupply += amount;
            return true;
        }

        /// <summary>Called when a combat unit dies.</summary>
        public void ReleaseSupply(int amount) => _usedSupply = Mathf.Max(0, _usedSupply - amount);

        public bool HasSupply(int amount) => _usedSupply + amount <= _totalSupply;
    }
}
