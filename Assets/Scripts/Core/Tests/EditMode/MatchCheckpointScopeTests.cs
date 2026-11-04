// 필수 Feature writer 누락이 저장 성공으로 바뀌지 않는지와 Match scope 종료/재등록을 검증합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchCheckpointScopeTests
    {
        [Test] public void MissingRequiredWriterBlocksDirectRequestsAndUnregisterBlocksAgain()
        {
            using var runtime = new CheckpointServiceTests.Runtime(); var store = new CheckpointServiceTests.Store();
            using var scope = new MatchCheckpointScope(runtime, store, new BinaryCheckpointPayloadCodec(),
                new[] { new CheckpointServiceTests.Participant("world", 1) }, new[] { "world", "player" });
            var slot = new CheckpointSlot("scope");
            Assert.That(scope.Service.SaveAsync(slot).Result.Reason, Is.EqualTo("RequiredFeatureWritersUnavailable"));
            Assert.That(store.Saves, Is.Zero); Assert.That(scope.Service.MissingParticipantKeys, Is.EqualTo(new[] { "player" }));
            var lease = scope.Register(new CheckpointServiceTests.Participant("player", 7));
            Assert.That(scope.Service.SaveAsync(slot).Result.IsSuccess, Is.True);
            lease.Dispose();
            Assert.That(scope.Service.SaveAsync(slot).Result.Status, Is.EqualTo(CheckpointOperationStatus.Rejected));
        }
        [Test] public void DisposeEndsRequestsWithoutDisposingTheAppStoreOrWriterObjects()
        {
            using var runtime = new CheckpointServiceTests.Runtime(); var store = new CheckpointServiceTests.Store();
            var scope = new MatchCheckpointScope(runtime, store, new BinaryCheckpointPayloadCodec(),
                new[] { new CheckpointServiceTests.Participant("world", 1) }, new[] { "world" });
            scope.Dispose(); scope.Dispose();
            Assert.That(scope.Service.SaveAsync(new CheckpointSlot("old")).Result.Status, Is.EqualTo(CheckpointOperationStatus.Rejected));
            Assert.That(scope.Service.ParticipantCount, Is.Zero); Assert.That(store.Saves, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => scope.Register(new CheckpointServiceTests.Participant("other", 1)));
        }
        [Test] public void EmptyRequiredManifestCannotSilentlyStandInForAFullGameCheckpoint()
        {
            using var runtime = new CheckpointServiceTests.Runtime();
            Assert.Throws<ArgumentException>(() => new MatchCheckpointScope(runtime, new CheckpointServiceTests.Store(),
                new BinaryCheckpointPayloadCodec(), Array.Empty<ICheckpointParticipant>(), Array.Empty<string>()));
        }
    }
}
