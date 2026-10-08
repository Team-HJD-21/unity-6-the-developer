using UnityEngine;

namespace TeamHJD.Game.Turrets.Tests
{
    // A minimal concrete turret isolates shared lifecycle behavior from art/combat setup.
    public sealed class InitializationProbeTurret : TurretBase
    {
        private void Awake() => ConfigureSceneActivation();
        public bool ConfigureForTest() => ConfigureSceneActivation();
    }
}
