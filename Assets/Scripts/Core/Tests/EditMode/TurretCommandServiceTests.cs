// 정식 터렛 요청의 검증·정책·중복/실패 확정·종료 재진입을 순수 C# 경계에서 검사합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class TurretCommandServiceTests
    {
        private static readonly MatchId Match = new MatchId("match");
        private static readonly PlayerId Player = new PlayerId("participant");
        private static readonly EntityId Target = new EntityId("turret/stable-id");
        private sealed class Policy : ITurretCommandAuthorization
        {
            public bool Allow = true; public Action OnAuthorize;
            public TurretCommandPermission Authorize(TurretCommandRequest request, PlayerId issuer)
            { OnAuthorize?.Invoke(); return Allow ? TurretCommandPermission.Allowed : TurretCommandPermission.Denied("OutOfRange"); }
        }
        private sealed class Executor : ITurretCommandExecutor
        {
            public int Calls; public bool Throws; public Action<TurretCommandRequest> Inside;
            public TurretCommandExecutionResult Response = new TurretCommandExecutionResult(TurretCommandStatus.Applied, "Activated");
            public TurretCommandExecutionResult Execute(TurretCommandRequest request)
            { Calls++; Inside?.Invoke(request); if (Throws) throw new InvalidOperationException("Possible partial mutation"); return Response; }
        }
        private static TurretCommandRequest Request(Guid? id = null, bool on = true, PlayerId? issuer = null, MatchId? match = null)
            => new TurretCommandRequest(new MatchRequestContext(match ?? Match, id ?? Guid.NewGuid(), issuer ?? Player),
                TurretCommandKind.SetActivation, Target, on);
        private static TurretCommandService Service(Executor executor, Policy policy, int capacity = 4096)
            => new TurretCommandService(Match, () => true, () => true, id => id == Player, policy, executor, capacity);

        [Test]
        public void CachesAppliedAndFeatureRejectedResults_AndRejectsChangedIntent()
        {
            var executor = new Executor(); var policy = new Policy(); using var service = Service(executor, policy);
            var request = Request(); var first = service.Submit(request, Player); var replay = service.Submit(request, Player);
            Assert.That(first.Status, Is.EqualTo(TurretCommandStatus.Applied)); Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.ExecutionSequence, Is.EqualTo(first.ExecutionSequence)); Assert.That(executor.Calls, Is.EqualTo(1));
            Assert.That(service.Submit(Request(request.Context.RequestId, false), Player).Status, Is.EqualTo(TurretCommandStatus.RequestConflict));
            executor.Response = new TurretCommandExecutionResult(TurretCommandStatus.FeatureRejected, "CoolingDown");
            var refused = Request(); service.Submit(refused, Player);
            executor.Response = new TurretCommandExecutionResult(TurretCommandStatus.Applied, "Activated");
            var old = service.Submit(refused, Player); Assert.That(old.FeatureCode, Is.EqualTo("CoolingDown")); Assert.That(old.IsReplay, Is.True);
            Assert.That(executor.Calls, Is.EqualTo(2));
        }
        [Test]
        public void RejectsForgedIssuerUnknownParticipantOtherMatchAndMissingAuthority()
        {
            var executor = new Executor(); var policy = new Policy(); using var service = Service(executor, policy);
            var intruder = new PlayerId("intruder");
            Assert.That(service.Submit(Request(), intruder).Status, Is.EqualTo(TurretCommandStatus.Unauthorized));
            Assert.That(service.Submit(Request(issuer: intruder), intruder).Status, Is.EqualTo(TurretCommandStatus.Unauthorized));
            Assert.That(service.Submit(Request(match: new MatchId("old")), Player).Status, Is.EqualTo(TurretCommandStatus.DifferentMatch));
            using var client = new TurretCommandService(Match, () => false, () => true, _ => true, policy, executor);
            Assert.That(client.Submit(Request(), Player).Status, Is.EqualTo(TurretCommandStatus.Unauthorized));
            using var ended = new TurretCommandService(Match, () => true, () => false, _ => true, policy, executor);
            Assert.That(ended.Submit(Request(), Player).Status, Is.EqualTo(TurretCommandStatus.MatchUnavailable));
            Assert.That(executor.Calls, Is.Zero);
        }
        [Test]
        public void ProductPolicyIsRequired_AndItsDenialIsAnIdempotentDecision()
        {
            var executor = new Executor();
            Assert.Throws<ArgumentNullException>(() => new TurretCommandService(Match, () => true, () => true, _ => true, null, executor));
            var policy = new Policy { Allow = false }; using var service = Service(executor, policy); var request = Request();
            Assert.That(service.Submit(request, Player).FeatureCode, Is.EqualTo("OutOfRange"));
            policy.Allow = true;
            Assert.That(service.Submit(request, Player).IsReplay, Is.True);
            Assert.That(executor.Calls, Is.Zero);
            Assert.That(service.Submit(Request(), Player).Status, Is.EqualTo(TurretCommandStatus.Applied));
        }
        [Test]
        public void ReentrantRequestIsInProgress_AndThrowingExecutorIsNeverExecutedAgain()
        {
            var executor = new Executor { Throws = true }; using var service = Service(executor, new Policy());
            TurretCommandResult inner = null; executor.Inside = request => inner = service.Submit(request, Player);
            var input = Request(); var result = service.Submit(input, Player);
            Assert.That(inner.Status, Is.EqualTo(TurretCommandStatus.InProgress));
            Assert.That(result.Status, Is.EqualTo(TurretCommandStatus.ExecutionFailed));
            Assert.That(service.Submit(input, Player).IsReplay, Is.True); Assert.That(executor.Calls, Is.EqualTo(1));
        }
        [Test]
        public void DisposalDuringExecutionKeepsTheActualResultButDoesNotReuseADisposedLedger()
        {
            var executor = new Executor(); var service = Service(executor, new Policy()); executor.Inside = _ => service.Dispose();
            var request = Request(); Assert.That(service.Submit(request, Player).Status, Is.EqualTo(TurretCommandStatus.Applied));
            Assert.That(service.Submit(request, Player).Status, Is.EqualTo(TurretCommandStatus.MatchUnavailable)); service.Dispose();
        }
        [Test]
        public void RevalidatesAuthorityAndLifetimeAfterTheProductPolicyCallback()
        {
            bool authority = true; var executor = new Executor(); var policy = new Policy { OnAuthorize = () => authority = false };
            using var service = new TurretCommandService(Match, () => authority, () => true, _ => true, policy, executor);
            Assert.That(service.Submit(Request(), Player).Status, Is.EqualTo(TurretCommandStatus.Unauthorized));
            Assert.That(executor.Calls, Is.Zero);
            var other = new Policy(); var closing = Service(executor, other); other.OnAuthorize = () => closing.Dispose();
            Assert.That(closing.Submit(Request(), Player).Status, Is.EqualTo(TurretCommandStatus.MatchUnavailable));
            Assert.That(executor.Calls, Is.Zero);
        }
        [Test]
        public void CapacityIsBoundedWithoutEvictingAnExecutedRequest()
        {
            var executor = new Executor(); using var service = Service(executor, new Policy(), 1); var request = Request();
            service.Submit(request, Player);
            Assert.That(service.Submit(Request(), Player).Status, Is.EqualTo(TurretCommandStatus.CapacityExceeded));
            Assert.That(service.Submit(request, Player).IsReplay, Is.True); Assert.That(executor.Calls, Is.EqualTo(1));
        }
        [Test]
        public void RejectsAmbiguousPayloadsBeforeTheyReachTheFeature()
        {
            var context = new MatchRequestContext(Match, Guid.NewGuid(), Player);
            Assert.Throws<ArgumentException>(() => new TurretCommandRequest(context, TurretCommandKind.StartAutoActivation, Target));
            Assert.Throws<ArgumentException>(() => new TurretCommandRequest(context, TurretCommandKind.ApplyUpgrade, Target));
            Assert.Throws<ArgumentException>(() => new TurretCommandRequest(context, TurretCommandKind.UpgradeLevel, Target, true));
            Assert.Throws<ArgumentException>(() => new TurretCommandRequest(context, TurretCommandKind.SetActivation, Target, false, new DefinitionId("irrelevant")));
        }
    }
}
