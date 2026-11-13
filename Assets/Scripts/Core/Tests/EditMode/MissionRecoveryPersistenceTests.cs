// 비환급 기록의 실행 ID·저장 슬롯·차감 상태가 성장 파일에서 왕복하는지 확인합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MissionRecoveryPersistenceTests
    {
        private static CheckpointCaptureContext Context() => new(new MatchConfig(new MatchId("recovery-test"), MatchMode.Story,
            new StageId("stage-1"), new DefinitionId("normal"), "v1", 1,
            new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) }), 10, Guid.NewGuid());
        [Test]
        public void LatestLedgerRestoresTheSameRunAndPendingFailureWithoutRefund()
        {
            var codec = new BinaryCheckpointPayloadCodec(); var before = Context();
            var source = new MissionRecoveryLedger(); source.SpendRescueTime(8);
            var failure = Guid.NewGuid(); source.TryBeginFailure(failure);
            var checkpoint = Guid.NewGuid(); source.RecordCheckpoint(new CheckpointSlot("manual"), checkpoint);
            var writer = new MissionRecoveryParticipant(source); var section = writer.Capture(before, codec);
            var c = before.Config;
            var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, c.MatchId, c.StageId, c.DifficultyProfileId,
                c.Mode, c.RandomSeed, c.ContentVersion, 10, 1, new[] { section });
            var restored = new MissionRecoveryLedger(); Guid initialRun = restored.RunId;
            var target = new MissionRecoveryParticipant(restored);
            using var step = target.PrepareRestore(new CheckpointRestoreContext(before, data, CheckpointRestorePolicy.Default), section, codec);
            step.Apply();
            Assert.That(restored.RunId, Is.EqualTo(source.RunId)); Assert.That(restored.RescueSeconds, Is.EqualTo(52));
            Assert.That(restored.SafetyUses, Is.EqualTo(1)); Assert.That(restored.PendingFailure, Is.EqualTo(failure));
            Assert.That(restored.LastCheckpointId, Is.EqualTo(checkpoint)); Assert.That(restored.LastCheckpoint.Value, Is.EqualTo("manual"));
            Assert.That(CheckpointRestorePolicy.Default.ShouldRestore(section.Kind), Is.False, "World rollback must not rewind this source.");
            step.Rollback(); Assert.That(restored.RunId, Is.EqualTo(initialRun)); Assert.That(restored.SafetyUses, Is.Zero);
        }
        [Test]
        public void GrowthSubscriptionSeparatesFrequentTimeAndCriticalChangesAndReleasesIt()
        {
            var ledger = new MissionRecoveryLedger(); var writer = new MissionRecoveryParticipant(ledger);
            int coalesced = 0, immediate = 0;
            Action<GrowthSaveUrgency> receive = urgency => { if (urgency == GrowthSaveUrgency.Immediate) immediate++; else coalesced++; };
            writer.GrowthChanged += receive;
            ledger.SpendRescueTime(.1); ledger.TryBeginFailure(Guid.NewGuid());
            Assert.That(coalesced, Is.EqualTo(1)); Assert.That(immediate, Is.EqualTo(1));
            writer.GrowthChanged -= receive; ledger.BeginNewMission();
            Assert.That(immediate, Is.EqualTo(1));
        }
    }
}
