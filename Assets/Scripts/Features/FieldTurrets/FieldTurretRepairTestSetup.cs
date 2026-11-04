using System;
using UnityEngine;

namespace TeamHJD.Game.FieldTurrets
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class FieldTurretRepairTestSetup : MonoBehaviour
    {
        [SerializeField] private FieldTurretController _controller;
        [SerializeField] private FieldTurretBase _destroyedTurret;
        [SerializeField] private FieldTurretBase _damagedTurret;
        [SerializeField, Min(1)] private int _damagedHealth = 50; // Initial_Stage 수리 검증용 시작 체력입니다.

        private void Awake()
        {
            ValidateTurret(_destroyedTurret);
            ValidateTurret(_damagedTurret);
            if (_destroyedTurret == _damagedTurret || _damagedHealth <= 0 || _damagedHealth >= _damagedTurret.MaxHealth)
                throw new InvalidOperationException("Assign two different repair-test turrets and a damaged health below maximum.");

            _destroyedTurret.PrepareInitialization(
                new FieldTurretInitializationData(_destroyedTurret.Definition, 0), _controller);
            _damagedTurret.PrepareInitialization(
                new FieldTurretInitializationData(_damagedTurret.Definition, _damagedHealth), _controller);
            _destroyedTurret.gameObject.SetActive(true); // Awake·등록 전에 파괴 상태를 전달합니다.
            _damagedTurret.gameObject.SetActive(true); // 이후 수리·복구는 실제 터렛 API만 사용합니다.
        }

        private void ValidateTurret(FieldTurretBase turret)
        {
            if (_controller == null || turret == null || turret.Definition == null || !turret.enabled ||
                turret.gameObject.activeInHierarchy || turret.IsInitialized ||
                turret.gameObject.scene != gameObject.scene || _controller.gameObject.scene != gameObject.scene ||
                !turret.transform.IsChildOf(transform))
                throw new InvalidOperationException("Repair-test turrets must be inactive children with a Scene controller and definition.");
        }
    }
}
