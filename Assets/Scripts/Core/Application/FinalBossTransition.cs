// 최종 보스 전환의 저장 선행·임시 정지·로컬 입력 잠금을 같은 수명으로 묶습니다.
// 실제 Scene 로드·참가자 이동·컷씬은 호출자가 수행하며 준비 완료 전에는 전투 진입을 확정하지 않습니다.
// 실패·취소·월드 복원 때 임대를 해제하고 이전 저장 완료를 새 월드의 허가로 쓰지 않습니다.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class FinalBossTransition : IDisposable
    {
        private readonly StageProgression _stage;
        private readonly MatchTimeService _time;
        private readonly Func<bool> _available;
        private readonly Guid _epoch;
        private readonly CancellationTokenSource _lifetime;
        private IDisposable _pause, _input;
        private bool _started;
        public CancellationToken Cancellation { get; }
        public bool IsPrepared { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool IsCurrent => !IsDisposed && !Cancellation.IsCancellationRequested &&
            _time.IsGameplayRunning && _available() && _time.Clock.Snapshot.Epoch == _epoch && _stage.Snapshot.CanEnterFinalBattle;

        public FinalBossTransition(StageProgression stage, MatchTimeService time, IInputBlocker input,
            Func<bool> available, CancellationToken lifetime, CancellationToken cancellation = default)
        {
            _stage = stage ?? throw new ArgumentNullException(nameof(stage));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _available = available ?? throw new ArgumentNullException(nameof(available));
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (_stage.Snapshot.MatchId != _time.Clock.Snapshot.MatchId)
                throw new ArgumentException("Stage 진행과 시간은 같은 Match에 속해야 합니다.");
            if (!_time.IsGameplayRunning || !_available() || !_stage.Snapshot.CanEnterFinalBattle)
                throw new InvalidOperationException("최종 진입은 현재 100% 점유·두 Field 생성 확인·생존 참가자가 필요합니다.");
            _epoch = time.Clock.Snapshot.Epoch;
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime, cancellation);
            Cancellation = _lifetime.Token;
            try
            {
                Cancellation.ThrowIfCancellationRequested();
                _pause = time.AcquirePause("FinalBossTransition");
                _input = input.Acquire("FinalBossTransition");
            }
            catch { Dispose(); throw; }
        }

        /// <summary>Scene 소유자가 한 번 호출합니다. 실제 Saved/현재 scope 완료 뒤에만 Scene 준비를 시작할 수 있습니다.</summary>
        public async Task PrepareAsync(CheckpointSlot slot,
            Func<CheckpointSlot, CancellationToken, Task<CheckpointOperationResult>> save)
        {
            if (_started) throw new InvalidOperationException("이 전환의 저장은 이미 요청됐습니다.");
            if (save == null) throw new ArgumentNullException(nameof(save));
            _started = true;
            try
            {
                RequireCurrent();
                var result = await save(slot, Cancellation);
                RequireCurrent();
                if (result?.Status != CheckpointOperationStatus.Saved || !result.IsCurrentScope)
                    throw new InvalidOperationException("최종 진입 전 저장 실패: " + (result?.Reason ?? "저장 결과 없음"));
                IsPrepared = true;
            }
            catch { Dispose(); throw; }
        }

        /// <summary>실제 보스 Scene의 전원 도착·보스 생성·연출 완료 뒤 호출합니다. 공용 시간은 재개하고 기존 Field만 보류합니다.</summary>
        public void ConfirmReady(EntityId actor)
        {
            RequireCurrent();
            if (!IsPrepared) throw new InvalidOperationException("최종 진입 전 실제 저장 완료가 필요합니다.");
            bool previouslyHeld = _time.FieldClock.IsHeld;
            _time.SetFieldHeld(true);
            try { _stage.ConfirmFinalBossReady(actor); }
            catch { _time.SetFieldHeld(previouslyHeld); throw; }
            Dispose();
        }

        /// <summary>Root Update가 호출합니다. 외부 취소/복원/실패 뒤 남은 입력·정지는 Unity 소유 스레드에서 해제합니다.</summary>
        public void InvalidateIfStale()
        {
            if (!IsDisposed && !IsCurrent) Dispose();
        }

        private void RequireCurrent()
        {
            if (!IsCurrent) throw new OperationCanceledException("이전 최종 진입 허가는 현재 월드에서 사용할 수 없습니다.", Cancellation);
        }

        /// <summary>호출자가 using으로 실패 경로도 정리합니다. 전환 Cancellation은 로드/연출용이며 보스 전투 수명으로 쓰지 않습니다.</summary>
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            var errors = new List<Exception>();
            Release(_lifetime.Cancel, errors);
            Release(() => _pause?.Dispose(), errors);
            Release(() => _input?.Dispose(), errors);
            Release(_lifetime.Dispose, errors);
            _pause = _input = null;
            if (errors.Count > 0) throw new AggregateException("최종 전환 임대를 해제하는 중 오류가 발생했습니다.", errors);
        }

        private static void Release(Action release, List<Exception> errors)
        {
            try { release(); } catch (Exception error) { errors.Add(error); }
        }
    }
}
