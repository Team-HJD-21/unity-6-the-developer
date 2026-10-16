using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>Match와 UI의 조작 잠금 상태를 투영합니다. 레거시 매니저를 참조하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerControlState : MonoBehaviour, IPlayerMatchBinding, IPlayerNetworkMatchBinding
{
    [Tooltip("현재 PoC에서는 전투 중에도 스킬 변경을 허용합니다. 정식 준비 단계 적용 시 끕니다.")]
    [SerializeField] private bool _allowSelectionDuringCombat = true;
    [SerializeField] private bool _allowActionsDuringPreparation = true;
    private MatchSession _session;
    private NetworkMatchCoordinator _networkMatch;
    private bool _wasBound, _uiBlocked;
    public bool HasMatchBinding => _wasBound;
    public bool IsBlocked => !isActiveAndEnabled || _uiBlocked || Time.timeScale <= 0;
    private bool IsPreparing => _networkMatch != null
        ? _networkMatch.Phase == NetworkMatchPhase.Preparing
        : _session != null && !_session.IsDisposed && _session.State.Phase == MatchPhase.Preparing;
    private bool IsRunning => _networkMatch != null
        ? _networkMatch.Phase == NetworkMatchPhase.Running
        : _session != null && !_session.IsDisposed && _session.State.Phase == MatchPhase.Running;
    public bool CanPrepare => !IsBlocked && (!_wasBound || IsPreparing || (_allowSelectionDuringCombat && IsRunning));
    public bool CanAct => !IsBlocked && (!_wasBound || IsRunning || (_allowActionsDuringPreparation && IsPreparing));
    public void SetInputBlocked(bool blocked) => _uiBlocked = blocked;
    public void BindMatch(MatchSession session)
    {
        _session = session ?? throw new System.ArgumentNullException(nameof(session));
        _networkMatch = null;
        _wasBound = true;
    }
    public void UnbindMatch() { _session = null; _networkMatch = null; _wasBound = true; }
    public void BindNetworkMatch(NetworkMatchCoordinator match, PlayerId localPlayerId)
    {
        if (localPlayerId.IsEmpty) throw new System.ArgumentException("A local participant is required.", nameof(localPlayerId));
        _networkMatch = match ?? throw new System.ArgumentNullException(nameof(match));
        _session = null; _wasBound = true;
    }
    public void UnbindNetworkMatch() => UnbindMatch();
}
