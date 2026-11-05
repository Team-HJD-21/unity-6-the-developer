// 실제 전투/아트 설정 없이 공통 터렛 수명·냉각·발사체 연결을 검증하는 테스트 전용 개체입니다.
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
        public bool CanSimulateForTest => CanSimulateField;
        public void BindProjectileForTest(GameObject projectile) => BindProjectileSimulation(projectile);

        protected override void OnActivationChanged(bool isActivated)
        {
            if (!isActivated) BeginCooling(_cooldownAfterDeactivation);
        }
    }
}
