namespace Pantheum.Buildings
{
    /// <summary>
    /// Provides upgrade levels that CombatUnits will eventually read.
    /// Upgrade cost / unlock logic to be implemented when upgrade UI is built.
    /// </summary>
    public class Blacksmith : BuildingBase
    {
        public int AttackUpgradeLevel { get; private set; }
        public int ArmorUpgradeLevel { get; private set; }

        public void UpgradeAttack() => AttackUpgradeLevel++;
        public void UpgradeArmor() => ArmorUpgradeLevel++;
    }
}
