using UnityEngine;
using Pantheum.Selection;

namespace Pantheum.UI
{
    /// <summary>
    /// Bottom-of-screen HUD panel rendered via IMGUI.
    /// Shows selection count, selected object name, and a Deselect All button.
    /// Command buttons (Train Knight, etc.) are added when the relevant
    /// building/unit type is identified from the selection.
    /// </summary>
    public class SelectionPanel : MonoBehaviour
    {
        [SerializeField] private float _panelHeight = 100f;

        private SelectionManager _selectionManager;

        private void Start()
        {
            // Use Start so SelectionManager.Awake has already run
            _selectionManager = SelectionManager.Instance;
        }

        private void OnGUI()
        {
            if (_selectionManager == null) return;

            var selected = _selectionManager.Selected;
            if (selected.Count == 0) return;

            Rect panelRect = new(0, Screen.height - _panelHeight, Screen.width, _panelHeight);
            GUI.Box(panelRect, GUIContent.none);
            GUILayout.BeginArea(panelRect);

            GUILayout.Label($"Selected: {selected.Count}");

            if (selected.Count == 1)
                GUILayout.Label(selected[0].gameObject.name);

            if (GUILayout.Button("Deselect All"))
                _selectionManager.DeselectAll();

            GUILayout.EndArea();
        }
    }
}
