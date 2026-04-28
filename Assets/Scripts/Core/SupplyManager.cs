using UnityEngine;

namespace Pantheum.Core
{
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

        public void AddCapacity(int amount) => _totalSupply += amount;

        public void RemoveCapacity(int amount) => _totalSupply = Mathf.Max(0, _totalSupply - amount);

        public bool TryUseSupply(int amount)
        {
            if (_usedSupply + amount > _totalSupply) return false;
            _usedSupply += amount;
            return true;
        }

        public void ReleaseSupply(int amount) => _usedSupply = Mathf.Max(0, _usedSupply - amount);

        public bool HasSupply(int amount) => _usedSupply + amount <= _totalSupply;
    }
}
