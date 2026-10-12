using UnityEngine;

namespace TeamHJD.Game.Turrets.Tests
{
    // A minimal concrete turret isolates shared lifecycle behavior from art/combat setup.
    public sealed class InitializationProbeTurret : TurretBase
    {
        private float _cooldownAfterDeactivation;

        private void Awake() => ConfigureSceneActivation();
        public bool ConfigureForTest() => ConfigureSceneActivation();
        public void SetCooldownAfterDeactivation(float seconds) => _cooldownAfterDeactivation = seconds;

        protected override void OnActivationChanged(bool isActivated)
        {
            if (!isActivated) BeginActivationCooldown(_cooldownAfterDeactivation);
        }
    }
}
