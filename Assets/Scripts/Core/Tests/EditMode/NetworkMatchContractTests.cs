// 준비 barrier와 요청 기록의 Match/설정 세대·실제 발신 연결 경계를 검증합니다.
// NGO 연결/패킷 전송 검증은 별도이며 이 테스트로 대체하지 않습니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class NetworkMatchContractTests
    {
        private static readonly PlayerId Host = new PlayerId("host");
        private static readonly PlayerId Client = new PlayerId("client");
        private static MatchConfig Config(params PlayerId[] players)
        {
            var loadouts = new PlayerLoadoutSnapshot[players.Length];
            for (int index = 0; index < players.Length; index++)
                loadouts[index] = new PlayerLoadoutSnapshot(players[index], null);
            return new MatchConfig(new MatchId("match"), MatchMode.Cooperative, new StageId("stage"),
                new DefinitionId("difficulty"), "content", 7, loadouts);
        }

        [Test]
        public void HostOnly_RequiresExplicitReadyAndStart_NoAutomaticServerOpening()
        {
            var barrier = new MatchReadyBarrier(Config(Host), new[] { new MatchParticipantBinding(0, Host, true) });
            Assert.That(barrier.CanStart, Is.False);
            Assert.That(barrier.TryStart(0), Is.False);
            Assert.That(barrier.MarkReady(0, barrier.MatchId, barrier.DescriptorId), Is.EqualTo(MatchReadinessStatus.Accepted));
            Assert.That(barrier.CanStart, Is.True);
            Assert.That(barrier.HasStarted, Is.False);
            Assert.That(barrier.TryStart(0), Is.True);
            Assert.That(barrier.TryStart(0), Is.False);
            Assert.That(barrier.MarkReady(0, barrier.MatchId, barrier.DescriptorId), Is.EqualTo(MatchReadinessStatus.AlreadyStarted));
        }

        [Test]
        public void ConnectedCohort_AllMustBeReady_AndOnlyHostCanStart()
        {
            var barrier = new MatchReadyBarrier(Config(Host, Client), new[]
            {
                new MatchParticipantBinding(0, Host, true),
                new MatchParticipantBinding(21, Client, false)
            });
            barrier.MarkReady(0, barrier.MatchId, barrier.DescriptorId);
            Assert.That(barrier.CanStart, Is.False);
            barrier.MarkReady(21, barrier.MatchId, barrier.DescriptorId);
            Assert.That(barrier.TryStart(21), Is.False);
            Assert.That(barrier.TryStart(0), Is.True);
            Assert.That(barrier.TryResolvePlayer(21, out PlayerId player), Is.True);
            Assert.That(player, Is.EqualTo(Client));
            Assert.That(barrier.TryResolvePlayer(99, out _), Is.False);
        }

        [Test]
        public void Readiness_RejectsUnknownConnectionOtherMatchAndPreviousPreparation()
        {
            var barrier = new MatchReadyBarrier(Config(Host), new[] { new MatchParticipantBinding(0, Host, true) });
            Assert.That(barrier.MarkReady(99, barrier.MatchId, barrier.DescriptorId), Is.EqualTo(MatchReadinessStatus.UnknownConnection));
            Assert.That(barrier.MarkReady(0, new MatchId("other"), barrier.DescriptorId), Is.EqualTo(MatchReadinessStatus.DifferentMatch));
            Assert.That(barrier.MarkReady(0, barrier.MatchId, Guid.NewGuid()), Is.EqualTo(MatchReadinessStatus.DifferentDescriptor));
            Assert.That(barrier.CanStart, Is.False);
            barrier.MarkReady(0, barrier.MatchId, barrier.DescriptorId);
            Assert.That(barrier.MarkReady(0, barrier.MatchId, barrier.DescriptorId), Is.EqualTo(MatchReadinessStatus.AlreadyReady));
        }

        [Test]
        public void Binding_RejectsDuplicatePlayersConnectionsAndMissingHost()
        {
            Assert.Throws<ArgumentException>(() => new MatchReadyBarrier(Config(Host, Client), new[]
            {
                new MatchParticipantBinding(0, Host, true), new MatchParticipantBinding(0, Client, false)
            }));
            Assert.Throws<ArgumentException>(() => new MatchReadyBarrier(Config(Host), new[]
            {
                new MatchParticipantBinding(0, Host, false)
            }));
            Assert.Throws<ArgumentException>(() => new MatchReadyBarrier(Config(Host, Client), new[]
            {
                new MatchParticipantBinding(0, Host, true)
            }));
        }

        [Test]
        public void Ledger_ReturnsSamePartialResult_AndRejectsChangedPayloadOrIssuer()
        {
            using var ledger = new MatchRequestLedger<string>(new MatchId("match"));
            var context = new MatchRequestContext(ledger.MatchId, Guid.NewGuid(), Host);
            Assert.That(ledger.TryBegin(context, "spawn:normal:10", out _), Is.EqualTo(MatchRequestRegistration.Started));
            Assert.That(ledger.TryBegin(context, "spawn:normal:10", out _), Is.EqualTo(MatchRequestRegistration.InProgress));
            ledger.Complete(context.RequestId, "partial:3/10");
            Assert.That(ledger.TryBegin(context, "spawn:normal:10", out string cached), Is.EqualTo(MatchRequestRegistration.Completed));
            Assert.That(cached, Is.EqualTo("partial:3/10"));
            Assert.That(ledger.TryBegin(context, "spawn:normal:20", out _), Is.EqualTo(MatchRequestRegistration.Conflict));
            Assert.That(ledger.TryBegin(new MatchRequestContext(context.MatchId, context.RequestId, Client), "spawn:normal:10", out _),
                Is.EqualTo(MatchRequestRegistration.Conflict));
            Assert.Throws<InvalidOperationException>(() => ledger.Complete(context.RequestId, "overwrite"));
        }

        [Test]
        public void Ledger_RejectsOldMatchAndFullCapacityWithoutEvictingExecutedRequests()
        {
            using var ledger = new MatchRequestLedger<string>(new MatchId("match"), 1);
            var first = new MatchRequestContext(ledger.MatchId, Guid.NewGuid(), Host);
            Assert.That(ledger.TryBegin(new MatchRequestContext(new MatchId("old"), first.RequestId, Host), "key", out _),
                Is.EqualTo(MatchRequestRegistration.DifferentMatch));
            Assert.That(ledger.Count, Is.Zero);
            ledger.TryBegin(first, "key", out _);
            ledger.Complete(first.RequestId, "done");
            Assert.That(ledger.TryBegin(new MatchRequestContext(ledger.MatchId, Guid.NewGuid(), Host), "key", out _),
                Is.EqualTo(MatchRequestRegistration.CapacityExceeded));
            Assert.That(ledger.TryBegin(first, "key", out string result), Is.EqualTo(MatchRequestRegistration.Completed));
            Assert.That(result, Is.EqualTo("done"));
        }

        [Test]
        public void Ledger_DisposeClearsScopeAndRejectsReuse()
        {
            var ledger = new MatchRequestLedger<string>(new MatchId("match"));
            ledger.Dispose();
            ledger.Dispose();
            Assert.Throws<ObjectDisposedException>(() => { _ = ledger.Count; });
        }
    }
}
