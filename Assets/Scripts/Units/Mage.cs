using UnityEngine;
using Pantheum.Core;
using Pantheum.Selection;

namespace Pantheum.Units
{
    public class Mage : CombatUnit
    {
        [Header("AoE")]
        [SerializeField] private float _aoeRadius = 2.5f;

        protected override void PerformAttack(UnitBase target)
        {
            Faction enemy  = Faction == Faction.Player ? Faction.Enemy : Faction.Player;
            Vector3 center = target.transform.position;

            foreach (var sel in Selectable.All)
            {
                var unit = sel.GetComponent<UnitBase>();
                if (unit == null || !unit.IsAlive || unit.Faction != enemy) continue;
                if (Vector3.Distance(center, unit.transform.position) <= _aoeRadius)
                    unit.TakeDamage(TotalAttack);
            }
        }
    }
}
