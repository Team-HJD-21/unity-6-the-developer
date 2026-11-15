// 개인 성장의 독립 슬롯·쓰기 순서·XP 합치기·손상 보존을 실제 저장 경계에서 검사합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
namespace TeamHJD.Game.Tests
{
    public sealed class GrowthSaveServiceTests
    {
        private static CheckpointCaptureContext Context() => new(new MatchConfig(new MatchId("growth-test"), MatchMode.Story,
            new StageId("stage-1"), new DefinitionId("normal"), "v1", 1,
            new[] { new PlayerLoadoutSnapshot(new PlayerId("alice"), null) }), 10, Guid.NewGuid());
        private static readonly CheckpointSlot Slot = GrowthSaveService.SlotForOwner("player/alice");
        private static GrowthSaveService Service(Store store, params Owner[] owners)
            => new(store, new BinaryCheckpointPayloadCodec(), Slot, Context, owners);

        [Test]
        public void FrequentXpCoalescesAndImportantChangesCaptureAllRelatedSourcesTogether()
        {
            var store = new Store(); var xp = new Owner("experience"); var stats = new Owner("stats");
            using var service = Service(store, xp, stats); service.Load();
            xp.Change(10, GrowthSaveUrgency.Coalesced); service.Pump(10);
            xp.Change(20, GrowthSaveUrgency.Coalesced); service.Pump(11);
            Assert.That(store.Writes, Is.Zero); service.Pump(12); service.Flush();
            Assert.That(store.Writes, Is.EqualTo(1));
            xp.Change(100, GrowthSaveUrgency.Coalesced); stats.Change(1, GrowthSaveUrgency.Immediate); service.Flush();
            Assert.That(store.Writes, Is.EqualTo(2));
            xp.Value = stats.Value = 0;
            using var next = Service(store, xp, stats); next.Load();
            Assert.That(xp.Value, Is.EqualTo(100)); Assert.That(stats.Value, Is.EqualTo(1));
        }

        [Test]
        public void PersonalGrowthSurvivesRosterSlotChangeAndIsNotAnEmptyWorldCheckpoint()
        {
            var store = new Store(); var first = new Owner("experience");
            using (var service = Service(store, first)) { service.Load(); first.Change(55, GrowthSaveUrgency.Immediate); }
            var otherSlot = new Owner("experience", "world/player/1/experience");
            using var next = Service(store, otherSlot); next.Load();
            Assert.That(otherSlot.Value, Is.EqualTo(55));
            Assert.That(store.Data.Sections[0].Kind, Is.EqualTo(CheckpointSectionKind.Progression));
            Assert.That(store.Data.Sections[0].Key, Is.EqualTo("experience"));
            Assert.That(GrowthSaveService.SlotForOwner("player/bob"), Is.Not.EqualTo(Slot));
        }

        [Test]
        public void DiskFailureIsVisibleAndFlushDoesNotClaimItSaved()
        {
            var store = new Store { FailWrite = true }; var owner = new Owner("experience");
            var service = Service(store, owner); service.Load(); owner.Change(9, GrowthSaveUrgency.Immediate);
            Assert.Throws<IOException>(() => service.Flush()); Assert.That(service.LastError, Is.TypeOf<IOException>());
            Assert.That(service.SavedRevision, Is.Zero); store.FailWrite = false;
            owner.Change(10, GrowthSaveUrgency.Immediate); service.Flush();
            Assert.That(service.SavedRevision, Is.EqualTo(2)); Assert.That(service.LastError, Is.Null); service.Dispose();
        }

        [Test]
        public void CorruptSourceDoesNotApplyOrOverwriteOriginalFile()
        {
            var store = new Store(); var owner = new Owner("experience");
            using (var saved = Service(store, owner)) { saved.Load(); owner.Change(8, GrowthSaveUrgency.Immediate); }
            int writes = store.Writes; var current = new Owner("different-source") { Value = 99 };
            using var load = Service(store, current);
            Assert.Throws<InvalidDataException>(() => load.Load());
            Assert.That(current.Value, Is.EqualTo(99)); Assert.That(store.Writes, Is.EqualTo(writes));
        }

        [Test]
        public void ApplyFailureRollsBackEarlierSources()
        {
            var store = new Store(); var one = new Owner("one"); var two = new Owner("two");
            using (var saved = Service(store, one, two)) { saved.Load(); one.Change(1, GrowthSaveUrgency.Immediate); }
            var beforeOne = new Owner("one") { Value = 90 };
            var beforeTwo = new Owner("two") { Value = 80, FailApply = true };
            using var load = Service(store, beforeOne, beforeTwo);
            Assert.Throws<AggregateException>(() => load.Load());
            Assert.That(beforeOne.Value, Is.EqualTo(90)); Assert.That(beforeTwo.Value, Is.EqualTo(80));
        }

        [Test]
        public void DelayedEarlierWriteCannotOverwriteTheLaterRevision()
        {
            var store = new Store { Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };
            var owner = new Owner("experience"); using var service = Service(store, owner); service.Load();
            owner.Change(10, GrowthSaveUrgency.Immediate); owner.Change(20, GrowthSaveUrgency.Immediate);
            Assert.That(store.Started, Is.EqualTo(1)); Assert.That(service.SavedRevision, Is.Zero);
            store.Gate.SetResult(true); service.Flush();
            Assert.That(store.Writes, Is.EqualTo(2)); Assert.That(service.SavedRevision, Is.EqualTo(2));
            var restored = new Owner("experience"); using var next = Service(store, restored); next.Load();
            Assert.That(restored.Value, Is.EqualTo(20));
        }

        private sealed class Store : ICheckpointStore
        {
            public CheckpointData Data; public int Writes, Started; public bool FailWrite;
            public TaskCompletionSource<bool> Gate;
            public Task<CheckpointData> LoadAsync(CheckpointSlot slot, CancellationToken cancellationToken = default) => Task.FromResult(Data);
            public async Task SaveAsync(CheckpointSlot slot, CheckpointData data, CancellationToken cancellationToken = default)
            {
                Started++;
                if (Gate != null) await Gate.Task.ConfigureAwait(false);
                if (FailWrite) throw new IOException("Test disk failure");
                Data = data; Writes++;
            }
        }
        private sealed class Owner : IGrowthCheckpointParticipant
        {
            public int Value; public bool FailApply;
            public string Key { get; } public string PersistenceKey { get; }
            public int Version => 1; public int RestoreOrder => 1;
            public CheckpointSectionKind Kind => CheckpointSectionKind.Progression;
            public event Action<GrowthSaveUrgency> GrowthChanged;
            public Owner(string persistent, string key = null) { PersistenceKey = persistent; Key = key ?? persistent; }
            public void Change(int value, GrowthSaveUrgency urgency) { Value = value; GrowthChanged?.Invoke(urgency); }
            public CheckpointSection Capture(CheckpointCaptureContext context, ICheckpointPayloadCodec codec)
                => codec.Write(Key, 1, Kind, writer => writer.Write(Value));
            public ICheckpointRestoreStep PrepareRestore(CheckpointRestoreContext context, CheckpointSection section, ICheckpointPayloadCodec codec)
            {
                Assert.That(section.Key, Is.EqualTo(Key)); int target = codec.Read(section, 1, r => r.ReadInt32()), backup = Value;
                return new CheckpointRestoreStep(() => { if (FailApply) throw new IOException("Test apply"); Value = target; }, () => Value = backup);
            }
        }
    }
}
