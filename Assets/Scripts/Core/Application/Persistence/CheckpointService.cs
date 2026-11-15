// 명시적 캡처·저장·로드·복원을 현재 권위 Match에서 조율합니다.
// Feature 원본·저장 시점·Scene 전환 규칙은 소유하지 않고 등록 port와 실행 수명을 사용합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class CheckpointService : IDisposable
    {
        private readonly ICheckpointRuntime _runtime;
        private readonly ICheckpointStore _store;
        private readonly ICheckpointPayloadCodec _codec;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly int _thread = Environment.CurrentManagedThreadId;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Dictionary<string, Binding> _participants = new(StringComparer.Ordinal);
        private readonly string[] _requiredKeys;
        private long _registrationRevision;
        private int _busy;
        private bool _mutating, _disposed;
        public MatchId MatchId => _runtime.Config.MatchId;
        public Guid Epoch => !_disposed && _runtime.IsCurrent && _runtime.Time.IsAvailable ? _runtime.Time.Snapshot.Epoch : Guid.Empty;
        public bool IsBusy => Volatile.Read(ref _busy) != 0;
        public CheckpointSlot LastSavedSlot { get; private set; }
        public CheckpointData LastSavedData { get; private set; }
        public int ParticipantCount => _participants.Count;
        /// <summary>조립부가 요구했지만 아직 등록되지 않은 writer 키입니다. UI/진단은 이 목록으로 연결 대기를 설명합니다.</summary>
        public IReadOnlyList<string> MissingParticipantKeys => Array.AsReadOnly(_requiredKeys.Where(key => !_participants.ContainsKey(key)).ToArray());
        public CheckpointService(ICheckpointRuntime runtime, ICheckpointStore store, ICheckpointPayloadCodec codec,
            Func<DateTimeOffset> utcNow = null, IEnumerable<string> requiredParticipantKeys = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime)); _store = store ?? throw new ArgumentNullException(nameof(store));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec)); _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in requiredParticipantKeys ?? Array.Empty<string>())
            {
                _ = new CheckpointSection(key, 1, CheckpointSectionKind.World, Array.Empty<byte>());
                if (!keys.Add(key) || keys.Count > CheckpointData.MaximumSections)
                    throw new ArgumentException("Required checkpoint writer keys must be unique and bounded.", nameof(requiredParticipantKeys));
            }
            _requiredKeys = keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        }
        /// <summary>Scene/Feature 조립부가 실제 원본을 등록합니다. 교체·해제 시 같은 메인 스레드에서 lease를 Dispose합니다.</summary>
        public IDisposable Register(ICheckpointParticipant participant)
        {
            RequireThread(); if (_disposed) throw new ObjectDisposedException(nameof(CheckpointService));
            if (_mutating) throw new InvalidOperationException("Do not change checkpoint writers during capture or restore.");
            if (_participants.Count >= CheckpointData.MaximumSections) throw new InvalidOperationException("The checkpoint writer limit was reached.");
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            // 같은 검증을 사용해 키/버전/분류를 고정하고 등록 뒤 메타데이터 변경을 거부합니다.
            _ = new CheckpointSection(participant.Key, participant.Version, participant.Kind, Array.Empty<byte>());
            var binding = new Binding(participant);
            if (_participants.ContainsKey(binding.Key)) throw new InvalidOperationException("A checkpoint writer already owns: " + binding.Key);
            _participants.Add(binding.Key, binding); _registrationRevision++; return new Registration(this, binding);
        }
        /// <summary>Host의 명시 호출을 받아 같은 프레임에 캡처한 뒤 게임을 재개하고 불변 자료만 디스크에 저장합니다.</summary>
        /// <remarks>완료 뒤 IsCurrentScope를 확인합니다. 이미 파일 교체가 끝난 저장은 Scene 종료 취소로 실패라고 바꾸지 않습니다.</remarks>
        public async Task<CheckpointOperationResult> SaveAsync(CheckpointSlot slot, CancellationToken cancellation = default)
        {
            Guid id = Guid.NewGuid(); if (!TryBegin(id, out var rejected)) return rejected;
            Guid epoch = Guid.Empty; CheckpointData data = null;
            try
            {
                if (slot.IsEmpty) return Result(id, CheckpointOperationStatus.Rejected, "InvalidSlot");
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token, _runtime.Lifetime);
                linked.Token.ThrowIfCancellationRequested(); RequireReady(capture: true);
                using (_runtime.AcquirePause("CheckpointCapture"))
                {
                    _mutating = true;
                    try
                    {
                        var context = CaptureContext(); epoch = context.Epoch; long revision = _registrationRevision;
                        var sections = new List<CheckpointSection>();
                        foreach (var binding in Ordered())
                        {
                            linked.Token.ThrowIfCancellationRequested(); binding.RequireMetadata();
                            var section = binding.Participant.Capture(context, _codec);
                            if (section == null || section.Key != binding.Key || section.Kind != binding.Kind || section.Version != binding.Version)
                                throw new InvalidDataException("Feature capture returned different metadata: " + binding.Key);
                            sections.Add(section);
                        }
                        if (revision != _registrationRevision || !Current(epoch)) throw new OperationCanceledException("Capture scope changed.", linked.Token);
                        var config = context.Config;
                        data = new CheckpointData(Guid.NewGuid(), _utcNow(), config.MatchId, config.StageId, config.DifficultyProfileId,
                            config.Mode, config.RandomSeed, config.ContentVersion, context.GameSeconds, config.Players.Count, sections);
                    }
                    finally { _mutating = false; }
                }
                await _store.SaveAsync(slot, data, linked.Token);
                LastSavedSlot = slot; LastSavedData = data;
                return Result(id, CheckpointOperationStatus.Saved, "Saved", data, epoch: epoch);
            }
            catch (Exception error) { return Failure(id, error, data, epoch); }
            finally { Volatile.Write(ref _busy, 0); }
        }
        /// <summary>현재 대상 Match의 Host가 지정 슬롯을 읽고 명시 복원합니다. 새 Scene/Match 구성은 GameFlow가 먼저 수행합니다.</summary>
        public async Task<CheckpointOperationResult> RestoreAsync(CheckpointSlot slot, CheckpointRestorePolicy policy = null,
            CancellationToken cancellation = default)
        {
            Guid id = Guid.NewGuid(); if (!TryBegin(id, out var rejected)) return rejected;
            try
            {
                if (slot.IsEmpty) return Result(id, CheckpointOperationStatus.Rejected, "InvalidSlot");
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token, _runtime.Lifetime);
                RequireReady(capture: false); var data = await _store.LoadAsync(slot, linked.Token);
                RequireThread(); linked.Token.ThrowIfCancellationRequested();
                return data == null ? Result(id, CheckpointOperationStatus.NotFound, "SlotNotFound") : RestoreCore(id, data, policy ?? CheckpointRestorePolicy.Default, linked.Token);
            }
            catch (Exception error) { return Failure(id, error); }
            finally { Volatile.Write(ref _busy, 0); }
        }
        /// <summary>재실행/출격 조율부가 먼저 읽어 새 Match를 구성한 자료를 준비 단계에서 적용합니다.</summary>
        public CheckpointOperationResult Restore(CheckpointData data, CheckpointRestorePolicy policy = null,
            CancellationToken cancellation = default)
        {
            Guid id = Guid.NewGuid(); if (!TryBegin(id, out var rejected)) return rejected;
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token, _runtime.Lifetime);
                return RestoreCore(id, data, policy ?? CheckpointRestorePolicy.Default, linked.Token);
            }
            catch (Exception error) { return Failure(id, error, data); }
            finally { Volatile.Write(ref _busy, 0); }
        }
        private CheckpointOperationResult RestoreCore(Guid id, CheckpointData data, CheckpointRestorePolicy policy, CancellationToken cancellation)
        {
            var steps = new List<ICheckpointRestoreStep>(); IDisposable pause = null; bool started = false;
            CheckpointCaptureContext before = null; CheckpointOperationResult result;
            try
            {
                cancellation.ThrowIfCancellationRequested(); RequireReady(capture: false); ValidateTarget(data);
                pause = _runtime.AcquirePause("CheckpointRestore"); _mutating = true; before = CaptureContext();
                var context = new CheckpointRestoreContext(before, data, policy);
                var sections = data.Sections.ToDictionary(section => section.Key, StringComparer.Ordinal);
                foreach (var section in data.Sections)
                    if (policy.ShouldRestore(section.Kind) && (!_participants.TryGetValue(section.Key, out var owner) || owner.Kind != section.Kind))
                        throw new InvalidDataException("No matching writer for saved Feature: " + section.Key);
                foreach (var binding in Ordered())
                {
                    if (!policy.ShouldRestore(binding.Kind)) continue;
                    cancellation.ThrowIfCancellationRequested(); binding.RequireMetadata();
                    if (!sections.TryGetValue(binding.Key, out var section) || section.Kind != binding.Kind)
                        throw new InvalidDataException("Required saved Feature is missing: " + binding.Key);
                    steps.Add(binding.Participant.PrepareRestore(context, section, _codec) ??
                        throw new InvalidOperationException("Feature returned no prepared restore step: " + binding.Key));
                }
                if (steps.Count == 0) throw new InvalidDataException("No restorable world state was supplied.");
                cancellation.ThrowIfCancellationRequested(); RequireReady(capture: false);
                started = true; _runtime.BeginRestore(data.GameSeconds);
                // Field 시각은 냉각/수명/예약을 복원하는 모든 Feature보다 먼저 적용합니다.
                foreach (var step in steps.OfType<ICheckpointClockRestoreStep>()) step.Apply();
                foreach (var step in steps)
                {
                    if (step is ICheckpointClockRestoreStep) continue;
                    cancellation.ThrowIfCancellationRequested(); RequireReady(capture: false); step.Apply();
                }
                // 단순 적용 순서만으로는 뒤에 재생성될 다른 Feature의 과거 instance를 참조할 수 있습니다.
                foreach (var step in steps)
                {
                    cancellation.ThrowIfCancellationRequested(); RequireReady(capture: false);
                    if (step is ICheckpointReferenceRestoreStep references) references.ResolveReferences();
                }
                cancellation.ThrowIfCancellationRequested(); RequireReady(capture: false);
                _runtime.FinishRestore(); result = Result(id, CheckpointOperationStatus.Restored, "Restored", data);
            }
            catch (Exception error)
            {
                result = Failure(id, error, data);
                if (started && _runtime.IsCurrent && !_disposed)
                {
                    var rollbackErrors = new List<Exception>();
                    Try(() => _runtime.BeginRestore(before.GameSeconds), rollbackErrors);
                    // 예약은 전역 시간 복원으로 취소됐으므로 아직 Apply하지 않은 Feature의 백업도 다시 연결합니다.
                    foreach (var step in steps.OfType<ICheckpointClockRestoreStep>()) Try(step.Rollback, rollbackErrors);
                    for (int i = steps.Count - 1; i >= 0; i--)
                        if (steps[i] is not ICheckpointClockRestoreStep) Try(steps[i].Rollback, rollbackErrors);
                    // 역순 백업 복구가 끝나야 최종 대상 instance가 확정됩니다. 복구 중에는 취소를 다시 적용하지 않습니다.
                    foreach (var step in steps)
                        if (step is ICheckpointReferenceRestoreStep references) Try(references.ResolveRollbackReferences, rollbackErrors);
                    if (rollbackErrors.Count == 0) Try(_runtime.FinishRestore, rollbackErrors);
                    if (rollbackErrors.Count > 0)
                    {
                        rollbackErrors.Insert(0, error); var failure = new AggregateException("Checkpoint rollback failed.", rollbackErrors);
                        Try(() => _runtime.Fault(failure), rollbackErrors);
                        result = Result(id, CheckpointOperationStatus.Faulted, "RollbackFailed", data, failure);
                    }
                    else result = Result(id, error is OperationCanceledException ? CheckpointOperationStatus.Cancelled : CheckpointOperationStatus.Failed,
                        "RestoreRolledBack", data, error);
                }
            }
            var cleanupErrors = new List<Exception>();
            for (int i = steps.Count - 1; i >= 0; i--) Try(steps[i].Dispose, cleanupErrors);
            _mutating = false;
            if (cleanupErrors.Count > 0 && _runtime.IsCurrent)
            {
                var failure = new AggregateException("Checkpoint restore cleanup failed.", cleanupErrors);
                Try(() => _runtime.Fault(failure), cleanupErrors);
                result = Result(id, CheckpointOperationStatus.Faulted, "CleanupFailed", data, failure);
            }
            if (pause != null)
                try { pause.Dispose(); }
                catch (Exception error)
                {
                    try { if (_runtime.IsCurrent) _runtime.Fault(error); }
                    catch (Exception faultError) { error = new AggregateException(error, faultError); }
                    return Result(id, CheckpointOperationStatus.Faulted, "PauseReleaseFailed", data, error);
                }
            return result;
        }
        private void ValidateTarget(CheckpointData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data)); var config = _runtime.Config;
            if (data.StageId != config.StageId || data.DifficultyProfileId != config.DifficultyProfileId || data.Mode != config.Mode ||
                data.RandomSeed != config.RandomSeed || data.ContentVersion != config.ContentVersion || data.ParticipantCount != config.Players.Count)
                throw new InvalidDataException("Compose the target Match from the checkpoint metadata before restoring.");
        }
        private CheckpointCaptureContext CaptureContext() => new CheckpointCaptureContext(_runtime.Config, _runtime.Time.ElapsedSeconds, _runtime.Time.Snapshot.Epoch);
        private Binding[] Ordered() => _participants.Values.OrderBy(binding => binding.Order).ThenBy(binding => binding.Key, StringComparer.Ordinal).ToArray();
        private void RequireReady(bool capture)
        {
            RequireThread();
            if (_disposed || !_runtime.IsCurrent || !_runtime.IsAuthority || _runtime.Lifetime.IsCancellationRequested ||
                !(capture ? _runtime.CanCapture : _runtime.CanRestore)) throw new RequestRejectedException("CurrentAuthorityScopeUnavailable");
            if (_requiredKeys.Any(key => !_participants.ContainsKey(key)))
                throw new RequestRejectedException("RequiredFeatureWritersUnavailable");
            if (!_participants.Values.Any(binding => binding.Kind == CheckpointSectionKind.World))
                throw new RequestRejectedException("ActualWorldWritersUnavailable");
        }
        private bool TryBegin(Guid id, out CheckpointOperationResult rejected)
        {
            RequireThread(); rejected = null;
            if (_disposed) { rejected = Result(id, CheckpointOperationStatus.Rejected, "ScopeDisposed"); return false; }
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) { rejected = Result(id, CheckpointOperationStatus.Busy, "OperationInProgress"); return false; }
            return true;
        }
        private bool Current(Guid epoch = default) => Environment.CurrentManagedThreadId == _thread && !_disposed && _runtime.IsCurrent && !_runtime.Lifetime.IsCancellationRequested &&
            _runtime.Time.IsAvailable && (epoch == Guid.Empty || _runtime.Time.Snapshot.Epoch == epoch);
        private CheckpointOperationResult Result(Guid id, CheckpointOperationStatus status, string reason, CheckpointData data = null,
            Exception error = null, Guid epoch = default) => new CheckpointOperationResult(id, status, reason, Current(epoch), data, error);
        private CheckpointOperationResult Failure(Guid id, Exception error, CheckpointData data = null, Guid epoch = default) =>
            Result(id, error is OperationCanceledException ? CheckpointOperationStatus.Cancelled :
                error is InvalidDataException || error is NotSupportedException || error is RequestRejectedException ? CheckpointOperationStatus.Rejected : CheckpointOperationStatus.Failed,
                error.Message, data, error, epoch);
        private static void Try(Action action, List<Exception> errors) { try { action(); } catch (Exception error) { errors.Add(error); } }
        private void RequireThread() { if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("Use checkpoint gameplay APIs on the owning thread."); }
        private void Unregister(Binding binding)
        {
            if (_disposed) return; RequireThread();
            if (_mutating) throw new InvalidOperationException("Do not unregister Feature writers during capture or restore.");
            if (_participants.TryGetValue(binding.Key, out var current) && ReferenceEquals(current, binding))
            { _participants.Remove(binding.Key); _registrationRevision++; }
        }
        public void Dispose()
        {
            if (_disposed) return; RequireThread(); _disposed = true;
            try { _lifetime.Cancel(); } finally { _participants.Clear(); _lifetime.Dispose(); }
        }
        private sealed class Binding
        {
            public readonly ICheckpointParticipant Participant; public readonly string Key;
            public readonly int Version, Order; public readonly CheckpointSectionKind Kind;
            public Binding(ICheckpointParticipant participant)
            { Participant = participant; Key = participant.Key; Version = participant.Version; Order = participant.RestoreOrder; Kind = participant.Kind; }
            public void RequireMetadata()
            { if (Participant.Key != Key || Participant.Version != Version || Participant.Kind != Kind || Participant.RestoreOrder != Order) throw new InvalidOperationException("Registered Feature metadata changed: " + Key); }
        }
        private sealed class RequestRejectedException : InvalidOperationException
        { public RequestRejectedException(string reason) : base(reason) { } }
        private sealed class Registration : IDisposable
        {
            private CheckpointService _service; private readonly Binding _binding;
            public Registration(CheckpointService service, Binding binding) { _service = service; _binding = binding; }
            public void Dispose() { var service = _service; if (service == null) return; service.Unregister(_binding); _service = null; }
        }
    }
}
