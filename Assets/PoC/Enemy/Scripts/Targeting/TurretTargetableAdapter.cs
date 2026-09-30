using TeamHJD.Game.Turrets;
using UnityEngine;

/// <summary>
/// 터렛의 현재 상태를 <see cref="TargetableComponent"/>에 전달하는 어댑터다.
/// 화력 계산 기준이 확정되기 전까지 Inspector에서 지정한 임시 화력 비율을 사용한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TargetableComponent))]
public sealed class TurretTargetableAdapter : MonoBehaviour
{
    [SerializeField] private TurretBase _turret;
    [SerializeField, Range(0f, 1f)] private float _fakeFirepowerRatio = 0.5f;

    private TargetableComponent _targetable;

    /// <summary>
    /// Inspector에서 컴포넌트를 추가하거나 초기화할 때 상위 터렛 참조를 자동으로 설정한다.
    /// </summary>
    private void Reset()
    {
        _turret = GetComponentInParent<TurretBase>();
    }

    /// <summary>
    /// 타깃 상태 컴포넌트와 상위 터렛 참조를 초기화한다.
    /// </summary>
    private void Awake()
    {
        _targetable = GetComponent<TargetableComponent>();
        if (_turret == null)
            _turret = GetComponentInParent<TurretBase>();

        _targetable.SetTargetType(TargetType.Turret);
        Synchronize();
    }

    /// <summary>
    /// 터렛 상태 변경 이벤트를 구독하고 현재 상태를 즉시 동기화한다.
    /// </summary>
    private void OnEnable()
    {
        TurretInstanceRegistry.ActivationChanged += OnActivationChanged;
        TurretInstanceRegistry.HealthChanged += OnHealthChanged;
        TurretInstanceRegistry.Registered += OnTurretChanged;
        TurretInstanceRegistry.Unregistered += OnTurretChanged;
        TurretInstanceRegistry.Destroyed += OnTurretChanged;
        TurretInstanceRegistry.Restored += OnTurretChanged;
        Synchronize();
    }

    /// <summary>
    /// 다른 컴포넌트의 초기화가 끝난 뒤 터렛 상태를 다시 동기화한다.
    /// </summary>
    private void Start() => Synchronize();

    /// <summary>
    /// 초기화 순서와 Inspector 값 변경, 컴포넌트 활성 상태 변경을 반영한다.
    /// </summary>
    private void LateUpdate() => Synchronize();

    /// <summary>
    /// 터렛 상태 변경 이벤트를 해제하고 타깃 선택을 비활성화한다.
    /// </summary>
    private void OnDisable()
    {
        TurretInstanceRegistry.ActivationChanged -= OnActivationChanged;
        TurretInstanceRegistry.HealthChanged -= OnHealthChanged;
        TurretInstanceRegistry.Registered -= OnTurretChanged;
        TurretInstanceRegistry.Unregistered -= OnTurretChanged;
        TurretInstanceRegistry.Destroyed -= OnTurretChanged;
        TurretInstanceRegistry.Restored -= OnTurretChanged;
        if (_targetable != null)
            _targetable.enabled = false;
    }

    /// <summary>
    /// 터렛 활성 상태가 변경되면 대상 터렛의 상태를 다시 동기화한다.
    /// </summary>
    /// <param name="turret">활성 상태가 변경된 터렛.</param>
    /// <param name="active">변경 후 활성 상태.</param>
    private void OnActivationChanged(TurretBase turret, bool active) => OnTurretChanged(turret);

    /// <summary>
    /// 터렛 체력이 변경되면 대상 터렛의 상태를 다시 동기화한다.
    /// </summary>
    /// <param name="turret">체력이 변경된 터렛.</param>
    /// <param name="current">변경 후 현재 체력.</param>
    /// <param name="maximum">현재 최대 체력.</param>
    private void OnHealthChanged(TurretBase turret, int current, int maximum) => OnTurretChanged(turret);

    /// <summary>
    /// 이벤트가 현재 연결된 터렛에서 발생했을 때 상태를 동기화한다.
    /// </summary>
    /// <param name="turret">상태가 변경된 터렛.</param>
    private void OnTurretChanged(TurretBase turret)
    {
        if (turret == _turret)
            Synchronize();
    }

    /// <summary>
    /// 터렛의 체력, 화력, 선택 가능 상태를 타깃 상태 컴포넌트에 반영한다.
    /// </summary>
    private void Synchronize()
    {
        if (_targetable == null)
            return;

        _targetable.SetHealth(_turret != null ? _turret.CurrentHealth : 0,
            _turret != null ? _turret.MaxHealth : 0);
        _targetable.SetFirepower(Mathf.Clamp01(_fakeFirepowerRatio), 1f);
        _targetable.enabled = isActiveAndEnabled &&
            _turret != null && _turret.isActiveAndEnabled &&
            _turret.IsActivated && !_turret.IsDestroyed && _turret.CurrentHealth > 0;
    }
}
