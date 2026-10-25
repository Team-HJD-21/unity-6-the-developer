// 메모리 Port로 발신자 검증·응답 상관관계·용량·재전송·Match 종료 경계를 검사합니다.
// 실제 NGO 패킷이나 두 실행 파일 검증과 구분합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class TurretCommandGatewayTests
    {
        private sealed class Session : INetworkSession
        {
            public bool Server; public ulong Id; public NetworkConnectionState Current;
            public NetworkConnectionState State => Current;
            public bool IsServer => Server;
            public ulong LocalConnectionId => Id;
            public event Action<NetworkConnectionState> StateChanged { add { } remove { } }
            public event Action<ulong> PeerConnected { add { } remove { } }
            public event Action<ulong> PeerDisconnected { add { } remove { } }
            public bool TryStartHost() => false; public bool TryStartClient() => false;
            public void Shutdown() { } public void Dispose() { }
        }
        private sealed class Channel : ITurretCommandTransport
        {
            public ulong Id; public Channel Other; public bool FailRequest, FailResult;
            public event Action<ulong, Guid, TurretCommandRequest> RequestReceived;
            public event Action<ulong, Guid, TurretCommandResult> ResultReceived;
            public bool TrySendRequest(ulong target, Guid descriptor, TurretCommandRequest request)
            { if (FailRequest) return false; Other?.InjectRequest(Id, descriptor, request); return true; }
            public bool TrySendResult(ulong target, Guid descriptor, TurretCommandResult result)
            { if (FailResult) return false; Other?.InjectResult(Id, descriptor, result); return true; }
            public void InjectRequest(ulong sender, Guid descriptor, TurretCommandRequest request) => RequestReceived?.Invoke(sender, descriptor, request);
            public void InjectResult(ulong sender, Guid descriptor, TurretCommandResult result) => ResultReceived?.Invoke(sender, descriptor, result);
        }
        private sealed class Policy : ITurretCommandAuthorization
        { public TurretCommandPermission Authorize(TurretCommandRequest request, PlayerId issuer) => TurretCommandPermission.Allowed; }
        private sealed class Executor : ITurretCommandExecutor
        {
            public int Calls;
            public TurretCommandExecutionResult Execute(TurretCommandRequest request)
            { Calls++; return new TurretCommandExecutionResult(TurretCommandStatus.Applied, "DResult"); }
        }
        private static readonly MatchId Match = new MatchId("match");
        private static readonly PlayerId Host = new PlayerId("host"), Client = new PlayerId("client");
        private static readonly EntityId Target = new EntityId("turret/id");
        private static PlayerId? Resolve(ulong connection) => connection == 0 ? Host : connection == 42 ? Client : (PlayerId?)null;
        private static TurretCommandRequest Request(PlayerId player, Guid? id = null, bool on = true)
            => new TurretCommandRequest(new MatchRequestContext(Match, id ?? Guid.NewGuid(), player), TurretCommandKind.SetActivation, Target, on);
        private static TurretCommandResult Result(TurretCommandRequest request, EntityId? target = null)
            => new TurretCommandResult(Match, request.Context.RequestId, request.Kind, target ?? request.TargetId, 1, TurretCommandStatus.Applied);

        [Test]
        public void ActualIssuerIsResolvedAndRetriesDoNotRepeatFeatureExecution()
        {
            Guid descriptor = Guid.NewGuid(); var server = new Session { Server = true, Id = 0, Current = NetworkConnectionState.Hosting };
            var client = new Session { Id = 42, Current = NetworkConnectionState.ConnectedClient };
            var a = new Channel { Id = 0 }; var b = new Channel { Id = 42 }; a.Other = b; b.Other = a;
            var executor = new Executor();
            using var service = new TurretCommandService(Match, () => true, () => true, id => id == Host || id == Client, new Policy(), executor);
            using var hostGateway = new TurretCommandGateway(Match, descriptor, Host, 0, server, a, () => true, Resolve, service);
            using var clientGateway = new TurretCommandGateway(Match, descriptor, Client, 0, client, b, () => true, Resolve);
            TurretCommandResult received = null; int results = 0; clientGateway.ResultReceived += value => { received = value; results++; };
            var request = Request(Client);
            Assert.That(clientGateway.TrySubmit(request), Is.True); Assert.That(clientGateway.PendingCount, Is.EqualTo(1));
            Assert.That(clientGateway.TryRetry(request.Context.RequestId), Is.True); Assert.That(executor.Calls, Is.EqualTo(1));
            hostGateway.Pump(); Assert.That(received.FeatureCode, Is.EqualTo("DResult"));
            Assert.That(clientGateway.PendingCount, Is.Zero); Assert.That(results, Is.EqualTo(1));
            Assert.That(hostGateway.RejectedMessages, Is.Zero);
            a.InjectRequest(42, descriptor, Request(Host)); hostGateway.Pump();
            Assert.That(executor.Calls, Is.EqualTo(1), "Payload issuer must not replace actual sender.");
            Assert.That(hostGateway.TrySubmit(Request(Host)), Is.True); Assert.That(executor.Calls, Is.EqualTo(2));
        }
        [Test]
        public void UnknownOldGenerationAndWrongRepliesCannotConfirmPendingIntent()
        {
            Guid descriptor = Guid.NewGuid(); var network = new Session { Id = 42, Current = NetworkConnectionState.ConnectedClient };
            var channel = new Channel { Id = 42 };
            using var gateway = new TurretCommandGateway(Match, descriptor, Client, 0, network, channel, () => true, Resolve);
            var request = Request(Client); gateway.TrySubmit(request);
            channel.InjectResult(9, descriptor, Result(request));
            channel.InjectResult(0, Guid.NewGuid(), Result(request));
            channel.InjectResult(0, descriptor, Result(request, new EntityId("other")));
            channel.InjectResult(0, descriptor, Result(Request(Client)));
            Assert.That(gateway.PendingCount, Is.EqualTo(1)); Assert.That(gateway.RejectedMessages, Is.EqualTo(4));
            channel.InjectResult(0, descriptor, new TurretCommandResult(Match, request.Context.RequestId,
                request.Kind, request.TargetId, 0, TurretCommandStatus.InProgress));
            Assert.That(gateway.PendingCount, Is.EqualTo(1), "In-progress is not a final execution outcome.");
            channel.InjectResult(0, descriptor, Result(request)); Assert.That(gateway.PendingCount, Is.Zero);
        }
        [Test]
        public void BoundedPendingFailedSendAndChangedIntentAreExplicit()
        {
            Guid descriptor = Guid.NewGuid(); var network = new Session { Id = 42, Current = NetworkConnectionState.ConnectedClient };
            var channel = new Channel { Id = 42, FailRequest = true };
            using var gateway = new TurretCommandGateway(Match, descriptor, Client, 0, network, channel, () => true, Resolve, capacity: 1);
            var request = Request(Client); Assert.That(gateway.TrySubmit(request), Is.False); Assert.That(gateway.PendingCount, Is.Zero);
            channel.FailRequest = false; Assert.That(gateway.TrySubmit(request), Is.True);
            Assert.That(gateway.TrySubmit(Request(Client)), Is.False);
            Assert.That(gateway.TrySubmit(Request(Client, request.Context.RequestId, false)), Is.False);
            Assert.That(gateway.TryRetry(request.Context.RequestId), Is.True);
            network.Server = true; Assert.That(gateway.TrySubmit(Request(Client)), Is.False);
            Assert.That(gateway.TryRetry(request.Context.RequestId), Is.False);
            gateway.Dispose(); Assert.That(gateway.PendingCount, Is.Zero);
        }
        [Test]
        public void HostReplyBackpressureDoesNotReexecuteAndEndClosesBothDirections()
        {
            Guid descriptor = Guid.NewGuid(); bool running = true;
            var network = new Session { Server = true, Id = 0, Current = NetworkConnectionState.Hosting };
            var channel = new Channel { Id = 0, FailResult = true }; var executor = new Executor();
            using var service = new TurretCommandService(Match, () => true, () => running, id => id == Host || id == Client, new Policy(), executor);
            using var gateway = new TurretCommandGateway(Match, descriptor, Host, 0, network, channel, () => running, Resolve, service, 1);
            channel.InjectRequest(99, descriptor, Request(Client)); channel.InjectRequest(42, Guid.NewGuid(), Request(Client));
            var request = Request(Client); channel.InjectRequest(42, descriptor, request); gateway.Pump();
            channel.InjectRequest(42, descriptor, Request(Client)); Assert.That(executor.Calls, Is.EqualTo(1));
            channel.FailResult = false; gateway.Pump();
            channel.InjectRequest(42, descriptor, request); gateway.Pump(); Assert.That(executor.Calls, Is.EqualTo(1));
            running = false; channel.InjectRequest(42, descriptor, Request(Client));
            Assert.That(gateway.TrySubmit(Request(Host)), Is.False); Assert.That(executor.Calls, Is.EqualTo(1));
        }
    }
}
