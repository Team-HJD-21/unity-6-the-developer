// Match/UI의 입력 잠금을 투영하고 Host 검증용 actor 상태를 조회합니다.
// 이 Port의 존재가 원격 actor 복제나 Client 좌표의 권위 승인을 의미하지는 않습니다.
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>Match와 UI의 조작 잠금 상태를 투영합니다. 레거시 매니저를 참조하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerControlState : MonoBehaviour, IPlayerMatchBinding, IPlayerNetworkMatchBinding, IPlayerInteractionActor, ILocalInputConsumer
{
    [Tooltip("현재 PoC에서는 전투 중에도 스킬 변경을 허용합니다. 정식 준비 단계 적용 시 끕니다.")]
    [SerializeField] private bool _allowSelectionDuringCombat = true;
    [SerializeField] private bool _allowActionsDuringPreparation = true;
    private MatchSession _session;
    private NetworkMatchCoordinator _networkMatch;
    private bool _wasBound, _uiBlocked, _lifeBlocked;
    private IInputBlocker _inputLocks;
    public bool HasMatchBinding => _wasBound;
    public bool IsBlocked => !isActiveAndEnabled || _uiBlocked || _lifeBlocked || (_inputLocks?.IsBlocked ?? false) || Time.timeScale <= 0;
    private bool IsPreparing => _networkMatch != null
        ? _networkMatch.Phase == NetworkMatchPhase.Preparing
        : _session != null && !_session.IsDisposed && _session.State.Phase == MatchPhase.Preparing;
    private bool IsRunning => _networkMatch != null
        ? _networkMatch.IsGameplayRunning
        : _session != null && !_session.IsDisposed && _session.State.Phase == MatchPhase.Running;
    public bool CanPrepare => !IsBlocked && (!_wasBound || IsPreparing || (_allowSelectionDuringCombat && IsRunning));
    public bool CanAct => !IsBlocked && (!_wasBound || IsRunning || (_allowActionsDuringPreparation && IsPreparing));
    public bool TryCaptureInteraction(out PlayerInteractionSnapshot snapshot)
    {
        snapshot = default;
        var health = GetComponent<PlayerInfo>();
        if (!HasMatchBinding || !isActiveAndEnabled || health == null || !health.isActiveAndEnabled) return false;
        Vector3 position = transform.position;
        if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y)) return false;
        snapshot = new PlayerInteractionSnapshot(new BattlefieldPoint(position.x, position.y), health.curHp > 0, CanAct);
        return true;
    }
    public void SetInputBlocked(bool blocked) => _uiBlocked = blocked;
    /// <summary>PlayerInfo의 사망·복원만 갱신하는 생존 잠금입니다. UI나 메뉴 잠금을 해제하지 않습니다.</summary>
    internal void SetLifeBlocked(bool blocked) => _lifeBlocked = blocked;
    public void BindInputBlocker(IInputBlocker input) => _inputLocks = input ?? throw new System.ArgumentNullException(nameof(input));
    public void BindMatch(MatchSession session)
    {
        _session = session ?? throw new System.ArgumentNullException(nameof(session));
        _networkMatch = null;
        _wasBound = true;
    }
    public void UnbindMatch() { _session = null; _networkMatch = null; _inputLocks = null; _wasBound = true; }
    public void BindNetworkMatch(NetworkMatchCoordinator match, PlayerId localPlayerId)
    {
        if (localPlayerId.IsEmpty) throw new System.ArgumentException("A local participant is required.", nameof(localPlayerId));
        _networkMatch = match ?? throw new System.ArgumentNullException(nameof(match));
        _session = null; _wasBound = true;
    }
    public void UnbindNetworkMatch() => UnbindMatch();
}
