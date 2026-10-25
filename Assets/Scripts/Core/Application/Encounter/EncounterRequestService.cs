// 현재 Match·실행 권한·참가자를 확인하고 최신 Snapshot을 E에 전달합니다.
// 같은 요청은 최대 한 번 실행하고 확정 결과를 재사용합니다. Unity 메인 스레드 소유입니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class EncounterRequestService : IEncounterRequestService, IDisposable
    {
        private readonly MatchId _matchId;
        private readonly Func<BattlefieldSpatialSnapshot> _snapshot;
        private readonly Func<bool> _isAuthority;
        private readonly Func<PlayerId, bool> _isParticipant;
        private readonly IEncounterExecutor _executor;
        private readonly Func<MatchEnvironmentSnapshot> _environment;
        private readonly Func<EncounterSpawnOrigin, EncounterSpawnPlacement> _resolveOrigin;
        private readonly MatchRequestLedger<EncounterRequestResult> _ledger;
        private long _sequence;
        private bool _disposed;
        public EncounterRequestService(MatchId matchId, Func<BattlefieldSpatialSnapshot> snapshot,
            Func<bool> isAuthority, Func<PlayerId, bool> isParticipant, IEncounterExecutor executor, int requestCapacity = 4096,
            Func<MatchEnvironmentSnapshot> environment = null,
            Func<EncounterSpawnOrigin, EncounterSpawnPlacement> resolveOrigin = null)
        {
            _matchId = matchId;
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _isAuthority = isAuthority ?? throw new ArgumentNullException(nameof(isAuthority));
            _isParticipant = isParticipant ?? throw new ArgumentNullException(nameof(isParticipant));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            if (environment != null && !(executor is IEnvironmentAwareEncounterExecutor))
                throw new ArgumentException("The executor must explicitly consume the environment context.", nameof(executor));
            _environment = environment;
            _resolveOrigin = resolveOrigin;
            _ledger = new MatchRequestLedger<EncounterRequestResult>(matchId, requestCapacity);
        }
        public EncounterRequestResult Submit(EncounterRequest request, PlayerId verifiedIssuer)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (_disposed) return Reject(request, EncounterRequestStatus.MatchUnavailable);
            if (request.Context.MatchId != _matchId) return Reject(request, EncounterRequestStatus.DifferentMatch);
            if (!_isAuthority() || verifiedIssuer.IsEmpty || verifiedIssuer != request.Context.IssuerId || !_isParticipant(verifiedIssuer))
                return Reject(request, EncounterRequestStatus.Unauthorized);
            BattlefieldSpatialSnapshot snapshot;
            try { snapshot = _snapshot(); }
            catch (ObjectDisposedException) { return Reject(request, EncounterRequestStatus.MatchUnavailable); }
            if (snapshot == null || snapshot.MatchId != _matchId) return Reject(request, EncounterRequestStatus.MatchUnavailable);
            MatchEnvironmentSnapshot environment = null;
            if (_environment != null)
            {
                try { environment = _environment(); }
                catch (ObjectDisposedException) { return Reject(request, EncounterRequestStatus.MatchUnavailable); }
                if (environment == null || environment.MatchId != _matchId) return Reject(request, EncounterRequestStatus.MatchUnavailable);
            }
            MatchRequestRegistration registration = _ledger.TryBegin(request.Context, request.GetPayloadKey(), out EncounterRequestResult cached);
            if (registration == MatchRequestRegistration.Completed) return cached.AsReplay();
            if (registration != MatchRequestRegistration.Started)
                return Reject(request, registration == MatchRequestRegistration.InProgress ? EncounterRequestStatus.InProgress :
                    registration == MatchRequestRegistration.CapacityExceeded ? EncounterRequestStatus.CapacityExceeded : EncounterRequestStatus.RequestConflict);
            EncounterRequestResult result;
            long sequence = checked(++_sequence);
            try
            {
                if (request.Origin.Kind == SpawnOriginKind.Automatic)
                    result = _environment == null ? _executor.Execute(request, snapshot, sequence) :
                        ((IEnvironmentAwareEncounterExecutor)_executor).Execute(request, snapshot, environment, sequence);
                else if (!(_executor is IOriginAwareEncounterExecutor originExecutor))
                    result = new EncounterRequestResult(request.Context.RequestId, sequence, snapshot.Revision,
                        EncounterRequestStatus.UnsupportedOrigin, reason: "The Feature executor does not support explicit origins.",
                        environmentRevision: environment?.Revision ?? 0);
                else
                {
                    var placement = _resolveOrigin?.Invoke(request.Origin);
                    result = placement == null ? new EncounterRequestResult(request.Context.RequestId, sequence, snapshot.Revision,
                        EncounterRequestStatus.OriginUnavailable, reason: "The source/placement is not available in this Host Match.",
                        environmentRevision: environment?.Revision ?? 0) :
                        originExecutor.Execute(request, snapshot, environment, placement, sequence);
                }
                if (result == null || result.RequestId != request.Context.RequestId ||
                    result.ExecutionSequence != sequence || result.SnapshotRevision != snapshot.Revision ||
                    result.EnvironmentRevision != (environment?.Revision ?? 0))
                    throw new InvalidOperationException("Executor returned an unrelated result.");
            }
            catch (Exception error)
            {
                // 실제 생성이 일부 진행됐을 수 있으므로 예외도 확정하고 자동 재실행하지 않습니다.
                result = new EncounterRequestResult(request.Context.RequestId, sequence, snapshot.Revision,
                    EncounterRequestStatus.ExecutionFailed, reason: error.Message, environmentRevision: environment?.Revision ?? 0);
            }
            // 실행 중 Feature 이벤트가 Match를 종료할 수 있습니다. 종료된 기록을 다시 사용하지 않습니다.
            if (!_disposed) _ledger.Complete(request.Context.RequestId, result);
            return result;
        }
        private static EncounterRequestResult Reject(EncounterRequest request, EncounterRequestStatus status)
            => new EncounterRequestResult(request.Context.RequestId, 0, 0, status, reason: status.ToString());
        public void Dispose() { if (_disposed) return; _disposed = true; _ledger.Dispose(); }
    }
}
