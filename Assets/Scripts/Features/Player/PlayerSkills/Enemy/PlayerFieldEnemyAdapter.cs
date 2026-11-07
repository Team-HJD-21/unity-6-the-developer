// Player 장치가 사용하는 적 대상 계약을 실제 적 컴포넌트에 연결합니다.
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>서버 네트워크 적과 방벽·비콘을 연결합니다. 원래 목표와 몬스터 원본은 변경하지 않습니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class PlayerFieldEnemyAdapter : MonoBehaviour
{
    // 신규 적의 공격 수치 API가 없어 장비 상호작용용 임시 설정을 사용합니다.
    [SerializeField, Min(.1f)] private float _fallbackSpeed = 1.5f;
    [SerializeField, Min(1)] private float _fallbackDamage = 5;
    [SerializeField, Min(.1f)] private float _fallbackAttackCooldown = 1;
    [SerializeField, Min(.1f)] private float _attackRange = .4f;
    private PlayerFieldEnemyControlLease _control;
    private PlayerFieldEnemyAnimation _animation;
    private readonly PlayerFieldEnemyBehaviour _behaviour = new();
    private IPlayerFieldTarget _device;
    private readonly PlayerBarrierEnemyBehaviour _barrierBehaviour = new();
    private readonly PlayerBeaconEnemyBehaviour _beaconBehaviour = new();
    private Vector3 _previousPosition;
    private Vector2 _lastDirection;
    private float _measuredSpeed;
    private Collider2D[] _bodyColliders;
    private void Awake()
    {
        _control = new PlayerFieldEnemyControlLease(gameObject);
        _animation = new PlayerFieldEnemyAnimation(gameObject);
        _bodyColliders = GetComponentsInChildren<Collider2D>(true);
        _previousPosition = transform.position;
    }
    // 기존 NetworkAnimator가 LateUpdate에서 게시하기 전에 표현을 갱신합니다.
    private void Update()
    {
        if (!FieldSimulationTime.CanAdvance(this)) return;
        if (!_control.CanControl || !PlayerFieldDevice.HasActiveDevices) { Release(); return; }
        Collider2D body = FindBody();
        if (body == null) { Release(); return; }
        if (!_control.IsHeld)
        {
            Vector2 moved = transform.position - _previousPosition;
            if (moved.sqrMagnitude > .000001f && Time.deltaTime > 0)
            {
                _measuredSpeed = moved.magnitude / Time.deltaTime;
                _lastDirection = moved.normalized;
            }
            _device = PlayerFieldDeviceRegistry.ResolveTarget(_previousPosition,
                (Vector2)transform.position + _lastDirection * .4f, body);
            if (_device == null || !_control.TryAcquire()) { _previousPosition = transform.position; return; }
            transform.position = _previousPosition;
        }
        else if (_device == null || !_device.IsAlive) { Release(); return; }
        if (_device.Kind == PlayerFieldDeviceKind.Beacon) _device = _beaconBehaviour.Resolve(transform.position, _device, body);
        else if (_device.Kind == PlayerFieldDeviceKind.Barrier) _device = _barrierBehaviour.Resolve(_device);
        if (_device == null) { Release(); return; }
        bool attacked = _behaviour.Step(transform, body, _device,
            _measuredSpeed > 0 ? _measuredSpeed : _fallbackSpeed, _attackRange,
            _fallbackDamage, _fallbackAttackCooldown, FieldSimulationTime.Seconds(this), Time.deltaTime, out var direction, out bool moving);
        _animation.SetMovement(direction, moving);
        if (attacked) _animation.PlayAttack();
        _previousPosition = transform.position;
    }
    private Collider2D FindBody()
    {
        foreach (var collider in _bodyColliders)
            if (PlayerDeviceContactQuery.IsBody(collider)) return collider;
        return null;
    }
    private void Release()
    {
        if (_control != null && _control.IsHeld) _animation.SetMovement(Vector2.zero, false);
        _control?.Release();
        _device = null;
        _previousPosition = transform.position;
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
