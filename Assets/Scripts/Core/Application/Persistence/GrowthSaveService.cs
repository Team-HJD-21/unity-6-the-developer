// 개인/Host 전장의 성장 section을 월드 체크포인트와 다른 슬롯에 저장합니다.
// 실제 원본은 메인 스레드에서 복사하고, 불변 자료의 파일 쓰기만 순서대로 처리합니다.
// 중요 변경은 즉시 캡처, 잦은 XP는 짧게 합칩니다. 실패 자료를 기본값으로 덮지 않습니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class GrowthSaveService : IDisposable
    {
        private readonly ICheckpointStore _store;
        private readonly ICheckpointPayloadCodec _codec;
        private readonly CheckpointSlot _slot;
        private readonly Func<CheckpointCaptureContext> _context;
        private readonly IGrowthCheckpointParticipant[] _owners;
        private readonly double _coalesceSeconds;
        private readonly int _thread = Environment.CurrentManagedThreadId;
        private Task _pending = Task.CompletedTask;
        private bool _dirty, _restoring, _initialized, _disposed;
        private double? _due;
        private long _issued, _saved;
        private Exception _lastError;
        /// <summary>사용자/전장 논리 키를 경로 안전한 독립 저장 슬롯으로 바꿉니다. Match ID를 넣지 않습니다.</summary>
        public static CheckpointSlot SlotForOwner(string owner)
        {
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("A persistent owner is required.", nameof(owner));
            using var sha = SHA256.Create();
            string id = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(owner))).Replace("-", "").ToLowerInvariant();
            return new CheckpointSlot("growth-" + id.Substring(0, 48));
        }
        public CheckpointSlot Slot => _slot;
        public long IssuedRevision => Interlocked.Read(ref _issued);
        public long SavedRevision => Interlocked.Read(ref _saved);
        public Exception LastError => Volatile.Read(ref _lastError);
        public bool HasPendingChanges => _dirty || IssuedRevision != SavedRevision;

        public GrowthSaveService(ICheckpointStore store, ICheckpointPayloadCodec codec, CheckpointSlot slot,
            Func<CheckpointCaptureContext> context, IEnumerable<IGrowthCheckpointParticipant> owners, double coalesceSeconds = 2)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (slot.IsEmpty) throw new ArgumentException("A growth slot is required.", nameof(slot));
            if (double.IsNaN(coalesceSeconds) || double.IsInfinity(coalesceSeconds) || coalesceSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(coalesceSeconds));
            _slot = slot; _coalesceSeconds = coalesceSeconds;
            _owners = (owners ?? throw new ArgumentNullException(nameof(owners))).OrderBy(x => x?.RestoreOrder ?? 0).ToArray();
            if (_owners.Length == 0 || _owners.Any(x => x == null || x.Kind == CheckpointSectionKind.World) ||
                _owners.Select(x => x.PersistenceKey).Distinct(StringComparer.Ordinal).Count() != _owners.Length)
                throw new ArgumentException("Provide unique growth/currency sources, not world writers.", nameof(owners));
            foreach (var owner in _owners)
                _ = new CheckpointSection(owner.PersistenceKey, owner.Version, owner.Kind, Array.Empty<byte>());
        }

        /// <summary>Scene 조립부가 게임 시작 전에 읽습니다. 저장소 IO는 Unity 객체나 Unity 동기화 문맥을 사용하면 안 됩니다.</summary>
        public void Load()
        {
            RequireThread(); if (_initialized) throw new InvalidOperationException("Growth was already loaded.");
            // 기본 BinaryCheckpointStore는 Task.Run 안에서만 IO합니다. 로딩 구간의 제한된 파일을 기다립니다.
            var data = _store.LoadAsync(_slot).GetAwaiter().GetResult();
            if (data != null) Restore(data);
            _initialized = true;
            foreach (var owner in _owners) owner.GrowthChanged += OnChanged;
        }

        private void Restore(CheckpointData data)
        {
            var saved = data.Sections.ToDictionary(x => x.Key, StringComparer.Ordinal);
            if (saved.Count != _owners.Length) throw new InvalidDataException("Growth source set changed; explicit migration is required.");
            var current = _context(); var steps = new List<ICheckpointRestoreStep>();
            var context = new CheckpointRestoreContext(current, data, CheckpointRestorePolicy.Default);
            _restoring = true;
            try
            {
                foreach (var owner in _owners)
                {
                    if (!saved.TryGetValue(owner.PersistenceKey, out var section) || section.Kind != owner.Kind)
                        throw new InvalidDataException("Missing growth source: " + owner.PersistenceKey);
                    var mapped = new CheckpointSection(owner.Key, section.Version, section.Kind, section.CopyPayload());
                    steps.Add(owner.PrepareRestore(context, mapped, _codec) ?? throw new InvalidDataException("No growth restore step."));
                }
                try { foreach (var step in steps) step.Apply(); }
                catch (Exception apply)
                {
                    var errors = new List<Exception> { apply };
                    for (int i = steps.Count - 1; i >= 0; i--)
                        try { steps[i].Rollback(); } catch (Exception rollback) { errors.Add(rollback); }
                    throw new AggregateException("Growth restore failed; original file was preserved.", errors);
                }
            }
            finally { foreach (var step in steps) step.Dispose(); _restoring = false; }
        }

        private void OnChanged(GrowthSaveUrgency urgency)
        {
            RequireThread(); if (_restoring || !_initialized) return;
            _dirty = true;
            if (urgency == GrowthSaveUrgency.Immediate) CaptureAndQueue();
        }

        /// <summary>Scene의 Update가 unscaled 절대 시각으로 호출합니다. 게임 정지 중에도 대기 XP 저장을 마칩니다.</summary>
        public void Pump(double realSeconds)
        {
            RequireThread();
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0) throw new ArgumentOutOfRangeException(nameof(realSeconds));
            if (!_dirty) { _due = null; return; }
            _due ??= realSeconds + _coalesceSeconds;
            if (realSeconds >= _due) CaptureAndQueue();
        }

        private void CaptureAndQueue()
        {
            var context = _context();
            var sections = _owners.Select(owner =>
            {
                var section = owner.Capture(context, _codec);
                if (section.Key != owner.Key || section.Kind != owner.Kind || section.Version != owner.Version)
                    throw new InvalidDataException("Growth capture metadata changed: " + owner.PersistenceKey);
                return new CheckpointSection(owner.PersistenceKey, section.Version, section.Kind, section.CopyPayload());
            }).ToArray();
            var c = context.Config;
            var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, c.MatchId, c.StageId, c.DifficultyProfileId,
                c.Mode, c.RandomSeed, c.ContentVersion, context.GameSeconds, c.Players.Count, sections);
            long revision = Interlocked.Increment(ref _issued);
            _pending = WriteInOrder(_pending, data, revision);
            _dirty = false; _due = null;
        }
        private async Task WriteInOrder(Task previous, CheckpointData data, long revision)
        {
            try { await previous.ConfigureAwait(false); } catch { /* 다음 유효 원본의 명시 저장은 다시 시도할 수 있습니다. */ }
            try
            {
                await _store.SaveAsync(_slot, data).ConfigureAwait(false);
                Interlocked.Exchange(ref _saved, revision); Volatile.Write(ref _lastError, null);
            }
            catch (Exception error) { Volatile.Write(ref _lastError, error); throw; }
        }

        /// <summary>월드 복귀/정상 종료 전에 호출합니다. 실제 디스크 교체의 완료 또는 실패를 반환하며 강제 종료 보장은 아닙니다.</summary>
        public void Flush()
        {
            RequireThread(); if (!_initialized) return;
            if (_dirty) CaptureAndQueue();
            _pending.GetAwaiter().GetResult();
        }
        private void RequireThread()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GrowthSaveService));
            if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Capture growth on its owning Unity thread.");
        }
        public void Dispose()
        {
            if (_disposed) return;
            try { Flush(); }
            finally { foreach (var owner in _owners) owner.GrowthChanged -= OnChanged; _disposed = true; }
        }
    }
}
