using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Network;
using Mirror;

namespace Pantheum.Core
{
    [System.Serializable]
    public class PlayerConfig
    {
        public string  playerName      = "Player";
        public Faction faction         = Faction.Player;
        public float   spawnX         = 0f;
        public float   spawnZ         = 0f;
        public int     startingWorkers = 0;
    }

    public class GameBootstrap : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject _castlePrefab;

        [Header("Players")]
        [SerializeField] private List<PlayerConfig> _players = new()
        {
            new PlayerConfig { playerName = "Player 1", faction = Faction.Player, spawnX =  0f, spawnZ =  0f, startingWorkers = 2 },
            new PlayerConfig { playerName = "Player 2", faction = Faction.Enemy,  spawnX = 30f, spawnZ = 30f, startingWorkers = 2 },
        };

        private bool _gameStarted;

        public void StartGame()
        {
            if (_gameStarted) return;
            _gameStarted = true;
            StartCoroutine(RunGame());
        }

        private IEnumerator RunGame()
        {
            if (_castlePrefab == null)
            {
                Debug.LogError("[GameBootstrap] Castle prefab not assigned.");
                yield break;
            }

            foreach (var config in _players)
            {
                var castle = SpawnCastle(config);
                if (castle == null) continue;

                for (int i = 0; i < config.startingWorkers; i++)
                {
                    castle.SpawnWorkerImmediate();
                    yield return null;
                }
            }
        }

        private Castle SpawnCastle(PlayerConfig config)
        {
            float groundY     = FindGroundY(config.spawnX, config.spawnZ);
            float pivotOffset = GetPivotToBottomOffset(_castlePrefab);
            var   pos         = new Vector3(config.spawnX, groundY + pivotOffset, config.spawnZ);

            var go = Instantiate(_castlePrefab, pos, Quaternion.identity);
            go.name = $"Castle ({config.playerName})";

            var factionSync = go.GetComponent<NetworkFactionSync>();
            if (factionSync != null)
            {
                factionSync.SetUnderConstruction(false);
                factionSync.SetNetworkFaction(config.faction);
            }

            if (go.TryGetComponent<ConstructionSite>(out var constructionSite))
                DestroyImmediate(constructionSite);

            var building = go.GetComponent<BuildingBase>();
            if (building == null)
            {
                Debug.LogError($"[GameBootstrap] Castle prefab has no BuildingBase.");
                return null;
            }

            building.SetFaction(config.faction);

            if (config.faction != Faction.Player)
            {
                var castle = go.GetComponent<Castle>();
                if (castle != null)
                    BuildingManager.Instance?.RemoveCastleFromTierLists(castle);
            }

            if (NetworkServer.active)
            {
                var playerCtrl = PlayerNetworkController.FindForFaction(config.faction);
                if (playerCtrl != null)
                    NetworkServer.Spawn(go, playerCtrl.connectionToClient);
                else
                    NetworkServer.Spawn(go);
                PlayerNetworkController.BroadcastFaction(
                    go.GetComponent<NetworkIdentity>(), config.faction);
            }

            return go.GetComponent<Castle>();
        }

        private static float GetPivotToBottomOffset(GameObject prefab)
        {
            var temp = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            foreach (var bb in temp.GetComponentsInChildren<BuildingBase>())
                bb.CancelRegistration();
            var rend = temp.GetComponentInChildren<Renderer>();
            float offset = rend != null ? temp.transform.position.y - rend.bounds.min.y : 0f;
            Destroy(temp);
            return offset;
        }

        private static float FindGroundY(float x, float z)
        {
            var ray = new Ray(new Vector3(x, 500f, z), Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
                return hit.point.y;
            return 0f;
        }
    }
}
