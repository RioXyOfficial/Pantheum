using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Core;
using Pantheum.Network;
using Pantheum.Selection;
using Pantheum.Units;

namespace Pantheum.UI
{
    public class SelectionPanel : MonoBehaviour
    {
        [SerializeField] private float _panelHeight = 200f;
        [SerializeField] private BuildingMenu _buildingMenu;

        private SelectionManager _selectionManager;

        private void Start() => _selectionManager = SelectionManager.Instance;

        public static bool IsPointerOverUI { get; private set; }

        private static bool Net => NetworkClient.active;
        private static PlayerNetworkController NC => PlayerNetworkController.LocalPlayer;

        private void OnGUI()
        {
            DrawResourceBar();

            if (_selectionManager == null) { IsPointerOverUI = false; return; }
            var selected = _selectionManager.Selected;

            if (selected.Count > 0)
            {
                var panelRect = new Rect(0, Screen.height - _panelHeight, Screen.width, _panelHeight);
                IsPointerOverUI = panelRect.Contains(Event.current.mousePosition);
            }
            else
            {
                IsPointerOverUI = false;
            }

            if (selected.Count == 0) return;
            DrawSelectionPanel(selected);
        }

        private void DrawResourceBar()
        {
            const float w = 340f, h = 28f;
            float x = Screen.width - w - 10f;
            GUI.Box(new Rect(x, 10, w, h), GUIContent.none);

            int gold = Net && NC != null ? NC.Gold : (ResourceManager.Instance?.Gold ?? 0);
            int mana = Net && NC != null ? NC.Mana : (ResourceManager.Instance?.Mana ?? 0);
            string txt = $"  Or : {gold}    Mana : {mana}";
            if (SupplyManager.Instance != null)
                txt += $"    Supply : {SupplyManager.Instance.UsedSupply}/{SupplyManager.Instance.TotalSupply}";

            GUI.Label(new Rect(x + 4f, 16f, w - 8f, 20f), txt);
        }

        private void DrawSelectionPanel(IReadOnlyList<Selectable> selected)
        {
            Rect panelRect = new(0, Screen.height - _panelHeight, Screen.width, _panelHeight);
            GUI.Box(panelRect, GUIContent.none);
            GUILayout.BeginArea(panelRect);

            if (selected.Count == 1)
                DrawSingleSelection(selected[0].gameObject);
            else
                DrawMultiSelectionSummary(selected);

            if (_buildingMenu != null && selected.Count == 1)
            {
                var w = selected[0].GetComponent<WorkerController>();
                if (w != null)
                    DrawBuildingButtons(w);
            }

            GUILayout.EndArea();
        }

        private void DrawSingleSelection(GameObject go)
        {
            GUILayout.Label(go.name);

            var site = go.GetComponent<ConstructionSite>();
            if (site != null)
            {
                GUILayout.Label($"En construction : {site.BuildProgress * 100f:0}%");
                int refund = site.GoldCost / 2;
                if (GUILayout.Button($"Annuler (remboursement : {refund}g)"))
                {
                    if (Net) NC?.CmdCancelConstruction(go.GetComponent<NetworkIdentity>());
                    else site.CancelConstruction();
                }
                return;
            }

            var building = go.GetComponent<BuildingBase>();
            if (building != null)
            {
                GUILayout.Label($"HP : {building.CurrentHealth:0} / {building.MaxHealth:0}");
                if (building.MaxTier > 1)
                    GUILayout.Label($"Tier {building.CurrentTier} / {building.MaxTier}");
            }

            var unit = go.GetComponent<UnitBase>();
            if (unit != null)
                GUILayout.Label($"HP : {unit.CurrentHealth:0} / {unit.MaxHealth:0}");

            var worker = go.GetComponent<WorkerController>();
            if (worker != null)
                GUILayout.Label($"État : {worker.State}");

            var castle     = go.GetComponent<Castle>();
            var barracks   = go.GetComponent<Barracks>();
            var academy    = go.GetComponent<Academy>();
            var blacksmith = go.GetComponent<Blacksmith>();

            if (castle != null || barracks != null || academy != null || blacksmith != null)
            {
                GUILayout.BeginHorizontal();

                if (castle != null && GUILayout.Button($"Spawn Worker ({castle.WorkerGoldCost}g)"))
                {
                    if (Net) NC?.CmdSpawnWorker(go.GetComponent<NetworkIdentity>());
                    else castle.SpawnWorker();
                }

                if (barracks != null)
                {
                    if (GUILayout.Button($"Knight ({barracks.KnightGoldCost}g)"))
                    {
                        if (Net) NC?.CmdTrainKnight(go.GetComponent<NetworkIdentity>());
                        else barracks.TrainKnight();
                    }
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = barracks.CurrentTier >= 2;
                    if (GUILayout.Button($"Archer ({barracks.ArcherGoldCost}g){(barracks.CurrentTier < 2 ? " [T2]" : "")}"))
                    {
                        if (Net) NC?.CmdTrainArcher(go.GetComponent<NetworkIdentity>());
                        else barracks.TrainArcher();
                    }
                    GUI.enabled = wasEnabled;
                }

                if (academy != null)
                {
                    if (GUILayout.Button($"Mage ({academy.MageGoldCost}g)"))
                    {
                        if (Net) NC?.CmdTrainMage(go.GetComponent<NetworkIdentity>());
                        else academy.TrainMage();
                    }
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = academy.CurrentTier >= 2;
                    if (GUILayout.Button($"Valkyrie ({academy.ValkyrieGoldCost}g){(academy.CurrentTier < 2 ? " [T2]" : "")}"))
                    {
                        if (Net) NC?.CmdTrainValkyrie(go.GetComponent<NetworkIdentity>());
                        else academy.TrainValkyrie();
                    }
                    GUI.enabled = wasEnabled;
                }

                if (blacksmith != null)
                {
                    if (GUILayout.Button("Upgrade ATK"))
                    {
                        if (Net) NC?.CmdUpgradeAttack(go.GetComponent<NetworkIdentity>());
                        else blacksmith.UpgradeAttack();
                    }
                    if (GUILayout.Button("Upgrade ARM"))
                    {
                        if (Net) NC?.CmdUpgradeArmor(go.GetComponent<NetworkIdentity>());
                        else blacksmith.UpgradeArmor();
                    }
                }

                GUILayout.EndHorizontal();

                if (castle != null)
                {
                    bool isRemoteClient = NetworkClient.active && !NetworkServer.active;
                    var nfs = go.GetComponent<NetworkFactionSync>();

                    int workerCount = castle.DisplayWorkerCount;
                    int queue   = isRemoteClient && nfs != null ? nfs.SyncWorkerQueue     : castle.WorkerQueueCount;
                    int maxQ    = castle.MaxWorkerQueueSize;
                    bool prod   = isRemoteClient && nfs != null ? nfs.SyncWorkerProducing : castle.IsProducingWorker;
                    float timer = isRemoteClient && nfs != null ? nfs.SyncWorkerTimer     : castle.WorkerTimeRemaining;

                    GUILayout.Label($"Workers : {workerCount}/{Castle.MaxWorkers}");
                    DrawProductionStatus(queue, maxQ, prod, timer);
                }

                var production = go.GetComponent<UnitProduction>();
                if (production != null)
                {
                    bool isRemoteClient = NetworkClient.active && !NetworkServer.active;
                    var nfs = go.GetComponent<NetworkFactionSync>();

                    int queue   = isRemoteClient && nfs != null ? nfs.SyncUnitQueue     : production.QueueCount;
                    int maxQ    = isRemoteClient && nfs != null ? nfs.SyncUnitMaxQueue  : production.MaxQueueSize;
                    bool prod   = isRemoteClient && nfs != null ? nfs.SyncUnitProducing : production.IsProducing;
                    float timer = isRemoteClient && nfs != null ? nfs.SyncUnitTimer     : production.TimeRemaining;

                    DrawProductionStatus(queue, maxQ, prod, timer);
                }
            }

            if (building != null && building.BuildingType != BuildingType.Castle)
            {
                if (GUILayout.Button("Démolir"))
                {
                    if (Net) NC?.CmdDemolish(go.GetComponent<NetworkIdentity>());
                    else building.Demolish();
                }
            }

            if (building != null && building.MaxTier > 1 && building.CurrentTier < building.MaxTier)
            {
                string label = $"Upgrade → T{building.CurrentTier + 1} ({building.UpgradeCost}g)";
                if (!building.UpgradeCastleReqMet)
                    label += $" [Castle T{building.NextTierCastleReq} requis]";
                else
                {
                    int curGold = Net && NC != null ? NC.Gold : (ResourceManager.Instance?.Gold ?? 0);
                    if (curGold < building.UpgradeCost)
                        label += " [or insuffisant]";
                }

                int upgradeGold = Net && NC != null ? NC.Gold : (ResourceManager.Instance?.Gold ?? 0);
                bool prev = GUI.enabled;
                GUI.enabled = building.CanUpgrade && upgradeGold >= building.UpgradeCost;
                if (GUILayout.Button(label))
                {
                    if (Net) NC?.CmdUpgradeBuilding(go.GetComponent<NetworkIdentity>());
                    else building.Upgrade();
                }
                GUI.enabled = prev;
            }
        }

        private static void DrawMultiSelectionSummary(IReadOnlyList<Selectable> selected)
        {
            int workers = 0, knights = 0, archers = 0, mages = 0, valkyries = 0, other = 0;
            foreach (var sel in selected)
            {
                var go = sel.gameObject;
                if      (go.GetComponent<WorkerController>() != null) workers++;
                else if (go.GetComponent<Knight>()           != null) knights++;
                else if (go.GetComponent<Archer>()           != null) archers++;
                else if (go.GetComponent<Mage>()             != null) mages++;
                else if (go.GetComponent<Valkyrie>()         != null) valkyries++;
                else other++;
            }

            GUILayout.Label($"Sélection : {selected.Count}");

            var parts = new List<string>();
            if (workers   > 0) parts.Add($"Workers: {workers}");
            if (knights   > 0) parts.Add($"Knights: {knights}");
            if (archers   > 0) parts.Add($"Archers: {archers}");
            if (mages     > 0) parts.Add($"Mages: {mages}");
            if (valkyries > 0) parts.Add($"Valkyries: {valkyries}");
            if (other     > 0) parts.Add($"Autres: {other}");
            GUILayout.Label(string.Join("   ", parts));
        }

        private void DrawBuildingButtons(WorkerController worker)
        {
            GUILayout.Label("Construire :");
            GUILayout.BeginHorizontal();
            foreach (var entry in _buildingMenu.Entries)
            {
                bool canBuild = _buildingMenu.CanBuild(entry);
                bool tierMet  = _buildingMenu.TierMet(entry);

                int current = BuildingManager.Instance?.GetCount(entry.type) ?? 0;
                int limit   = BuildingManager.Instance?.GetLimit(entry.type) ?? 0;
                string countStr = limit >= int.MaxValue ? "" : $" {current}/{limit}";

                int effectiveCost = _buildingMenu.GetEffectiveCost(entry);
                string label = tierMet
                    ? $"{entry.label}{countStr} ({effectiveCost}g)"
                    : $"{entry.label} [T2] ({effectiveCost}g)";

                bool wasEnabled = GUI.enabled;
                GUI.enabled = canBuild;
                if (GUILayout.Button(label))
                    _buildingMenu.BeginBuild(entry, worker);
                GUI.enabled = wasEnabled;
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawProductionStatus(int queue, int maxQueue, bool isProducing, float timeRemaining)
        {
            string status = $"File : {queue}/{maxQueue}";
            if (isProducing)
                status += $"   |   {timeRemaining:F1}s restantes";
            GUILayout.Label(status);
        }
    }
}
