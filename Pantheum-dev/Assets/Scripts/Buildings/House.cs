using Pantheum.Core;

namespace Pantheum.Buildings
{
    /// <summary>
    /// Adds supply capacity when placed; removes it when destroyed.
    /// Workers are excluded from supply — only combat units are tracked.
    /// </summary>
    public class House : BuildingBase
    {
        private const int SupplyProvided = 8;

        protected override void Awake()
        {
            base.Awake();
            SupplyManager.Instance.AddCapacity(SupplyProvided);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SupplyManager.Instance?.RemoveCapacity(SupplyProvided);
        }
    }
}
