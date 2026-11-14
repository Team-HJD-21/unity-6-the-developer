// 복귀 파일 읽기와 새 Mission 의도가 겹칠 때 이전 요청이 새 상태/정리를 침범하지 않는지 검증합니다.
// 제어 가능한 IO만 대체하며 실제 AppRoot의 재시도 진입점을 실행합니다.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Infrastructure.Fakes;
using UnityEngine;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MissionRecoveryEntryTests
    {
        private sealed class PendingStore : ICheckpointStore
        {
            private readonly List<TaskCompletionSource<CheckpointData>> _reads = new();
            public Task SaveAsync(CheckpointSlot slot, CheckpointData data, CancellationToken token) =>
                throw new InvalidOperationException("This test must not write progression or world data.");
            public Task<CheckpointData> LoadAsync(CheckpointSlot slot, CancellationToken token)
            {
                var read = new TaskCompletionSource<CheckpointData>();
                _reads.Add(read);
                return read.Task;
            }
            public void Complete(int index) => _reads[index].TrySetResult(null);
            public void CompleteAll() { foreach (var read in _reads) read.TrySetResult(null); }
        }

        private GameObject _object;
        private AppRoot _app;
        private PendingStore _store;
        private FakeSceneFlow _scenes;
        private readonly List<Task> _attempts = new();
        private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            _attempts.Clear();
            _store = new PendingStore();
            _scenes = new FakeSceneFlow();
            _object = new GameObject("Mission recovery IO test");
            _app = _object.AddComponent<AppRoot>();
            _app.Initialize(new AppServices(new FakeProfileService(), new FakeContentCatalog(),
                new FakePlatformService(new PlatformUser(new PlayerId("test"), "Test")), _scenes,
                checkpointStore: _store, checkpointPayloads: new BinaryCheckpointPayloadCodec()));
        }

        [TearDown]
        public async Task TearDown()
        {
            _store.CompleteAll();
            foreach (var task in _attempts) await Rejection(task);
            UnityEngine.Object.DestroyImmediate(_object);
        }

        private Task BeginRetry()
        {
            var ledger = new MissionRecoveryLedger();
            ledger.RecordCheckpoint(new CheckpointSlot(Guid.NewGuid().ToString("N")), Guid.NewGuid());
            ledger.TryBeginFailure(Guid.NewGuid());
            typeof(AppRoot).GetMethod("RememberMissionRecovery", Internal).Invoke(_app, new object[] { "stage", ledger });
            Task attempt = _app.RetryPendingMissionRecoveryAsync();
            _attempts.Add(attempt);
            Assert.That(attempt.IsCompleted, Is.False, "The check requires an actual pending read.");
            return attempt;
        }

        private void BeginNewIntent() => typeof(AppRoot).GetMethod("PrepareMissionEntry", Internal)
            .Invoke(_app, new object[] { MissionEntryKind.NewMission });

        private static async Task<Exception> Rejection(Task task)
        {
            try { await task; return null; }
            catch (Exception error) { return error; }
        }

        [Test]
        public async Task SupersededReadCannotStartSceneTravelOrPublishAnOldError()
        {
            Task old = BeginRetry();
            BeginNewIntent();
            _store.Complete(0);
            Assert.That(await Rejection(old), Is.InstanceOf<OperationCanceledException>());
            Assert.That(_app.LastMissionRecoveryError, Is.Null);
            Assert.That(_app.CanRetryMissionRecovery, Is.False);
            Assert.That(_scenes.LoadedScenes, Is.Empty);
        }

        [Test]
        public async Task OldCompletionCannotReleaseTheNewPendingAttempt()
        {
            Task old = BeginRetry();
            BeginNewIntent();
            Task current = BeginRetry();
            _store.Complete(0);
            Assert.That(await Rejection(old), Is.InstanceOf<OperationCanceledException>());
            Assert.That(current.IsCompleted, Is.False);
            Assert.That(_app.CanRetryMissionRecovery, Is.False);
            Assert.That(_app.LastMissionRecoveryError, Is.Null);
            _store.Complete(1);
            Assert.That(await Rejection(current), Is.InstanceOf<InvalidOperationException>());
            Assert.That(_app.LastMissionRecoveryError, Is.Not.Null);
            Assert.That(_app.CanRetryMissionRecovery, Is.True);
            Assert.That(_scenes.LoadedScenes, Is.Empty);
        }
    }
}
