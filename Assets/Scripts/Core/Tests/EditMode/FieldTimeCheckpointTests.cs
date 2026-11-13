// Feature 적용 실패 때 실제 CheckpointService가 Field 시각을 먼저 원복하는지 확인합니다.
// 냉각 deadline이 월드 복원 전/후의 다른 시간 기준에 묶이는 회귀를 재현합니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class FieldTimeCheckpointTests
    {
        private sealed class Clock : IMatchTime
        {
            public double Now = 100;
            public Guid Epoch = Guid.NewGuid();
            public bool IsAvailable => true;
            public double ElapsedSeconds => Now;
            public MatchTimeSnapshot Snapshot => new MatchTimeSnapshot(new MatchId("checkpoint-test"), Epoch, 0, Now, Now, 1, false, true);
        }
        private sealed class Runtime : ICheckpointRuntime
        {
            public Clock Clock = new Clock();
            public MatchConfig Config { get; } = new MatchConfig(new MatchId("checkpoint-test"), MatchMode.Cooperative,
                new StageId("stage-1"), new DefinitionId("test"), "v1", 10,
                new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) });
            public IMatchTime Time => Clock;
            public bool IsCurrent => true;
            public bool IsAuthority => true;
            public bool CanCapture => true;
            public bool CanRestore => true;
            public CancellationToken Lifetime => default;
            public bool Faulted;
            public IDisposable AcquirePause(string owner) => new CheckpointRestoreStep(() => { }, () => { });
            public void BeginRestore(double seconds) { Clock.Now = seconds; Clock.Epoch = Guid.NewGuid(); }
            public void FinishRestore() { }
            public void Fault(Exception error) => Faulted = true;
        }
        private sealed class Store : ICheckpointStore
        {
            public Task SaveAsync(CheckpointSlot slot, CheckpointData data, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<CheckpointData> LoadAsync(CheckpointSlot slot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }
        private sealed class Feature : ICheckpointParticipant
        {
            private readonly IMatchClock _clock;
            public double Deadline, AppliedAt, RolledBackAt;
            public string Key => "world/failing-feature";
            public int Version => 1;
            public CheckpointSectionKind Kind => CheckpointSectionKind.World;
            public int RestoreOrder => 200;
            public Feature(IMatchClock clock) { _clock = clock; Deadline = clock.ElapsedSeconds + 5; }
            public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
                => codec.Write(Key, Version, Kind, w => w.Write(Math.Max(0, Deadline - _clock.ElapsedSeconds)));
            public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
            {
                double remaining = codec.Read(section, 1, r => r.ReadDouble());
                double backupRemaining = Deadline - _clock.ElapsedSeconds;
                return new CheckpointRestoreStep(() =>
                {
                    AppliedAt = _clock.ElapsedSeconds; Deadline = AppliedAt + remaining;
                    throw new InvalidOperationException("Deliberate Feature restore failure.");
                }, () => { RolledBackAt = _clock.ElapsedSeconds; Deadline = RolledBackAt + backupRemaining; });
            }
        }
        [Test]
        public void FailedWorldRestore_RestoresFieldClockBeforeFeatureDeadlineBackup()
        {
            var runtime = new Runtime(); var field = new FieldSimulationClock(runtime.Clock); field.Restore(20, false);
            var feature = new Feature(field); var codec = new BinaryCheckpointPayloadCodec();
            using var service = new CheckpointService(runtime, new Store(), codec);
            using var clockRegistration = service.Register(new FieldTimeCheckpointParticipant(field));
            using var featureRegistration = service.Register(feature);
            var config = runtime.Config;
            var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, config.MatchId, config.StageId,
                config.DifficultyProfileId, config.Mode, config.RandomSeed, config.ContentVersion, 50, 1, new[]
                {
                    codec.Write("world/field-time", 1, CheckpointSectionKind.World, w => { w.Write(10d); w.Write(false); }),
                    codec.Write(feature.Key, 1, CheckpointSectionKind.World, w => w.Write(2d))
                });
            var result = service.Restore(data);
            Assert.That(result.Status, Is.EqualTo(CheckpointOperationStatus.Failed));
            Assert.That(runtime.Faulted, Is.False);
            Assert.That(feature.AppliedAt, Is.EqualTo(10)); Assert.That(feature.RolledBackAt, Is.EqualTo(20));
            Assert.That(runtime.Clock.ElapsedSeconds, Is.EqualTo(100)); Assert.That(field.ElapsedSeconds, Is.EqualTo(20));
            Assert.That(feature.Deadline - field.ElapsedSeconds, Is.EqualTo(5));
        }
    }
}
