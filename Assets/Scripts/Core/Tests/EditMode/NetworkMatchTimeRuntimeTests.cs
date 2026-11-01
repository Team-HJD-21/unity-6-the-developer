// 실제 UTP Host/Client에서 시간 NetworkVariable의 초기 동기화·정지·복원 전달을 확인합니다.
// 같은 프로세스 두 SDK 인스턴스 검사이며 별도 실행 파일/실제 원격 Player 출입 인수와 구분합니다.
using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class NetworkMatchTimeRuntimeTests
    {
        private SceneSetup[] _setup;
        private NetworkManager _host, _client;
        [SetUp] public void Prepare()
        {
            if (UnityEngine.Application.isPlaying) return;
            _setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (_client != null) _client.Shutdown(); if (_host != null) _host.Shutdown();
            yield return null; yield return null;
            if (_client != null) Object.DestroyImmediate(_client.gameObject);
            if (_host != null) Object.DestroyImmediate(_host.gameObject);
            if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
            if (_setup != null && _setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(_setup);
        }
        private static NetworkManager Manager(string name, ushort port, GameObject prefab)
        {
            var owner = new GameObject(name); var manager = owner.AddComponent<NetworkManager>();
            var transport = owner.AddComponent<UnityTransport>(); transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, PlayerPrefab = null };
            manager.AddNetworkPrefab(prefab); return manager;
        }
        [UnityTest]
        public IEnumerator HostAnchorReplicatesPausePresenceAndRestoreToRealClient()
        {
            yield return new EnterPlayMode();
            ushort port; using (var socket = new UdpClient(0)) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Core/NetworkMatchAnchor.prefab");
            _host = Manager("Time Host", port, prefab); _client = Manager("Time Client", port, prefab);
            Assert.That(_host.StartHost(), Is.True); Assert.That(_client.StartClient(), Is.True);
            double timeout = Time.realtimeSinceStartupAsDouble + 15;
            while (!_client.IsConnectedClient && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            Assert.That(_client.IsConnectedClient, Is.True);
            var hostId = new PlayerId("host"); var guestId = new PlayerId("guest");
            var config = new MatchConfig(new MatchId("wire-time"), MatchMode.Cooperative, new StageId("stage"),
                new DefinitionId("difficulty"), "content", 1,
                new[] { new PlayerLoadoutSnapshot(hostId, null), new PlayerLoadoutSnapshot(guestId, null) });
            var grid = new RegionGridDefinition(BattlefieldGridConfiguration.Default, Enumerable.Repeat(1, 256));
            var descriptor = new NetworkMatchDescriptor(Guid.NewGuid(), 1, config, RegionPolicyMode.Static, grid,
                new[] { new MatchParticipantBinding(0, hostId, true), new MatchParticipantBinding(_client.LocalClientId, guestId, false) });
            var spawned = NetworkObject.InstantiateAndSpawn(prefab, _host, destroyWithScene: true);
            var authority = spawned.GetComponent<NetworkMatchAnchor>();
            authority.ApplyAuthorityState(descriptor, RegionGridSnapshot.FromCapturedCells(config.MatchId, Guid.NewGuid(), 0,
                RegionPolicyMode.Static, grid, Array.Empty<int>()));
            var epoch = Guid.NewGuid();
            authority.ApplyAuthorityTime(descriptor, new MatchTimeSnapshot(config.MatchId, epoch, 1, 100, _host.ServerTime.Time, 2, false, true));
            NetworkMatchAnchor received = null;
            timeout = Time.realtimeSinceStartupAsDouble + 10;
            while (received == null && Time.realtimeSinceStartupAsDouble < timeout)
            {
                foreach (var item in _client.SpawnManager.SpawnedObjectsList)
                { var candidate = item.GetComponent<NetworkMatchAnchor>(); if (candidate != null && candidate.TimeState.Revision == 1) received = candidate; }
                yield return null;
            }
            Assert.That(received, Is.Not.Null); Assert.That(received.IsServer, Is.False);
            Assert.That(received.TimeState.TryRead(out Guid generation, out var initial), Is.True);
            using var replica = new MatchTimeReplica(config.MatchId, descriptor.DescriptorId, new NgoTimestampSource(_client));
            Assert.That(replica.TryApply(generation, initial, true), Is.True); Assert.That(replica.ElapsedSeconds, Is.GreaterThanOrEqualTo(100));
            authority.ApplyAuthorityTime(descriptor, new MatchTimeSnapshot(config.MatchId, epoch, 2, 120, _host.ServerTime.Time, 2, true, true), 2, 3);
            timeout = Time.realtimeSinceStartupAsDouble + 10;
            while (received.TimeState.Revision < 2 && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            Assert.That(received.TimeState.TryRead(out generation, out var paused), Is.True);
            Assert.That(replica.TryApply(generation, paused, true), Is.True);
            Assert.That(received.TimeState.SpaceshipMask, Is.EqualTo(3)); Assert.That(replica.ElapsedSeconds, Is.EqualTo(120));
            double waitUntil = Time.realtimeSinceStartupAsDouble + .15;
            while (Time.realtimeSinceStartupAsDouble < waitUntil) { replica.Pump(); yield return null; }
            Assert.That(replica.ElapsedSeconds, Is.EqualTo(120)); Assert.That(_client.IsConnectedClient, Is.True);
            var restoredEpoch = Guid.NewGuid();
            authority.ApplyAuthorityTime(descriptor, new MatchTimeSnapshot(config.MatchId, restoredEpoch, 3, 5, _host.ServerTime.Time, 1, true, true), 3, 1);
            timeout = Time.realtimeSinceStartupAsDouble + 10;
            while (received.TimeState.Revision < 3 && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            Assert.That(received.TimeState.TryRead(out generation, out var restored), Is.True);
            replica.TryApply(generation, restored, true);
            Assert.That(replica.Snapshot.Epoch, Is.EqualTo(restoredEpoch)); Assert.That(replica.ElapsedSeconds, Is.EqualTo(5));
        }
    }
}
