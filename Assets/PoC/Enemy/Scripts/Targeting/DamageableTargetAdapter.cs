using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>터렛과 CU의 공통 피격 상태를 Enemy 타깃 정보로 전달한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TargetableComponent))]
public sealed class DamageableTargetAdapter : MonoBehaviour
{
    [FormerlySerializedAs("_turret")]
    [SerializeField] private MonoBehaviour _damageableSource;

    private TargetableComponent _targetable;
    private IDamageableTarget _damageable;
    private TurretBase _weaponTurret;
    private int _instanceId;

    private void Awake()
    {
        _targetable = GetComponent<TargetableComponent>();
        _targetable.SetTargetType(TargetType.Turret);
        ResolveDamageable();
        Synchronize();
    }

    private void OnEnable()
    {
        TurretInstanceRegistry.SnapshotChanged += OnSnapshotChanged;
        Synchronize();
    }

    private void Start() => Synchronize();
    private void LateUpdate() => Synchronize();

    private void OnDisable()
    {
        TurretInstanceRegistry.SnapshotChanged -= OnSnapshotChanged;
        if (_targetable != null)
        {
            _targetable.enabled = false;
        }
    }

    private void OnSnapshotChanged(int instanceId)
    {
        if (_weaponTurret == null)
        {
            return;
        }

        if (_instanceId == 0)
        {
            _instanceId = _weaponTurret.InstanceId;
        }

        if (instanceId == _instanceId)
        {
            Synchronize();
        }
    }

    private void ResolveDamageable()
    {
        if (_damageableSource is IDamageableTarget configured)
        {
            _damageable = configured;
        }
        else
        {
            MonoBehaviour[] candidates = GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is not IDamageableTarget candidate)
                {
                    continue;
                }

                _damageableSource = candidates[i];
                _damageable = candidate;
                break;
            }
        }

        _weaponTurret = _damageableSource as TurretBase;
        if (_weaponTurret != null)
        {
            _instanceId = _weaponTurret.InstanceId;
        }
    }

    private void Synchronize()
    {
        if (_targetable == null)
        {
            return;
        }

        if (_damageable == null ||
            _damageableSource is Object sourceObject && sourceObject == null)
        {
            ResolveDamageable();
        }

        bool hasTarget = _damageable != null && !_damageable.IsDestroyed && _damageable.CurrentHealth > 0;
        _targetable.SetHealth(
            hasTarget ? _damageable.CurrentHealth : 0,
            _damageable != null ? _damageable.MaxHealth : 0);
        _targetable.SetFirepowerEfficiency(_weaponTurret != null ? _weaponTurret.DpsPerPower : 0f);

        bool activationAllowsTargeting = _weaponTurret == null || _weaponTurret.IsActivated;
        _targetable.enabled = isActiveAndEnabled && hasTarget && activationAllowsTargeting;
    }
}
