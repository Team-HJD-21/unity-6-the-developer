using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
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
    private FieldTurretBase _weaponTurret;
    private int _instanceId;

    /// <summary>현재 연결된 대상의 공격 방식을 반환한다.</summary>
    public TargetAttackType AttackType
    {
        get
        {
            if (_damageableSource == null)
                return TargetAttackType.Unknown;
            if (_damageableSource is ControlUnitHealth)
                return TargetAttackType.None;
            if (_weaponTurret == null)
                return TargetAttackType.Unknown;

            return _weaponTurret.Kind switch
            {
                FieldTurretKind.Canon => TargetAttackType.SingleTarget,
                FieldTurretKind.Missile => TargetAttackType.Area,
                FieldTurretKind.Laser => TargetAttackType.Unknown,
                FieldTurretKind.Tesla => TargetAttackType.Unknown,
                _ => TargetAttackType.Unknown
            };
        }
    }

    private void Awake()
    {
        _targetable = GetComponent<TargetableComponent>();
        _targetable.SetTargetType(TargetType.Turret);
        ResolveDamageable();
        Synchronize();
    }

    private void OnEnable()
    {
        FieldTurretInstanceRegistry.SnapshotChanged += OnSnapshotChanged;
        Synchronize();
    }

    private void Start() => Synchronize();
    private void LateUpdate() => Synchronize();

    private void OnDisable()
    {
        FieldTurretInstanceRegistry.SnapshotChanged -= OnSnapshotChanged;
        if (_targetable != null)
        {
            _targetable.enabled = false;
        }
    }

    /// <summary>
    /// 연결된 터렛의 상태가 바뀐 경우에만 Enemy 타깃 정보를 갱신한다.
    /// </summary>
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

    /// <summary>
    /// 제작 참조 또는 부모 계층에서 공통 피해 수신자를 찾고 터렛 정보를 연결한다.
    /// </summary>
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

        _weaponTurret = _damageableSource as FieldTurretBase;
        if (_weaponTurret != null)
        {
            _instanceId = _weaponTurret.InstanceId;
        }
    }

    /// <summary>
    /// 실제 체력·파괴·활성 상태를 타깃 정보에 반영하고 공격 가능한 상태에서만 후보를 활성화한다.
    /// </summary>
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

        bool unlockAllowsTargeting = _weaponTurret == null || !_weaponTurret.IsLocked;
        _targetable.enabled = isActiveAndEnabled && hasTarget && unlockAllowsTargeting;
    }
}
