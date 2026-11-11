// 생성 전 귀속 준비가 NGO 경고를 내지 않는지 검사합니다.
// 실제 UTP Host/Client의 최초 Spawn 통지와 제거·재생성의 귀속도 대조합니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Domain;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemySimulationSpawnTests
{
    private List<GameObject> _objects;
    private List<string> _initializationWarnings;
    private List<(bool Server, string Match, ulong Owner)> _observations;
    private NetworkManager _host, _client;
    private string _prefabPath;
    private const string PrefabSessionKey = "EnemySimulationSpawnTests.Prefab";
    private Action<EnemyNetworkController> _observer;
    private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly EventInfo Spawned = typeof(EnemyNetworkController).GetEvent(
        "SpawnedInMatch", BindingFlags.Static | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        // EnterPlayMode의 domain reload 후 테스트 fixture의 비직렬화 자료도 다시 준비합니다.
        _objects = new List<GameObject>();
        _initializationWarnings = new List<string>();
        _observations = new List<(bool Server, string Match, ulong Owner)>();
        Application.logMessageReceived -= CaptureWarning;
        if (_observer != null) Spawned.GetRemoveMethod(true).Invoke(null, new object[] { _observer });
        Application.logMessageReceived += CaptureWarning;
        _observer = enemy => _observations.Add((enemy.IsServer,
            Read<string>(enemy, "SimulationMatchId"), Read<ulong>(enemy, "SimulationOwnerId")));
        Spawned.GetAddMethod(true).Invoke(null, new object[] { _observer });
    }

    [Test]
    public void PreparingUnspawnedEnemyDoesNotWriteNetworkVariablesOrWarn()
    {
        var enemy = CreateEnemy();
        Prepare(enemy, "before-spawn", 42);
        Assert.That(Read<string>(enemy, "SimulationMatchId"), Is.Empty);
        Assert.That(Read<ulong>(enemy, "SimulationOwnerId"), Is.EqualTo(ulong.MaxValue));
        Assert.That(_initializationWarnings, Is.Empty);
        Assert.That(_observations, Is.Empty);
    }

    [UnityTest]
    public IEnumerator ActualHostAndClientObserveIdentityOnFirstSpawnAndRespawn()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        Assert.That(Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None), Is.Empty);
        var template = CreateEnemy().gameObject;
        _prefabPath = AssetDatabase.GenerateUniqueAssetPath("Assets/__EnemySimulationSpawnTest.prefab");
        PrefabUtility.SaveAsPrefabAsset(template, _prefabPath);
        SessionState.SetString(PrefabSessionKey, _prefabPath);
        Object.DestroyImmediate(template);
        // NGO의 메시지 등록도 Runtime 초기화에 포함되므로 실제 Play Mode에서 접속합니다.
        yield return new EnterPlayMode();
        // EnterPlayMode 뒤에는 이전 NUnit fixture 참조를 사용하지 않습니다.
        var runtime = new EnemySimulationSpawnTests();
        runtime.SetUp();
        yield return runtime.VerifyHostAndClient();
        yield return runtime.TearDown();
    }

    private IEnumerator VerifyHostAndClient()
    {
        _prefabPath = SessionState.GetString(PrefabSessionKey, "");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
        Assert.That(prefab, Is.Not.Null, "Editor에서 만든 임시 prefab이 Play Mode에도 존재해야 합니다.");
        ushort port;
        using (var socket = new UdpClient(0)) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
        _host = Manager("Identity host", port, prefab);
        _client = Manager("Identity client", port, prefab);
        Assert.That(_host.StartHost(), Is.True);
        Assert.That(_client.StartClient(), Is.True);
        float deadline = Time.realtimeSinceStartup + 20;
        while (!_client.IsConnectedClient && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(_client.IsConnectedClient, Is.True);

        var actor = Object.Instantiate(prefab);
        _objects.Add(actor);
        var enemy = actor.GetComponent<EnemyNetworkController>();
        var network = actor.GetComponent<NetworkObject>();
        Assert.That(NetworkManager.Singleton, Is.SameAs(_host));
        for (int cycle = 0; cycle < 2; cycle++)
        {
            string match = "spawn-cycle-" + cycle;
            ulong owner = (ulong)(42 + cycle);
            _observations.Clear();
            Prepare(enemy, match, owner);
            network.Spawn();
            var mutation = Assert.Throws<TargetInvocationException>(() => Prepare(enemy, "forbidden", 999));
            Assert.That(mutation.InnerException, Is.TypeOf<InvalidOperationException>());
            deadline = Time.realtimeSinceStartup + 20;
            while (_observations.Count < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_observations.Count, Is.EqualTo(2), "Host/Client 모두 최초 Spawn 통지가 필요합니다.");
            Assert.That(_observations.Exists(item => item.Server), Is.True);
            Assert.That(_observations.Exists(item => !item.Server), Is.True);
            foreach (var item in _observations)
            {
                Assert.That(item.Match, Is.EqualTo(match));
                Assert.That(item.Owner, Is.EqualTo(owner));
            }
            network.Despawn(false);
            deadline = Time.realtimeSinceStartup + 20;
            while (_client.SpawnManager.SpawnedObjects.Count > 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_client.SpawnManager.SpawnedObjects, Is.Empty);
        }
        Assert.That(_initializationWarnings, Is.Empty);
        TestContext.WriteLine("UTP host/client: first identity notifications correct for two spawn cycles; initialization warnings=0.");
    }

    private EnemyNetworkController CreateEnemy()
    {
        var go = new GameObject("Enemy identity fixture");
        _objects.Add(go);
        go.AddComponent<NetworkObject>();
        var enemy = go.AddComponent<EnemyNetworkController>();
        // 귀속·직렬화만 검사하는 fixture입니다. AI/체력은 실제 Stage 통합 검사에서 확인합니다.
        enemy.enabled = false;
        return enemy;
    }

    private NetworkManager Manager(string name, ushort port, GameObject prefab)
    {
        var go = new GameObject(name);
        _objects.Add(go);
        var manager = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
        manager.AddNetworkPrefab(prefab);
        return manager;
    }

    private static void Prepare(EnemyNetworkController enemy, string match, ulong owner) =>
        typeof(EnemyNetworkController).GetMethod("SetSimulationMatch", Instance)
            .Invoke(enemy, new object[] { new MatchId(match), owner });

    private static T Read<T>(EnemyNetworkController enemy, string property) =>
        (T)typeof(EnemyNetworkController).GetProperty(property, Instance).GetValue(enemy);

    private void CaptureWarning(string message, string stack, LogType kind)
    {
        if (message.Contains("NetworkVariable is written to, but doesn't know its NetworkBehaviour yet"))
            _initializationWarnings.Add(message + "\n" + stack);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_observer != null) Spawned.GetRemoveMethod(true).Invoke(null, new object[] { _observer });
        Application.logMessageReceived -= CaptureWarning;
        if (_client != null) _client.Shutdown();
        if (_host != null) _host.Shutdown();
        float deadline = Time.realtimeSinceStartup + 20;
        while (((_host != null && _host.ShutdownInProgress) || (_client != null && _client.ShutdownInProgress)) &&
            Time.realtimeSinceStartup < deadline) yield return null;
        if (_objects != null) foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
        _objects?.Clear();
        _observations?.Clear();
        _initializationWarnings?.Clear();
        if (!string.IsNullOrEmpty(_prefabPath)) AssetDatabase.DeleteAsset(_prefabPath);
        string retainedPath = SessionState.GetString(PrefabSessionKey, "");
        if (!string.IsNullOrEmpty(retainedPath)) AssetDatabase.DeleteAsset(retainedPath);
        SessionState.EraseString(PrefabSessionKey);
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
