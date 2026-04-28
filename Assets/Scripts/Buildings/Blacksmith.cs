using Pantheum.Core;

namespace Pantheum.Buildings
{
    public class Blacksmith : BuildingBase
    {
        private const int SmithUpgradeCost = 150;
        private const int MaxLevel    = 5;

        public static int AttackLevel { get; private set; }
        public static int ArmorLevel  { get; private set; }

        protected override void OnDestroy()
        {
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
            if (!ResourceManager.Instance.SpendGold(SmithUpgradeCost)) return;
            AttackLevel++;
        }

        public void UpgradeArmor()
        {
            if (ArmorLevel >= MaxLevel) return;
            if (!ResourceManager.Instance.SpendGold(SmithUpgradeCost)) return;
            ArmorLevel++;
        }
    }
}
