// 환경의 불변성·권위·revision·요청 상관관계를 검증합니다.
// 실제 World 콘텐츠 및 네트워크 전송 검증과 구분되는 계약 테스트입니다.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchEnvironmentTests
    {
        private static readonly MatchId Match = new MatchId("environment-test");
        private sealed class Clock : IMatchClock { public double Seconds; public double ElapsedSeconds => Seconds; }
        private static MatchEnvironmentSnapshot Snapshot(long revision = 1, double seconds = 0, string weather = "rain", MatchId? match = null)
            => new MatchEnvironmentSnapshot(match ?? Match, revision, seconds, new DefinitionId(weather), default);
        [Test]
        public void SnapshotCopiesForecast_AndKeepsUnknownTimeDistinct()
        {
            var forecast = new List<DefinitionId> { new DefinitionId("sunny") };
            var snapshot = new MatchEnvironmentSnapshot(Match, 1, 0, new DefinitionId("rain"), default, forecast);
            forecast.Clear();
            Assert.That(snapshot.Forecast.Count, Is.EqualTo(1));
            Assert.That(snapshot.HasTimeOfDay, Is.False);
            Assert.Throws<ArgumentException>(() => new MatchEnvironmentSnapshot(Match, 1, 0, default, default, snapshot.Forecast));
            Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot(seconds: double.NaN));
        }
        [Test]
        public void RuntimeOnlyPublishesChanges_DeliversAllObservers_AndRejectsClosedAuthority()
        {
            var clock = new Clock(); bool authority = true; int calls = 0;
            using var runtime = new MatchEnvironmentRuntime(Match, clock, () => authority);
            runtime.Changed += _ => throw new InvalidOperationException("observer");
            runtime.Changed += _ => calls++;
            Assert.Throws<AggregateException>(() => runtime.TryUpdate(new DefinitionId("rain"), default, null));
            Assert.That(runtime.Snapshot.Revision, Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(runtime.TryUpdate(new DefinitionId("rain"), default, null), Is.False);
            authority = false;
            Assert.That(runtime.TryUpdate(new DefinitionId("sunny"), default, null), Is.False);
            runtime.Dispose();
            Assert.That(runtime.Snapshot, Is.Null);
        }
        [Test]
        public void ReplicaRejectsUntrustedStaleConflictingAndOtherGenerationFrames()
        {
            var generation = Guid.NewGuid();
            using var replica = new MatchEnvironmentReplica(Match, generation);
            EnvironmentApplyStatus Apply(MatchEnvironmentSnapshot value, bool server = true, Guid? id = null)
                => replica.Apply(new EnvironmentReplicationFrame(id ?? generation, value), server);
            Assert.That(Apply(Snapshot(2, 5), false), Is.EqualTo(EnvironmentApplyStatus.Unauthorized));
            Assert.That(Apply(Snapshot(2, 5)), Is.EqualTo(EnvironmentApplyStatus.Applied));
            Assert.That(Apply(Snapshot(2, 5)), Is.EqualTo(EnvironmentApplyStatus.Unchanged));
            Assert.That(Apply(Snapshot(1)), Is.EqualTo(EnvironmentApplyStatus.Stale));
            Assert.That(Apply(Snapshot(2, 5, "sunny")), Is.EqualTo(EnvironmentApplyStatus.ConflictingRevision));
            Assert.That(Apply(Snapshot(3, 4)), Is.EqualTo(EnvironmentApplyStatus.ClockReversed));
            Assert.That(Apply(Snapshot(3, 6), id: Guid.NewGuid()), Is.EqualTo(EnvironmentApplyStatus.DifferentDescriptor));
            Assert.That(Apply(Snapshot(3, 6, match: new MatchId("other"))), Is.EqualTo(EnvironmentApplyStatus.DifferentMatch));
            Assert.That(Apply(Snapshot(3, 6, "sunny")), Is.EqualTo(EnvironmentApplyStatus.Applied));
        }
        private sealed class Executor : IEnvironmentAwareEncounterExecutor
        {
            public int Calls;
            public MatchEnvironmentSnapshot Used;
            public EncounterRequestResult Execute(EncounterRequest request, BattlefieldSpatialSnapshot battlefield, long sequence)
                => throw new InvalidOperationException("The environment-aware overload is required.");
            public EncounterRequestResult Execute(EncounterRequest request, BattlefieldSpatialSnapshot battlefield,
                MatchEnvironmentSnapshot environment, long sequence)
            {
                Calls++; Used = environment;
                return new EncounterRequestResult(request.Context.RequestId, sequence, battlefield.Revision,
                    EncounterRequestStatus.Spawned, 1, 1, environmentRevision: environment.Revision);
            }
        }
        [Test]
        public void RequestUsesOneAuthoritySnapshot_AndReplayRetainsOriginalEnvironment()
        {
            var player = new PlayerId("host"); var executor = new Executor();
            using var spatial = new BattlefieldSpatialRuntime(Match, BattlefieldSpatialInput.Empty,
                BattlefieldDynamicSpatialInput.Empty, BattlefieldGridConfiguration.Default);
            var current = Snapshot(); int queries = 0;
            using var service = new EncounterRequestService(Match, () => spatial.Snapshot, () => true, _ => true, executor,
                environment: () => { queries++; return current; });
            var request = new EncounterRequest(new MatchRequestContext(Match, Guid.NewGuid(), player),
                new DefinitionId("Normal"), 10, EncounterRequestSource.DebugWindow);
            var result = service.Submit(request, player);
            Assert.That(executor.Used, Is.SameAs(current));
            current = Snapshot(2, 1, "sunny");
            var replay = service.Submit(request, player);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.EnvironmentRevision, Is.EqualTo(result.EnvironmentRevision));
            Assert.That(executor.Calls, Is.EqualTo(1));
            current = Snapshot(match: new MatchId("old"));
            var next = new EncounterRequest(new MatchRequestContext(Match, Guid.NewGuid(), player),
                new DefinitionId("Normal"), 10, EncounterRequestSource.DebugWindow);
            Assert.That(service.Submit(next, player).Status, Is.EqualTo(EncounterRequestStatus.MatchUnavailable));
        }
    }
}
