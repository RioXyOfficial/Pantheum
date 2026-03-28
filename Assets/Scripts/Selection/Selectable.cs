using System.Collections.Generic;
using UnityEngine;

namespace Pantheum.Selection
{
    public interface ISelectable
    {
        void OnSelected();
        void OnDeselected();
    }

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
