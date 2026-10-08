using System.Collections.Generic;
using UnityEngine;

/// <summary>몬스터 원본이나 프리팹을 수정하지 않고 지원 장비에 영향받는 동안만 행동을 중재합니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(2000)]
public sealed class PlayerFieldEnemyAdapter : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float _fallbackSpeed = 1.5f;
    [SerializeField, Min(1)] private float _fallbackDamage = 5;
    [SerializeField, Min(.1f)] private float _fallbackAttackCooldown = 1;
    private Monster _legacy;
    private EnemyNetworkController _networkDriver;
    private EnemyNetworkAnimator _networkAnimator;
    private Animator _animator;
    private readonly HashSet<int> _parameters = new HashSet<int>();
    private Vector3 _previousPosition;
    private PlayerFieldDevice _device;
    private bool _overridden, _legacyWasEnabled, _driverWasEnabled;
    private float _nextAttack, _measuredSpeed;

    private void Awake()
    {
        _legacy = GetComponent<Monster>();
        _networkDriver = GetComponent<EnemyNetworkController>();
        _networkAnimator = GetComponent<EnemyNetworkAnimator>();
        _animator = GetComponent<Animator>();
        if (_animator != null && _animator.runtimeAnimatorController != null)
            foreach (var parameter in _animator.parameters) _parameters.Add(parameter.nameHash);
        _previousPosition = transform.position;
    }

    private bool CanMediate => _legacy != null ? !_legacy.isDead && (_legacy.enabled || _overridden) :
        _networkDriver != null && (_networkDriver.enabled || _overridden) && _networkDriver.IsSpawned && _networkDriver.IsServer;

    private void LateUpdate()
    {
        if (!CanMediate || !PlayerFieldDevice.HasActiveDevices)
        {
            Release();
            _previousPosition = transform.position;
            return;
        }
        if (!_overridden)
        {
            Vector2 moved = transform.position - _previousPosition;
            if (moved.sqrMagnitude > .000001f && Time.deltaTime > 0)
                _measuredSpeed = moved.magnitude / Time.deltaTime;
            // 실제 이동 방향에서 가까운 방벽만 반응하며 멀리 보이는 방벽에 유인되지 않습니다.
            Vector2 probe = (Vector2)transform.position + moved.normalized * .4f;
            _device = PlayerFieldDevice.Resolve(_previousPosition, probe);
            if (_device == null)
            {
                _previousPosition = transform.position;
                return;
            }
            TakeControl();
            transform.position = _previousPosition;
        }
        else if (_device == null || !_device.IsAlive)
        {
            Release();
            _previousPosition = transform.position;
            return;
        }

        if (_device.Kind == PlayerFieldDeviceKind.Beacon)
        {
            var next = PlayerFieldDevice.Resolve(transform.position, _device.transform.position);
            if (next == null) { Release(); _previousPosition = transform.position; return; }
            _device = next;
        }
        Vector2 point = _device.ApproachPoint(transform.position);
        Vector2 delta = point - (Vector2)transform.position;
        float range = _legacy != null ? Mathf.Max(.35f, _legacy.attackRange) : .4f;
        bool attacking = delta.magnitude <= range;
        Animate(delta.normalized, !attacking);
        if (attacking)
        {
            if (Time.time >= _nextAttack)
            {
                _nextAttack = Time.time + (_legacy != null ? Mathf.Max(.2f, _legacy.attackCooldown) : _fallbackAttackCooldown);
                _device.TakeDamage(_legacy != null ? Mathf.Max(1, _legacy.attackDamage) : _fallbackDamage);
                if (_networkAnimator != null && _networkAnimator.IsSpawned && _networkAnimator.IsServer)
                    _networkAnimator.PlayAttack();
                else if (_animator != null && _parameters.Contains(Animator.StringToHash("Attack")))
                    _animator.SetTrigger("Attack");
            }
        }
        else
        {
            float speed = _legacy != null ? Mathf.Max(0, _legacy.moveSpeed) : _measuredSpeed > 0 ? _measuredSpeed : _fallbackSpeed;
            float step = Mathf.Min(speed * Time.deltaTime, Mathf.Max(0, delta.magnitude - range));
            transform.position += (Vector3)delta.normalized * step;
        }
        _previousPosition = transform.position;
    }

    private void TakeControl()
    {
        _overridden = true;
        if (_legacy != null)
        {
            _legacyWasEnabled = _legacy.enabled;
            _legacy.enabled = false;
        }
        if (_networkDriver != null)
        {
            _driverWasEnabled = _networkDriver.enabled;
            _networkDriver.enabled = false;
        }
        var body = GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
    }

    private void Release()
    {
        if (!_overridden) return;
        _overridden = false;
        if (_legacy != null && !_legacy.isDead) _legacy.enabled = _legacyWasEnabled;
        if (_networkDriver != null) _networkDriver.enabled = _driverWasEnabled;
        _device = null;
    }

    private void Animate(Vector2 direction, bool moving)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;
        SetBool("IsMoving", moving);
        SetBool("isMoving", moving);
        SetFloat("MoveX", Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? Mathf.Sign(direction.x) : 0);
        SetFloat("MoveY", Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? 0 : Mathf.Sign(direction.y));
        SetFloat("MoveAnimSpeed", 1);
        if (_parameters.Contains(Animator.StringToHash("direction")))
        {
            var facing = PlayerAimUtility.GetFacing(direction);
            _animator.SetInteger("direction", facing == PlayerAimFacing.Up ? 1 : facing == PlayerAimFacing.Left ? 2 : facing == PlayerAimFacing.Right ? 3 : 0);
        }
    }

    private void SetBool(string name, bool value) { if (_parameters.Contains(Animator.StringToHash(name))) _animator.SetBool(name, value); }
    private void SetFloat(string name, float value) { if (_parameters.Contains(Animator.StringToHash(name))) _animator.SetFloat(name, value); }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
