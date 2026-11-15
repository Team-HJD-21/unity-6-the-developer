// Stage 생성은 디스크 저장 완료 뒤에만 허용되고, 실제 처치 저장은 다음 Pump에서 진행되는지 검증합니다.
// 단위 검사의 저장/생성 Port만 제어하며 실제 Scene 전체 저장 성공을 대신하지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class StageCheckpointGateTests
    {
        private static readonly MatchId Match = new("stage-save-test");
        private sealed class Spawns : IFieldBossSpawnService
        {
            public int Calls;
            public bool TrySpawnFieldBoss(Guid id, int threshold, RegionGridSnapshot region, out EntityId actor, out string reason)
            { Calls++; actor = new EntityId("confirmed-" + threshold); reason = null; return true; }
        }
        private sealed class Saves
        {
            public readonly List<CheckpointSlot> Slots = new();
            public readonly List<TaskCompletionSource<CheckpointOperationResult>> Pending = new();
            public Task<CheckpointOperationResult> Save(CheckpointSlot slot, CancellationToken token)
            {
                Slots.Add(slot);
                var pending = new TaskCompletionSource<CheckpointOperationResult>();
                Pending.Add(pending);
                return pending.Task;
            }
            public void Complete(int index, CheckpointOperationStatus status = CheckpointOperationStatus.Saved, bool current = true) =>
                Pending[index].SetResult(new CheckpointOperationResult(Guid.NewGuid(), status, status.ToString(), current));
        }
        private static RegionGridSnapshot Region() => RegionGridSnapshot.FromCapturedCells(Match, Guid.NewGuid(), 1, RegionPolicyMode.Static,
            new RegionGridDefinition(BattlefieldGridConfiguration.Default), Array.Empty<int>());

        [Test]
        public async Task SpawnWaitsForDiskCompletionAndReusesTheSuccessfulBeforeSave()
        {
            var epoch = Guid.NewGuid(); var saves = new Saves(); var spawns = new Spawns();
            using var gate = new StageCheckpointGate(Match, Guid.NewGuid(), () => epoch, saves.Save, spawns, default);
            var id = Guid.NewGuid();
            Assert.That(gate.TrySpawnFieldBoss(id, 33, Region(), out _, out _), Is.False);
            Assert.That(spawns.Calls, Is.Zero);
            Assert.That(saves.Pending.Count, Is.EqualTo(1));
            saves.Complete(0); await Task.Yield();
            Assert.That(gate.TrySpawnFieldBoss(id, 33, Region(), out _, out _), Is.True);
            Assert.That(saves.Pending.Count, Is.EqualTo(1));
            Assert.That(spawns.Calls, Is.EqualTo(1));
        }

        [TestCase(CheckpointOperationStatus.Rejected, true)]
        [TestCase(CheckpointOperationStatus.Saved, false)]
        public async Task FailureDoesNotSpawnOrWriteEveryFrameAndExplicitRetryKeepsTheSlot(CheckpointOperationStatus status, bool current)
        {
            var saves = new Saves(); var spawns = new Spawns(); var epoch = Guid.NewGuid();
            using var gate = new StageCheckpointGate(Match, Guid.NewGuid(), () => epoch, saves.Save, spawns, default);
            var id = Guid.NewGuid();
            gate.TrySpawnFieldBoss(id, 66, Region(), out _, out _);
            saves.Complete(0, status, current);
            await Task.Yield();
            for (int i = 0; i < 4; i++) gate.TrySpawnFieldBoss(id, 66, Region(), out _, out _);
            Assert.That(spawns.Calls, Is.Zero);
            Assert.That(saves.Pending.Count, Is.EqualTo(1));
            Assert.That(gate.FailureReason, Is.Not.Null);
            gate.RetryFailed();
            Assert.That(saves.Slots[1], Is.EqualTo(saves.Slots[0]));
            saves.Complete(1);
            await Task.Yield();
            Assert.That(gate.TrySpawnFieldBoss(id, 66, Region(), out _, out _), Is.True);
        }

        [Test]
        public async Task DefeatQueuesUntilNextPumpAndBusyDoesNotDropOrDuplicateTheRequest()
        {
            var saves = new Saves();
            var epoch = Guid.NewGuid();
            using var gate = new StageCheckpointGate(Match, Guid.NewGuid(), () => epoch, saves.Save, null, default);
            gate.RecordFieldBossDefeated(33);
            gate.RecordFieldBossDefeated(33);
            Assert.That(saves.Pending, Is.Empty, "Do not capture a dead actor before its Despawn stack finishes.");
            gate.Pump();
            saves.Complete(0,CheckpointOperationStatus.Busy);
            await Task.Yield();
            gate.Pump();
            Assert.That(saves.Pending.Count,Is.EqualTo(2));
            Assert.That(saves.Slots[1],Is.EqualTo(saves.Slots[0]));
            saves.Complete(1);
            await Task.Yield();
            gate.Pump();
            Assert.That(gate.PendingKeys,Is.Empty);
            Assert.That(saves.Pending.Count,Is.EqualTo(2));
        }

        [Test]
        public async Task OldEpochCompletionCannotAuthorizeSpawnInTheRestoredWorld()
        {
            var saves = new Saves();
            var spawns = new Spawns();
            var epoch = Guid.NewGuid();
            using var gate = new StageCheckpointGate(Match,Guid.NewGuid(),()=>epoch,saves.Save,spawns,default);
            var id = Guid.NewGuid();
            gate.TrySpawnFieldBoss(id,33,Region(),out _,out _);
            epoch=Guid.NewGuid();
            gate.Pump();
            saves.Complete(0);
            await Task.Yield();
            Assert.That(gate.TrySpawnFieldBoss(id,33,Region(),out _,out _),Is.False);
            Assert.That(saves.Pending.Count,Is.EqualTo(2));
            Assert.That(spawns.Calls,Is.Zero);
            saves.Complete(1);
            await Task.Yield();
            Assert.That(gate.TrySpawnFieldBoss(id,33,Region(),out _,out _),Is.True);
        }

        [Test]
        public async Task DisposeCannotPublishLateSuccessOrSpawn()
        {
            var saves = new Saves();
            var spawns = new Spawns();
            var epoch = Guid.NewGuid();
            var gate = new StageCheckpointGate(Match,Guid.NewGuid(),()=>epoch,saves.Save,spawns,default);
            gate.TrySpawnFieldBoss(Guid.NewGuid(),33,Region(),out _,out _);
            gate.Dispose();
            saves.Complete(0);
            await Task.Yield();
            gate.Pump();
            Assert.That(gate.TrySpawnFieldBoss(Guid.NewGuid(),33,Region(),out _,out _),Is.False);
            Assert.That(spawns.Calls,Is.Zero);
            Assert.That(gate.PendingKeys,Is.Empty);
        }
    }
}
