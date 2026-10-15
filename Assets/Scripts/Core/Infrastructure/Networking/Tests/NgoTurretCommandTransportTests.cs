// 실제 UTP Host/Client에서 명령·재전송·발신자 변조·옛 준비 세대·재접속을 확인합니다.
// 한 프로세스의 두 SDK 검사이며 독립 실행 파일/실제 D/HUD 인수와 구분합니다.
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
    public sealed class NgoTurretCommandTransportTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private NgoNetworkSession _host, _client;
        private NgoTurretCommandTransport _hostTransport, _clientTransport;
        private TurretCommandGateway _hostGateway, _clientGateway;
        private TurretCommandService _service;
        private NetworkManager _hostManager, _clientManager;
        private TurretCommandResult _lastResult;
        private int _calls;
        private static readonly PlayerId Host = new PlayerId("host"), Client = new PlayerId("client");
        private sealed class ValidationPolicy : ITurretCommandAuthorization
        { public TurretCommandPermission Authorize(TurretCommandRequest request, PlayerId issuer) => TurretCommandPermission.Allowed; }
        private sealed class Executor : ITurretCommandExecutor
        {
            private readonly NgoTurretCommandTransportTests _owner;
            public Executor(NgoTurretCommandTransportTests owner) { _owner = owner; }
            public TurretCommandExecutionResult Execute(TurretCommandRequest request)
            { _owner._calls++; return new TurretCommandExecutionResult(TurretCommandStatus.Applied, "TestFeatureApplied"); }
        }
        private NetworkManager Manager(string name, ushort port)
        {
            var go = new GameObject(name); _objects.Add(go);
            var manager = go.AddComponent<NetworkManager>(); var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, PlayerPrefab = null };
            return manager;
        }
        private PlayerId? Resolve(ulong id) => id == _host.LocalConnectionId ? Host :
            _clientManager.IsConnectedClient && id == _client.LocalConnectionId ? Client : (PlayerId?)null;
        private void Capture(ulong sender, Guid descriptor, TurretCommandResult result) => _lastResult = result;
        private static TurretCommandRequest Request(MatchId match, PlayerId issuer, Guid? id = null)
            => new TurretCommandRequest(new MatchRequestContext(match, id ?? Guid.NewGuid(), issuer),
                TurretCommandKind.SetActivation, new EntityId("turret/persistent-key"), true);
        private void Pump()
        { _hostGateway?.Pump(); _clientGateway?.Pump(); _hostTransport.Pump(); _clientTransport.Pump(); }

        [UnityTest]
        public IEnumerator ActualRemoteCommandsVerifySenderAndSurviveExplicitReconnectWithoutReexecution()
        {
            Assert.That(UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None), Is.Empty);
            ushort port; using (var socket = new UdpClient(0)) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            _hostManager = Manager("Turret Host", port); _clientManager = Manager("Turret Client", port);
            _host = new NgoNetworkSession(_hostManager); _client = new NgoNetworkSession(_clientManager);
            Guid oldDescriptor = Guid.Empty;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                Assert.That(_host.TryStartHost(), Is.True); Assert.That(_client.TryStartClient(), Is.True);
                float deadline = Time.realtimeSinceStartup + 20;
                while (!_clientManager.IsConnectedClient && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(_clientManager.IsConnectedClient, Is.True);
                if (_hostTransport == null)
                {
                    _hostTransport = new NgoTurretCommandTransport(_hostManager); _clientTransport = new NgoTurretCommandTransport(_clientManager);
                    _clientTransport.ResultReceived += Capture;
                }
                var match = new MatchId("match-" + cycle); Guid descriptor = Guid.NewGuid(); _lastResult = null;
                _service = new TurretCommandService(match, IsHost, IsConnected, IsParticipant, new ValidationPolicy(), new Executor(this));
                _hostGateway = new TurretCommandGateway(match, descriptor, Host, NetworkManager.ServerClientId,
                    _host, _hostTransport, IsConnected, Resolve, _service);
                _clientGateway = new TurretCommandGateway(match, descriptor, Client, NetworkManager.ServerClientId,
                    _client, _clientTransport, IsConnected, Resolve);
                var request = Request(match, Client); int before = _calls;
                Assert.That(_clientGateway.TrySubmit(request), Is.True);
                Assert.That(_clientGateway.TryRetry(request.Context.RequestId), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while ((_lastResult == null || _clientGateway.PendingCount != 0) && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_lastResult, Is.Not.Null); Assert.That(_lastResult.WasAccepted, Is.True);
                Assert.That(_calls, Is.EqualTo(before + 1)); Assert.That(_clientGateway.PendingCount, Is.Zero);
                _lastResult = null;
                var automatic = new TurretCommandRequest(new MatchRequestContext(match, Guid.NewGuid(), Client), TurretCommandKind.StartAutoActivation);
                Assert.That(_clientGateway.TrySubmit(automatic), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while ((_lastResult == null || _clientGateway.PendingCount != 0) && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_lastResult, Is.Not.Null); Assert.That(_lastResult.Kind, Is.EqualTo(TurretCommandKind.StartAutoActivation));
                Assert.That(_clientGateway.PendingCount, Is.Zero); Assert.That(_calls, Is.EqualTo(before + 2));
                _lastResult = null;
                Assert.That(_clientTransport.TrySendRequest(NetworkManager.ServerClientId, descriptor, Request(match, Host)), Is.True);
                deadline = Time.realtimeSinceStartup + 20;
                while (_lastResult == null && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                Assert.That(_lastResult.Status, Is.EqualTo(TurretCommandStatus.Unauthorized)); Assert.That(_calls, Is.EqualTo(before + 2));
                Assert.That(_hostTransport.TrySendRequest(_client.LocalConnectionId, descriptor, request), Is.False);
                Assert.That(_clientTransport.TrySendResult(NetworkManager.ServerClientId, descriptor, _lastResult), Is.False);
                if (cycle > 0)
                {
                    long rejected = _hostGateway.RejectedMessages;
                    Assert.That(_clientTransport.TrySendRequest(NetworkManager.ServerClientId, oldDescriptor, Request(match, Client)), Is.True);
                    deadline = Time.realtimeSinceStartup + 20;
                    while (_hostGateway.RejectedMessages == rejected && Time.realtimeSinceStartup < deadline) { Pump(); yield return null; }
                    Assert.That(_hostGateway.RejectedMessages, Is.GreaterThan(rejected)); Assert.That(_calls, Is.EqualTo(before + 2));
                }
                oldDescriptor = descriptor; _hostGateway.Dispose(); _clientGateway.Dispose(); _service.Dispose();
                _hostGateway = _clientGateway = null; _service = null;
                _client.Shutdown(); _host.Shutdown(); deadline = Time.realtimeSinceStartup + 20;
                while ((_hostManager.IsListening || _clientManager.IsListening || _hostManager.ShutdownInProgress ||
                    _clientManager.ShutdownInProgress) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(_hostManager.IsListening || _clientManager.IsListening, Is.False);
            }
            Assert.That(_hostTransport.RejectedPackets + _clientTransport.RejectedPackets, Is.Zero);
        }
        private bool IsHost() => _host.IsServer;
        private bool IsConnected() => _host.State == NetworkConnectionState.Hosting && _client.State == NetworkConnectionState.ConnectedClient;
        private static bool IsParticipant(PlayerId player) => player == Host || player == Client;
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _hostGateway?.Dispose(); _clientGateway?.Dispose(); _service?.Dispose();
            _hostTransport?.Dispose(); _clientTransport?.Dispose();
            _client?.Dispose(); _host?.Dispose();
            foreach (GameObject go in _objects) if (go != null) UnityEngine.Object.Destroy(go);
            _objects.Clear(); yield return null;
        }
    }
}
