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
    /// </summary>
    public class Selectable : MonoBehaviour, ISelectable
    {
        public event System.Action OnSelectedEvent;
        public event System.Action OnDeselectedEvent;

        public bool IsSelected { get; private set; }

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
