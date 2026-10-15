// 실제 UTP에서 전체 HUD 투영·최신 합치기·세대/충돌 거부·재접속·상태 주입 차단을 검사합니다.
// 표시 DTO 공급 fixture이며 실제 D/Player 조립의 두 실행 파일 검증과 구분합니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = TeamHJD.Game.Domain.EntityId;
namespace TeamHJD.Game.Infrastructure.Tests
{
    public sealed class NgoTurretPresentationTransportTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private NgoNetworkSession _host, _client;
        private NetworkManager _hostManager, _clientManager;
        private NgoMatchLifecycleTransport _hostLifecycle, _clientLifecycle;
        private NetworkMatchCoordinator _hostMatch, _clientMatch;
        private NgoTurretPresentationTransport _hostTransport, _clientTransport;
        private TurretPresentationReplication _hostReplication, _clientReplication;
        private TurretPresentationReplica _replica;
        private Source _source;
        private sealed class Source : ITurretPresentationSource
        {
            public TurretPresentationFrame Snapshot { get; private set; }
            public event Action<TurretPresentationFrame> Changed;
            public void Set(TurretPresentationFrame frame) { Snapshot = frame; Changed?.Invoke(frame); }
        }
        private NetworkManager Manager(string name, ushort port)
        {
            var go = new GameObject(name); _objects.Add(go);
            var manager = go.AddComponent<NetworkManager>(); var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, PlayerPrefab = null };
            return manager;
        }
        private static TurretPresentationFrame Frame(NetworkMatchDescriptor descriptor, long revision, int health = 100)
        {
            var rows = new List<TurretPresentationSnapshot>();
            for (int index = 0; index < 32; index++) rows.Add(new TurretPresentationSnapshot(new EntityId("turret/" + index),
                new DefinitionId("canon"), "Canon", new BattlefieldPoint(index, index), 1, health, 100, 10, 5, 11, 0, default, 0, TurretPresentationFlags.Initialized));
            return new TurretPresentationFrame(descriptor.Config.MatchId, descriptor.DescriptorId, revision,
                new TurretControllerPresentationSnapshot(100, 100, 0, 0, 1000, 1000, default, false, 0, RegionPolicyMode.Dynamic), rows, revision);
        }
        private void Pump()
        {
            _hostLifecycle.Pump(); _clientLifecycle.Pump(); _hostReplication?.Pump(); _clientReplication?.Pump();
            _hostTransport.Pump(); _clientTransport?.Pump();
        }
        [UnityTest]
        public IEnumerator ActualPacketsReplicateOnlyHostStateAndSurvivePreparationAndConnectionRestart()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None), Is.Empty);
            ushort port; using (var socket = new UdpClient(0)) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            _hostManager = Manager("Presentation Host", port); _clientManager = Manager("Presentation Client", port);
            _host = new NgoNetworkSession(_hostManager); _client = new NgoNetworkSession(_clientManager);
            NetworkMatchDescriptor previous = null;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                Assert.That(_host.TryStartHost(), Is.True); Assert.That(_client.TryStartClient(), Is.True);
                float deadline = Time.realtimeSinceStartup + 20;
                while (!_clientManager.IsConnectedClient && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(_clientManager.IsConnectedClient, Is.True);
                if (_hostLifecycle == null)
                {
                    _hostLifecycle = new NgoMatchLifecycleTransport(_hostManager); _clientLifecycle = new NgoMatchLifecycleTransport(_clientManager);
                    _hostTransport = new NgoTurretPresentationTransport(_hostManager); _clientTransport = new NgoTurretPresentationTransport(_clientManager);
                    _hostMatch = new NetworkMatchCoordinator(_host, _hostLifecycle); _clientMatch = new NetworkMatchCoordinator(_client, _clientLifecycle);
                }
                var hostPlayer = new PlayerId("host"); var clientPlayer = new PlayerId("client");
                var config = new MatchConfig(new MatchId("same-match"), MatchMode.Cooperative, new StageId("initial-stage"),
                    new DefinitionId("difficulty"), "poc", 1, new[] { new PlayerLoadoutSnapshot(hostPlayer, null), new PlayerLoadoutSnapshot(clientPlayer, null) });
                Assert.That(_hostMatch.TryPrepare(config, new RegionGridDefinition(BattlefieldGridConfiguration.Default), RegionPolicyMode.Dynamic,
                    new[] { new MatchParticipantBinding(_host.LocalConnectionId, hostPlayer, true), new MatchParticipantBinding(_client.LocalConnectionId, clientPlayer, false) }), Is.True);
                Assert.That(_hostMatch.MarkLocalReady(), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (_clientMatch.Descriptor == null && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_clientMatch.MarkLocalReady(), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (!_hostMatch.CanStart && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_hostMatch.TryStart(), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (_clientMatch.Phase != NetworkMatchPhase.Running && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_clientMatch.Phase, Is.EqualTo(NetworkMatchPhase.Running));
                _source = new Source(); _source.Set(Frame(_hostMatch.Descriptor, 1));
                _replica = new TurretPresentationReplica(config.MatchId, _clientMatch.Descriptor.DescriptorId);
                _hostReplication = new TurretPresentationReplication(_host, _hostMatch, _hostTransport, _source, null);
                _clientReplication = new TurretPresentationReplication(_client, _clientMatch, _clientTransport, null, _replica);
                deadline = Time.realtimeSinceStartup + 20;
                while (_replica.Snapshot == null && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_replica.Snapshot, Is.Not.Null); Assert.That(_replica.Snapshot.HasSameContent(_source.Snapshot), Is.True);
                _source.Set(Frame(_hostMatch.Descriptor, 2)); _hostReplication.Pump();
                _source.Set(Frame(_hostMatch.Descriptor, 3)); _hostReplication.Pump();
                Assert.That(_hostTransport.CoalescedFrames, Is.GreaterThan(0));
                deadline = Time.realtimeSinceStartup + 20;
                while (_replica.Snapshot.Revision < 3 && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_replica.Snapshot.Revision, Is.EqualTo(3));
                Assert.That(_clientTransport.TrySendFrame(NetworkManager.ServerClientId, _source.Snapshot), Is.False);
                long rejected = _clientReplication.RejectedFrames;
                Assert.That(_hostTransport.TrySendFrame(_client.LocalConnectionId, Frame(_hostMatch.Descriptor, 3, 90)), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (_clientReplication.RejectedFrames == rejected && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_clientReplication.RejectedFrames, Is.GreaterThan(rejected)); Assert.That(_replica.Snapshot.Turrets[0].CurrentHealth, Is.EqualTo(100));
                if (previous != null)
                {
                    rejected = _clientReplication.RejectedFrames;
                    Assert.That(_hostTransport.TrySendFrame(_client.LocalConnectionId, Frame(previous, 10)), Is.True);
                    deadline = Time.realtimeSinceStartup + 20;
                    while (_clientReplication.RejectedFrames == rejected && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                    Assert.That(_clientReplication.RejectedFrames, Is.GreaterThan(rejected));
                }
                previous = _hostMatch.Descriptor;
                if (cycle == 1)
                {
                    _clientTransport.Dispose(); _clientTransport = null;
                    using var forged = new NgoMessageChannel(_clientManager, "teamhjd.core.turret-presentation.v1", 0);
                    long rejectedPackets = _hostTransport.RejectedPackets;
                    Assert.That(forged.TrySend(NetworkManager.ServerClientId, TurretPresentationCodec.Encode(_source.Snapshot)), Is.True);
                    deadline = Time.realtimeSinceStartup + 20;
                    while (_hostTransport.RejectedPackets == rejectedPackets && Time.realtimeSinceStartup < deadline)
                    { forged.Pump(); _hostTransport.Pump(); yield return null; }
                    Assert.That(_hostTransport.RejectedPackets, Is.GreaterThan(rejectedPackets));
                    Assert.That(_source.Snapshot.Revision, Is.EqualTo(3));
                }
                Assert.That(_hostMatch.TryEnd(), Is.True); deadline = Time.realtimeSinceStartup + 20;
                while (_clientMatch.Phase != NetworkMatchPhase.Ended && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_clientMatch.Phase, Is.EqualTo(NetworkMatchPhase.Ended));
                _hostReplication.Dispose(); _clientReplication.Dispose(); _replica.Dispose();
                _hostReplication = _clientReplication = null; _replica = null;
                _client.Shutdown(); _host.Shutdown(); deadline = Time.realtimeSinceStartup + 20;
                while ((_hostManager.IsListening || _clientManager.IsListening || _hostManager.ShutdownInProgress || _clientManager.ShutdownInProgress)
                    && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(_hostManager.IsListening || _clientManager.IsListening, Is.False);
            }
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            _hostReplication?.Dispose(); _clientReplication?.Dispose(); _replica?.Dispose();
            _hostMatch?.Dispose(); _clientMatch?.Dispose(); _hostTransport?.Dispose(); _clientTransport?.Dispose();
            _hostLifecycle?.Dispose(); _clientLifecycle?.Dispose(); _client?.Dispose(); _host?.Dispose();
            foreach (var go in _objects) if (go != null) UnityEngine.Object.Destroy(go);
            _objects.Clear(); yield return null;
        }
    }
}
