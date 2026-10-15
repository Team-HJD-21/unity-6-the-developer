// App 정리 중 서비스 실패가 있어도 Match와 나머지 서비스가 정리되는지 검증합니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure.Fakes;
using UnityEngine;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class AppShutdownTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Dispose_CleansRemainingServicesAndIsIdempotent(bool failProfile)
        {
            var owner = new GameObject("Shutdown Test");
            var root = owner.AddComponent<AppRoot>();
            var profiles = new ProbeProfiles { Fail = failProfile };
            var scenes = new ProbeScenes();
            var network = new ProbeNetwork();
            var services = new AppServices(profiles, new FakeContentCatalog(),
                new FakePlatformService(new PlatformUser(new PlayerId("test"), "Test")),
                scenes, new LocalAuthority(), network);
            root.Initialize(services);
            var transport = new ProbeTransport();
            root.AttachNetworkMatchTransport(transport);
            var regionTransport = new ProbeRegionTransport();
            root.AttachRegionSnapshotTransport(regionTransport);
            var turretTransport = new ProbeTurretTransport();
            root.AttachTurretCommandTransport(turretTransport);
            Assert.Throws<InvalidOperationException>(() => root.AttachTurretCommandTransport(new ProbeTurretTransport()));
            services.PumpNetwork();
            try
            {
                if (failProfile) Assert.Throws<AggregateException>(() => root.Dispose());
                else Assert.DoesNotThrow(() => root.Dispose());
                Assert.That(profiles.DisposedCount, Is.EqualTo(1));
                Assert.That(scenes.DisposedCount, Is.EqualTo(1));
                Assert.That(network.DisposedCount, Is.EqualTo(1));
                Assert.That(root.NetworkSession, Is.Null);
                Assert.That(root.NetworkMatches, Is.Null);
                Assert.That(transport.Pumps, Is.EqualTo(1));
                Assert.That(transport.Disposals, Is.EqualTo(1));
                Assert.That(root.NetworkRegions, Is.Null);
                Assert.That(regionTransport.Pumps, Is.EqualTo(1));
                Assert.That(regionTransport.Disposals, Is.EqualTo(1));
                Assert.That(root.NetworkTurretCommands, Is.Null);
                Assert.That(turretTransport.Pumps, Is.EqualTo(1));
                Assert.That(turretTransport.Disposals, Is.EqualTo(1));
                Assert.That(root.CurrentMatchId.IsEmpty, Is.True);
                Assert.DoesNotThrow(() => root.Dispose());
                Assert.That(scenes.DisposedCount, Is.EqualTo(1));
                Assert.Throws<ObjectDisposedException>(() => root.ReconfigureBattlefieldGrid(BattlefieldGridConfiguration.Default));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        private sealed class ProbeProfiles : IProfileService, IDisposable
        {
            public bool Fail;
            public int DisposedCount;
            public Task<PlayerProfileSnapshot> LoadAsync(PlayerId id, CancellationToken cancellationToken)
                => Task.FromResult<PlayerProfileSnapshot>(null);
            public Task SaveAsync(PlayerProfileSnapshot profile, CancellationToken cancellationToken)
                => Task.CompletedTask;
            public void Dispose()
            {
                DisposedCount++;
                if (Fail) throw new InvalidOperationException("profile cleanup");
            }
        }

        private sealed class ProbeScenes : ISceneFlow, IDisposable
        {
            public int DisposedCount;
            public Task LoadAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
            public Task UnloadAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
            public void Dispose() => DisposedCount++;
        }

        private sealed class ProbeNetwork : INetworkSession
        {
            public int DisposedCount;
            public NetworkConnectionState State => NetworkConnectionState.Disconnected;
            public bool IsServer => false;
            public ulong LocalConnectionId => 0;
            public event Action<NetworkConnectionState> StateChanged { add { } remove { } }
            public event Action<ulong> PeerConnected { add { } remove { } }
            public event Action<ulong> PeerDisconnected { add { } remove { } }
            public bool TryStartHost() => false;
            public bool TryStartClient() => false;
            public void Shutdown() { }
            public void Dispose() => DisposedCount++;
        }
        private sealed class ProbeTransport : IMatchLifecycleTransport, INetworkMessagePump, IDisposable
        {
            public int Pumps, Disposals;
            public event Action<ulong, NetworkMatchDescriptor> DescriptorReceived { add { } remove { } }
            public event Action<ulong, MatchLifecycleSignalKind, MatchLifecycleSignal> SignalReceived { add { } remove { } }
            public bool TrySendDescriptor(ulong connectionId, NetworkMatchDescriptor descriptor) => false;
            public bool TrySendSignal(ulong connectionId, MatchLifecycleSignalKind kind, MatchLifecycleSignal signal) => false;
            public void Pump() => Pumps++;
            public void Dispose() => Disposals++;
        }
        private sealed class ProbeRegionTransport : IRegionSnapshotTransport, INetworkMessagePump, IDisposable
        {
            public int Pumps, Disposals;
            public event Action<ulong, RegionReplicationFrame> FrameReceived { add { } remove { } }
            public bool TrySendFrame(ulong connectionId, RegionReplicationFrame frame) => false;
            public void Pump() => Pumps++;
            public void Dispose() => Disposals++;
        }
        private sealed class ProbeTurretTransport : ITurretCommandTransport, INetworkMessagePump, IDisposable
        {
            public int Pumps, Disposals;
            public event Action<ulong, Guid, TurretCommandRequest> RequestReceived { add { } remove { } }
            public event Action<ulong, Guid, TurretCommandResult> ResultReceived { add { } remove { } }
            public bool TrySendRequest(ulong connectionId, Guid descriptorId, TurretCommandRequest request) => false;
            public bool TrySendResult(ulong connectionId, Guid descriptorId, TurretCommandResult result) => false;
            public void Pump() => Pumps++;
            public void Dispose() => Disposals++;
        }
    }
}
