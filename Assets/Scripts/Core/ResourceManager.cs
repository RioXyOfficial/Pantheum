using UnityEngine;
using Mirror;

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

        public static Pantheum.Network.PlayerNetworkController ActiveNetworkPlayer { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Gold = _startingGold;
            Mana = _startingMana;
        }

        public void DepositGold(int amount)
        {
            if (NetworkServer.active && ActiveNetworkPlayer != null)
            { ActiveNetworkPlayer.DepositGold(amount); return; }
            Gold += amount;
        }

        public void DepositMana(int amount)
        {
            if (NetworkServer.active && ActiveNetworkPlayer != null)
            { ActiveNetworkPlayer.DepositMana(amount); return; }
            Mana += amount;
        }

        public bool SpendGold(int amount)
        {
            if (amount <= 0) return false;
            if (NetworkServer.active && ActiveNetworkPlayer != null)
                return ActiveNetworkPlayer.SpendGold(amount);
            if (Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        public bool SpendMana(int amount)
        {
            if (amount <= 0) return false;
            if (NetworkServer.active && ActiveNetworkPlayer != null)
                return ActiveNetworkPlayer.SpendMana(amount);
            if (Mana < amount) return false;
            Mana -= amount;
            return true;
        }
    }
}
