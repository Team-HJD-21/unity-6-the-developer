// 독립 Preview Scene에서 실제 지역 바인딩·Host 설정·설정 저장/복원을 검증합니다.
// 제품 Scene이나 터렛/Player 소스를 수정하지 않으며 모든 임시 객체를 정리합니다.
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Infrastructure;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class DynamicRegionBindingTests
    {
        private Scene _scene;
        private MatchSession _session;
        private RegionSceneBinding _binding;
        private Transform _cu;
        private bool _authority;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            GameObject root = new GameObject("Core region QA");
            SceneManager.MoveGameObjectToScene(root, _scene);
            var controller = root.AddComponent<FieldTurretController>();
            _cu = new GameObject("CU position only").transform;
            _cu.SetParent(root.transform);
            _cu.position = new Vector3(.5f, .5f, 0);
            _binding = root.AddComponent<RegionSceneBinding>();
            var fields = new SerializedObject(_binding);
            fields.FindProperty("_controller").objectReferenceValue = controller;
            fields.FindProperty("_controlUnit").objectReferenceValue = _cu;
            fields.ApplyModifiedPropertiesWithoutUndo();
            var match = new MatchId(Guid.NewGuid().ToString("N"));
            var config = new MatchConfig(match, MatchMode.Story, new StageId("stage"),
                new DefinitionId("normal"), "region-qa", 1, Array.Empty<PlayerLoadoutSnapshot>());
            var state = new MatchState(match, MatchPhase.Preparing);
            _session = new MatchSessionFactory().Create(config, state, new StoryModeRules());
            _authority = true;
            var definition = new RegionGridDefinition(new BattlefieldGridConfiguration(new BattlefieldPoint(2, 2), 0, 4, 4, 4, 4));
            _binding.BindMatch(_session, definition, RegionPolicyMode.Dynamic, () => _authority);
        }

        [TearDown]
        public void TearDown()
        {
            _session?.Dispose();
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void BindStartsWithRealCuCellAndFollowsCuPosition()
        {
            Assert.That(_binding.Source.Snapshot.CapturedCellCount, Is.EqualTo(1));
            Assert.That(_binding.Source.Snapshot.IsCaptured(0, 0), Is.True);
            _cu.position = new Vector3(1.5f, .5f, 0);
            _binding.RefreshDynamicAfterRestore();
            Assert.That(_binding.Source.Snapshot.IsCaptured(0, 0), Is.False);
            Assert.That(_binding.Source.Snapshot.IsCaptured(1, 0), Is.True);
        }

        [Test]
        public void ExplicitStartCellsAreNormalizedCopiedAndCreateNewGridGeneration()
        {
            Guid before = _binding.Source.Snapshot.GridId;
            int[] cells = { 1, 0, 1 };
            Assert.That(_binding.TrySetDynamicStartCells(cells, true, out string error), Is.True, error);
            cells[0] = 15;
            CollectionAssert.AreEqual(new[] { 0, 1 }, _binding.CaptureDynamicStartCells());
            Assert.That(_binding.Source.Snapshot.GridId, Is.Not.EqualTo(before));
            Assert.That(_binding.Source.Snapshot.CapturedCellCount, Is.EqualTo(2));
        }

        [Test]
        public void InvalidStartAreaAndUnauthorizedChangesPreserveCurrentState()
        {
            var before = _binding.Source.Snapshot;
            Assert.That(_binding.TrySetDynamicStartCells(new[] { 0, 5 }, true, out _), Is.False);
            Assert.That(_binding.TrySetDynamicStartCells(new[] { 0, 16 }, true, out _), Is.False);
            Assert.That(_binding.TrySetDynamicStartCells(new[] { 1 }, true, out _), Is.False);
            Assert.That(_binding.TrySetDynamicStartCells(null, false, out _), Is.False);
            _authority = false;
            Assert.That(_binding.TrySetDynamicStartCells(new[] { 0, 1 }, true, out _), Is.False);
            Assert.That(_binding.Source.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void StaticCaptureRetainsBothOutcomesWhenSecuringAndLosingOnTheSameTick()
        {
            var grid = _binding.Source.Snapshot.Definition.Geometry;
            var regionIds = Enumerable.Range(0, grid.TotalCellCount)
                .Select(cell => Math.Max(1, cell % grid.CellsX)).ToArray();
            var definition = new RegionGridDefinition(grid, regionIds);
            _binding.RestoreCheckpoint(definition, RegionPolicyMode.Static, new[] { 0 }, new[] { 1 });
            _binding.ConfigureCapture(new StaticRegionCaptureSettings(1, 180, 1, 20, 2));
            var combat = new TestCombatSource();
            _binding.BindCombatSources(new[] { combat });

            var west = new BattlefieldPoint(.5, .5);
            var east = new BattlefieldPoint(2.5, .5);
            var playerId = new EntityId("capture-player");
            var westernPlayers = new[] { new PlayerSpatialInput(playerId, west) };
            var easternPlayers = new[] { new PlayerSpatialInput(playerId, east) };
            var westernEnemies = new[] { new EnemySpatialInput(new EntityId("capture-enemy"), west) };

            combat.Report(new EnemyDamageObservation(_session.Config.MatchId, west, 20, EnemyDamageContributor.Player));
            _binding.TickStaticCapture(0, westernPlayers, Array.Empty<EnemySpatialInput>());
            _binding.TickStaticCapture(1, westernPlayers, Array.Empty<EnemySpatialInput>());
            Assert.That(_binding.Source.Snapshot.CapturedCellCount, Is.EqualTo(8));
            Assert.That(_binding.Source.Snapshot.IsRegionUnlocked(2), Is.True);

            combat.Report(new EnemyDamageObservation(_session.Config.MatchId, east, 20, EnemyDamageContributor.Player));
            _binding.TickStaticCapture(2, easternPlayers, westernEnemies);
            _binding.TickStaticCapture(3, easternPlayers, westernEnemies);

            var current = _binding.Source.Snapshot;
            Assert.That(current.CapturedCellCount, Is.EqualTo(5));
            Assert.That(current.IsCaptured(0, 0), Is.True, "상실 후에도 CU 셀은 유지합니다.");
            Assert.That(current.IsRegionUnlocked(1), Is.True);
            Assert.That(current.IsRegionUnlocked(3), Is.True);
            Assert.That(current.Revision, Is.EqualTo(current.Change.Revision + 1));
            Assert.That(current.Change.SecuredRegionIds, Is.EqualTo(new[] { 2 }));
            Assert.That(_binding.GetStaticLostRegions(current), Is.EqualTo(new[] { 1 }));

            _binding.RestoreCheckpoint(definition, RegionPolicyMode.Static, new[] { 0 }, new[] { 1 });
            Assert.That(_binding.GetStaticLostRegions(_binding.Source.Snapshot), Is.Empty);
        }

        [Test]
        public void StartSettingsRestoreAndRollbackDoNotLeakTheNewerOverride()
        {
            Assert.That(_binding.TrySetDynamicStartCells(new[] { 0, 1 }, true, out _), Is.True);
            var codec = new BinaryCheckpointPayloadCodec();
            var context = new CheckpointCaptureContext(_session.Config, 10, Guid.NewGuid());
            var writer = (ICheckpointParticipant)_binding;
            var saved = writer.Capture(context, codec);
            Assert.That(_binding.TrySetDynamicStartCells(null, true, out _), Is.True);
            var data = Target(saved);
            using var step = writer.PrepareRestore(new CheckpointRestoreContext(context, data, CheckpointRestorePolicy.Default), saved, codec);
            step.Apply();
            _binding.RefreshDynamicAfterRestore();
            Assert.That(_binding.Source.Snapshot.CapturedCellCount, Is.EqualTo(2));
            step.Rollback();
            _binding.RefreshDynamicAfterRestore();
            Assert.That(_binding.Source.Snapshot.CapturedCellCount, Is.EqualTo(1));
            Assert.That(_binding.CaptureDynamicStartCells(), Is.Empty);
        }

        [Test]
        public void CorruptCheckpointSettingsAreRejectedDuringPrepare()
        {
            var codec = new BinaryCheckpointPayloadCodec();
            var saved = codec.Write("world/region-start", 1, CheckpointSectionKind.World,
                w => { w.Write(2); w.Write(0); w.Write(0); });
            var context = new CheckpointCaptureContext(_session.Config, 10, Guid.NewGuid());
            var before = _binding.Source.Snapshot;
            Assert.Throws<InvalidDataException>(() => ((ICheckpointParticipant)_binding).PrepareRestore(
                new CheckpointRestoreContext(context, Target(saved), CheckpointRestorePolicy.Default), saved, codec));
            Assert.That(_binding.Source.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void SessionDisposalReleasesTheSettingsWriterAndRegionSource()
        {
            var context = new CheckpointCaptureContext(_session.Config, 10, Guid.NewGuid());
            _session.Dispose();
            Assert.That(_binding.Source, Is.Null);
            Assert.That(_binding.HasWriteAuthority, Is.False);
            Assert.Throws<InvalidOperationException>(() => ((ICheckpointParticipant)_binding).Capture(context, new BinaryCheckpointPayloadCodec()));
        }

        private CheckpointData Target(CheckpointSection section) => new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow,
            _session.Config.MatchId, _session.Config.StageId, _session.Config.DifficultyProfileId,
            _session.Config.Mode, _session.Config.RandomSeed, _session.Config.ContentVersion, 10, 1, new[] { section });

        private sealed class TestCombatSource : IEnemyCombatSource
        {
            public event Action<EnemyDamageObservation> DamageObserved;
            public event Action<MatchId, EntityId> DeathObserved { add { } remove { } }
            public void Report(EnemyDamageObservation damage) => DamageObserved?.Invoke(damage);
        }
    }
}
