// Stage 사건의 등장 전/처치 후 저장을 직렬화하고 디스크 완료 전 생성을 보류합니다.
// 실제 저장은 주입된 CheckpointService가 수행하며 필수 writer 누락을 우회하지 않습니다.
// 작업 상태는 월드 원본이 아니며 복원 epoch/Match 종료 때 폐기합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class StageCheckpointGate : IFieldBossSpawnService, IDisposable
    {
        private enum State { Queued, Saving, Saved, Failed }
        private sealed class Entry
        {
            public string Key;
            public Guid Epoch;
            public State State;
            public string Error;
        }
        private readonly MatchId _match;
        private readonly Guid _run;
        private readonly Func<Guid> _epoch;
        private readonly Func<CheckpointSlot, CancellationToken, Task<CheckpointOperationResult>> _save;
        private readonly IFieldBossSpawnService _spawns;
        private readonly CancellationTokenSource _lifetime;
        private readonly List<Entry> _entries = new();
        private Guid _observedEpoch;
        private bool _saving, _disposed;

        public bool HasSpawnSource => _spawns != null;
        public IReadOnlyList<string> PendingKeys => _entries.Where(e => e.State != State.Saved)
            .Select(e => e.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        public string FailureReason => _entries.FirstOrDefault(e => e.State == State.Failed)?.Error;

        public StageCheckpointGate(MatchId match, Guid run, Func<Guid> epoch,
            Func<CheckpointSlot, CancellationToken, Task<CheckpointOperationResult>> save,
            IFieldBossSpawnService spawns, CancellationToken lifetime)
        {
            if (match.IsEmpty || run == Guid.Empty) throw new ArgumentException("A current Match/Mission is required.");
            _match = match;
            _run = run;
            _epoch = epoch ?? throw new ArgumentNullException(nameof(epoch));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _spawns = spawns;
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        }

        /// <summary>StageProgression이 호출합니다. 같은 epoch의 등장 전 저장이 실제 완료된 뒤에만 생성 원본을 호출합니다.</summary>
        public bool TrySpawnFieldBoss(Guid eventId, int threshold, RegionGridSnapshot regions, out EntityId actor, out string reason)
        {
            actor = default;
            reason = "StageCheckpointUnavailable";
            if (_disposed || _lifetime.IsCancellationRequested || regions == null || regions.MatchId != _match || eventId == Guid.Empty) return false;
            if (threshold != 33 && threshold != 66) throw new ArgumentOutOfRangeException(nameof(threshold));
            if (_spawns == null) { reason = "FieldBossContentNotConnected"; return false; }
            var entry = Queue("before-" + threshold);
            Pump();
            if (entry?.State != State.Saved)
            {
                reason = entry?.State == State.Failed ? "등장 전 저장 실패: " + entry.Error : "등장 전 저장 대기 · " + threshold + "%";
                return false;
            }
            return _spawns.TrySpawnFieldBoss(eventId, threshold, regions, out actor, out reason);
        }

        /// <summary>실제 사망을 처음 확인한 조립부가 호출합니다. 반복 통지/다른 저장 작업과 겹쳐도 한 번만 큐에 넣습니다.</summary>
        public void RecordFieldBossDefeated(int threshold)
        {
            if (threshold != 33 && threshold != 66) throw new ArgumentOutOfRangeException(nameof(threshold));
            Queue("defeat-" + threshold);
            // Enemy의 같은 사망 호출 스택에서 Despawn/등록 해제가 끝난 뒤 다음 Root Update가 캡처합니다.
        }

        private Entry Queue(string key)
        {
            if (!SyncEpoch()) return null;
            var entry = _entries.FirstOrDefault(e => e.Key == key);
            if (entry == null)
            {
                entry = new Entry { Key = key, Epoch = _observedEpoch, State = State.Queued };
                _entries.Add(entry);
            }
            return entry;
        }

        private bool SyncEpoch()
        {
            if (_disposed || _lifetime.IsCancellationRequested) return false;
            Guid epoch = _epoch();
            if (epoch == Guid.Empty) return false;
            if (_observedEpoch != epoch)
            {
                _entries.Clear();
                _observedEpoch = epoch;
            }
            return true;
        }

        /// <summary>Scene Update가 호출합니다. Busy는 다음 프레임에 다시 시도하고 기술 실패는 명시 RetryFailed 전까지 유지합니다.</summary>
        public void Pump()
        {
            if (!SyncEpoch() || _saving) return;
            var next = _entries.FirstOrDefault(e => e.State == State.Queued);
            if (next == null) return;
            next.State = State.Saving;
            _saving = true;
            _ = SaveAsync(next);
        }

        private async Task SaveAsync(Entry entry)
        {
            try
            {
                var result = await _save(new CheckpointSlot("stage-" + _run.ToString("N") + "-" + entry.Key), _lifetime.Token);
                if (_disposed || _lifetime.IsCancellationRequested || entry.Epoch != _epoch()) return;
                if (result?.Status == CheckpointOperationStatus.Saved && result.IsCurrentScope) entry.State = State.Saved;
                else if (result?.Status == CheckpointOperationStatus.Busy) entry.State = State.Queued;
                else
                {
                    entry.State = State.Failed;
                    entry.Error = result?.Reason ?? "저장 결과가 없습니다.";
                }
            }
            catch (Exception error)
            {
                if (!_disposed && !_lifetime.IsCancellationRequested && entry.Epoch == _epoch())
                {
                    entry.State = State.Failed;
                    entry.Error = error.Message;
                }
            }
            finally { _saving = false; }
        }

        /// <summary>실패 UI/Debugger가 원인을 해결한 뒤 호출합니다. 이미 성공한 저장이나 실제 생성은 반복하지 않습니다.</summary>
        public void RetryFailed()
        {
            if (!SyncEpoch()) return;
            foreach (var entry in _entries.Where(e => e.State == State.Failed))
            {
                entry.State = State.Queued;
                entry.Error = null;
            }
            Pump();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _entries.Clear();
            try { _lifetime.Cancel(); } finally { _lifetime.Dispose(); }
        }
    }
}
