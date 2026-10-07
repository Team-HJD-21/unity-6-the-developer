// Window 종료 후 진단 소스 유지, 셀 선택 범위와 Match 종료 시 stale 제거를 검증합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Editor;
using TeamHJD.Game.Infrastructure.Fakes;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class BattlefieldEditorDiagnosticsTests
    {
        private GameObject _rootObject;
        private AppRoot _root;
        private string _displayBefore;
        private BattlefieldDebugWindow _window;

        [SetUp]
        public void SetUp()
        {
            _displayBefore = JsonUtility.ToJson(BattlefieldDebugSession.Display);
            _rootObject = new GameObject("Editor Diagnostic Test");
            _root = _rootObject.AddComponent<AppRoot>();
            _root.Initialize(new AppServices(new FakeProfileService(), new FakeContentCatalog(),
                new FakePlatformService(new PlatformUser(new PlayerId("test"), "Test")),
                new FakeSceneFlow(), new LocalAuthority()));
            var id = new MatchId("diagnostic-test");
            _root.StartMatch(new MatchConfig(id, MatchMode.Story, new StageId("test"),
                new DefinitionId("test"), "test", 1, Array.Empty<PlayerLoadoutSnapshot>()),
                new MatchState(id, MatchPhase.Preparing, Array.Empty<PlayerState>(),
                    new ControlUnitState(1, 1, 1, 1), new WaveState(0, 1, 0, 0),
                    Array.Empty<TurretState>(), Array.Empty<EnemyState>(), Array.Empty<SectorState>()),
                new TestModeRules());
            BattlefieldDebugSession.Bind(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) UnityEngine.Object.DestroyImmediate(_window);
            BattlefieldDebugSession.Bind(null);
            UnityEngine.Object.DestroyImmediate(_rootObject);
            JsonUtility.FromJsonOverwrite(_displayBefore, BattlefieldDebugSession.Display);
            BattlefieldDebugSession.NotifyDisplayChanged();
        }

        [Test]
        public void WindowClose_DoesNotUnbindDiagnosticSource()
        {
            _window = ScriptableObject.CreateInstance<BattlefieldDebugWindow>();
            UnityEngine.Object.DestroyImmediate(_window);
            _window = null;
            Assert.That(BattlefieldDebugSession.AppRoot, Is.SameAs(_root));
            Assert.That(BattlefieldDebugSession.Snapshot, Is.SameAs(_root.CurrentBattlefieldSnapshot));
        }

        [Test]
        public void Selection_RejectsInvalidIds_AndMatchEndClearsData()
        {
            BattlefieldDebugSession.SelectCell(0);
            Assert.That(BattlefieldDebugSession.SelectedCellId, Is.EqualTo(0));
            BattlefieldDebugSession.SelectCell(int.MaxValue);
            Assert.That(BattlefieldDebugSession.SelectedCellId, Is.EqualTo(-1));
            BattlefieldDebugSession.SelectCell(0);
            _root.EndCurrentMatch();
            Assert.That(BattlefieldDebugSession.Snapshot, Is.Null);
            Assert.That(BattlefieldDebugSession.Grid, Is.Null);
            Assert.That(BattlefieldDebugSession.SelectedCellId, Is.EqualTo(-1));
        }

        [Test]
        public void CapturePreset_IsDisplayOnly_AndRecordsSourceIdentity()
        {
            var snapshot = _root.CurrentBattlefieldSnapshot;
            BattlefieldDebugSession.ApplyPreset("Capture");
            Assert.That(BattlefieldDebugSession.Display.IsCaptureMode, Is.True);
            Assert.That(BattlefieldDebugSession.Display.IsCellPickingEnabled, Is.False);
            Assert.That(_root.CurrentBattlefieldSnapshot, Is.SameAs(snapshot));
            Assert.That(BattlefieldDebugSession.CreateCaptureMetadata(), Does.Contain("diagnostic-test"));
            Assert.That(BattlefieldDebugSession.CreateCaptureMetadata(), Does.Contain("Authority/PlanId/pressure: N/A"));
            Assert.That(new BattlefieldSceneOverlay().CreatePanelContent(), Is.Not.Null);
        }

        private sealed class TestModeRules : IModeRules
        {
            public bool IsVictory(MatchState state) => false;
            public bool IsDefeat(MatchState state) => false;
            public MatchResult CreateResult(MatchState state, MatchOutcome outcome) =>
                new MatchResult(state.MatchId, outcome, 0, DateTimeOffset.UtcNow);
            public RewardReceipt CreateRewardReceipt(MatchResult result) =>
                new RewardReceipt(result.MatchId, result.Outcome, Array.Empty<CurrencyDelta>());
        }
    }
}
