using Mirror;
using UnityEngine;
using Pantheum.Core;

namespace Pantheum.Network
{
    public class PantheumNetworkManager : NetworkManager
    {
        [Header("Pantheum")]
        [SerializeField] private GameObject _playerControllerPrefab;

        private int _playerCount;

        public static PantheumNetworkManager Inst => singleton as PantheumNetworkManager;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _playerCount = 0;
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (_playerControllerPrefab == null)
            {
                Debug.LogError("[PantheumNetworkManager] _playerControllerPrefab not assigned!");
                return;
            }

            var go   = Instantiate(_playerControllerPrefab);
            var ctrl = go.GetComponent<PlayerNetworkController>();
            Faction assignedFaction = _playerCount == 0 ? Faction.Player : Faction.Enemy;
            if (ctrl != null)
                ctrl.ServerSetFaction(assignedFaction);

            NetworkServer.AddPlayerForConnection(conn, go);
            _playerCount++;

            if (_playerCount >= 2)
            {
                var bootstrap = FindFirstObjectByType<GameBootstrap>();
                if (bootstrap != null)
                    bootstrap.StartGame();
                else
                    Debug.LogError("[PantheumNetworkManager] GameBootstrap not found in scene.");
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _playerCount = 0;
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            Debug.Log("[PantheumNetworkManager] Client disconnected.");
        }
    }
}
