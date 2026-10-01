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
    [SerializeField, Range(0f, 1f)] private float _fakeFirepowerRatio = 0.5f;

    private TargetableComponent _targetable;
    private int _instanceId;

    /// <summary>
    /// 타깃 상태 컴포넌트를 찾고 터렛 타깃으로 초기화한다.
    /// </summary>
    private void Awake()
    {
        _targetable = GetComponent<TargetableComponent>();
        _targetable.SetTargetType(TargetType.Turret);
        Synchronize();
    }

    /// <summary>
    /// 터렛 상태 변경 이벤트를 구독하고 현재 상태를 즉시 동기화한다.
    /// </summary>
    private void OnEnable()
    {
        TurretInstanceRegistry.SnapshotChanged += OnSnapshotChanged;
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
        TurretInstanceRegistry.SnapshotChanged -= OnSnapshotChanged;
        if (_targetable != null)
            _targetable.enabled = false;
    }

    /// <summary>
    /// 현재 연결된 터렛의 스냅샷이 변경되면 타깃 상태를 다시 동기화한다.
    /// </summary>
    /// <param name="instanceId">상태가 변경된 터렛의 런타임 식별 값.</param>
    private void OnSnapshotChanged(int instanceId)
    {
        if (_instanceId == 0)
            TurretInstanceRegistry.TryGetInstanceId(transform, out _instanceId);

        if (instanceId == _instanceId)
            Synchronize();
    }

    /// <summary>
    /// 터렛의 체력, 화력, 선택 가능 상태를 타깃 상태 컴포넌트에 반영한다.
    /// </summary>
    private void Synchronize()
    {
        if (_targetable == null)
            return;

        if (_instanceId == 0)
            TurretInstanceRegistry.TryGetInstanceId(transform, out _instanceId);

        TurretSnapshot snapshot = default;
        bool hasSnapshot = _instanceId > 0 &&
            TurretInstanceRegistry.TryGetSnapshot(_instanceId, out snapshot);
        _targetable.SetHealth(hasSnapshot ? snapshot.CurrentHealth : 0,
            hasSnapshot ? snapshot.MaxHealth : 0);
        _targetable.SetFirepower(Mathf.Clamp01(_fakeFirepowerRatio), 1f);
        _targetable.enabled = isActiveAndEnabled &&
            hasSnapshot && snapshot.IsActivated && !snapshot.IsDestroyed &&
            snapshot.CurrentHealth > 0;
    }
}
