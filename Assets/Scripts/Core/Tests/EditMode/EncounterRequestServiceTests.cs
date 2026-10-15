// 정식 Encounter 요청의 최신 Snapshot·권한·중복 및 부분 실행 실패를 검증합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class EncounterRequestServiceTests
    {
        private static readonly MatchId Match = new MatchId("match");
        private static readonly PlayerId Host = new PlayerId("host");
        private static EncounterRequest Request(Guid? id = null, int budget = 10, PlayerId? issuer = null, MatchId? match = null)
            => new EncounterRequest(new MatchRequestContext(match ?? Match, id ?? Guid.NewGuid(), issuer ?? Host),
                new DefinitionId("Normal"), budget, EncounterRequestSource.DebugWindow);
        private sealed class Executor : IEncounterExecutor
        {
            public int Calls;
            public bool Throws;
            public Func<EncounterRequest, EncounterRequestResult> Reentrant;
            public BattlefieldSpatialSnapshot LastSnapshot;
            public EncounterRequestResult Execute(EncounterRequest request, BattlefieldSpatialSnapshot snapshot, long executionSequence)
            {
                Calls++; LastSnapshot = snapshot;
                Reentrant?.Invoke(request);
                if (Throws) throw new InvalidOperationException("Partial side effect may already exist");
                return new EncounterRequestResult(request.Context.RequestId, executionSequence, snapshot.Revision,
                    EncounterRequestStatus.PartialSpawn, 10, 3);
            }
        }
        private static BattlefieldSpatialRuntime Spatial() => new BattlefieldSpatialRuntime(Match, BattlefieldSpatialInput.Empty,
            BattlefieldDynamicSpatialInput.Empty, BattlefieldGridConfiguration.Default);
        [Test]
        public void UsesLatestSnapshot_AndCachesPartialResultWithoutExecutingAgain()
        {
            using var spatial = Spatial();
            var executor = new Executor();
            using var service = new EncounterRequestService(Match, () => spatial.Snapshot, () => true, id => id == Host, executor);
            spatial.UpdateDynamicInput(BattlefieldDynamicSpatialInput.Empty);
            EncounterRequest request = Request();
            EncounterRequestResult first = service.Submit(request, Host), replay = service.Submit(request, Host);
            Assert.That(first.Status, Is.EqualTo(EncounterRequestStatus.PartialSpawn));
            Assert.That(first.SpawnedCount, Is.EqualTo(3));
            Assert.That(executor.LastSnapshot, Is.SameAs(spatial.Snapshot));
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.ExecutionSequence, Is.EqualTo(first.ExecutionSequence));
            Assert.That(executor.Calls, Is.EqualTo(1));
            Assert.That(service.Submit(Request(request.Context.RequestId, 20), Host).Status, Is.EqualTo(EncounterRequestStatus.RequestConflict));
        }
        [Test]
        public void RejectsForgedIssuerUnknownParticipantAndOtherMatchBeforeExecuting()
        {
            using var spatial = Spatial(); var executor = new Executor();
            using var service = new EncounterRequestService(Match, () => spatial.Snapshot, () => true, id => id == Host, executor);
            Assert.That(service.Submit(Request(), new PlayerId("intruder")).Status, Is.EqualTo(EncounterRequestStatus.Unauthorized));
            Assert.That(service.Submit(Request(issuer: new PlayerId("intruder")), new PlayerId("intruder")).Status, Is.EqualTo(EncounterRequestStatus.Unauthorized));
            Assert.That(service.Submit(Request(match: new MatchId("old")), Host).Status, Is.EqualTo(EncounterRequestStatus.DifferentMatch));
            Assert.That(executor.Calls, Is.Zero);
        }
        [Test]
        public void MissingAuthorityOrDisposedProviderDoesNotExecute()
        {
            using var spatial = Spatial(); var executor = new Executor(); bool authority = false;
            using var service = new EncounterRequestService(Match, () => spatial.Snapshot, () => authority, _ => true, executor);
            Assert.That(service.Submit(Request(), Host).Status, Is.EqualTo(EncounterRequestStatus.Unauthorized));
            authority = true; spatial.Dispose();
            Assert.That(service.Submit(Request(), Host).Status, Is.EqualTo(EncounterRequestStatus.MatchUnavailable));
            service.Dispose();
            Assert.That(service.Submit(Request(), Host).Status, Is.EqualTo(EncounterRequestStatus.MatchUnavailable));
            Assert.That(executor.Calls, Is.Zero);
        }
        [Test]
        public void ExceptionsAreFinal_NoAutomaticRetryAfterPossiblePartialSideEffects()
        {
            using var spatial = Spatial(); var executor = new Executor { Throws = true };
            using var service = new EncounterRequestService(Match, () => spatial.Snapshot, () => true, _ => true, executor);
            var request = Request();
            Assert.That(service.Submit(request, Host).Status, Is.EqualTo(EncounterRequestStatus.ExecutionFailed));
            Assert.That(service.Submit(request, Host).IsReplay, Is.True);
            Assert.That(executor.Calls, Is.EqualTo(1));
        }
        [Test]
        public void ReentrantDuplicateIsInProgress_AndCapacityDoesNotEvictExecutionHistory()
        {
            using var spatial = Spatial(); var executor = new Executor();
            using var service = new EncounterRequestService(Match, () => spatial.Snapshot, () => true, _ => true, executor, 1);
            executor.Reentrant = request =>
            {
                var result = service.Submit(request, Host);
                Assert.That(result.Status, Is.EqualTo(EncounterRequestStatus.InProgress));
                return result;
            };
            var first = Request();
            service.Submit(first, Host);
            Assert.That(service.Submit(Request(), Host).Status, Is.EqualTo(EncounterRequestStatus.CapacityExceeded));
            Assert.That(service.Submit(first, Host).IsReplay, Is.True);
            Assert.That(executor.Calls, Is.EqualTo(1));
        }
    }
}
