// 실행기의 사전 검증·보존 정책·적용/백업 복구·중복 작업·Scope 종료를 검증합니다.
// Scene Feature의 실제 값 복원 검사는 각 adapter/통합 검사와 구분합니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using UnityEngine.TestTools;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class CheckpointServiceTests
    {
        private sealed class Source : ITimestampSource
        { public double Now; public bool TryReadSeconds(out double seconds) { seconds = Now; return true; } }
        private sealed class Lease : IDisposable
        { private Action _release; public Lease(Action release) { _release = release; } public void Dispose() { var action = _release; _release = null; action?.Invoke(); } }
        internal sealed class Runtime : ICheckpointRuntime, IDisposable
        {
            private readonly Source _source = new(); private readonly MatchClock _clock;
            private readonly CancellationTokenSource _lifetime = new(); private int _pauses;
            public bool Current = true, Authority = true, Faulted; public int Begins, Finishes, Faults;
            public MatchConfig Config { get; } = new MatchConfig(new MatchId("current"), MatchMode.Story, new StageId("stage"),
                new DefinitionId("normal"), "content", 42, new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) });
            public IMatchTime Time => _clock;
            public bool IsCurrent => Current && !Faulted;
            public bool IsAuthority => Authority;
            public bool CanCapture => IsCurrent && Authority;
            public bool CanRestore => IsCurrent && Authority;
            public CancellationToken Lifetime => _lifetime.Token;
            public Runtime() { _clock = new MatchClock(Config.MatchId, _source); }
            public void Advance(double seconds) { _source.Now += seconds; _clock.Pump(); }
            private void RefreshPause() => _clock.SetPaused(_pauses > 0 || Faulted);
            public IDisposable AcquirePause(string owner) { _pauses++; RefreshPause(); return new Lease(() => { _pauses--; RefreshPause(); }); }
            public void BeginRestore(double seconds) { Begins++; _clock.Restore(seconds); RefreshPause(); }
            public void FinishRestore() => Finishes++;
            public void Fault(Exception error) { Faults++; Faulted = true; _lifetime.Cancel(); RefreshPause(); }
            public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); _clock.Dispose(); }
        }
        internal sealed class Store : ICheckpointStore
        {
            public CheckpointData Last; public int Saves; public bool Block;
            public readonly TaskCompletionSource<bool> Completion = new();
            public Task SaveAsync(CheckpointSlot slot, CheckpointData data, CancellationToken cancellationToken = default)
            { Saves++; Last = data; return Block ? Completion.Task : Task.CompletedTask; }
            public Task<CheckpointData> LoadAsync(CheckpointSlot slot, CancellationToken cancellationToken = default) => Task.FromResult(Last);
        }
        internal sealed class Participant : ICheckpointParticipant
        {
            public string Key { get; } public int Version => 1; public CheckpointSectionKind Kind { get; } public int RestoreOrder { get; }
            public int Value, Prepared, Applied, RolledBack, Disposed;
            public bool BadPrepare, BadApply, BadRollback, BadDispose; public Action OnCapture, OnApply;
            public Participant(string key, int value, CheckpointSectionKind kind = CheckpointSectionKind.World, int order = 0)
            { Key = key; Value = value; Kind = kind; RestoreOrder = order; }
            public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
            { OnCapture?.Invoke(); return codec.Write(Key, Version, Kind, writer => writer.Write(Value)); }
            public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
            {
                Prepared++; if (BadPrepare) throw new InvalidDataException("prepare rejected");
                int target = codec.Read(section, Version, reader => reader.ReadInt32()), backup = Value;
                return new CheckpointRestoreStep(() => { Applied++; Value = target; OnApply?.Invoke(); if (BadApply) throw new InvalidOperationException("apply failed"); },
                    () => { RolledBack++; Value = backup; if (BadRollback) throw new InvalidOperationException("rollback failed"); },
                    () => { Disposed++; if (BadDispose) throw new InvalidOperationException("dispose failed"); });
            }
        }
        internal sealed class Harness : IDisposable
        {
            public readonly Runtime Runtime = new(); public readonly Store Store = new();
            public readonly BinaryCheckpointPayloadCodec Codec = new(); public readonly CheckpointService Service;
            public Harness() { Service = new CheckpointService(Runtime, Store, Codec); }
            public CheckpointData Target(params (string key, CheckpointSectionKind kind, int value)[] values) =>
                new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, new MatchId("old-run"), Runtime.Config.StageId,
                    Runtime.Config.DifficultyProfileId, Runtime.Config.Mode, Runtime.Config.RandomSeed, Runtime.Config.ContentVersion, 5, 1,
                    values.Select(value => Codec.Write(value.key, 1, value.kind, writer => writer.Write(value.value))));
            public Participant Add(string key, int value, CheckpointSectionKind kind = CheckpointSectionKind.World, int order = 0)
            { var participant = new Participant(key, value, kind, order); Service.Register(participant); return participant; }
            public void Dispose() { Store.Completion.TrySetResult(true); Service.Dispose(); Runtime.Dispose(); }
        }
        [Test] public void CaptureUsesFrozenCurrentStateAndDoesNotLeaveTheGamePausedDuringIO()
        {
            using var h = new Harness(); var world = h.Add("world", 17); h.Runtime.Advance(20);
            world.OnCapture = () => Assert.That(h.Runtime.Time.Snapshot.IsPaused, Is.True);
            var result = h.Service.SaveAsync(new CheckpointSlot("test")).GetAwaiter().GetResult();
            Assert.That(result.Status, Is.EqualTo(CheckpointOperationStatus.Saved)); Assert.That(h.Store.Last.GameSeconds, Is.EqualTo(20));
            Assert.That(h.Codec.Read(h.Store.Last.Sections[0], 1, reader => reader.ReadInt32()), Is.EqualTo(17));
            Assert.That(h.Runtime.Time.Snapshot.IsPaused, Is.False); Assert.That(result.IsCurrentScope, Is.True);
        }
        [Test] public void DefaultRestoreKeepsGrowthAndCurrencyWhileRollbackOnlyChangesCurrency()
        {
            using var h = new Harness(); var world = h.Add("world", 1); var cash = h.Add("cash", 10, CheckpointSectionKind.Currency);
            var growth = h.Add("growth", 20, CheckpointSectionKind.Progression);
            var data = h.Target(("world", CheckpointSectionKind.World, 5), ("cash", CheckpointSectionKind.Currency, 50), ("growth", CheckpointSectionKind.Progression, 99));
            Assert.That(h.Service.Restore(data).Status, Is.EqualTo(CheckpointOperationStatus.Restored));
            Assert.That(world.Value, Is.EqualTo(5)); Assert.That(cash.Value, Is.EqualTo(10)); Assert.That(growth.Value, Is.EqualTo(20));
            h.Service.Restore(data, new CheckpointRestorePolicy(CurrencyRestorePolicy.RestoreCheckpoint));
            Assert.That(cash.Value, Is.EqualTo(50)); Assert.That(growth.Value, Is.EqualTo(20)); Assert.That(growth.Prepared, Is.Zero);
            Assert.That(h.Runtime.Time.ElapsedSeconds, Is.EqualTo(5));
        }
        [Test] public void PreflightFailureDoesNotChangeTimeOrAnyWorldValue()
        {
            using var h = new Harness(); var first = h.Add("a", 1); var second = h.Add("b", 2, order: 1); second.BadPrepare = true;
            Guid epoch = h.Runtime.Time.Snapshot.Epoch;
            var result = h.Service.Restore(h.Target(("a", CheckpointSectionKind.World, 10), ("b", CheckpointSectionKind.World, 20)));
            Assert.That(result.Status, Is.EqualTo(CheckpointOperationStatus.Rejected)); Assert.That(first.Value, Is.EqualTo(1));
            Assert.That(first.Applied, Is.Zero); Assert.That(first.Disposed, Is.EqualTo(1)); Assert.That(h.Runtime.Begins, Is.Zero);
            Assert.That(h.Runtime.Time.Snapshot.Epoch, Is.EqualTo(epoch)); Assert.That(h.Runtime.Time.Snapshot.IsPaused, Is.False);
        }
        [Test] public void ApplyFailureRestoresAllBackupsIncludingNotYetAppliedTimerOwners()
        {
            using var h = new Harness(); var first = h.Add("a", 1); var second = h.Add("b", 2, order: 1); var third = h.Add("c", 3, order: 2);
            second.BadApply = true; h.Runtime.Advance(20);
            var result = h.Service.Restore(h.Target(("a", CheckpointSectionKind.World, 10), ("b", CheckpointSectionKind.World, 20), ("c", CheckpointSectionKind.World, 30)));
            Assert.That(result.Status, Is.EqualTo(CheckpointOperationStatus.Failed)); Assert.That(h.Runtime.Begins, Is.EqualTo(2));
            Assert.That(new[] { first.Value, second.Value, third.Value }, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(third.Applied, Is.Zero); Assert.That(third.RolledBack, Is.EqualTo(1)); Assert.That(h.Runtime.Time.ElapsedSeconds, Is.EqualTo(20));
        }
        [Test] public void RollbackFailureClosesTheMatchInsteadOfResumingPartialWorld()
        {
            using var h = new Harness(); var source = h.Add("world", 1); source.BadApply = true; source.BadRollback = true;
            var result = h.Service.Restore(h.Target(("world", CheckpointSectionKind.World, 2)));
            Assert.That(result.Status, Is.EqualTo(CheckpointOperationStatus.Faulted)); Assert.That(h.Runtime.Faults, Is.EqualTo(1));
            Assert.That(h.Runtime.Time.Snapshot.IsPaused, Is.True); Assert.That(result.IsCurrentScope, Is.False);
        }
        [Test] public void CleanupFailureAlsoClosesTheMatchAndIsObservable()
        {
            using var h = new Harness(); var source = h.Add("world", 1); source.BadDispose = true;
            var result = h.Service.Restore(h.Target(("world", CheckpointSectionKind.World, 2)));
            Assert.That(result.Status, Is.EqualTo(CheckpointOperationStatus.Faulted)); Assert.That(result.Error, Is.Not.Null);
            Assert.That(h.Runtime.Faults, Is.EqualTo(1));
        }
        [Test] public void UnknownWorldSectionsAreRejectedWhilePreservedFutureProgressionCanBeIgnored()
        {
            using var h = new Harness(); var source = h.Add("world", 1);
            var bad = h.Target(("world", CheckpointSectionKind.World, 2), ("future", CheckpointSectionKind.World, 3));
            Assert.That(h.Service.Restore(bad).Status, Is.EqualTo(CheckpointOperationStatus.Rejected)); Assert.That(source.Value, Is.EqualTo(1));
            var compatible = h.Target(("world", CheckpointSectionKind.World, 2), ("future-growth", CheckpointSectionKind.Progression, 3));
            Assert.That(h.Service.Restore(compatible).Status, Is.EqualTo(CheckpointOperationStatus.Restored));
        }
        [Test] public void MissingAuthorityOrActualWorldCannotProduceASuccessfulSave()
        {
            using var h = new Harness(); h.Add("world", 1); h.Runtime.Authority = false;
            Assert.That(h.Service.SaveAsync(new CheckpointSlot("test")).Result.Status, Is.EqualTo(CheckpointOperationStatus.Rejected));
            Assert.That(h.Store.Saves, Is.Zero);
            using var missing = new Harness(); missing.Add("cash", 10, CheckpointSectionKind.Currency);
            Assert.That(missing.Service.SaveAsync(new CheckpointSlot("test")).Result.Status, Is.EqualTo(CheckpointOperationStatus.Rejected));
        }
        [Test] public void MissingSlotAndInvalidSlotAreDistinctFromASuccessfulRestore()
        {
            using var h = new Harness(); h.Add("world", 1);
            Assert.That(h.Service.RestoreAsync(new CheckpointSlot("missing")).Result.Status, Is.EqualTo(CheckpointOperationStatus.NotFound));
            Assert.That(h.Service.SaveAsync(default).Result.Status, Is.EqualTo(CheckpointOperationStatus.Rejected));
            Assert.That(h.Store.Saves, Is.Zero);
        }
        [UnityTest] public IEnumerator PendingIOKeepsCapturedDataImmutableRejectsConcurrentWorkAndReportsLateScope()
        {
            using var h = new Harness(); var source = h.Add("world", 5); h.Store.Block = true;
            var saved = h.Service.SaveAsync(new CheckpointSlot("test")); Assert.That(saved.IsCompleted, Is.False);
            Assert.That(h.Runtime.Time.Snapshot.IsPaused, Is.False); source.Value = 999; h.Runtime.Advance(10);
            Assert.That(h.Service.SaveAsync(new CheckpointSlot("other")).Result.Status, Is.EqualTo(CheckpointOperationStatus.Busy));
            Assert.That(h.Codec.Read(h.Store.Last.Sections[0], 1, reader => reader.ReadInt32()), Is.EqualTo(5));
            h.Service.Dispose(); h.Store.Completion.SetResult(true);
            var timeout = DateTime.UtcNow.AddSeconds(5); while (!saved.IsCompleted && DateTime.UtcNow < timeout) yield return null;
            Assert.That(saved.IsCompleted, Is.True); Assert.That(saved.Result.Status, Is.EqualTo(CheckpointOperationStatus.Saved));
            Assert.That(saved.Result.IsCurrentScope, Is.False);
        }
    }
}
