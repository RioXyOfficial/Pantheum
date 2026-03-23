using UnityEngine;

namespace Pantheum.Core
{
    [DefaultExecutionOrder(-100)]
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [SerializeField] private int _startingGold = 200;
        [SerializeField] private int _startingMana = 0;

        public int Gold { get; private set; }
        public int Mana { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Gold = _startingGold;
            Mana = _startingMana;
        }

        public void DepositGold(int amount) => Gold += amount;
        public void DepositMana(int amount) => Mana += amount;

        /// <returns>True if gold was spent successfully.</returns>
        public bool SpendGold(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        /// <returns>True if mana was spent successfully.</returns>
        public bool SpendMana(int amount)
        {
            if (Mana < amount) return false;
            Mana -= amount;
            return true;
        }
    }
}
