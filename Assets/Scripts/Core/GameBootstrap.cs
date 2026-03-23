using System.Collections.Generic;
using UnityEngine;
using Pantheum.Buildings;

namespace Pantheum.Core
{
    [System.Serializable]
    public class PlayerConfig
    {
        public string  playerName        = "Player";
        public Faction faction           = Faction.Player;
        public float   spawnX            = 0f;
        public float   spawnZ            = 0f;
        public int     startingWorkers   = 0;   // only meaningful for Faction.Player
    }

    /// <summary>
    /// Spawns one Castle per entry in the Players list at game start.
    /// The first entry with Faction = Player also centers the camera on its Castle
    /// and spawns its initial workers.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject _castlePrefab;

        [Header("Players")]
        [SerializeField] private List<PlayerConfig> _players = new()
        {
            new PlayerConfig { playerName = "Player 1", faction = Faction.Player, spawnX =   0f, spawnZ =   0f, startingWorkers = 2 },
            new PlayerConfig { playerName = "Player 2", faction = Faction.Enemy,  spawnX =  30f, spawnZ =  30f, startingWorkers = 0 },
        };

        // IEnumerator Start() runs as a coroutine — Unity handles it automatically.
        // Yielding between worker spawns ensures each one gets its own frame so
        // the NavMesh has time to register the previous agent before the next spawn.
        private System.Collections.IEnumerator Start()
        {
            if (_castlePrefab == null)
            {
                Debug.LogError("[GameBootstrap] Castle prefab not assigned.");
                yield break;
            }

            bool cameraSet = false;

            foreach (var config in _players)
            {
                var castle = SpawnCastle(config);
                if (castle == null) continue;

                // Center camera on the first Player-faction castle
                if (!cameraSet && config.faction == Faction.Player)
                {
                    var rtsCamera = Camera.main?.GetComponent<RTSCamera>();
                    rtsCamera?.CenterOn(castle.transform.position);
                    cameraSet = true;
                }

                // Spawn initial workers one per frame so NavMesh positions don't collide.
                for (int i = 0; i < config.startingWorkers; i++)
                {
                    castle.SpawnWorkerImmediate();
                    yield return null;
                }
            }
        }

        private Castle SpawnCastle(PlayerConfig config)
        {
            float groundY      = FindGroundY(config.spawnX, config.spawnZ);
            float pivotOffset  = GetPivotToBottomOffset(_castlePrefab);
            var   pos          = new Vector3(config.spawnX, groundY + pivotOffset, config.spawnZ);

            var go = Instantiate(_castlePrefab, pos, Quaternion.identity);
            go.name = $"Castle ({config.playerName})";

            var building = go.GetComponent<BuildingBase>();
            if (building == null)
            {
                Debug.LogError($"[GameBootstrap] Castle prefab has no BuildingBase. ({config.playerName})");
                return null;
            }

            building.SetFaction(config.faction);

            // Awake() ran before SetFaction(), so enemy castles were registered
            // with the default Player faction. Remove them from the tier lists now.
            if (config.faction != Faction.Player)
            {
                var castle = go.GetComponent<Castle>();
                if (castle != null)
                    BuildingManager.Instance?.RemoveCastleFromTierLists(castle);
            }

            return go.GetComponent<Castle>();
        }

        /// <summary>
        /// Instantiates the prefab at origin, measures how far below the pivot the
        /// mesh bottom is, then immediately destroys the temp object.
        /// Same logic as BuildingPlacer.CreateGhost().
        /// </summary>
        private static float GetPivotToBottomOffset(GameObject prefab)
        {
            var temp = Instantiate(prefab, Vector3.zero, Quaternion.identity);

            // Disable Awake side-effects so managers aren't polluted
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
