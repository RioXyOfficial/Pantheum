using System.Collections.Generic;
using UnityEngine;

namespace Pantheum.Selection
{
    public interface ISelectable
    {
        void OnSelected();
        void OnDeselected();
    }

    /// <summary>
    /// Attach to any GameObject that can be selected.
    /// Fires OnSelectedEvent / OnDeselectedEvent so BuildingBase and UnitBase
    /// can react without coupling to SelectionManager.
    /// Maintains a static registry (All) so SelectionManager avoids FindObjectsByType.
    /// </summary>
    public class Selectable : MonoBehaviour, ISelectable
    {
        private static readonly List<Selectable> _all = new();
        public static IReadOnlyList<Selectable> All => _all;

        public event System.Action OnSelectedEvent;
        public event System.Action OnDeselectedEvent;

        public bool IsSelected { get; private set; }

        private void OnEnable() => _all.Add(this);

        private void OnDisable()
        {
            _all.Remove(this);
            // Si cet objet est détruit pendant qu'il est sélectionné, on le retire proprement
            if (IsSelected)
                SelectionManager.Instance?.RemoveFromSelection(this);
        }

        public void OnSelected()
        {
            IsSelected = true;
            OnSelectedEvent?.Invoke();
        }

        public void OnDeselected()
        {
            IsSelected = false;
            OnDeselectedEvent?.Invoke();
        }
    }
}
