// 제한된 메시지 복원과 실제 UTP Host/Client의 준비·시작·종료 패킷을 확인합니다.
// 같은 프로세스의 두 SDK 인스턴스 검사이며 별도 실행 파일 두 개의 검증과 구분합니다.
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
namespace TeamHJD.Game.Infrastructure.Tests
{
    public sealed class NgoMatchLifecycleTransportTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private NgoNetworkSession _hostSession, _clientSession;
        private NgoMatchLifecycleTransport _hostChannel, _clientChannel;
        private NetworkMatchCoordinator _host, _client;
        private static NetworkMatchDescriptor Descriptor()
        {
            int[] regions = new int[256]; for (int i = 0; i < regions.Length; i++) regions[i] = i % 2 + 1;
            PlayerId a = new PlayerId("host"), b = new PlayerId("client");
            return new NetworkMatchDescriptor(Guid.NewGuid(), 1,
                new MatchConfig(new MatchId("match"), MatchMode.Cooperative, new StageId("stage"), new DefinitionId("difficulty"),
                    "content", 41, new[] { new PlayerLoadoutSnapshot(a, new Dictionary<DefinitionId, int> { [new DefinitionId("armor")] = 3 }),
                        new PlayerLoadoutSnapshot(b, null) }), RegionPolicyMode.Dynamic,
                new RegionGridDefinition(BattlefieldGridConfiguration.Default, regions),
                new[] { new MatchParticipantBinding(0, a, true), new MatchParticipantBinding(1, b, false) });
        }
        [Test]
        public void CodecRoundTripPreservesSettingsMembershipParticipantsAndSignal()
        {
            var original = Descriptor(); byte[] bytes = MatchLifecycleCodec.Encode(original);
            Assert.That(bytes.Length, Is.GreaterThan(BoundedPacketAssembler.ChunkBytes));
            MatchLifecycleCodec.Decode(bytes, out var decoded, out _, out _);
            Assert.That(decoded.DescriptorId, Is.EqualTo(original.DescriptorId));
            Assert.That(decoded.Config.RandomSeed, Is.EqualTo(41));
            Assert.That(decoded.Config.Players[0].UpgradeLevels[new DefinitionId("armor")], Is.EqualTo(3));
            Assert.That(decoded.RegionGrid.RegionIds, Is.EqualTo(original.RegionGrid.RegionIds));
            Assert.That(decoded.Participants[1].PlayerId.Value, Is.EqualTo("client"));
            var signal = new MatchLifecycleSignal(original.Config.MatchId, original.DescriptorId);
            MatchLifecycleCodec.Decode(MatchLifecycleCodec.Encode(MatchLifecycleSignalKind.Started, signal),
                out var noDescriptor, out var kind, out var copy);
            Assert.That(noDescriptor, Is.Null); Assert.That(kind, Is.EqualTo(MatchLifecycleSignalKind.Started));
            Assert.That(copy.DescriptorId, Is.EqualTo(signal.DescriptorId));
        }
        [Test]
        public void CodecRejectsMalformedTruncatedAndTrailingPayload()
        {
            Assert.Throws<FormatException>(() => MatchLifecycleCodec.Decode(new byte[4], out _, out _, out _));
            byte[] bytes = MatchLifecycleCodec.Encode(Descriptor());
            Array.Resize(ref bytes, bytes.Length - 1);
            Assert.Throws<FormatException>(() => MatchLifecycleCodec.Decode(bytes, out _, out _, out _));
            bytes = MatchLifecycleCodec.Encode(Descriptor()); Array.Resize(ref bytes, bytes.Length + 1);
            Assert.Throws<FormatException>(() => MatchLifecycleCodec.Decode(bytes, out _, out _, out _));
        }
        [Test]
        public void AssemblerAcceptsOutOfOrderDuplicateAndIsolatesSenderAndConflictingChunk()
        {
            using var assembler = new BoundedPacketAssembler(); Guid id = Guid.NewGuid();
            byte[] start = new byte[1000], end = { 1, 2, 3 };
            Assert.That(assembler.Add(1, id, 1003, 1000, end, 0, out _), Is.EqualTo(PacketAssemblyStatus.Incomplete));
            Assert.That(assembler.Add(1, id, 1003, 1000, end, 1, out _), Is.EqualTo(PacketAssemblyStatus.Incomplete));
            Assert.That(assembler.Add(2, id, 1003, 0, start, 1, out _), Is.EqualTo(PacketAssemblyStatus.Incomplete));
            Assert.That(assembler.Add(1, id, 1003, 0, start, 2, out byte[] complete), Is.EqualTo(PacketAssemblyStatus.Complete));
            Assert.That(complete[1002], Is.EqualTo(3));
            start[0] = 99;
            Assert.That(assembler.Add(2, id, 1003, 0, start, 2, out _), Is.EqualTo(PacketAssemblyStatus.Rejected));
            Assert.That(assembler.PendingBytes, Is.Zero);
        }
        [Test]
        public void AssemblerBoundsMemoryAndPrunesMissingTransfers()
        {
            using var assembler = new BoundedPacketAssembler(); byte[] chunk = new byte[1000];
            for (ulong peer = 1; peer <= 2; peer++)
                Assert.That(assembler.Add(peer, Guid.NewGuid(), MatchLifecycleCodec.MaximumMessageBytes, 0, chunk, 0, out _),
                    Is.EqualTo(PacketAssemblyStatus.Incomplete));
            Assert.That(assembler.Add(3, Guid.NewGuid(), 1001, 0, chunk, 0, out _), Is.EqualTo(PacketAssemblyStatus.Rejected));
            Assert.That(assembler.PendingBytes, Is.EqualTo(BoundedPacketAssembler.MaximumPendingBytes));
            assembler.Prune(11); Assert.That(assembler.PendingBytes, Is.Zero);
            Assert.That(assembler.Add(1, Guid.NewGuid(), 1003, 1, chunk, 12, out _), Is.EqualTo(PacketAssemblyStatus.Rejected));
        }
        private NetworkManager Manager(string name, ushort port)
        {
            var go = new GameObject(name); _objects.Add(go);
            var manager = go.AddComponent<NetworkManager>(); var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, PlayerPrefab = null };
            return manager;
        }
        [UnityTest]
        public IEnumerator ActualUTPHostAndClientShareDescriptorReadyStartEndAndRestart()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None), Is.Empty);
            ushort port; using (var socket = new UdpClient(0)) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            NetworkManager hostManager = Manager("Core Host", port), clientManager = Manager("Core Client", port);
            _hostSession = new NgoNetworkSession(hostManager); _clientSession = new NgoNetworkSession(clientManager);
            Assert.That(_hostSession.TryStartHost(), Is.True); Assert.That(_clientSession.TryStartClient(), Is.True);
            float deadline = Time.realtimeSinceStartup + 20;
            while (!clientManager.IsConnectedClient && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(clientManager.IsConnectedClient, Is.True);
            _hostChannel = new NgoMatchLifecycleTransport(hostManager); _clientChannel = new NgoMatchLifecycleTransport(clientManager);
            _host = new NetworkMatchCoordinator(_hostSession, _hostChannel); _client = new NetworkMatchCoordinator(_clientSession, _clientChannel);
            var original = Descriptor();
            var bindings = new[] { original.Participants[0],
                new MatchParticipantBinding(clientManager.LocalClientId, original.Participants[1].PlayerId, false) };
            for (int cycle = 0; cycle < 2; cycle++)
            {
                Assert.That(_host.TryPrepare(original.Config, original.RegionGrid,
                    cycle == 0 ? RegionPolicyMode.Dynamic : RegionPolicyMode.Static, bindings), Is.True);
                _host.MarkLocalReady(); Assert.That(_host.TryStart(), Is.False);
                deadline = Time.realtimeSinceStartup + 20;
                while (_client.Descriptor == null && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_client.Descriptor, Is.Not.Null,
                    $"Host/client state={_hostSession.State}/{_clientSession.State}; queued={_hostChannel.PendingOutgoingMessages}; rejected={_clientChannel.RejectedPackets}");
                Assert.That(_client.Descriptor.Config.MatchId, Is.EqualTo(_host.Descriptor.Config.MatchId));
                Assert.That(_client.Descriptor.Config.RandomSeed, Is.EqualTo(41));
                Assert.That(_client.Descriptor.RegionGrid.RegionIds, Is.EqualTo(original.RegionGrid.RegionIds));
                Assert.That(_client.MarkLocalReady(), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (!_host.CanStart && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_host.TryStart(), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (_client.Phase != NetworkMatchPhase.Running && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_client.Phase, Is.EqualTo(NetworkMatchPhase.Running));
                Assert.That(_host.TryEnd(), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (_client.Descriptor != null && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_client.Descriptor, Is.Null);
            }
            Assert.That(_hostChannel.RejectedPackets + _clientChannel.RejectedPackets, Is.Zero);
        }
        private void Pump() { _hostChannel.Pump(); _clientChannel.Pump(); }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _host?.Dispose(); _client?.Dispose(); _host = _client = null;
            _hostChannel?.Dispose(); _clientChannel?.Dispose(); _hostChannel = _clientChannel = null;
            _clientSession?.Dispose(); _hostSession?.Dispose(); _clientSession = _hostSession = null;
            foreach (var go in _objects) if (go != null) UnityEngine.Object.Destroy(go);
            _objects.Clear(); yield return null;
        }
    }
}
