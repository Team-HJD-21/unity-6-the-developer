// 실제 Match의 공간 설정·지역 점유 복원과 백업 복구를 검증합니다.
// 개체·전투 상태 전체 복원 검사는 각 Feature 및 통합 검사에서 따로 수행합니다.
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchSpatialCheckpointTests
    {
        private static readonly BinaryCheckpointPayloadCodec Codec = new();
        private static readonly BattlefieldGridConfiguration Grid = new(new BattlefieldPoint(12, 4), 7, 12, 8, 3, 2);
        private static MatchSession CreateSession(string id = "current")
        {
            var match = new MatchId(id);
            var config = new MatchConfig(match, MatchMode.Story, new StageId("stage"), new DefinitionId("normal"), "content", 3,
                new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) });
            var state = new MatchState(match, MatchPhase.Preparing, new[] { new PlayerState(new PlayerId("host"), 100, 100, true) },
                new ControlUnitState(100, 100, 100, 100), new WaveState(0, 1, 0, 0), Array.Empty<TurretState>(), Array.Empty<EnemyState>(), Array.Empty<SectorState>());
            var session = new MatchSessionFactory(new LocalAuthority()).Create(config, state, new StoryModeRules());
            session.InitializeRegions(new RegionGridDefinition(Grid, new[] { 1, 1, 2, 1, 2, 2 }), RegionPolicyMode.Static);
            session.ReconfigureBattlefieldGrid(Grid);
            return session;
        }

        private static CheckpointCaptureContext Context(MatchSession session) => new(session.Config, 12, Guid.NewGuid());
        private static CheckpointRestoreContext RestoreContext(MatchSession session, CheckpointSection section)
        {
            var config = session.Config;
            var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, new MatchId("old-source"), config.StageId,
                config.DifficultyProfileId, config.Mode, config.RandomSeed, config.ContentVersion, 12, 1, new[] { section });
            return new CheckpointRestoreContext(Context(session), data, CheckpointRestorePolicy.Default);
        }

        [Test]
        public void ActualGeometryMembershipAndCaptureRestoreAtomicallyThenRollback()
        {
            using var session = CreateSession();
            session.UpdateRegionCapture(session.Regions.Snapshot, new[] { 0, 4 }, RegionCaptureUpdateMode.Replace);
            var writer = new MatchSpatialCheckpointParticipant(session, session.ResetRegions);
            var section = writer.Capture(Context(session), Codec);
            var original = session.Regions.Snapshot;
            session.ReconfigureBattlefieldGrid(BattlefieldGridConfiguration.Default);
            session.ResetRegions(new RegionGridDefinition(BattlefieldGridConfiguration.Default), RegionPolicyMode.Dynamic, new[] { 5 });
            var before = session.Regions.Snapshot;
            using var prepared = writer.PrepareRestore(RestoreContext(session, section), section, Codec);
            Assert.That(session.Regions.Snapshot, Is.SameAs(before), "Preparation must not change the world.");
            prepared.Apply();
            Assert.That(session.Battlefield.Grid.Configuration.Origin, Is.EqualTo(Grid.Origin));
            Assert.That(session.Battlefield.Grid.Configuration.OriginZ, Is.EqualTo(7));
            Assert.That(session.Regions.Snapshot.Mode, Is.EqualTo(RegionPolicyMode.Static));
            Assert.That(session.Regions.Snapshot.Definition.RegionIds, Is.EqualTo(original.Definition.RegionIds));
            Assert.That(session.Regions.Snapshot.CapturedCells, Is.EqualTo(original.CapturedCells));
            Assert.That(session.Regions.Snapshot.GridId, Is.Not.EqualTo(original.GridId));
            prepared.Rollback();
            Assert.That(session.Battlefield.Grid.Configuration.CellsX, Is.EqualTo(16));
            Assert.That(session.Regions.Snapshot.CapturedCells, Is.EqualTo(before.CapturedCells));
            Assert.That(session.Regions.Snapshot.Mode, Is.EqualTo(RegionPolicyMode.Dynamic));
        }

        [Test]
        public void NewMatchUsesLogicalValuesInsteadOfOldGridOrMatchIdentity()
        {
            using var oldSession = CreateSession("old");
            oldSession.UpdateRegionCapture(oldSession.Regions.Snapshot, new[] { 1, 2 }, RegionCaptureUpdateMode.Replace);
            var section = new MatchSpatialCheckpointParticipant(oldSession, oldSession.ResetRegions).Capture(Context(oldSession), Codec);
            using var next = CreateSession("new");
            using var step = new MatchSpatialCheckpointParticipant(next, next.ResetRegions).PrepareRestore(RestoreContext(next, section), section, Codec);
            step.Apply();
            Assert.That(next.Regions.Snapshot.MatchId, Is.EqualTo(next.Config.MatchId));
            Assert.That(next.Regions.Snapshot.CapturedCellCount, Is.EqualTo(2));
        }

        [Test]
        public void IncompleteOrFuturePayloadDoesNotMutateActualState()
        {
            using var session = CreateSession();
            var writer = new MatchSpatialCheckpointParticipant(session, session.ResetRegions);
            var before = session.Regions.Snapshot;
            var bad = new CheckpointSection(writer.Key, 1, writer.Kind, new byte[] { 1 });
            Assert.Throws<InvalidDataException>(() => writer.PrepareRestore(RestoreContext(session, bad), bad, Codec));
            var future = new CheckpointSection(writer.Key, 2, writer.Kind, Array.Empty<byte>());
            Assert.Throws<NotSupportedException>(() => writer.PrepareRestore(RestoreContext(session, future), future, Codec));
            Assert.That(session.Regions.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void CaptureRejectsWrongMatchAndMissingRegionSource()
        {
            using var session = CreateSession(); using var other = CreateSession("other");
            var writer = new MatchSpatialCheckpointParticipant(session, session.ResetRegions);
            Assert.Throws<InvalidOperationException>(() => writer.Capture(Context(other), Codec));
            session.Dispose();
            Assert.Throws<InvalidOperationException>(() => writer.Capture(Context(session), Codec));
        }

        [Test]
        public void TransformRoundTripKeepsWorldPoseAndLocalScale()
        {
            var source = new GameObject("checkpoint-pose");
            try
            {
                source.transform.SetPositionAndRotation(new Vector3(3, -7, 1), Quaternion.Euler(0, 0, 45));
                source.transform.localScale = new Vector3(2, 1, -1);
                var section = Codec.Write("pose", 1, CheckpointSectionKind.World, writer => CheckpointTransformState.Capture(source.transform).Write(writer));
                source.transform.position = Vector3.zero; source.transform.localScale = Vector3.one;
                Codec.Read(section, 1, CheckpointTransformState.Read).Apply(source.transform);
                Assert.That(source.transform.position, Is.EqualTo(new Vector3(3, -7, 1)));
                Assert.That(source.transform.localScale, Is.EqualTo(new Vector3(2, 1, -1)));
                Assert.That(Quaternion.Angle(source.transform.rotation, Quaternion.Euler(0, 0, 45)), Is.LessThan(0.01f));
            }
            finally { Object.DestroyImmediate(source); }
            Assert.Throws<InvalidDataException>(() => new CheckpointTransformState(new Vector3(float.NaN, 0, 0), Quaternion.identity, Vector3.one));
            Assert.Throws<InvalidDataException>(() => new CheckpointTransformState(Vector3.zero, new Quaternion(), Vector3.one));
        }
    }
}
