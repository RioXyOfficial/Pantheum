using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pantheum.Selection
{
    /// <summary>
    /// Handles left-click selection and drag-box multi-selection.
    /// Hold Shift to add to the current selection.
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        [SerializeField] private LayerMask _selectableLayer;

        private readonly List<Selectable> _selected = new();
        private Vector2 _dragStart;
        private bool _isDragging;
        private Camera _cam;

        public IReadOnlyList<Selectable> Selected => _selected;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _cam = Camera.main;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _dragStart = mouse.position.ReadValue();
                _isDragging = false;
            }

            if (mouse.leftButton.isPressed)
            {
                if (Vector2.Distance(_dragStart, mouse.position.ReadValue()) > 20f)
                    _isDragging = true;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                bool additive = Keyboard.current?.leftShiftKey.isPressed ?? false;

                if (_isDragging)
                    SelectInBox(_dragStart, mouse.position.ReadValue(), additive);
                else
                    SelectAtPoint(mouse.position.ReadValue(), additive);

                _isDragging = false;
            }
        }

        private void SelectAtPoint(Vector2 screenPos, bool additive)
        {
            if (!additive) DeselectAll();

            Ray ray = _cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, _selectableLayer))
            {
                var sel = hit.collider.GetComponentInParent<Selectable>();
                if (sel != null) AddToSelection(sel);
            }
        }

        private void SelectInBox(Vector2 screenA, Vector2 screenB, bool additive)
        {
            if (!additive) DeselectAll();

            Rect rect = ScreenRect(screenA, screenB);
            foreach (var sel in FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            {
                Vector2 sp = _cam.WorldToScreenPoint(sel.transform.position);
                if (rect.Contains(sp))
                    AddToSelection(sel);
            }
        }

        private void AddToSelection(Selectable sel)
        {
            if (_selected.Contains(sel)) return;
            _selected.Add(sel);
            sel.OnSelected();
        }

        public void DeselectAll()
        {
            foreach (var sel in _selected)
                sel.OnDeselected();
            _selected.Clear();
        }

        private static Rect ScreenRect(Vector2 a, Vector2 b) =>
            new(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
