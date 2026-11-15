// 실제 Anchor Prefab과 별도 UTP Host/Client에서 공용·Field NetworkVariable 수신을 검사합니다.
// 한 프로세스의 SDK 두 인스턴스 검사이며 실제 두 Build/Steam/Player 인수와 구분합니다.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
namespace TeamHJD.Game.Infrastructure.Tests
{
    public sealed class NetworkFieldAnchorTests
    {
        private NetworkManager _host, _client;
        private NgoNetworkSession _hostSession, _clientSession;
        [UnityTest]
        public IEnumerator RealAnchorReplicatesBothClockAnchorsAndFieldOnlyHold()
        {
            ushort port; using (var socket = new UdpClient(0)) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Core/NetworkMatchAnchor.prefab");
            Assert.That(prefab, Is.Not.Null);
            _host = Create("Field anchor Host", port, prefab); _client = Create("Field anchor Client", port, prefab);
            _hostSession = new NgoNetworkSession(_host); _clientSession = new NgoNetworkSession(_client);
            Assert.That(_hostSession.TryStartHost(), Is.True); Assert.That(_clientSession.TryStartClient(), Is.True);
            Assert.That(_host.NetworkConfig.ProtocolVersion, Is.EqualTo(NgoNetworkSession.CoreProtocolVersion));
            Assert.That(_client.NetworkConfig.ProtocolVersion, Is.EqualTo(NgoNetworkSession.CoreProtocolVersion));
            float deadline = Time.realtimeSinceStartup + 8;
            while (!_client.IsConnectedClient && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_client.IsConnectedClient, Is.True);
            var match = new MatchId("field-anchor-wire"); var host = new PlayerId("host"); var client = new PlayerId("client");
            var region = RegionGridSnapshot.FromCapturedCells(match, Guid.NewGuid(), 1, RegionPolicyMode.Static,
                new RegionGridDefinition(BattlefieldGridConfiguration.Default), Array.Empty<int>());
            var descriptor = new NetworkMatchDescriptor(Guid.NewGuid(), 1,
                new MatchConfig(match, MatchMode.Cooperative, new StageId("stage"), new DefinitionId("normal"), "test", 1,
                    new[] { new PlayerLoadoutSnapshot(host, null), new PlayerLoadoutSnapshot(client, null) }),
                RegionPolicyMode.Static, region.Definition, new[] { new MatchParticipantBinding(_host.LocalClientId, host, true),
                    new MatchParticipantBinding(_client.LocalClientId, client, false) });
            var anchor = UnityEngine.Object.Instantiate(prefab).GetComponent<NetworkMatchAnchor>();
            anchor.GetComponent<NetworkObject>().Spawn(); anchor.ApplyAuthorityState(descriptor, region);
            var epoch = Guid.NewGuid();
            anchor.ApplyAuthorityTime(descriptor, new MatchTimeSnapshot(match, epoch, 10, 100, 20, 1, false, true),
                new MatchTimeSnapshot(match, epoch, 14, 40, 20, 1, true, true));
            NetworkMatchAnchor received = null; deadline = Time.realtimeSinceStartup + 8;
            while (Time.realtimeSinceStartup < deadline)
            {
                received = _client.SpawnManager.SpawnedObjectsList.Select(x => x.GetComponent<NetworkMatchAnchor>()).FirstOrDefault(x => x != null);
                if (received != null && received.FieldTimeState.Revision == 14) break;
                yield return null;
            }
            Assert.That(received, Is.Not.Null);
            Assert.That(received.TimeState.TryRead(out var commonDescriptor, out var common), Is.True);
            Assert.That(received.FieldTimeState.TryRead(out var fieldDescriptor, out var field), Is.True);
            Assert.That(commonDescriptor, Is.EqualTo(descriptor.DescriptorId)); Assert.That(fieldDescriptor, Is.EqualTo(commonDescriptor));
            Assert.That(common.IsPaused, Is.False); Assert.That(field.IsPaused, Is.True);
            Assert.That(field.GameSecondsAtAnchor, Is.EqualTo(40)); Assert.That(common.GameSecondsAtAnchor, Is.EqualTo(100));
            anchor.ApplyAuthorityTime(descriptor, common, new MatchTimeSnapshot(match, epoch, 15, 40, 25, 1, false, true));
            deadline = Time.realtimeSinceStartup + 8;
            while (received.FieldTimeState.Revision != 15 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(received.FieldTimeState.Revision, Is.EqualTo(15)); Assert.That(received.TimeState.Revision, Is.EqualTo(10));
            Assert.That(received.FieldTimeState.Paused, Is.False);
        }
        private static NetworkManager Create(string name, ushort port, GameObject prefab)
        {
            var go = new GameObject(name); var manager = go.AddComponent<NetworkManager>(); var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, PlayerPrefab = null };
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab }); return manager;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            _clientSession?.Dispose(); _hostSession?.Dispose();
            if (_client != null && _client.IsListening) _client.Shutdown();
            if (_host != null && _host.IsListening) _host.Shutdown();
            float deadline = Time.realtimeSinceStartup + 5;
            while (((_client != null && _client.ShutdownInProgress) || (_host != null && _host.ShutdownInProgress)) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            if (_client != null) UnityEngine.Object.Destroy(_client.gameObject);
            if (_host != null) UnityEngine.Object.Destroy(_host.gameObject);
            yield return null;
        }
    }
}
#endif
