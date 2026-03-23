using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Pantheum.Buildings;
using Pantheum.Construction;
using Pantheum.Core;
using Pantheum.UI;
using Pantheum.Units;

namespace Pantheum.Selection
{
    /// <summary>
    /// Gère la sélection (clic gauche / drag) et les commandes (clic droit).
    ///
    /// Clic gauche  — sélectionne l'objet visé ; Shift = ajout à la sélection.
    ///               Si la souris est sur le panel UI et qu'aucun objet n'est visé,
    ///               la sélection courante est conservée (pas de désélection involontaire).
    /// Clic droit   — commande contextuelle sur la sélection courante :
    ///                  • ResourceNode      → Workers assignés à la récolte
    ///                  • ConstructionSite  → Workers assignés à la construction
    ///                  • Terrain           → toutes les unités se déplacent
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        [Header("Layers")]
        [SerializeField] private LayerMask _selectableLayer;
        [SerializeField] private LayerMask _groundLayer;

        private readonly List<Selectable> _selected = new();
        private readonly HashSet<Selectable> _selectedSet = new();
        private Vector2 _dragStart;
        private bool _isDragging;
        private bool _clickStartedDuringPlacement;
        private Camera _cam;
        private Vector2 _dragCurrent;

        public IReadOnlyList<Selectable> Selected => _selected;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _cam = Camera.main;
            _boxFill   = MakeTex(new Color(0.2f, 0.6f, 1f, 0.08f));
            _boxBorder = MakeTex(new Color(0.2f, 0.7f, 1f, 0.7f));
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // ── Clic gauche : sélection ──────────────────────────────────
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
                    // skipDeselectIfMiss = true when the panel is showing so that clicking
                    // buttons / grey area doesn't accidentally deselect the current object.
                    SelectAtPoint(release, additive, skipDeselectIfMiss: SelectionPanel.IsPointerOverUI);

                _isDragging = false;
                _clickStartedDuringPlacement = false;
            }

            // ── Clic droit : commande ────────────────────────────────────
            if (mouse.rightButton.wasPressedThisFrame
                && !BuildingPlacer.IsActive
                && _selected.Count > 0)
                HandleCommand(mouse.position.ReadValue());
        }

        // ── Sélection ────────────────────────────────────────────────────────

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
            if (sel.GetComponent<BuildingBase>()?.Faction == Faction.Enemy) return;
            if (sel.GetComponent<UnitBase>()    ?.Faction == Faction.Enemy) return;

            if (!_selectedSet.Add(sel)) return;
            _selected.Add(sel);
            sel.OnSelected();
        }

        /// <summary>Called by Selectable.OnDisable() when a selected object is destroyed.</summary>
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

        // ── Commandes clic droit ─────────────────────────────────────────────

        private void HandleCommand(Vector2 screenPos)
        {
            Ray ray = _cam.ScreenPointToRay(screenPos);

            if (Physics.Raycast(ray, out RaycastHit selHit, 1000f, _selectableLayer))
            {
                var site = selHit.collider.GetComponentInParent<ConstructionSite>();
                if (site != null)
                {
                    foreach (var sel in _selected)
                        sel.GetComponent<WorkerController>()?.AssignToConstruction(site);
                    return;
                }

                var node = selHit.collider.GetComponentInParent<ResourceNode>();
                if (node != null)
                {
                    foreach (var sel in _selected)
                        sel.GetComponent<WorkerController>()?.AssignToHarvest(node);
                    return;
                }
            }

            if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, _groundLayer))
            {
                foreach (var sel in _selected)
                {
                    var worker = sel.GetComponent<WorkerController>();
                    if (worker != null) worker.OrderMove(groundHit.point);
                    else sel.GetComponent<UnitBase>()?.MoveTo(groundHit.point);
                }
            }
        }

        // ── Utilitaires ──────────────────────────────────────────────────────

        private static Rect ScreenRect(Vector2 a, Vector2 b) =>
            new(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        // ── Drag-box visual ──────────────────────────────────────────────────
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
