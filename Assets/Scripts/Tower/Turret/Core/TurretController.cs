using System;
using System.Collections.Generic;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Turrets
{
    /// <summary>씬의 전력 예산을 소유하고 ID 기반 터렛 활성화 요청을 처리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class TurretController : MonoBehaviour, ITurretPowerSource
    {
        [SerializeField, Min(0)] private int maximumPower = 100;
        private TurretPowerBudget _budget;
        public TurretPowerBudget Budget => _budget ??= new TurretPowerBudget(Mathf.Max(0, maximumPower));
        public int CurrentPower => Budget.CurrentPower;
        public int MaximumPower => Budget.MaximumPower;
        public event Action<int, int> PowerChanged
        {
            add => Budget.PowerChanged += value;
            remove => Budget.PowerChanged -= value;
        }

        private void Awake() { _ = Budget; }
        private void Update() => Budget.Tick(Time.deltaTime);

        public bool TrySetMaximumPower(int power)
        {
            if (!Budget.TrySetMaximumPower(power)) return false;
            maximumPower = power;
            return true;
        }

        public bool TryConsumePower(int power) => Budget.TryConsumePower(power);
        public bool TryChangeReservation(int previousPower, int newPower) => Budget.TryChangeReservation(previousPower, newPower);
        public void ReleasePower(int power) => Budget.ReleasePower(power);

        /// <summary>메인 스레드에서 호출한다. 같은 전력 공급자를 사용하는 등록 터렛만 집계한다.</summary>
        public TurretControllerSnapshot GetSnapshot()
        {
            int registered = 0;
            int activated = 0;
            int operational = 0;
            var activatedByType = new Dictionary<TurretKind, int>();
            foreach (TurretKind kind in Enum.GetValues(typeof(TurretKind)))
                activatedByType[kind] = 0;

            foreach (TurretBase turret in TurretInstanceRegistry.RegisteredInstances.Values)
            {
                if (turret == null || !turret.UsesPowerSource(this)) continue;
                registered++;
                if (turret.IsOperational) operational++;
                if (!turret.IsActivated || turret.IsDestroyed) continue;
                activated++;
                activatedByType.TryGetValue(turret.Kind, out int count);
                activatedByType[turret.Kind] = count + 1;
            }

            TurretPowerBudget budget = Budget;
            return new TurretControllerSnapshot(
                budget.MaximumPower, budget.CurrentPower, budget.ReservedPower, budget.PendingRecovery,
                registered, activated, operational, activatedByType);
        }

        public TurretActivationResult RequestActivation(int instanceId, bool shouldActivate)
        {
            if (!TurretInstanceRegistry.TryGet(instanceId, out TurretBase turret) || turret == null ||
                !turret.UsesPowerSource(this))
                return TurretActivationResult.PowerSourceUnavailable;
            return turret.RequestActivation(shouldActivate);
        }

        public static TurretController FindForScene(Scene scene)
        {
            foreach (TurretController controller in FindObjectsByType<TurretController>(FindObjectsSortMode.None))
                if (controller.gameObject.scene == scene && controller.isActiveAndEnabled) return controller;
            return null;
        }

        // Compatibility bootstrap: no CU or specially named object is required.
        public static TurretController GetOrCreateForScene(Scene scene)
        {
            TurretController controller = FindForScene(scene);
            if (controller != null) return controller;
            GameObject owner = new GameObject("TurretController");
            SceneManager.MoveGameObjectToScene(owner, scene);
            return owner.AddComponent<TurretController>();
        }
    }
}
