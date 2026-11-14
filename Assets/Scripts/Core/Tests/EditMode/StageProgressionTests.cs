// 점유 임계값의 실제 생성 확인·중복 방지·최종 승리·월드 복원 경계를 검사합니다.
// 시험용 spawn port이며 실제 Boss 콘텐츠/Scene 인수를 대신하지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class StageProgressionTests
    {
        private static readonly MatchId Match = new("stage-progress-test");
        private sealed class Clock : IMatchClock { public double Now; public double ElapsedSeconds => Now; }
        private sealed class Spawns : IFieldBossSpawnService
        {
            public bool BlockSecond;
            public readonly List<(Guid Event, int Percent)> Attempts = new();
            public bool TrySpawnFieldBoss(Guid id, int percent, RegionGridSnapshot regions, out EntityId actor, out string reason)
            {
                Attempts.Add((id, percent));
                bool success = percent != 66 || !BlockSecond;
                actor = success ? new EntityId("actual-field-boss-" + percent) : default;
                reason = success ? null : "ReservedEntranceNotReady"; return success;
            }
        }
        private static RegionGridSnapshot Region(int count)
        {
            var grid = new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 10, 10, 10, 10);
            return RegionGridSnapshot.FromCapturedCells(Match, Guid.NewGuid(), 1, RegionPolicyMode.Static,
                new RegionGridDefinition(grid), Enumerable.Range(0, count));
        }
        [Test]
        public void OneJumpAcrossBothThresholdsSpawnsInOrderAndNeverRequiresTheirDeaths()
        {
            var spawns = new Spawns(); var progress = new StageProgression(Match, Guid.NewGuid(), new Clock(), () => true, spawns);
            progress.Pump(Region(100)); progress.Pump(Region(100));
            Assert.That(spawns.Attempts.Select(x => x.Percent), Is.EqualTo(new[] { 33, 66 }));
            Assert.That(progress.Snapshot.CanEnterFinalBattle, Is.True);
            Assert.That(progress.Snapshot.FirstFieldBossDefeated || progress.Snapshot.SecondFieldBossDefeated, Is.False);
        }
        [Test]
        public void UnconfirmedSecondSpawnBlocksFinalAndRetriesTheSameEventWithoutRepeatingTheFirst()
        {
            var clock = new Clock(); var spawns = new Spawns { BlockSecond = true };
            var progress = new StageProgression(Match, Guid.NewGuid(), clock, () => true, spawns);
            progress.Pump(Region(100)); progress.Pump(Region(100));
            Assert.That(progress.Snapshot.CanEnterFinalBattle, Is.False);
            Assert.That(progress.Snapshot.PendingReason, Is.EqualTo("ReservedEntranceNotReady"));
            Assert.That(spawns.Attempts.Count, Is.EqualTo(2));
            clock.Now = 1; spawns.BlockSecond = false; progress.Pump(Region(100));
            Assert.That(spawns.Attempts.Select(x => x.Percent), Is.EqualTo(new[] { 33, 66, 66 }));
            Assert.That(spawns.Attempts[1].Event, Is.EqualTo(spawns.Attempts[2].Event));
            Assert.That(progress.Snapshot.CanEnterFinalBattle, Is.True);
        }
        [Test]
        public void MissingContentCannotProduceFakeBossesOrFinalReadiness()
        {
            var progress = new StageProgression(Match, Guid.NewGuid(), new Clock(), () => true, null);
            progress.Pump(Region(100));
            Assert.That(progress.Snapshot.PendingReason, Is.EqualTo("FieldBossContentNotConnected"));
            Assert.That(progress.Snapshot.FirstFieldBoss.IsEmpty, Is.True);
            Assert.That(progress.Snapshot.CanEnterFinalBattle, Is.False);
            Assert.Throws<InvalidOperationException>(() => progress.ConfirmFinalBossReady(new EntityId("final")));
        }
        [Test]
        public void FinalReadinessUsesCurrentCaptureAndVictoryNeedsTheRegisteredFinalDeath()
        {
            var progress = new StageProgression(Match, Guid.NewGuid(), new Clock(), () => true, new Spawns());
            progress.Pump(Region(100)); progress.Pump(Region(90));
            Assert.That(progress.Snapshot.CanEnterFinalBattle, Is.False);
            progress.Pump(Region(100)); var boss = new EntityId("final-real-actor"); progress.ConfirmFinalBossReady(boss);
            int victories = 0; progress.Victory += () => victories++;
            progress.ObserveEnemyDeath(new EntityId("other-enemy")); Assert.That(victories, Is.Zero);
            progress.ObserveEnemyDeath(boss); progress.ObserveEnemyDeath(boss);
            Assert.That(victories, Is.EqualTo(1));
        }
        [Test]
        public void FieldLossDoesNotCancelTheThresholdThatWasAlreadyReached()
        {
            var spawns = new Spawns { BlockSecond = true }; var clock = new Clock();
            var progress = new StageProgression(Match, Guid.NewGuid(), clock, () => true, spawns);
            progress.Pump(Region(70)); spawns.BlockSecond = false; clock.Now = 1; progress.Pump(Region(20));
            Assert.That(progress.Snapshot.SecondFieldBoss.IsEmpty, Is.False);
            Assert.That(progress.Snapshot.CaptureRatio, Is.EqualTo(.2));
            Assert.That(progress.Snapshot.HighestCaptureRatio, Is.EqualTo(.7));
        }
        [Test]
        public void FieldDeathNotificationIsOncePerActualTrackedActor()
        {
            var progress = new StageProgression(Match, Guid.NewGuid(), new Clock(), () => true, new Spawns());
            progress.Pump(Region(100));
            var observed = new List<int>();
            progress.FieldBossDefeated += observed.Add;
            progress.ObserveEnemyDeath(new EntityId("ordinary"));
            progress.ObserveEnemyDeath(progress.Snapshot.FirstFieldBoss);
            progress.ObserveEnemyDeath(progress.Snapshot.FirstFieldBoss);
            progress.ObserveEnemyDeath(progress.Snapshot.SecondFieldBoss);
            progress.ObserveEnemyDeath(progress.Snapshot.SecondFieldBoss);
            Assert.That(observed, Is.EqualTo(new[] { 33, 66 }));
        }

        private sealed class LivingControlUnit : IControlUnitVitals
        {
            public int CurrentHealth => 100;
            public int MaxHealth => 100;
        }

        private sealed class MutableControlUnit : IControlUnitVitals
        {
            public int CurrentHealth { get; set; } = 100;
            public int MaxHealth => 100;
        }

        [Test]
        public void FinalDeathCannotOvertakeAnAlreadyDestroyedControlUnitBeforeItsUpdateObservation()
        {
            foreach (bool destroyed in new[] { false, true })
            {
                var config = new MatchConfig(Match, MatchMode.Story, new StageId("stage-1"), new DefinitionId("normal"), "v1", 1,
                    new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) });
                using var session = new MatchSessionFactory().Create(config, new MatchState(Match, MatchPhase.Preparing), new StoryModeRules());
                var cu = new MutableControlUnit();
                using var coordinator = new MatchCoordinator(session, cu, () => true, new Clock());
                coordinator.ConfigureStageProgression(Guid.NewGuid(), new Clock(), new Spawns());
                session.Start(); coordinator.Start();
                var stage = coordinator.StageProgression;
                stage.Pump(Region(100));
                var final = new EntityId("final-real-actor");
                stage.ConfirmFinalBossReady(final);
                int completions = 0;
                coordinator.Completed += _ => completions++;
                // 파괴 이벤트를 구현하지 않은 HP 원본입니다. Root Update가 아직 읽지 않은 실제 0을 재현합니다.
                if (destroyed) cu.CurrentHealth = 0;
                stage.ObserveEnemyDeath(final);
                stage.ObserveEnemyDeath(final);
                Assert.That(session.Completion.Result.Outcome, Is.EqualTo(destroyed ? MatchOutcome.Defeat : MatchOutcome.Victory));
                Assert.That(completions, Is.EqualTo(1));
            }
        }

        [Test]
        public void PendingPlayerRecoveryTakesPriorityOverFieldSpawn()
        {
            var config = new MatchConfig(Match, MatchMode.Story, new StageId("stage-1"), new DefinitionId("normal"), "v1", 1,
                new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) });
            using var session = new MatchSessionFactory().Create(config, new MatchState(Match, MatchPhase.Preparing), new StoryModeRules());
            using var coordinator = new MatchCoordinator(session, new LivingControlUnit(), () => true, new Clock());
            var ledger = new MissionRecoveryLedger();
            Guid failure = Guid.NewGuid();
            ledger.TryBeginFailure(failure);
            coordinator.ConfigureRecovery(ledger, new MissionRecoverySettings());
            var spawns = new Spawns();
            coordinator.ConfigureStageProgression(ledger.RunId, new Clock(), spawns);
            session.Start();
            coordinator.Start();
            coordinator.Recovery.AdoptPendingRecovery(failure);
            coordinator.StageProgression.Pump(Region(100));
            Assert.That(coordinator.Recovery.IsRecoveryPending, Is.True);
            Assert.That(spawns.Attempts, Is.Empty);
        }

        [Test]
        public void WorldRoundtripPreservesSpawnConfirmationWithoutReplayingAnEvent()
        {
            var run = Guid.NewGuid(); var clock = new Clock(); var codec = new BinaryCheckpointPayloadCodec();
            var source = new StageProgression(Match, run, clock, () => true, new Spawns()); source.Pump(Region(40));
            var config = new MatchConfig(Match, MatchMode.Story, new StageId("stage-1"), new DefinitionId("normal"), "v1", 1,
                new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) });
            var capture = new CheckpointCaptureContext(config, 10, Guid.NewGuid());
            var section = new StageProgressCheckpointParticipant(source).Capture(capture, codec);
            var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, Match, config.StageId, config.DifficultyProfileId,
                config.Mode, 1, "v1", 10, 1, new[] { section });
            var spawns = new Spawns(); var restored = new StageProgression(Match, run, clock, () => true, spawns);
            using var step = new StageProgressCheckpointParticipant(restored).PrepareRestore(
                new CheckpointRestoreContext(capture, data, CheckpointRestorePolicy.Default), section, codec);
            step.Apply(); restored.Pump(Region(40));
            Assert.That(restored.Snapshot.FirstFieldBoss, Is.EqualTo(source.Snapshot.FirstFieldBoss));
            Assert.That(spawns.Attempts, Is.Empty); step.Rollback(); Assert.That(restored.Snapshot.FirstFieldBoss.IsEmpty, Is.True);
        }
    }
}
