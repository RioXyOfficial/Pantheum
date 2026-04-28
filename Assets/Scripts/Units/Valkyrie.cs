using UnityEngine;
using Pantheum.Core;
using Pantheum.Selection;

namespace Pantheum.Units
{
    public class Valkyrie : CombatUnit
    {
        [Header("Valkyrie")]
        [SerializeField] private float _lifeStealRatio = 0.3f;

        protected override void PerformAttack(UnitBase target)
        {
            Faction enemy   = Faction == Faction.Player ? Faction.Enemy : Faction.Player;
            float totalDealt = 0f;

            foreach (var sel in Selectable.All)
            {
                var unit = sel.GetComponent<UnitBase>();
                if (unit == null || !unit.IsAlive || unit.Faction != enemy) continue;
                if (Vector3.Distance(transform.position, unit.transform.position) <= AttackRange)
                {
                    unit.TakeDamage(TotalAttack);
                    totalDealt += TotalAttack;
                }
            }

            if (totalDealt > 0f)
                RestoreHealth(totalDealt * _lifeStealRatio);
        }
    }
}
