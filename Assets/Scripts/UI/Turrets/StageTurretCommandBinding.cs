// HUD의 요청 ID·미확정 상태·확정 결과 구독을 관리합니다. 실제 D 상태는 변경하지 않습니다.
// 잠시 숨겨져도 요청 추적은 유지하고, 다른 Match/대상의 늦은 결과는 표시하지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
public sealed class StageTurretCommandBinding : IDisposable
{
    private readonly ITurretCommandRequester _requester;
    private readonly Dictionary<Guid, TurretCommandRequest> _pending = new Dictionary<Guid, TurretCommandRequest>();
    private readonly Dictionary<Guid, TurretCommandResult> _awaitingState = new Dictionary<Guid, TurretCommandResult>();
    private bool _disposed;
    public MatchId MatchId { get; }
    public PlayerId PlayerId { get; }
    public ITurretPresentationSource Presentation { get; }
    public bool Matches(MatchId matchId, PlayerId playerId, ITurretCommandRequester requester, ITurretPresentationSource presentation)
        => !_disposed && MatchId == matchId && PlayerId == playerId && ReferenceEquals(_requester, requester) && ReferenceEquals(Presentation, presentation);
    public event Action<TurretCommandResult> ResultReceived;
    public StageTurretCommandBinding(MatchId matchId, PlayerId playerId, ITurretCommandRequester requester, ITurretPresentationSource presentation)
    {
        if (matchId.IsEmpty || playerId.IsEmpty) throw new ArgumentException("Bind a Match participant.");
        _requester = requester ?? throw new ArgumentNullException(nameof(requester));
        Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        MatchId = matchId; PlayerId = playerId; _requester.ResultReceived += OnResult;
        Presentation.Changed += OnPresentationChanged;
    }
    public bool HasPending(EntityId entityId) => _pending.Values.Any(request => request.TargetId == entityId);
    public bool TrySubmit(TurretCommandKind kind, EntityId target = default, bool activate = false, DefinitionId upgrade = default)
    {
        if (_disposed || _pending.Count >= 64 || HasPending(target)) return false;
        var request = new TurretCommandRequest(new MatchRequestContext(MatchId, Guid.NewGuid(), PlayerId), kind, target, activate, upgrade);
        _pending.Add(request.Context.RequestId, request);
        if (_requester.TrySubmit(request)) return true;
        _pending.Remove(request.Context.RequestId); return false;
    }
    public bool TryRetry(EntityId target)
    {
        if (_disposed) return false;
        TurretCommandRequest request = _pending.Values.FirstOrDefault(item => item.TargetId == target);
        return request != null && _requester.TryRetry(request.Context.RequestId);
    }
    private void OnResult(TurretCommandResult result)
    {
        if (_disposed || result == null || result.MatchId != MatchId || !_pending.TryGetValue(result.RequestId, out var request) ||
            request.Kind != result.Kind || request.TargetId != result.TargetId || result.Status == TurretCommandStatus.InProgress) return;
        // 실행 예외도 일부 D 변경이 있었을 수 있으므로 최신 상태를 확인한 뒤 수동 입력을 다시 엽니다.
        if ((result.WasAccepted || result.Status == TurretCommandStatus.ExecutionFailed) && (Presentation.Snapshot == null || Presentation.Snapshot.MatchId != MatchId ||
            Presentation.Snapshot.ConfirmedCommandSequence < result.ExecutionSequence))
        { _awaitingState[result.RequestId] = result; return; }
        Complete(result);
    }
    private void OnPresentationChanged(TurretPresentationFrame frame)
    {
        if (_disposed || frame == null || frame.MatchId != MatchId) return;
        foreach (var result in _awaitingState.Values.Where(item => item.ExecutionSequence <= frame.ConfirmedCommandSequence).ToArray())
        { if (_disposed) break; Complete(result); }
    }
    private void Complete(TurretCommandResult result)
    {
        _pending.Remove(result.RequestId); _awaitingState.Remove(result.RequestId); ResultReceived?.Invoke(result);
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _requester.ResultReceived -= OnResult;
        Presentation.Changed -= OnPresentationChanged; _pending.Clear(); _awaitingState.Clear(); ResultReceived = null;
    }
}
