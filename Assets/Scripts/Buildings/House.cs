using UnityEngine;
using Pantheum.Core;

namespace Pantheum.Buildings
{
    public class House : BuildingBase
    {
        [SerializeField] private int _supplyProvided = 5;

        private bool _supplyActive;

        protected override void Awake()
        {
            base.Awake();
            AddSupply();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            RemoveSupply();
        }

        // Ghost/clone : retire le supply ajouté par Awake.
        public override void CancelRegistration()
        {
            if (!_registeredInManagers) return;
            base.CancelRegistration();
            RemoveSupply();
        }

        // Chantier en cours : retire le supply (pas encore construit).
        public override void StartConstruction()
        {
            base.StartConstruction();
            RemoveSupply();
        }

        // Construction terminée : re-ajoute le supply.
        public override void CompleteConstruction()
        {
            base.CompleteConstruction();
            AddSupply();
        }

        private void AddSupply()
        {
            if (_supplyActive) return;
            if (SupplyManager.Instance == null) { Debug.LogError("[House] SupplyManager introuvable."); return; }
            SupplyManager.Instance.AddCapacity(_supplyProvided);
            _supplyActive = true;
        }

        private void RemoveSupply()
        {
            if (!_supplyActive) return;
            SupplyManager.Instance?.RemoveCapacity(_supplyProvided);
            _supplyActive = false;
        }
    }
}
