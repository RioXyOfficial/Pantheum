using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Core;
using Pantheum.Network;
using Pantheum.UI;
using Pantheum.Units;

namespace Pantheum.Selection
{
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        [Header("Layers")]
        [SerializeField] private LayerMask _selectableLayer;
        [SerializeField] private LayerMask _groundLayer;

        private readonly List<Selectable> _selected    = new();
        private readonly HashSet<Selectable> _selectedSet = new();
        private Vector2  _dragStart;
        private bool     _isDragging;
        private bool     _clickStartedDuringPlacement;
        private Camera   _cam;
        private Vector2  _dragCurrent;
        private bool     _attackMoveQueued;

        public IReadOnlyList<Selectable> Selected => _selected;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance   = this;
            _cam       = Camera.main;
            _boxFill   = MakeTex(new Color(0.2f, 0.6f, 1f, 0.08f));
            _boxBorder = MakeTex(new Color(0.2f, 0.7f, 1f, 0.7f));
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _clickStartedDuringPlacement = BuildingPlacer.IsActive;
                _dragStart  = mouse.position.ReadValue();
                _isDragging = false;
            }

            if (mouse.leftButton.isPressed && !_clickStartedDuringPlacement)
            {
                _dragCurrent = mouse.position.ReadValue();
                if (Vector2.Distance(_dragStart, _dragCurrent) > 20f)
                    _isDragging = true;
            }

            if (mouse.leftButton.wasReleasedThisFrame && !_clickStartedDuringPlacement)
            {
                bool additive   = Keyboard.current?.leftShiftKey.isPressed ?? false;
                Vector2 release = mouse.position.ReadValue();
                bool isRealDrag = _isDragging && Vector2.Distance(_dragStart, release) > 10f;

                if (isRealDrag)
                    SelectInBox(_dragStart, release, additive);
                else
                    SelectAtPoint(release, additive, skipDeselectIfMiss: SelectionPanel.IsPointerOverUI);

                _isDragging = false;
                _clickStartedDuringPlacement = false;
            }

            if (Keyboard.current?.aKey.wasPressedThisFrame ?? false)
                _attackMoveQueued = true;

            if (mouse.rightButton.wasPressedThisFrame
                && !BuildingPlacer.IsActive
                && _selected.Count > 0)
            {
                HandleCommand(mouse.position.ReadValue());
                _attackMoveQueued = false;
            }
        }

        private void SelectAtPoint(Vector2 screenPos, bool additive, bool skipDeselectIfMiss = false)
        {
            Ray ray = _cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _selectableLayer))
            {
                if (!skipDeselectIfMiss && !additive) DeselectAll();
                return;
            }

            var sel = hit.collider.GetComponentInParent<Selectable>();
            if (sel == null)
            {
                if (!skipDeselectIfMiss && !additive) DeselectAll();
                return;
            }

            bool isBuilding = sel.GetComponent<BuildingBase>() != null;
            if (!additive || isBuilding) DeselectAll();

            AddToSelection(sel);
        }

        private void SelectInBox(Vector2 screenA, Vector2 screenB, bool additive)
        {
            if (!additive) DeselectAll();
            Rect rect = ScreenRect(screenA, screenB);
            foreach (var sel in Selectable.All)
            {
                if (sel.GetComponent<BuildingBase>() != null) continue;
                Vector2 sp = _cam.WorldToScreenPoint(sel.transform.position);
                if (rect.Contains(sp)) AddToSelection(sel);
            }
        }

        private void AddToSelection(Selectable sel)
        {
            Faction localFaction   = PlayerNetworkController.LocalPlayer?.Faction ?? Faction.Player;
            Faction opponentFaction = localFaction == Faction.Player ? Faction.Enemy : Faction.Player;
            if (sel.GetComponent<BuildingBase>()?.Faction == opponentFaction) return;
            if (sel.GetComponent<UnitBase>()?.Faction == opponentFaction) return;

            if (!_selectedSet.Add(sel)) return;
            _selected.Add(sel);
            sel.OnSelected();
        }

        public void RemoveFromSelection(Selectable sel)
        {
            if (_selectedSet.Remove(sel))
                _selected.Remove(sel);
        }

        public void DeselectAll()
        {
            foreach (var sel in _selected) sel.OnDeselected();
            _selected.Clear();
            _selectedSet.Clear();
        }

        private void HandleCommand(Vector2 screenPos)
        {
            Ray ray = _cam.ScreenPointToRay(screenPos);

            if (Physics.Raycast(ray, out RaycastHit selHit, 1000f, _selectableLayer))
            {
                if (_attackMoveQueued)
                {
                    var targetBuilding = selHit.collider.GetComponentInParent<BuildingBase>();
                    if (targetBuilding != null)
                    {
                        Faction localFaction = PlayerNetworkController.LocalPlayer?.Faction ?? Faction.Player;
                        if (targetBuilding.Faction != localFaction)
                        {
                            var buildingNI = targetBuilding.GetComponent<NetworkIdentity>();
                            foreach (var sel in _selected)
                            {
                                var combat = sel.GetComponent<CombatUnit>();
                                if (combat == null) continue;
                                var ni = sel.GetComponent<NetworkIdentity>();
                                if (NetworkClient.active && ni != null && buildingNI != null)
                                    PlayerNetworkController.LocalPlayer?.CmdAttackBuilding(ni, buildingNI);
                                else
                                    combat.SetBuildingTarget(targetBuilding);
                            }
                            return;
                        }
                    }
                }

                ConstructionSite site = selHit.collider.GetComponentInParent<ConstructionSite>();
                if (site == null)
                {
                    var hitBuilding = selHit.collider.GetComponentInParent<BuildingBase>();
                    if (hitBuilding != null)
                        site = hitBuilding.GetComponent<ConstructionSite>();
                }

                if (site != null)
                {
                    var siteNI = site.GetComponent<NetworkIdentity>();
                    foreach (var sel in _selected)
                    {
                        var worker = sel.GetComponent<WorkerController>();
                        if (worker == null) continue;

                        if (NetworkClient.active && siteNI != null)
                        {
                            var workerNI = sel.GetComponent<NetworkIdentity>();
                            PlayerNetworkController.LocalPlayer?.CmdAssignWorkerBuild(workerNI, siteNI);
                        }
                        else
                        {
                            worker.AssignToConstruction(site);
                        }
                    }
                    return;
                }

                var node = selHit.collider.GetComponentInParent<ResourceNode>();
                if (node != null)
                {
                    var nodeNI = node.GetComponent<NetworkIdentity>();
                    foreach (var sel in _selected)
                    {
                        var worker = sel.GetComponent<WorkerController>();
                        if (worker == null) continue;
                        if (NetworkClient.active && nodeNI != null)
                        {
                            var workerNI = sel.GetComponent<NetworkIdentity>();
                            PlayerNetworkController.LocalPlayer?.CmdAssignWorkerHarvest(workerNI, nodeNI);
                        }
                        else
                            worker.AssignToHarvest(node);
                    }
                    return;
                }
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, _groundLayer))
            {
                bool isAttackMove = _attackMoveQueued;

                var movers = new List<Selectable>();
                foreach (var sel in _selected)
                    if (sel.GetComponent<WorkerController>() != null || sel.GetComponent<UnitBase>() != null)
                        movers.Add(sel);

                for (int i = 0; i < movers.Count; i++)
                {
                    Vector3 dest   = groundHit.point + FormationOffset(i, movers.Count, 0.6f);
                    var worker     = movers[i].GetComponent<WorkerController>();
                    var combat     = movers[i].GetComponent<CombatUnit>();
                    var ni         = movers[i].GetComponent<NetworkIdentity>();

                    if (NetworkClient.active)
                    {
                        if (worker != null)
                            PlayerNetworkController.LocalPlayer?.CmdOrderWorkerMove(ni, dest);
                        else if (isAttackMove && combat != null)
                            PlayerNetworkController.LocalPlayer?.CmdAttackMove(ni, dest);
                        else
                            PlayerNetworkController.LocalPlayer?.CmdMoveUnit(ni, dest);
                    }
                    else
                    {
                        if (worker != null)              worker.OrderMove(dest);
                        else if (isAttackMove && combat != null) combat.OrderAttackMove(dest);
                        else                             movers[i].GetComponent<UnitBase>()?.MoveTo(dest);
                    }
                }
            }
        }

        private static Vector3 FormationOffset(int index, int total, float spacing)
        {
            if (index == 0) return Vector3.zero;
            int ring    = 1;
            int counted = 1;
            while (counted + ring * 6 <= index)
            {
                counted += ring * 6;
                ring++;
            }
            int posInRing = index - counted;
            float angle   = posInRing * (360f / (ring * 6)) * Mathf.Deg2Rad;
            float r       = ring * spacing;
            return new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
        }

        private static Rect ScreenRect(Vector2 a, Vector2 b) =>
            new(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        private Texture2D _boxFill;
        private Texture2D _boxBorder;

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (!_isDragging) return;

            float startY   = Screen.height - _dragStart.y;
            float currentY = Screen.height - _dragCurrent.y;

            float x = Mathf.Min(_dragStart.x,   _dragCurrent.x);
            float y = Mathf.Min(startY,          currentY);
            float w = Mathf.Abs(_dragStart.x   - _dragCurrent.x);
            float h = Mathf.Abs(startY         - currentY);

            Rect rect = new(x, y, w, h);
            GUI.DrawTexture(rect, _boxFill);

            float b = 1f;
            GUI.DrawTexture(new Rect(x,         y,         w, b), _boxBorder);
            GUI.DrawTexture(new Rect(x,         y + h - b, w, b), _boxBorder);
            GUI.DrawTexture(new Rect(x,         y,         b, h), _boxBorder);
            GUI.DrawTexture(new Rect(x + w - b, y,         b, h), _boxBorder);
        }
    }
}
