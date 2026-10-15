// 실제 NGO/UTP Host로 명시적 시작·종료와 연결 소유권을 검증합니다.
// Host 단독 검증이며 두 프로세스의 참가/상태 복제를 검증했다고 간주하지 않습니다.
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace TeamHJD.Game.Infrastructure.Tests
{
    public sealed class NgoNetworkSessionTests
    {
        private GameObject _owner;
        private NetworkManager _manager;
        private NgoNetworkSession _session;

        [SetUp]
        public void SetUp()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None), Is.Empty,
                "Run in the isolated test scene; do not replace a live project's NetworkManager.");
            _owner = new GameObject("Session Ownership Test");
            _manager = _owner.AddComponent<NetworkManager>();
            var transport = _owner.AddComponent<UnityTransport>();
            ushort port;
            using (var socket = new UdpClient(0)) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            _manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                PlayerPrefab = null
            };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _session?.Dispose();
            if (_manager != null && _manager.IsListening && !_manager.ShutdownInProgress) _manager.Shutdown();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_manager != null && (_manager.IsListening || _manager.ShutdownInProgress) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (_owner != null) UnityEngine.Object.Destroy(_owner);
            yield return null;
        }

        [Test]
        public void ConstructionDoesNotOpenServer_AndDisposeIsIdempotent()
        {
            _session = new NgoNetworkSession(_manager);
            Assert.That(_session.State, Is.EqualTo(NetworkConnectionState.Disconnected));
            Assert.That(_manager.IsListening, Is.False);
            _session.Dispose();
            _session.Dispose();
            Assert.That(_session.State, Is.EqualTo(NetworkConnectionState.Disposed));
            Assert.Throws<ObjectDisposedException>(() => _session.TryStartHost());
        }

        [Test]
        public void DisposingDuringStartingNotificationPreventsOpeningServer()
        {
            _session = new NgoNetworkSession(_manager);
            _session.StateChanged += state =>
            {
                if (state == NetworkConnectionState.StartingHost) _session.Dispose();
            };
            Assert.That(_session.TryStartHost(), Is.False);
            Assert.That(_manager.IsListening, Is.False);
        }

        [UnityTest]
        public IEnumerator ExplicitHostStart_RequiresNoOtherParticipant_AndStops()
        {
            _session = new NgoNetworkSession(_manager);
            Assert.That(_session.TryStartHost(), Is.True);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_session.State != NetworkConnectionState.Hosting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_session.IsServer, Is.True);
            Assert.That(_session.State, Is.EqualTo(NetworkConnectionState.Hosting));
            Assert.That(_session.TryStartHost(), Is.False);
            _session.Shutdown();
            deadline = Time.realtimeSinceStartup + 5f;
            while (_session.State != NetworkConnectionState.Disconnected && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_session.State, Is.EqualTo(NetworkConnectionState.Disconnected));
            Assert.That(_manager.IsListening, Is.False);
        }

        [UnityTest]
        public IEnumerator ObservingExternalHost_DoesNotTakeShutdownOwnership()
        {
            Assert.That(_manager.StartHost(), Is.True);
            yield return null;
            _session = new NgoNetworkSession(_manager);
            Assert.That(_session.State, Is.EqualTo(NetworkConnectionState.Hosting));
            _session.Dispose();
            Assert.That(_manager.IsListening, Is.True);
        }
    }
}
