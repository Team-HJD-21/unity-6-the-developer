// Client 읽기 모델의 Match/준비 세대·Grid 세대·revision 검증을 확인합니다.
// 실제 NGO 전송 검증과 구분합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class RegionGridReplicaTests
    {
        private static readonly MatchId Match = new MatchId("match");
        private static readonly Guid Descriptor = Guid.NewGuid();
        private static RegionGridSnapshot Snapshot(Guid gridId, long revision, params int[] cells)
            => RegionGridSnapshot.FromCapturedCells(Match, gridId, revision, RegionPolicyMode.Dynamic,
                new RegionGridDefinition(BattlefieldGridConfiguration.Default), cells);
        private static RegionReplicationFrame Frame(Guid grid, long generation, long revision, params int[] cells)
            => new RegionReplicationFrame(Descriptor, generation, Snapshot(grid, revision, cells));
        [Test]
        public void FullSnapshotIsImmutable_AndDuplicateRevisionDoesNotNotifyAgain()
        {
            using var replica = new RegionGridReplica(Match, Descriptor);
            Guid grid = Guid.NewGuid(); int notifications = 0; replica.Changed += _ => notifications++;
            int[] cells = { 1, 2 };
            var frame = Frame(grid, 1, 7, cells); cells[0] = 15;
            Assert.That(replica.Apply(frame, true), Is.EqualTo(RegionReplicationStatus.Applied));
            Assert.That(replica.Snapshot.CapturedCells[1], Is.True);
            Assert.That(replica.Snapshot.CapturedCells[15], Is.False);
            Assert.That(replica.Apply(Frame(grid, 1, 7, 1, 2), true), Is.EqualTo(RegionReplicationStatus.Unchanged));
            Assert.That(replica.Apply(Frame(grid, 1, 7, 1), true), Is.EqualTo(RegionReplicationStatus.ConflictingRevision));
            Assert.That(notifications, Is.EqualTo(1));
        }
        [Test]
        public void RejectsUnauthorizedOtherMatchAndPreviousPreparation()
        {
            using var replica = new RegionGridReplica(Match, Descriptor);
            var frame = Frame(Guid.NewGuid(), 1, 1, 0);
            Assert.That(replica.Apply(frame, false), Is.EqualTo(RegionReplicationStatus.Unauthorized));
            Assert.That(replica.Apply(new RegionReplicationFrame(Guid.NewGuid(), 1, frame.Snapshot), true),
                Is.EqualTo(RegionReplicationStatus.DifferentDescriptor));
            using var other = new RegionGridReplica(new MatchId("other"), Descriptor);
            Assert.That(other.Apply(frame, true), Is.EqualTo(RegionReplicationStatus.DifferentMatch));
            Assert.That(replica.Snapshot, Is.Null);
        }
        [Test]
        public void ResetGenerationRejectsPreviousGridEvenWithHigherRevision()
        {
            using var replica = new RegionGridReplica(Match, Descriptor);
            Guid first = Guid.NewGuid(), second = Guid.NewGuid();
            replica.Apply(Frame(first, 1, 10, 0), true);
            Assert.That(replica.Apply(Frame(second, 2, 0), true), Is.EqualTo(RegionReplicationStatus.Applied));
            Assert.That(replica.Apply(Frame(first, 1, 1000, 1), true), Is.EqualTo(RegionReplicationStatus.StaleGrid));
            Assert.That(replica.Snapshot.CapturedCellCount, Is.Zero);
            Assert.That(replica.Apply(Frame(first, 2, 20), true), Is.EqualTo(RegionReplicationStatus.ConflictingGrid));
            Assert.That(replica.Apply(Frame(second, 3, 20), true), Is.EqualTo(RegionReplicationStatus.ConflictingGrid));
        }
        [Test]
        public void SameGridRejectsOldRevisionAndUnresetDefinitionChange()
        {
            using var replica = new RegionGridReplica(Match, Descriptor); Guid grid = Guid.NewGuid();
            replica.Apply(Frame(grid, 1, 10), true);
            Assert.That(replica.Apply(Frame(grid, 1, 9), true), Is.EqualTo(RegionReplicationStatus.StaleRevision));
            var changed = RegionGridSnapshot.FromCapturedCells(Match, grid, 11, RegionPolicyMode.Static,
                new RegionGridDefinition(BattlefieldGridConfiguration.Default), new[] { 0 });
            Assert.That(replica.Apply(new RegionReplicationFrame(Descriptor, 1, changed), true),
                Is.EqualTo(RegionReplicationStatus.ConflictingGrid));
        }
        [Test]
        public void RestoreValidatesBoundsAndGeneration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot(Guid.NewGuid(), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot(Guid.NewGuid(), 0, 256));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RegionReplicationFrame(Descriptor, 0, Snapshot(Guid.NewGuid(), 0)));
        }
        [Test]
        public void ObserverFailureDoesNotRollbackOrSkipOthers_AndDisposeIsFinal()
        {
            var replica = new RegionGridReplica(Match, Descriptor); int notifications = 0;
            replica.Changed += _ => throw new InvalidOperationException("observer");
            replica.Changed += _ => notifications++;
            Assert.Throws<AggregateException>(() => replica.Apply(Frame(Guid.NewGuid(), 1, 1, 2), true));
            Assert.That(notifications, Is.EqualTo(1)); Assert.That(replica.Snapshot.CapturedCellCount, Is.EqualTo(1));
            replica.Dispose(); replica.Dispose();
            Assert.Throws<ObjectDisposedException>(() => { var ignored = replica.Snapshot; });
        }
    }
}
