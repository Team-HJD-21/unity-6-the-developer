using UnityEngine;

namespace TeamHJD.Game.FieldTurrets.Tests
{
    // A minimal concrete turret isolates shared lifecycle behavior from art/combat setup.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "TeamHJD.Game.Turrets.Tests", sourceAssembly: "TeamHJD.Game.Turrets.Tests", sourceClassName: "InitializationProbeTurret")]
    public sealed class InitializationProbeFieldTurret : FieldTurretBase
    {
        private float _cooldownAfterDeactivation;

        private void Awake() => ConfigureSceneActivation();
        public bool ConfigureForTest() => ConfigureSceneActivation();
        public void SetCooldownAfterDeactivation(float seconds) => _cooldownAfterDeactivation = seconds;
        public void BeginCoolingForTest(float seconds) => BeginCooling(seconds);
        public void SetFiringProgressForTest(float seconds) => TimeTilFire = seconds;
        public float FiringProgressForTest => TimeTilFire;

        protected override void OnActivationChanged(bool isActivated)
        {
            if (!isActivated) BeginCooling(_cooldownAfterDeactivation);
        }
    }
}
