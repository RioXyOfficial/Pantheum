using UnityEngine;
using Pantheum.Core;

namespace Pantheum.Buildings
{
    public class House : BuildingBase
    {
        [Tooltip("Supply provided at each tier (index 0 = T1, 1 = T2, 2 = T3).")]
        [SerializeField] private int[] _supplyPerTier = { 5, 8, 12 };

        private bool _supplyActive;
        private int  _currentSupply;

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

        public override void CancelRegistration()
        {
            if (!_registeredInManagers) return;
            base.CancelRegistration();
            RemoveSupply();
        }

        public override void StartConstruction()
        {
            base.StartConstruction();
            RemoveSupply();
        }

        public override void CompleteConstruction()
        {
            base.CompleteConstruction();
            AddSupply();
        }

        protected override void OnTierUpgraded(int newTier)
        {
            RemoveSupply();
            AddSupply();
        }

        private int SupplyForCurrentTier()
        {
            int idx = Mathf.Clamp(CurrentTier - 1, 0, _supplyPerTier.Length - 1);
            return _supplyPerTier.Length > 0 ? _supplyPerTier[idx] : 5;
        }

        private void AddSupply()
        {
            if (_supplyActive) return;
            if (SupplyManager.Instance == null) { Debug.LogError("[House] SupplyManager introuvable."); return; }
            _currentSupply = SupplyForCurrentTier();
            SupplyManager.Instance.AddCapacity(_currentSupply);
            _supplyActive = true;
        }

        private void RemoveSupply()
        {
            if (!_supplyActive) return;
            SupplyManager.Instance?.RemoveCapacity(_currentSupply);
            _supplyActive = false;
        }
    }
}
