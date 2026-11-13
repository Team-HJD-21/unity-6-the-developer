// 실제 Match Scheduler와 메모리 요청 Port로 정규 생산의 수명·중복·복원을 확인합니다.
// Enemy의 실제 편성과 생성은 Stage1 통합 관측에서 확인합니다.
using System;
using System.Threading;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchEncounterProductionTests
    {
        private static readonly MatchId Match = new MatchId("production-test");
        private static readonly PlayerId Host = new PlayerId("host");
        private sealed class Clock : IMatchTime
        {
            public double Now;
            public bool Paused;
            public Guid Epoch = Guid.NewGuid();
            public double ElapsedSeconds => Now;
            public bool IsAvailable => true;
            public MatchTimeSnapshot Snapshot => new MatchTimeSnapshot(Match, Epoch, 0, Now, Now, 1, Paused, true);
        }
        private sealed class Requests : IEncounterRequestService
        {
            public int Count;
            public EncounterRequest Latest;
            public EncounterRequestResult Submit(EncounterRequest request, PlayerId issuer)
            {
                Assert.That(issuer, Is.EqualTo(Host));
                Assert.That(request.Context.MatchId, Is.EqualTo(Match));
                Assert.That(request.UsesAutomaticComposition, Is.True);
                Assert.That(request.Source, Is.EqualTo(EncounterRequestSource.MatchProduction));
                Count++; Latest = request;
                return new EncounterRequestResult(request.Context.RequestId, Count, 1, EncounterRequestStatus.Spawned, 1, 1);
            }
        }
        private static MatchEncounterProduction Create(Clock time, Requests requests, MatchScheduler scheduler,
            Func<bool> ready = null, EncounterProductionSettings settings = null, CancellationToken lifetime = default)
            => new MatchEncounterProduction(Match, Host, requests, scheduler, time, ready ?? (() => true),
                settings ?? new EncounterProductionSettings { FirstDelaySeconds = 1, IntervalSeconds = 5, MaximumEnemiesPerRequest = 30 }, lifetime);

        [Test]
        public void StartsOnce_UsesCommonAutomaticRequest_ThenWaitsFromActualExecution()
        {
            var clock = new Clock(); var requests = new Requests();
            using var scheduler = new MatchScheduler(clock);
            using var production = Create(clock, requests, scheduler);
            Assert.That(scheduler.PendingCount, Is.Zero);
            production.Start();
            Assert.Throws<InvalidOperationException>(() => production.Start());
            clock.Now = .9; scheduler.Pump(); Assert.That(requests.Count, Is.Zero);
            clock.Now = 1; scheduler.Pump(); Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(requests.Latest.MaximumEnemyCount, Is.EqualTo(30));
            Assert.That(production.RemainingSeconds, Is.EqualTo(5));
            Assert.That(scheduler.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void DelayedFrame_ProducesOneRequest_WithoutCatchUpBurst()
        {
            var clock = new Clock(); var requests = new Requests();
            using var scheduler = new MatchScheduler(clock); using var production = Create(clock, requests, scheduler);
            production.Start(); clock.Now = 500; scheduler.Pump(); scheduler.Pump();
            Assert.That(requests.Count, Is.EqualTo(1));
            clock.Now = 504.9; scheduler.Pump(); Assert.That(requests.Count, Is.EqualTo(1));
            clock.Now = 505; scheduler.Pump(); Assert.That(requests.Count, Is.EqualTo(2));
        }

        [Test]
        public void PauseAndAuthorityLoss_DoNotSendRequests()
        {
            var clock = new Clock(); var requests = new Requests(); bool ready = false;
            using var scheduler = new MatchScheduler(clock); using var production = Create(clock, requests, scheduler, () => ready);
            production.Start(); clock.Now = 1; scheduler.Pump(); Assert.That(requests.Count, Is.Zero);
            clock.Paused = true; clock.Now = 6; ready = true; scheduler.Pump(); Assert.That(requests.Count, Is.Zero);
            clock.Paused = false; scheduler.Pump(); Assert.That(requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void RestoreRegistersOneNewEpochDeadline_WithoutOldTimerOrFreshStartDelay()
        {
            var clock = new Clock(); var requests = new Requests();
            using var scheduler = new MatchScheduler(clock); using var production = Create(clock, requests, scheduler);
            production.Start(); clock.Now = 1; scheduler.Pump(); clock.Now = 3;
            var saved = production.Capture(); Assert.That(saved.RemainingSeconds, Is.EqualTo(3));
            clock.Now = 20; scheduler.Pump(); Assert.That(requests.Count, Is.EqualTo(2));
            clock.Now = 3; clock.Epoch = Guid.NewGuid(); production.Restore(saved);
            Assert.That(production.AttemptedRequests, Is.EqualTo(1)); Assert.That(scheduler.PendingCount, Is.EqualTo(1));
            clock.Now = 5.9; scheduler.Pump(); Assert.That(requests.Count, Is.EqualTo(2));
            clock.Now = 6; scheduler.Pump(); Assert.That(requests.Count, Is.EqualTo(3));
        }

        [Test]
        public void CancellationAndDispose_RemoveFutureProduction()
        {
            var clock = new Clock(); var requests = new Requests(); using var token = new CancellationTokenSource();
            using var scheduler = new MatchScheduler(clock); using var production = Create(clock, requests, scheduler, lifetime: token.Token);
            production.Start(); token.Cancel(); clock.Now = 100; scheduler.Pump();
            Assert.That(requests.Count, Is.Zero); Assert.That(scheduler.PendingCount, Is.Zero);
            production.Dispose(); Assert.That(production.IsRunning, Is.False);
        }

        [Test]
        public void DisabledSettings_DoNotSchedule_AndRuntimeSettingsDoNotAliasAuthoring()
        {
            var clock = new Clock(); var requests = new Requests(); var settings = new EncounterProductionSettings { Enabled = false };
            using var scheduler = new MatchScheduler(clock); using var production = Create(clock, requests, scheduler, settings: settings);
            settings.Enabled = true; production.Start();
            Assert.That(scheduler.PendingCount, Is.Zero); Assert.That(production.IsRunning, Is.False);
        }
    }
}
