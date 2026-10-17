// Host/Client 준비·설정 일치·권한·종료를 실제 Application 조율기에 대조합니다.
// 메모리 전송 Port 검사이며 실제 NGO 두 프로세스 검증을 대체하지 않습니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class NetworkMatchCoordinatorTests
    {
        private sealed class Session : INetworkSession
        {
            public bool Server; public ulong Id; public int ShutdownCount;
            public NetworkConnectionState State { get; private set; }
            public bool IsServer => Server;
            public ulong LocalConnectionId => Id;
            public event Action<NetworkConnectionState> StateChanged;
            public event Action<ulong> PeerConnected { add { } remove { } }
            public event Action<ulong> PeerDisconnected;
            public Session(bool server, ulong id) { Server = server; Id = id; State = server ? NetworkConnectionState.Hosting : NetworkConnectionState.ConnectedClient; }
            public bool TryStartHost() => false;
            public bool TryStartClient() => false;
            public void Shutdown() => ShutdownCount++;
            public void Dispose() { }
            public void Disconnect(ulong id) => PeerDisconnected?.Invoke(id);
            public void SetState(NetworkConnectionState state) { State = state; StateChanged?.Invoke(state); }
        }
        private sealed class Channel : IMatchLifecycleTransport
        {
            public ulong Id; public Channel Other; public bool Fail;
            public event Action<ulong, NetworkMatchDescriptor> DescriptorReceived;
            public event Action<ulong, MatchLifecycleSignalKind, MatchLifecycleSignal> SignalReceived;
            public bool TrySendDescriptor(ulong target, NetworkMatchDescriptor descriptor)
            { if (Fail || Other == null) return false; Other.DescriptorReceived?.Invoke(Id, descriptor); return true; }
            public bool TrySendSignal(ulong target, MatchLifecycleSignalKind kind, MatchLifecycleSignal signal)
            { if (Fail || Other == null) return false; Other.SignalReceived?.Invoke(Id, kind, signal); return true; }
            public void InjectSignal(ulong sender, MatchLifecycleSignalKind kind, MatchLifecycleSignal signal) => SignalReceived?.Invoke(sender, kind, signal);
            public void InjectDescriptor(ulong sender, NetworkMatchDescriptor descriptor) => DescriptorReceived?.Invoke(sender, descriptor);
        }
        private static readonly PlayerId Host = new PlayerId("host"), Client = new PlayerId("client");
        private static MatchConfig Config(bool client = false) => new MatchConfig(new MatchId("match"), MatchMode.Cooperative,
            new StageId("initial-stage"), new DefinitionId("difficulty"), "content-v1", 77, client
                ? new[] { new PlayerLoadoutSnapshot(Host, null), new PlayerLoadoutSnapshot(Client, null) }
                : new[] { new PlayerLoadoutSnapshot(Host, null) });
        private static MatchParticipantBinding[] Bindings(bool client = false) => client
            ? new[] { new MatchParticipantBinding(0, Host, true), new MatchParticipantBinding(42, Client, false) }
            : new[] { new MatchParticipantBinding(0, Host, true) };
        private static RegionGridDefinition Grid() => new RegionGridDefinition(BattlefieldGridConfiguration.Default);
        [Test]
        public void DescriptorNormalizesBindingsToTheAuthoritativeConfigSlotOrder()
        {
            var descriptor = new NetworkMatchDescriptor(Guid.NewGuid(), 1, Config(true), RegionPolicyMode.Static,
                Grid(), new[] { Bindings(true)[1], Bindings(true)[0] });
            Assert.That(descriptor.Participants[0].PlayerId, Is.EqualTo(Host));
            Assert.That(descriptor.TryGetSlot(42, out int slot), Is.True);
            Assert.That(slot, Is.EqualTo(1));
            Assert.That(descriptor.TryGetSlot(99, out slot), Is.False);
            Assert.That(slot, Is.EqualTo(-1));
        }
        [Test]
        public void SoloHostRequiresExplicitPrepareReadyAndStart_AndDoesNotOwnConnection()
        {
            var session = new Session(true, 0); var channel = new Channel { Id = 0 };
            var runtime = new NetworkMatchCoordinator(session, channel);
            Assert.That(runtime.TryStart(), Is.False);
            Assert.That(runtime.TryPrepare(Config(), Grid(), RegionPolicyMode.Static, Bindings()), Is.True);
            Assert.That(runtime.TryStart(), Is.False);
            Assert.That(runtime.MarkLocalReady(), Is.True);
            Assert.That(runtime.Phase, Is.EqualTo(NetworkMatchPhase.Preparing));
            Assert.That(runtime.TryStart(), Is.True);
            Assert.That(runtime.TryStart(), Is.False);
            Assert.That(runtime.TryEnd(), Is.True);
            Assert.That(runtime.TryPrepare(Config(), Grid(), RegionPolicyMode.Dynamic, Bindings()), Is.True);
            runtime.Dispose(); runtime.Dispose();
            Assert.That(session.ShutdownCount, Is.Zero);
        }
        [Test]
        public void TwoParticipantsShareHostDescriptor_AndBothMustBeReadyBeforeHostStart()
        {
            var hostSession = new Session(true, 0); var clientSession = new Session(false, 42);
            var hostChannel = new Channel { Id = 0 }; var clientChannel = new Channel { Id = 42 };
            hostChannel.Other = clientChannel; clientChannel.Other = hostChannel;
            using var host = new NetworkMatchCoordinator(hostSession, hostChannel);
            using var client = new NetworkMatchCoordinator(clientSession, clientChannel);
            Assert.That(host.TryPrepare(Config(true), Grid(), RegionPolicyMode.Dynamic, Bindings(true)), Is.True);
            Assert.That(client.Descriptor.Config.MatchId, Is.EqualTo(host.Descriptor.Config.MatchId));
            Assert.That(client.Descriptor.Config.RandomSeed, Is.EqualTo(77));
            Assert.That(client.Descriptor.RegionMode, Is.EqualTo(RegionPolicyMode.Dynamic));
            host.MarkLocalReady(); Assert.That(host.TryStart(), Is.False);
            client.MarkLocalReady(); Assert.That(client.TryStart(), Is.False);
            Assert.That(host.TryStart(), Is.True);
            Assert.That(client.Phase, Is.EqualTo(NetworkMatchPhase.Running));
            Assert.That(host.TryResolveParticipant(42, out PlayerId player), Is.True); Assert.That(player, Is.EqualTo(Client));
            Assert.That(host.TryResolveParticipant(99, out _), Is.False);
            host.TryEnd(); Assert.That(client.Phase, Is.EqualTo(NetworkMatchPhase.Ended));
            Assert.That(client.Descriptor, Is.Null);
        }
        [Test]
        public void ForgedReadyStartAndPreviousDescriptorDoNotChangeState()
        {
            var hs = new Session(true, 0); var cs = new Session(false, 42);
            var hc = new Channel { Id = 0 }; var cc = new Channel { Id = 42 }; hc.Other = cc; cc.Other = hc;
            using var host = new NetworkMatchCoordinator(hs, hc); using var client = new NetworkMatchCoordinator(cs, cc);
            host.TryPrepare(Config(true), Grid(), RegionPolicyMode.Static, Bindings(true)); host.MarkLocalReady();
            var first = host.Descriptor; var signal = new MatchLifecycleSignal(first.Config.MatchId, first.DescriptorId);
            hc.InjectSignal(99, MatchLifecycleSignalKind.Ready, signal);
            Assert.That(host.TryStart(), Is.False);
            cc.InjectSignal(99, MatchLifecycleSignalKind.Started, signal);
            Assert.That(client.Phase, Is.EqualTo(NetworkMatchPhase.Preparing));
            client.MarkLocalReady();
            host.TryPrepare(Config(true), Grid(), RegionPolicyMode.Dynamic, Bindings(true));
            cc.InjectDescriptor(0, first);
            hc.InjectSignal(42, MatchLifecycleSignalKind.Ready, signal);
            Assert.That(client.Descriptor.RegionMode, Is.EqualTo(RegionPolicyMode.Dynamic));
            host.MarkLocalReady(); Assert.That(host.TryStart(), Is.False);
        }
        [Test]
        public void CohortDisconnectEndsBothSides_ObserverFailureDoesNotSkipCleanup()
        {
            var hs = new Session(true, 0); var cs = new Session(false, 42);
            var hc = new Channel { Id = 0 }; var cc = new Channel { Id = 42 }; hc.Other = cc; cc.Other = hc;
            using var host = new NetworkMatchCoordinator(hs, hc); using var client = new NetworkMatchCoordinator(cs, cc);
            host.TryPrepare(Config(true), Grid(), RegionPolicyMode.Static, Bindings(true));
            int cleanup = 0;
            host.Ended += _ => throw new InvalidOperationException("feature");
            host.Ended += _ => cleanup++;
            Assert.Throws<AggregateException>(() => hs.Disconnect(42));
            Assert.That(cleanup, Is.EqualTo(1)); Assert.That(host.Descriptor, Is.Null);
            Assert.That(client.Descriptor, Is.Null);
        }
        [Test]
        public void SendFailureCancelsPreparationInsteadOfStartingOnlyHost()
        {
            var channel = new Channel { Id = 0, Fail = true };
            using var runtime = new NetworkMatchCoordinator(new Session(true, 0), channel);
            Assert.That(runtime.TryPrepare(Config(true), Grid(), RegionPolicyMode.Static, Bindings(true)), Is.False);
            Assert.That(runtime.Descriptor, Is.Null); Assert.That(runtime.CanStart, Is.False);
        }
    }
}
