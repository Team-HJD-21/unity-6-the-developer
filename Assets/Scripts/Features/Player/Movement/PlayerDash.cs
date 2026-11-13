// Player의 대시 방향·남은 거리/쿨다운을 관리합니다.
// 필드에서는 공용 시간을, 허브 이동에서는 로컬 이동 시간을 사용하며 Rigidbody는 Movement가 갱신합니다.
using UnityEngine;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Domain;

/// <summary>Space 요청과 대시 방향을 관리합니다. Rigidbody 속도는 PlayerMovement만 변경합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement), typeof(PlayerInputHandler))]
public sealed partial class PlayerDash : MonoBehaviour
{
    [SerializeField, Min(1)] private float _speedMultiplier = 2.5f;
    [SerializeField, Min(.01f)] private float _duration = .16f;
    [SerializeField, Min(0)] private float _cooldown = 1.2f;
    private readonly PlayerDashTimer _state = new();
    private PlayerMovement _movement;
    private PlayerInputHandler _input;
    private PlayerControlState _controls;
    private Vector2 _lastDirection = Vector2.down;
    private Vector2 _direction;
    private float _speed;
    public event System.Action<bool> DashInputAttempted;

    public bool IsDashing => _state.IsDashing;
    public bool CanUseInput => isActiveAndEnabled && _movement != null && _movement.CanAcceptInput;
    public bool IsReady => CanUseInput && _state.IsReady;
    public float CooldownRemaining => _state.CooldownRemaining;
    public float CooldownProgress => _state.CooldownDuration > 0
        ? Mathf.Clamp01(CooldownRemaining / _state.CooldownDuration) : 0;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _input = GetComponent<PlayerInputHandler>();
        _controls = GetComponent<PlayerControlState>();
    }

    // PlayerInputHandler(-200)가 수집한 입력을 사용합니다. 설치 미리보기 중에도 대시할 수 있습니다.
    private void Update()
    {
        var presence = GetComponent<PlayerMatchPresence>();
        bool inHub = presence != null && presence.Location == MatchPlayerLocation.Spaceship;
        _state.TickCooldown(inHub ? Time.deltaTime : FieldSimulationTime.DeltaTime(this));
        if (_controls != null && _controls.IsFieldPaused && !_controls.IsBlocked) return;
        if (!CanUseInput)
        {
            if (IsDashing) _movement.StopImmediately();
            return;
        }
        if (PlayerAimUtility.IsValidDirection(_input.MoveInput)) _lastDirection = _input.MoveInput.normalized;
        if (_input.WasDashPressed)
        {
            bool accepted = TryDash();
            DashInputAttempted?.Invoke(accepted);
        }
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
