using Pantheum.Core;

namespace Pantheum.Buildings
{
    /// <summary>
    /// Upgrades attack damage and armor for all player CombatUnits.
    /// Levels are shared globally — multiple Blacksmiths share the same pool.
    /// </summary>
    public class Blacksmith : BuildingBase
    {
        private const int UpgradeCost = 150;
        private const int MaxLevel    = 5;

        // Shared across all Blacksmith instances (player-wide upgrades).
        public static int AttackLevel { get; private set; }
        public static int ArmorLevel  { get; private set; }

        protected override void OnDestroy()
        {
            // Reset global levels when the last blacksmith is destroyed.
            if (BuildingManager.Instance?.GetCount(BuildingType.Blacksmith) <= 1)
            {
                AttackLevel = 0;
                ArmorLevel  = 0;
            }
            base.OnDestroy();
        }

        public void UpgradeAttack()
        {
            if (AttackLevel >= MaxLevel) return;
            if (!ResourceManager.Instance.SpendGold(UpgradeCost)) return;
            AttackLevel++;
        }

        public void UpgradeArmor()
        {
            if (ArmorLevel >= MaxLevel) return;
            if (!ResourceManager.Instance.SpendGold(UpgradeCost)) return;
            ArmorLevel++;
        }
    }
}
