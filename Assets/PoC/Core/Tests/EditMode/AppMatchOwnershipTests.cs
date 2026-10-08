// 이전 Scene 소유자가 새 Match를 종료하지 못하는지와 공간 갱신 통지를 검증합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure.Fakes;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class AppMatchOwnershipTests
    {
        private GameObject _rootObject;
        private AppRoot _root;

        [SetUp]
        public void SetUp()
        {
            _rootObject = new GameObject("Ownership Test App");
            _root = _rootObject.AddComponent<AppRoot>();
            _root.Initialize(new AppServices(new FakeProfileService(), new FakeContentCatalog(),
                new FakePlatformService(new PlatformUser(new PlayerId("test"), "Test")),
                new FakeSceneFlow(), new LocalAuthority()));
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_rootObject);

        [Test]
        public void ReplacedMatch_OldOwnerCannotEndNewMatch()
        {
            MatchSession first = Start("first");
            MatchSession second = Start("second");
            Assert.That(_root.TryEndMatch(first.Config.MatchId), Is.False);
            Assert.That(_root.CurrentMatchId, Is.EqualTo(second.Config.MatchId));
            Assert.That(_root.TryEndMatch(default), Is.False);
            Assert.That(_root.TryEndMatch(second.Config.MatchId), Is.True);
            Assert.That(_root.CurrentMatchId.IsEmpty, Is.True);
            Assert.That(_root.TryEndMatch(second.Config.MatchId), Is.False);
        }

        [Test]
        public void ParticipantUpdate_PublishesCurrentRevision_AndEndClearsSnapshot()
        {
            Start("sample");
            long before = _root.CurrentBattlefieldSnapshot.Revision;
            BattlefieldSpatialSnapshot observed = null;
            int notifications = 0;
            _root.BattlefieldSnapshotChanged += snapshot => { observed = snapshot; notifications++; };
            _root.UpdateBattlefieldParticipants(new BattlefieldDynamicSpatialInput(
                new[] { new PlayerSpatialInput(new EntityId("test"), new BattlefieldPoint(1, 1)) },
                Array.Empty<EnemySpatialInput>()));
            Assert.That(observed, Is.SameAs(_root.CurrentBattlefieldSnapshot));
            Assert.That(observed.Revision, Is.GreaterThan(before));
            _root.EndCurrentMatch();
            Assert.That(observed, Is.Null);
            Assert.That(notifications, Is.EqualTo(2));
            _root.EndCurrentMatch();
            Assert.That(notifications, Is.EqualTo(2));
        }

        private MatchSession Start(string id)
        {
            var matchId = new MatchId(id);
            var playerId = new PlayerId("test");
            var config = new MatchConfig(matchId, MatchMode.Story, new StageId("test"),
                new DefinitionId("test"), "test", 1,
                new[] { new PlayerLoadoutSnapshot(playerId, null) });
            var state = new MatchState(matchId, MatchPhase.Preparing,
                new[] { new PlayerState(playerId, 1, 1, true) },
                new ControlUnitState(1, 1, 1, 1), new WaveState(0, 1, 0, 0),
                Array.Empty<TurretState>(), Array.Empty<EnemyState>(), Array.Empty<SectorState>());
            return _root.StartMatch(config, state, new TestModeRules());
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
