// 실제 Feature 사실과 초기 MatchState HP를 구분하고 단일 완료·취소·오류 격리를 검증합니다.
// 순수 계약 검사이며 실제 Scene/네트워크 검증과 구분합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchCoordinatorTests
    {
        private sealed class Cu : IControlUnitHealthSource
        {
            private Action _destroyed;
            public int CurrentHealth { get; private set; } = 100;
            public int MaxHealth => 100;
            public int Listeners => _destroyed?.GetInvocationList().Length ?? 0;
            public event Action Destroyed { add => _destroyed += value; remove => _destroyed -= value; }
            public void Destroy() { CurrentHealth = 0; _destroyed?.Invoke(); }
        }
        private sealed class Command : GameCommand
        {
            public Command() : base(Guid.NewGuid(), new PlayerId("host")) { }
        }
        private sealed class Timestamp : ITimestampSource
        {
            public double Now;
            public bool TryReadSeconds(out double seconds) { seconds = Now; return true; }
        }
        private sealed class Rules : IModeRules, IMatchProgressRules
        {
            private readonly StoryModeRules _story = new StoryModeRules();
            public int Results, Receipts;
            public Action BeforeResult;
            public bool IsVictory(MatchState state) => false;
            public bool IsDefeat(MatchState state) => false;
            public MatchOutcome? Evaluate(MatchProgressSnapshot progress) => _story.Evaluate(progress);
            public MatchResult CreateResult(MatchState state, MatchOutcome outcome)
            { Results++; BeforeResult?.Invoke(); return _story.CreateResult(state, outcome); }
            public RewardReceipt CreateRewardReceipt(MatchResult result)
            { Receipts++; return _story.CreateRewardReceipt(result); }
        }
        private static MatchSession Session(IModeRules rules)
        {
            var id = new MatchId("match"); var player = new PlayerId("host");
            var config = new MatchConfig(id, MatchMode.Story, new StageId("stage"), new DefinitionId("difficulty"),
                "content", 1, new[] { new PlayerLoadoutSnapshot(player, null) });
            var state = new MatchState(id, MatchPhase.Preparing, new[] { new PlayerState(player, 100, 100, true) },
                new ControlUnitState(1500, 1500, 250, 250), new WaveState(0, 1, 0, 0),
                Array.Empty<TurretState>(), Array.Empty<EnemyState>(), Array.Empty<SectorState>());
            var session = new MatchSessionFactory(new LocalAuthority()).Create(config, state, rules);
            session.Start(); return session;
        }

        [Test]
        public void FeatureDestructionCommitsOnceCancelsWorkAndStopsGameClock()
        {
            var rules = new Rules(); using var session = Session(rules); var cu = new Cu();
            using var coordinator = new MatchCoordinator(session, cu, () => true);
            int completed = 0; coordinator.Completed += _ => completed++;
            coordinator.Start(); coordinator.Tick(4.5); cu.Destroy(); cu.Destroy();
            var result = coordinator.Completion;
            Assert.That(result.Result.Outcome, Is.EqualTo(MatchOutcome.Defeat));
            Assert.That(session.State.ControlUnit.Health, Is.EqualTo(1500), "Feature HP is not copied into a second writer.");
            Assert.That(coordinator.GameplayCancellation.IsCancellationRequested, Is.True);
            Assert.That(coordinator.IsGameplayRunning, Is.False);
            coordinator.Tick(9); Assert.That(coordinator.ElapsedSeconds, Is.EqualTo(4.5));
            Assert.That(session.Submit(new Command()).Status, Is.EqualTo(CommandSubmissionStatus.Rejected));
            Assert.That(session.Complete(MatchOutcome.Defeat, 99), Is.SameAs(result));
            Assert.That(rules.Results, Is.EqualTo(1)); Assert.That(rules.Receipts, Is.EqualTo(1));
            Assert.That(completed, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => session.Complete(MatchOutcome.Victory, 100));
        }

        [Test]
        public void FailedObserversAndCancellationDoNotEraseResultOrSkipOtherObservers()
        {
            using var session = Session(new Rules()); var cu = new Cu();
            using var failingObserver = session.Events.Subscribe(_ => throw new InvalidOperationException("early observer"));
            using var coordinator = new MatchCoordinator(session, cu, () => true);
            using var callback = coordinator.GameplayCancellation.Register(() => throw new InvalidOperationException("cancel"));
            int observed = 0;
            coordinator.Completed += _ => throw new InvalidOperationException("result observer");
            coordinator.Completed += _ => observed++;
            coordinator.Start();
            Assert.Throws<AggregateException>(cu.Destroy);
            Assert.That(observed, Is.EqualTo(1));
            Assert.That(session.Completion, Is.Not.Null);
            Assert.That(coordinator.GameplayCancellation.IsCancellationRequested, Is.True);
            Assert.That(coordinator.IsGameplayRunning, Is.False);
            Assert.DoesNotThrow(() => session.Complete(MatchOutcome.Defeat, 9));
        }

        [Test]
        public void ReentrantResultPolicyCannotCreateASecondCompletionOrReceipt()
        {
            var rules = new Rules(); using var session = Session(rules);
            rules.BeforeResult = () => session.Complete(MatchOutcome.Defeat, 2);
            Assert.Throws<InvalidOperationException>(() => session.Complete(MatchOutcome.Defeat, 1));
            Assert.That(rules.Results, Is.EqualTo(1)); Assert.That(rules.Receipts, Is.Zero);
            Assert.That(session.Completion, Is.Null); Assert.That(session.IsGameplayRunning, Is.True);
        }

        [Test]
        public void SessionDisposeUnsubscribesCuAndCancelsWithoutOwningFeatureHealth()
        {
            var session = Session(new Rules()); var cu = new Cu();
            using var coordinator = new MatchCoordinator(session, cu, () => true);
            coordinator.Start(); Assert.That(cu.Listeners, Is.EqualTo(1));
            session.Dispose();
            Assert.That(cu.Listeners, Is.Zero);
            Assert.That(coordinator.GameplayCancellation.IsCancellationRequested, Is.True);
            Assert.That(cu.CurrentHealth, Is.EqualTo(100));
            cu.Destroy(); Assert.That(coordinator.Completion, Is.Null);
        }

        [Test]
        public void NonAuthorityAndForeignProgressCannotCompleteCurrentMatch()
        {
            using var session = Session(new Rules());
            using var coordinator = new MatchCoordinator(session, new Cu(), () => false);
            Assert.Throws<InvalidOperationException>(coordinator.Start);
            Assert.Throws<ArgumentException>(() => session.EvaluateProgress(
                new MatchProgressSnapshot(new MatchId("other"), 0, 100, 0)));
            Assert.That(session.Completion, Is.Null);
        }

        [Test]
        public void InjectedTimeDrivesProgressAndCompletionCancelsAllTimers()
        {
            using var session = Session(new Rules()); var source = new Timestamp(); var cu = new Cu();
            using var time = new MatchTimeService(session, source, () => true);
            using var coordinator = new MatchCoordinator(session, cu, () => true, time.Clock);
            coordinator.Start(); time.Pump(2);
            source.Now = 4; time.Pump(2);
            Assert.That(coordinator.ElapsedSeconds, Is.EqualTo(8));
            Assert.Throws<InvalidOperationException>(() => coordinator.Tick(1));
            time.Scheduler.Schedule(20, _ => Assert.Fail("A completed Match must not run old timers."));
            cu.Destroy();
            Assert.That(time.PendingTimers, Is.Zero); Assert.That(time.Clock.Snapshot.IsPaused, Is.True);
            source.Now += 20; time.Pump(2); Assert.That(time.Clock.ElapsedSeconds, Is.EqualTo(8));
            Assert.Throws<OperationCanceledException>(() => time.Scheduler.Schedule(0, _ => { }));
        }

        [Test]
        public void SessionDisposingStopsTimersBeforeProviderResourcesAreDisposed()
        {
            var session = Session(new Rules()); var source = new Timestamp();
            using var time = new MatchTimeService(session, source, () => true);
            time.Scheduler.Schedule(0, _ => Assert.Fail("Disposing must cancel due work."));
            session.Dispose();
            Assert.That(time.Clock.Snapshot.IsPaused, Is.True); Assert.That(time.PendingTimers, Is.Zero);
            Assert.Throws<OperationCanceledException>(() => time.Scheduler.Schedule(0, _ => { }));
            Assert.DoesNotThrow(time.Dispose);
        }
    }
}
