using UnityEngine;

/// <summary>Space 요청과 대시 방향을 관리합니다. Rigidbody 속도는 PlayerMovement만 변경합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement), typeof(PlayerInputHandler))]
[DefaultExecutionOrder(150)]
public sealed class PlayerDash : MonoBehaviour
{
    [SerializeField, Min(1)] private float _speedMultiplier = 2.5f;
    [SerializeField, Min(.01f)] private float _duration = .16f;
    [SerializeField, Min(0)] private float _cooldown = 1.2f;
    private readonly PlayerDashState _state = new();
    private PlayerMovement _movement;
    private PlayerInputHandler _input;
    private PlayerSkillController _skills;
    private Vector2 _lastDirection = Vector2.down;
    private Vector2 _direction;
    private float _speed;

    public bool IsDashing => _state.IsDashing;
    public bool CanUseInput => isActiveAndEnabled && _movement != null && _movement.CanAcceptInput &&
        (_skills == null || !_skills.IsPlacingSkill);
    public bool IsReady => CanUseInput && _state.IsReady;
    public float CooldownRemaining => _state.CooldownRemaining;
    public float CooldownProgress => _state.CooldownDuration > 0
        ? Mathf.Clamp01(CooldownRemaining / _state.CooldownDuration) : 0;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _input = GetComponent<PlayerInputHandler>();
        _skills = GetComponent<PlayerSkillController>();
    }

    private void Update()
    {
        _state.TickCooldown(Time.deltaTime);
        if (!CanUseInput)
        {
            if (IsDashing) _movement.StopImmediately();
            return;
        }
        if (PlayerAimUtility.IsValidDirection(_input.MoveInput)) _lastDirection = _input.MoveInput.normalized;
        if (_input.WasDashPressed) TryDash();
    }

    public bool TryDash()
    {
        if (!IsReady || _movement.MoveSpeed <= 0) return false;
        if (float.IsNaN(_speedMultiplier) || float.IsInfinity(_speedMultiplier) || _speedMultiplier < 1) return false;
        _speed = _movement.MoveSpeed * _speedMultiplier;
        if (float.IsNaN(_speed) || float.IsInfinity(_speed) || !_state.TryStart(_duration, _cooldown)) return false;
        _direction = PlayerAimUtility.IsValidDirection(_input.MoveInput) ? _input.MoveInput.normalized : _lastDirection;
        return true;
    }

    /// <summary>마지막 스텝은 남은 시간만 이동해 고정 물리 주기에 따른 거리 초과를 막습니다.</summary>
    public bool TryGetVelocity(float fixedStep, out Vector2 velocity)
    {
        velocity = Vector2.zero;
        if (!CanUseInput || !IsDashing) return false;
        float travelTime = _state.ConsumeStep(fixedStep);
        if (travelTime <= 0) return false;
        velocity = _direction * (_speed * travelTime / fixedStep);
        return true;
    }

    public void CancelDash() => _state.Cancel();

    private void OnCollisionEnter2D(Collision2D collision) => StopAtObstacle(collision);
    private void OnCollisionStay2D(Collision2D collision) => StopAtObstacle(collision);
    private void StopAtObstacle(Collision2D collision)
    {
        if (!IsDashing) return;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Vector2.Dot(_direction, collision.GetContact(i).normal) >= -.05f) continue;
            _movement.StopImmediately();
            return;
        }
    }

    private void OnDisable()
    {
        if (_movement != null && IsDashing) _movement.StopImmediately();
        _state.Cancel();
    }
}
