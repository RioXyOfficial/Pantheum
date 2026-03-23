using Pantheum.Core;

namespace Pantheum.Buildings
{
    /// <summary>
    /// Provides upgrade levels that CombatUnits will eventually read.
    /// Each upgrade costs 150g; maximum level is 5.
    /// </summary>
    public class Blacksmith : BuildingBase
    {
        private const int UpgradeCost  = 150;
        private const int MaxLevel     = 5;

        public int AttackUpgradeLevel { get; private set; }
        public int ArmorUpgradeLevel  { get; private set; }

        public void UpgradeAttack()
        {
            if (AttackUpgradeLevel >= MaxLevel) return;
            if (!ResourceManager.Instance.SpendGold(UpgradeCost)) return;
            AttackUpgradeLevel++;
        }

        public void UpgradeArmor()
        {
            if (ArmorUpgradeLevel >= MaxLevel) return;
            if (!ResourceManager.Instance.SpendGold(UpgradeCost)) return;
            ArmorUpgradeLevel++;
        }
    }
}
