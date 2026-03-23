using System.Collections.Generic;
using UnityEngine;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Core;
using Pantheum.Selection;
using Pantheum.Units;

namespace Pantheum.UI
{
    /// <summary>
    /// HUD principal du joueur.
    ///
    ///   • Barre de ressources (haut droite) — toujours visible.
    ///   • Panneau de sélection (bas) — visible quand quelque chose est sélectionné.
    ///     - Sélection unique  : nom, HP, état (worker), boutons de production.
    ///     - Multi-sélection   : résumé par type d'unité.
    ///     - Si workers présents (single ou multi) : boutons Construire en bas du panneau.
    /// </summary>
    public class SelectionPanel : MonoBehaviour
    {
        [SerializeField] private float _panelHeight = 200f;
        [SerializeField] private BuildingMenu _buildingMenu;

        private SelectionManager _selectionManager;

        private void Start() => _selectionManager = SelectionManager.Instance;

        /// <summary>
        /// True when the mouse is over an active UI panel this frame.
        /// Read by SelectionManager to avoid click-through.
        /// </summary>
        public static bool IsPointerOverUI { get; private set; }

        private void OnGUI()
        {
            DrawResourceBar();

            if (_selectionManager == null) { IsPointerOverUI = false; return; }
            var selected = _selectionManager.Selected;

            // Update block flag using IMGUI mouse coords (top-left origin).
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

        // ── Barre de ressources (haut droite) ─────────────────────────────
        private void DrawResourceBar()
        {
            const float w = 340f, h = 28f;
            float x = Screen.width - w - 10f;
            GUI.Box(new Rect(x, 10, w, h), GUIContent.none);

            string txt = "  ";
            if (ResourceManager.Instance != null)
                txt += $"Or : {ResourceManager.Instance.Gold}    Mana : {ResourceManager.Instance.Mana}";
            if (SupplyManager.Instance != null)
                txt += $"    Supply : {SupplyManager.Instance.UsedSupply}/{SupplyManager.Instance.TotalSupply}";

            GUI.Label(new Rect(x + 4f, 16f, w - 8f, 20f), txt);
        }

        // ── Panneau de sélection (bas) ────────────────────────────────────
        private void DrawSelectionPanel(IReadOnlyList<Selectable> selected)
        {
            Rect panelRect = new(0, Screen.height - _panelHeight, Screen.width, _panelHeight);
            GUI.Box(panelRect, GUIContent.none);
            GUILayout.BeginArea(panelRect);

            if (selected.Count == 1)
                DrawSingleSelection(selected[0].gameObject);
            else
                DrawMultiSelectionSummary(selected);

            // Boutons Construire — seulement quand UN seul worker est sélectionné
            if (_buildingMenu != null && selected.Count == 1)
            {
                var w = selected[0].GetComponent<WorkerController>();
                if (w != null)
                    DrawBuildingButtons(w);
            }

            GUILayout.EndArea();
        }

        // ── Sélection unique ──────────────────────────────────────────────
        private void DrawSingleSelection(GameObject go)
        {
            GUILayout.Label(go.name);

            // Si c'est un chantier, afficher la progression — pas la santé du BuildingBase
            // qui est toujours à max car Awake() l'initialise avant toute construction.
            var site = go.GetComponent<ConstructionSite>();
            if (site != null)
            {
                GUILayout.Label($"En construction : {site.BuildProgress * 100f:0}%");
                int refund = site.GoldCost / 2;
                if (GUILayout.Button($"Annuler (remboursement : {refund}g)"))
                    site.CancelConstruction();
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

            var castle    = go.GetComponent<Castle>();
            var barracks  = go.GetComponent<Barracks>();
            var academy   = go.GetComponent<Academy>();
            var blacksmith = go.GetComponent<Blacksmith>();

            if (castle != null || barracks != null || academy != null || blacksmith != null)
            {
                GUILayout.BeginHorizontal();

                if (castle != null && GUILayout.Button($"Spawn Worker ({castle.WorkerGoldCost}g)"))
                    castle.SpawnWorker();

                if (barracks != null)
                {
                    if (GUILayout.Button($"Knight ({barracks.KnightGoldCost}g)")) barracks.TrainKnight();
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = barracks.CurrentTier >= 2;
                    if (GUILayout.Button($"Archer ({barracks.ArcherGoldCost}g){(barracks.CurrentTier < 2 ? " [T2]" : "")}")) barracks.TrainArcher();
                    GUI.enabled = wasEnabled;
                }

                if (academy != null)
                {
                    if (GUILayout.Button($"Mage ({academy.MageGoldCost}g)")) academy.TrainMage();
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = academy.CurrentTier >= 2;
                    if (GUILayout.Button($"Valkyrie ({academy.ValkyrieGoldCost}g){(academy.CurrentTier < 2 ? " [T2]" : "")}")) academy.TrainValkyrie();
                    GUI.enabled = wasEnabled;
                }

                if (blacksmith != null)
                {
                    if (GUILayout.Button("Upgrade ATK")) blacksmith.UpgradeAttack();
                    if (GUILayout.Button("Upgrade ARM")) blacksmith.UpgradeArmor();
                }

                GUILayout.EndHorizontal();

                if (castle != null)
                {
                    GUILayout.Label($"Workers : {castle.WorkerCount}/{Castle.MaxWorkers}");
                    DrawProductionStatus(castle.WorkerQueueCount, castle.MaxWorkerQueueSize,
                                         castle.IsProducingWorker, castle.WorkerTimeRemaining);
                }

                var production = go.GetComponent<UnitProduction>();
                if (production != null)
                    DrawProductionStatus(production.QueueCount, production.MaxQueueSize,
                                         production.IsProducing, production.TimeRemaining);

            }

            // Bouton Démolir — tous les bâtiments sauf le Castle du joueur
            if (building != null && building.BuildingType != BuildingType.Castle)
            {
                if (GUILayout.Button("Démolir"))
                    building.Demolish();
            }

            // Bouton d'upgrade de tier — tous les bâtiments upgradables
            if (building != null && building.MaxTier > 1 && building.CurrentTier < building.MaxTier)
            {
                string label = $"Upgrade → T{building.CurrentTier + 1} ({building.UpgradeCost}g)";
                if (!building.UpgradeCastleReqMet)
                    label += $" [Castle T{building.NextTierCastleReq} requis]";
                else if (ResourceManager.Instance != null && ResourceManager.Instance.Gold < building.UpgradeCost)
                    label += " [or insuffisant]";

                bool prev = GUI.enabled;
                GUI.enabled = building.CanUpgrade;
                if (GUILayout.Button(label)) building.Upgrade();
                GUI.enabled = prev;
            }
        }

        // ── Résumé multi-sélection ────────────────────────────────────────
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

        // ── Boutons Construire (worker unique sélectionné) ────────────────
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

                string label = tierMet
                    ? $"{entry.label}{countStr} ({entry.goldCost}g)"
                    : $"{entry.label} [T2] ({entry.goldCost}g)";

                bool wasEnabled = GUI.enabled;
                GUI.enabled = canBuild;
                if (GUILayout.Button(label))
                    _buildingMenu.BeginBuild(entry, worker);
                GUI.enabled = wasEnabled;
            }
            GUILayout.EndHorizontal();
        }

        // ── Statut de production (format uniforme) ────────────────────────
        private static void DrawProductionStatus(int queue, int maxQueue, bool isProducing, float timeRemaining)
        {
            string status = $"File : {queue}/{maxQueue}";
            if (isProducing)
                status += $"   |   {timeRemaining:F1}s restantes";
            GUILayout.Label(status);
        }
    }
}
