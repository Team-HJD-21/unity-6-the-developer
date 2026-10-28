// 현재 Match·권한·참가자와 주입 정책을 검증하고 D 처리기의 결과를 확정합니다.
// 같은 요청은 한 번만 처리하며 재전송은 실패/거부도 포함한 원래 결과를 재사용합니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class TurretCommandService : ITurretCommandService, IDisposable
    {
        private readonly MatchId _matchId;
        private readonly Func<bool> _isAuthority, _isMatchRunning;
        private readonly Func<PlayerId, bool> _isParticipant;
        private readonly ITurretCommandAuthorization _authorization;
        private readonly ITurretCommandExecutor _executor;
        private readonly MatchRequestLedger<TurretCommandResult> _ledger;
        private long _sequence;
        private bool _disposed;
        public TurretCommandService(MatchId matchId, Func<bool> isAuthority, Func<bool> isMatchRunning,
            Func<PlayerId, bool> isParticipant, ITurretCommandAuthorization authorization,
            ITurretCommandExecutor executor, int requestCapacity = 4096)
        {
            _isAuthority = isAuthority ?? throw new ArgumentNullException(nameof(isAuthority));
            _isMatchRunning = isMatchRunning ?? throw new ArgumentNullException(nameof(isMatchRunning));
            _isParticipant = isParticipant ?? throw new ArgumentNullException(nameof(isParticipant));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _ledger = new MatchRequestLedger<TurretCommandResult>(matchId, requestCapacity); _matchId = matchId;
        }
        public TurretCommandResult Submit(TurretCommandRequest request, PlayerId verifiedIssuer)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (_disposed) return Result(request, TurretCommandStatus.MatchUnavailable);
            if (request.Context.MatchId != _matchId) return Result(request, TurretCommandStatus.DifferentMatch);
            if (!_isAuthority() || verifiedIssuer.IsEmpty || verifiedIssuer != request.Context.IssuerId || !_isParticipant(verifiedIssuer))
                return Result(request, TurretCommandStatus.Unauthorized);
            if (!_isMatchRunning()) return Result(request, TurretCommandStatus.MatchUnavailable);
            MatchRequestRegistration registration = _ledger.TryBegin(request.Context, request.GetPayloadKey(), out var cached);
            if (registration == MatchRequestRegistration.Completed) return cached.AsReplay();
            if (registration != MatchRequestRegistration.Started)
                return Result(request, registration == MatchRequestRegistration.InProgress ? TurretCommandStatus.InProgress :
                    registration == MatchRequestRegistration.CapacityExceeded ? TurretCommandStatus.CapacityExceeded : TurretCommandStatus.RequestConflict);
            long sequence = checked(++_sequence);
            TurretCommandResult result;
            try
            {
                var permission = _authorization.Authorize(request, verifiedIssuer) ??
                    throw new InvalidOperationException("The authorization policy returned no decision.");
                if (_disposed || !_isMatchRunning()) result = Result(request, TurretCommandStatus.MatchUnavailable, sequence: sequence);
                else if (!_isAuthority() || !_isParticipant(verifiedIssuer)) result = Result(request, TurretCommandStatus.Unauthorized, sequence: sequence);
                else if (!permission.IsAllowed) result = Result(request, TurretCommandStatus.Unauthorized, permission.Code, permission.Reason, sequence);
                else
                {
                    var execution = _executor.Execute(request) ?? throw new InvalidOperationException("The Feature returned no result.");
                    result = Result(request, execution.Status, execution.Code, execution.Reason, sequence);
                }
            }
            catch (Exception error)
            {
                // 일부 Feature 부작용 뒤 실패했을 수 있어 새 실행으로 자동 재시도하지 않습니다.
                string reason = error.Message ?? "Execution failed.";
                result = Result(request, TurretCommandStatus.ExecutionFailed, "ExecutionFailed",
                    reason.Length <= 512 ? reason : reason.Substring(0, 512), sequence);
            }
            if (!_disposed) _ledger.Complete(request.Context.RequestId, result);
            return result;
        }
        private static TurretCommandResult Result(TurretCommandRequest request, TurretCommandStatus status,
            string code = "", string reason = "", long sequence = 0)
            => new TurretCommandResult(request.Context.MatchId, request.Context.RequestId, request.Kind, request.TargetId,
                sequence, status, code, string.IsNullOrEmpty(reason) ? status.ToString() : reason);
        public void Dispose() { if (_disposed) return; _disposed = true; _ledger.Dispose(); }
    }
}
