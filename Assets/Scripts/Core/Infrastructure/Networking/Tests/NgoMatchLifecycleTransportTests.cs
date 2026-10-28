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
        private NgoRegionSnapshotTransport _hostRegionChannel, _clientRegionChannel;
        private RegionReplicationSession _hostRegions, _clientRegions;
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

        private static MatchCompletion CompleteFor(MatchId match) => new MatchCompletion(
            new MatchResult(match, MatchOutcome.Defeat, 3, new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero)),
            new RewardReceipt(match, MatchOutcome.Defeat, new[] { new CurrencyDelta(new CurrencyId("battery"), 5) }));

        [Test]
        public void CompletionCodecPreservesImmutableResultAndRejectsInvalidPayloadShape()
        {
            var descriptor = Descriptor(); var completion = CompleteFor(descriptor.Config.MatchId);
            var signal = new MatchLifecycleSignal(descriptor.Config.MatchId, descriptor.DescriptorId, completion);
            byte[] bytes = MatchLifecycleCodec.Encode(MatchLifecycleSignalKind.Completed, signal);
            MatchLifecycleCodec.Decode(bytes, out _, out var kind, out var copy);
            Assert.That(kind, Is.EqualTo(MatchLifecycleSignalKind.Completed));
            Assert.That(copy.Completion.Result.CompletedAt, Is.EqualTo(completion.Result.CompletedAt));
            Assert.That(copy.Completion.Result.CompletedWave, Is.EqualTo(3));
            Assert.That(copy.Completion.RewardReceipt.CurrencyDeltas[0].Amount, Is.EqualTo(5));
            Assert.Throws<ArgumentException>(() => MatchLifecycleCodec.Encode(MatchLifecycleSignalKind.Started, signal));
            Assert.Throws<ArgumentException>(() => MatchLifecycleCodec.Encode(MatchLifecycleSignalKind.Completed,
                new MatchLifecycleSignal(descriptor.Config.MatchId, descriptor.DescriptorId)));
            Array.Resize(ref bytes, bytes.Length - 1);
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
            _hostRegionChannel = new NgoRegionSnapshotTransport(hostManager); _clientRegionChannel = new NgoRegionSnapshotTransport(clientManager);
            _hostRegions = new RegionReplicationSession(_hostSession, _host, _hostRegionChannel);
            _clientRegions = new RegionReplicationSession(_clientSession, _client, _clientRegionChannel);
            var original = Descriptor();
            var bindings = new[] { original.Participants[0],
                new MatchParticipantBinding(clientManager.LocalClientId, original.Participants[1].PlayerId, false) };
            RegionReplicationFrame previousPreparation = null;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                if (cycle == 2)
                {
                    _clientSession.Shutdown(); _hostSession.Shutdown();
                    deadline = Time.realtimeSinceStartup + 20;
                    while ((hostManager.IsListening || clientManager.IsListening || hostManager.ShutdownInProgress ||
                        clientManager.ShutdownInProgress) && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(hostManager.IsListening || clientManager.IsListening, Is.False);
                    Assert.That(_hostSession.TryStartHost(), Is.True);
                    Assert.That(_clientSession.TryStartClient(), Is.True);
                    deadline = Time.realtimeSinceStartup + 20;
                    while (!clientManager.IsConnectedClient && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(clientManager.IsConnectedClient, Is.True);
                    bindings = new[] { original.Participants[0],
                        new MatchParticipantBinding(clientManager.LocalClientId, original.Participants[1].PlayerId, false) };
                }
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
                using var regions = new RegionRuntime(original.Config.MatchId, original.RegionGrid,
                    cycle == 0 ? RegionPolicyMode.Dynamic : RegionPolicyMode.Static);
                regions.TryApply(regions.Snapshot, new[] { 0, 2, 5 }, RegionCaptureUpdateMode.Replace);
                _hostRegions.BindHostSource(regions);
                deadline = Time.realtimeSinceStartup + 20;
                while (_clientRegions.Source == null && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_clientRegions.Source, Is.Not.Null);
                Assert.That(_clientRegions.Source.Snapshot.GridId, Is.EqualTo(regions.Snapshot.GridId));
                Assert.That(_clientRegions.Source.Snapshot.CapturedCells, Is.EqualTo(regions.Snapshot.CapturedCells));

                // 이미 큐에 있지만 아직 보내지 않은 두 상태는 최신 전체 Snapshot 하나로 합칩니다.
                regions.TryApply(regions.Snapshot, new[] { 20 }, RegionCaptureUpdateMode.Replace);
                _hostRegions.FlushPending();
                regions.TryApply(regions.Snapshot, new[] { 30, 40 }, RegionCaptureUpdateMode.Replace);
                _hostRegions.FlushPending();
                deadline = Time.realtimeSinceStartup + 20;
                while (_clientRegions.Source.Snapshot.Revision != regions.Snapshot.Revision && Time.realtimeSinceStartup < deadline)
                { Pump(); yield return null; }
                Assert.That(_clientRegions.Source.Snapshot.CapturedCells, Is.EqualTo(regions.Snapshot.CapturedCells));
                Assert.That(_hostRegionChannel.CoalescedFrames, Is.GreaterThan(0));
                RegionReplicationFrame oldGrid = _hostRegions.LastPublishedFrame;
                regions.Reset(new RegionGridDefinition(new BattlefieldGridConfiguration(new BattlefieldPoint(50, -20),
                    1, 21, 35, 7, 5)), RegionPolicyMode.Static, new[] { 1, 3 });
                deadline = Time.realtimeSinceStartup + 20;
                while (_clientRegions.Source.Snapshot.GridId != regions.Snapshot.GridId && Time.realtimeSinceStartup < deadline)
                { Pump(); yield return null; }
                Assert.That(_clientRegions.Source.Snapshot.Definition.HasSameDefinition(regions.Snapshot.Definition), Is.True);
                Assert.That(_clientRegions.Source.Snapshot.CapturedCells, Is.EqualTo(regions.Snapshot.CapturedCells));
                Assert.That(_hostRegionChannel.TrySendFrame(clientManager.LocalClientId, oldGrid), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (_clientRegions.LastReceiveStatus != RegionReplicationStatus.StaleGrid && Time.realtimeSinceStartup < deadline)
                { Pump(); yield return null; }
                Assert.That(_clientRegions.LastReceiveStatus, Is.EqualTo(RegionReplicationStatus.StaleGrid));
                if (previousPreparation != null)
                {
                    _hostRegionChannel.TrySendFrame(clientManager.LocalClientId, previousPreparation);
                    deadline = Time.realtimeSinceStartup + 20;
                    while (_clientRegions.LastReceiveStatus != RegionReplicationStatus.DifferentDescriptor && Time.realtimeSinceStartup < deadline)
                    { Pump(); yield return null; }
                    Assert.That(_clientRegions.LastReceiveStatus, Is.EqualTo(RegionReplicationStatus.DifferentDescriptor));
                }
                previousPreparation = _hostRegions.LastPublishedFrame;
                IRegionGridSource expiredClientSource = _clientRegions.Source;
                Assert.That(_host.TryPublishCompletion(CompleteFor(original.Config.MatchId)), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (_client.Completion == null && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_client.Completion, Is.Not.Null, "Actual UTP must deliver completion before Match cleanup.");
                Assert.That(_client.Completion.Result.Outcome, Is.EqualTo(MatchOutcome.Defeat));
                Assert.That(_client.Completion.RewardReceipt.CurrencyDeltas[0].Amount, Is.EqualTo(5));
                Assert.That(_client.IsGameplayRunning || _host.IsGameplayRunning, Is.False);
                Assert.That(_host.TryEnd(), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (_client.Descriptor != null && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_client.Descriptor, Is.Null);
                Assert.That(_clientRegions.Source, Is.Null);
                Assert.Throws<ObjectDisposedException>(() => { var unused = expiredClientSource.Snapshot; });
            }
            Assert.That(_hostChannel.RejectedPackets + _clientChannel.RejectedPackets, Is.Zero);
            Assert.That(_hostRegionChannel.RejectedPackets + _clientRegionChannel.RejectedPackets, Is.Zero);
        }
        private void Pump()
        {
            _hostChannel.Pump(); _clientChannel.Pump();
            _hostRegions.FlushPending(); _clientRegions.FlushPending();
            _hostRegionChannel.Pump(); _clientRegionChannel.Pump();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _host?.Dispose(); _client?.Dispose(); _host = _client = null;
            _hostRegions?.Dispose(); _clientRegions?.Dispose(); _hostRegions = _clientRegions = null;
            _hostRegionChannel?.Dispose(); _clientRegionChannel?.Dispose(); _hostRegionChannel = _clientRegionChannel = null;
            _hostChannel?.Dispose(); _clientChannel?.Dispose(); _hostChannel = _clientChannel = null;
            _clientSession?.Dispose(); _hostSession?.Dispose(); _clientSession = _hostSession = null;
            foreach (var go in _objects) if (go != null) UnityEngine.Object.Destroy(go);
            _objects.Clear(); yield return null;
        }
    }
}
