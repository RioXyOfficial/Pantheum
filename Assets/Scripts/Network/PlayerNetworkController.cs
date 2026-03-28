using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Pantheum.Core;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Units;
using Pantheum.Selection;

namespace Pantheum.Network
{
    public class PlayerNetworkController : NetworkBehaviour
    {
        [Header("Building Prefabs (index = BuildingType enum value)")]
        [SerializeField] private GameObject[] _buildingPrefabs;
        [SerializeField] private int[]        _buildingGoldCosts;

        [SyncVar(hook = nameof(OnFactionChanged))]
        private Faction _faction;
        public Faction Faction => _faction;

        [SyncVar] private int _gold = 100000;
        [SyncVar] private int _mana = 0;
        public int Gold => _gold;
        public int Mana => _mana;

        public static PlayerNetworkController LocalPlayer { get; private set; }

        public GameObject GetBuildingPrefab(int index)
        {
            if (index < 0 || index >= _buildingPrefabs.Length) return null;
            return _buildingPrefabs[index];
        }

        public override void OnStartLocalPlayer()
        {
            LocalPlayer = this;
            StartCoroutine(InitLocalPlayer());
        }

        private System.Collections.IEnumerator InitLocalPlayer()
        {
            yield return new WaitForSeconds(0.3f);
            BuildingManager.Instance?.RebuildCastleTierLists();
            StartCoroutine(CenterCameraOnOwnCastle());
        }

        public override void OnStopLocalPlayer()
        {
            if (LocalPlayer == this) LocalPlayer = null;
        }

        private void OnFactionChanged(Faction oldF, Faction newF)
        {
            BuildingManager.Instance?.RebuildCastleTierLists();
        }

        [Server]
        public void ServerSetFaction(Faction f) => _faction = f;

        [Server]
        public bool SpendGold(int amount)
        {
            if (_gold < amount) return false;
            _gold -= amount;
            return true;
        }

        [Server]
        public void DepositGold(int amount) => _gold += amount;

        [Server]
        public bool SpendMana(int amount)
        {
            if (_mana < amount) return false;
            _mana -= amount;
            return true;
        }

        [Server]
        public void DepositMana(int amount) => _mana += amount;

        public static PlayerNetworkController FindForFaction(Faction f)
        {
            foreach (var conn in NetworkServer.connections.Values)
            {
                var ctrl = conn.identity?.GetComponent<PlayerNetworkController>();
                if (ctrl != null && ctrl.Faction == f) return ctrl;
            }
            return null;
        }

        private System.Collections.IEnumerator CenterCameraOnOwnCastle()
        {
            Castle ownCastle = null;
            float timeout = 10f;
            while (ownCastle == null && timeout > 0f)
            {
                yield return new WaitForSeconds(0.2f);
                timeout -= 0.2f;
                foreach (var bb in FindObjectsByType<BuildingBase>(FindObjectsSortMode.None))
                {
                    if (bb is Castle c && bb.Faction == _faction)
                    {
                        ownCastle = c;
                        break;
                    }
                }
            }
            if (ownCastle != null)
                Camera.main?.GetComponent<RTSCamera>()?.CenterOn(ownCastle.transform.position);
        }

        [Command]
        public void CmdPlaceBuilding(int typeIndex, Vector3 position, int goldCost, NetworkIdentity workerIdentity)
        {
            if (typeIndex < 0 || typeIndex >= _buildingPrefabs.Length)
            {
                Debug.LogError($"[PlayerNetworkController] CmdPlaceBuilding: invalid index {typeIndex}");
                return;
            }
            var prefab = _buildingPrefabs[typeIndex];
            if (prefab == null) return;

            var bb = prefab.GetComponent<BuildingBase>();
            Vector2Int gridSize = bb != null ? bb.GridSize : new Vector2Int(2, 2);

            if (GridSystem.Instance != null && !GridSystem.Instance.CanPlace(position, gridSize))
                return;

            ResourceManager.ActiveNetworkPlayer = this;
            if (goldCost > 0 && !ResourceManager.Instance.SpendGold(goldCost))
            {
                ResourceManager.ActiveNetworkPlayer = null;
                return;
            }
            ResourceManager.ActiveNetworkPlayer = null;

            float pivotToBottom = GetPrefabPivotToBottom(prefab);
            Vector3 spawnPos = new Vector3(position.x, position.y + pivotToBottom, position.z);

            var siteGO = Instantiate(prefab, spawnPos, Quaternion.identity);

            foreach (var b in siteGO.GetComponentsInChildren<BuildingBase>())
                b.StartConstruction();
            foreach (var mb in siteGO.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is ConstructionSite) continue;
                if (mb is Mirror.NetworkBehaviour) continue;
                if (mb is Selectable) continue;
                mb.enabled = false;
            }

            var site = siteGO.GetComponent<ConstructionSite>();
            if (site == null)
            {
                Debug.LogError($"[PlayerNetworkController] ConstructionSite missing on prefab '{prefab.name}'.");
                NetworkServer.Destroy(siteGO);
                ResourceManager.ActiveNetworkPlayer = this;
                ResourceManager.Instance.DepositGold(goldCost);
                ResourceManager.ActiveNetworkPlayer = null;
                return;
            }
            site.enabled = true;
            site.Init(prefab, null, goldCost: goldCost, typeIndex: typeIndex);

            var factionSync = siteGO.GetComponent<NetworkFactionSync>();
            if (factionSync != null)
            {
                factionSync.SetNetworkFaction(_faction);
                factionSync.SetUnderConstruction(true);
            }
            else SetGameObjectFaction(siteGO, _faction);

            NetworkServer.Spawn(siteGO, connectionToClient);

            var ni = siteGO.GetComponent<NetworkIdentity>();
            if (ni != null) RpcApplyFaction(ni, _faction);

            if (workerIdentity != null)
            {
                var worker = workerIdentity.GetComponent<WorkerController>();
                if (worker != null)
                    worker.AssignToConstruction(site);
            }
        }

        [ClientRpc]
        private void RpcApplyFaction(NetworkIdentity objId, Faction f)
        {
            if (objId == null) return;
            foreach (var b in objId.GetComponentsInChildren<BuildingBase>(true))
                b.SetFaction(f);
            foreach (var ub in objId.GetComponentsInChildren<UnitBase>(true))
                ub.SetFaction(f);
        }

        public static void BroadcastFaction(NetworkIdentity objId, Faction f)
        {
            if (!NetworkServer.active || objId == null) return;
            foreach (var conn in NetworkServer.connections.Values)
            {
                var ctrl = conn.identity?.GetComponent<PlayerNetworkController>();
                if (ctrl != null) { ctrl.RpcApplyFaction(objId, f); return; }
            }
        }

        [Command]
        public void CmdSpawnWorker(NetworkIdentity castleId)
        {
            ResourceManager.ActiveNetworkPlayer = this;
            try { castleId?.GetComponent<Castle>()?.SpawnWorker(); }
            finally { ResourceManager.ActiveNetworkPlayer = null; }
        }

        [Command]
        public void CmdTrainKnight(NetworkIdentity barracksId)
        {
            ResourceManager.ActiveNetworkPlayer = this;
            barracksId?.GetComponent<Barracks>()?.TrainKnight();
            ResourceManager.ActiveNetworkPlayer = null;
        }

        [Command]
        public void CmdTrainArcher(NetworkIdentity barracksId)
        {
            ResourceManager.ActiveNetworkPlayer = this;
            barracksId?.GetComponent<Barracks>()?.TrainArcher();
            ResourceManager.ActiveNetworkPlayer = null;
        }

        [Command]
        public void CmdTrainMage(NetworkIdentity academyId)
        {
            ResourceManager.ActiveNetworkPlayer = this;
            academyId?.GetComponent<Academy>()?.TrainMage();
            ResourceManager.ActiveNetworkPlayer = null;
        }

        [Command]
        public void CmdTrainValkyrie(NetworkIdentity academyId)
        {
            ResourceManager.ActiveNetworkPlayer = this;
            academyId?.GetComponent<Academy>()?.TrainValkyrie();
            ResourceManager.ActiveNetworkPlayer = null;
        }

        [Command]
        public void CmdMoveUnit(NetworkIdentity unitId, Vector3 destination)
        {
            var unit = unitId?.GetComponent<UnitBase>();
            if (unit is CombatUnit cu) cu.OrderMove(destination);
            else unit?.MoveTo(destination);
        }

        [Command]
        public void CmdAttackMove(NetworkIdentity unitId, Vector3 destination)
        {
            unitId?.GetComponent<CombatUnit>()?.OrderAttackMove(destination);
        }

        [Command]
        public void CmdOrderWorkerMove(NetworkIdentity workerId, Vector3 destination)
        {
            workerId?.GetComponent<WorkerController>()?.OrderMove(destination);
        }

        [Command]
        public void CmdAssignWorkerHarvest(NetworkIdentity workerId, NetworkIdentity resourceId)
        {
            var w = workerId?.GetComponent<WorkerController>();
            var r = resourceId?.GetComponent<ResourceNode>();
            if (w != null && r != null) w.AssignToHarvest(r);
        }

        [Command]
        public void CmdAssignWorkerBuild(NetworkIdentity workerId, NetworkIdentity siteId)
        {
            var w = workerId?.GetComponent<WorkerController>();
            var s = siteId?.GetComponent<ConstructionSite>();
            if (w != null && s != null) w.AssignToConstruction(s);
        }

        [Command]
        public void CmdUpgradeBuilding(NetworkIdentity buildingId)
        {
            ResourceManager.ActiveNetworkPlayer = this;
            buildingId?.GetComponent<BuildingBase>()?.Upgrade();
            ResourceManager.ActiveNetworkPlayer = null;
        }

        [Command]
        public void CmdSetAttackTarget(NetworkIdentity attackerId, NetworkIdentity targetId)
        {
            var attacker = attackerId?.GetComponent<CombatUnit>();
            var target   = targetId?.GetComponent<UnitBase>();
            if (attacker != null && target != null) attacker.SetTarget(target);
        }

        [Command]
        public void CmdAttackBuilding(NetworkIdentity attackerId, NetworkIdentity buildingId)
        {
            var attacker = attackerId?.GetComponent<CombatUnit>();
            var building = buildingId?.GetComponent<BuildingBase>();
            if (attacker != null && building != null) attacker.SetBuildingTarget(building);
        }

        [Command]
        public void CmdUpgradeAttack(NetworkIdentity blacksmithId)
        {
            ResourceManager.ActiveNetworkPlayer = this;
            blacksmithId?.GetComponent<Blacksmith>()?.UpgradeAttack();
            ResourceManager.ActiveNetworkPlayer = null;
        }

        [Command]
        public void CmdUpgradeArmor(NetworkIdentity blacksmithId)
        {
            ResourceManager.ActiveNetworkPlayer = this;
            blacksmithId?.GetComponent<Blacksmith>()?.UpgradeArmor();
            ResourceManager.ActiveNetworkPlayer = null;
        }

        [Command]
        public void CmdDemolish(NetworkIdentity buildingId)
        {
            ResourceManager.ActiveNetworkPlayer = this;
            buildingId?.GetComponent<BuildingBase>()?.Demolish();
            ResourceManager.ActiveNetworkPlayer = null;
        }

        [Command]
        public void CmdCancelConstruction(NetworkIdentity siteId)
        {
            ResourceManager.ActiveNetworkPlayer = this;
            siteId?.GetComponent<ConstructionSite>()?.CancelConstruction();
            ResourceManager.ActiveNetworkPlayer = null;
        }

        private static float GetPrefabPivotToBottom(GameObject prefab)
        {
            if (prefab == null) return 0f;
            var rend = prefab.GetComponentInChildren<Renderer>();
            if (rend == null) return 0f;
            var mf = rend.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return 0f;
            float belowPivot = -mf.sharedMesh.bounds.min.y * prefab.transform.localScale.y;
            return Mathf.Max(0f, belowPivot);
        }

        private static void SetGameObjectFaction(GameObject go, Faction faction)
        {
            foreach (var b in go.GetComponentsInChildren<BuildingBase>())
                b.SetFaction(faction);
            foreach (var ub in go.GetComponentsInChildren<UnitBase>())
                ub.SetFaction(faction);
        }
    }
}
