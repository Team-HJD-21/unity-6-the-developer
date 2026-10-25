// 동적 source의 권한·등록 세대·취소와 origin별 요청 중복 경계를 검사합니다.
// 실제 Enemy 생성·위치 검증은 별도의 Feature 통합 검사로 확인합니다.
using System;
using System.Threading;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class EncounterSpawnSourceTests
    {
        private static readonly MatchId Match = new MatchId("source-match");
        private static readonly DefinitionId Tree = new DefinitionId("tree-1");
        private static SpawnArea Area(double x = 0) => new SpawnArea(new BattlefieldPoint(x, 0), 2, 4);
        [Test]
        public void SourceRemovalCancelsLease_AndReusedLogicalIdDoesNotReviveOldRequests()
        {
            using var gameplay = new CancellationTokenSource();
            using var sources = new EncounterSpawnSourceRegistry(Match, () => true, gameplay.Token);
            var lease = sources.Register(Match, Tree, Area());
            var origin = EncounterSpawnOrigin.FromSource(lease.Reference);
            Assert.That(lease.TryUpdate(Area(10)), Is.True);
            Assert.That(sources.Resolve(origin).Area.Center.X, Is.EqualTo(10));
            lease.Dispose();
            Assert.That(lease.Cancellation.IsCancellationRequested, Is.True);
            Assert.That(sources.Resolve(origin), Is.Null);
            using var replacement = sources.Register(Match, Tree, Area());
            Assert.That(replacement.Reference.Generation, Is.Not.EqualTo(lease.Reference.Generation));
            Assert.That(sources.Resolve(origin), Is.Null);
            gameplay.Cancel();
            Assert.That(replacement.Cancellation.IsCancellationRequested, Is.True);
            Assert.That(sources.Resolve(EncounterSpawnOrigin.FromSource(replacement.Reference)), Is.Null);
        }
        [Test]
        public void AuthorityMatchAndDuplicateChecksDoNotMutateTheRegistry()
        {
            bool authority = false;
            using var sources = new EncounterSpawnSourceRegistry(Match, () => authority, default);
            Assert.Throws<InvalidOperationException>(() => sources.Register(Match, Tree, Area()));
            authority = true;
            Assert.Throws<InvalidOperationException>(() => sources.Register(new MatchId("other"), Tree, Area()));
            using var lease = sources.Register(Match, Tree, Area());
            Assert.Throws<InvalidOperationException>(() => sources.Register(Match, Tree, Area()));
            authority = false;
            Assert.That(lease.TryUpdate(Area(4)), Is.False);
            Assert.That(sources.Resolve(EncounterSpawnOrigin.AtWorld(Area())), Is.Null);
        }
        private sealed class Executor : IOriginAwareEncounterExecutor
        {
            public int Calls;
            public EncounterSpawnPlacement Last;
            public EncounterRequestResult Execute(EncounterRequest r, BattlefieldSpatialSnapshot s, long sequence)
                => throw new InvalidOperationException("An explicit origin is required.");
            public EncounterRequestResult Execute(EncounterRequest r, BattlefieldSpatialSnapshot s, MatchEnvironmentSnapshot e, long sequence)
                => throw new InvalidOperationException("An explicit origin is required.");
            public EncounterRequestResult Execute(EncounterRequest r, BattlefieldSpatialSnapshot s, MatchEnvironmentSnapshot e,
                EncounterSpawnPlacement placement, long sequence)
            {
                Calls++; Last = placement;
                return new EncounterRequestResult(r.Context.RequestId, sequence, s.Revision, EncounterRequestStatus.Spawned, 1, 1);
            }
        }
        [Test]
        public void CommonRequestResolvesCurrentSourceOnce_AndCachesSuccessAfterSourceRemoval()
        {
            using var sources = new EncounterSpawnSourceRegistry(Match, () => true, default);
            using var lease = sources.Register(Match, Tree, Area());
            using var battlefield = new BattlefieldSpatialRuntime(Match, BattlefieldSpatialInput.Empty,
                BattlefieldDynamicSpatialInput.Empty, BattlefieldGridConfiguration.Default);
            var executor = new Executor(); var host = new PlayerId("host");
            using var service = new EncounterRequestService(Match, () => battlefield.Snapshot, () => true, _ => true, executor,
                resolveOrigin: sources.Resolve);
            var request = new EncounterRequest(new MatchRequestContext(Match, Guid.NewGuid(), host), new DefinitionId("Normal"), 10,
                EncounterRequestSource.Unspecified, EncounterSpawnOrigin.FromSource(lease.Reference), Guid.NewGuid());
            Assert.That(service.Submit(request, host).Status, Is.EqualTo(EncounterRequestStatus.Spawned));
            lease.Dispose();
            Assert.That(service.Submit(request, host).IsReplay, Is.True);
            Assert.That(executor.Calls, Is.EqualTo(1));
            var changed = new EncounterRequest(request.Context, request.PresetId, 10, request.Source,
                EncounterSpawnOrigin.AtWorld(Area()), request.CauseEventId);
            Assert.That(service.Submit(changed, host).Status, Is.EqualTo(EncounterRequestStatus.RequestConflict));
            var expired = new EncounterRequest(new MatchRequestContext(Match, Guid.NewGuid(), host), request.PresetId, 10,
                request.Source, request.Origin, request.CauseEventId);
            Assert.That(service.Submit(expired, host).Status, Is.EqualTo(EncounterRequestStatus.OriginUnavailable));
            Assert.That(executor.Calls, Is.EqualTo(1));
        }
        [Test]
        public void OriginPayloadIsCultureIndependent_AndDeliveryAndCauseAreDistinctIntents()
        {
            var before = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                var area = new SpawnArea(new BattlefieldPoint(1.5, 3.25), 2, 4);
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("ko-KR");
                string first = EncounterSpawnOrigin.AtWorld(area).GetPayloadKey();
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(EncounterSpawnOrigin.AtWorld(area).GetPayloadKey(), Is.EqualTo(first));
                Assert.That(EncounterSpawnOrigin.AtWorld(area, SpawnDeliveryMode.AirDrop).GetPayloadKey(), Is.Not.EqualTo(first));
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = before; }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void ResolverCannotReplaceTheIntentOrExecuteAfterClosingTheRequestScope(bool disposeDuringResolution)
        {
            using var battlefield = new BattlefieldSpatialRuntime(Match, BattlefieldSpatialInput.Empty,
                BattlefieldDynamicSpatialInput.Empty, BattlefieldGridConfiguration.Default);
            var executor = new Executor(); var host = new PlayerId("host");
            var origin = EncounterSpawnOrigin.AtWorld(Area());
            EncounterRequestService service = null;
            service = new EncounterRequestService(Match, () => battlefield.Snapshot, () => true, _ => true, executor,
                resolveOrigin: requested =>
                {
                    if (disposeDuringResolution) service.Dispose();
                    return new EncounterSpawnPlacement(disposeDuringResolution ? requested : EncounterSpawnOrigin.AtWorld(Area(20)), Area());
                });
            using (service)
            {
                var request = new EncounterRequest(new MatchRequestContext(Match, Guid.NewGuid(), host),
                    new DefinitionId("Normal"), 10, EncounterRequestSource.Unspecified, origin);
                Assert.That(service.Submit(request, host).Status, Is.EqualTo(EncounterRequestStatus.OriginUnavailable));
                Assert.That(executor.Calls, Is.Zero);
            }
        }
    }
}
