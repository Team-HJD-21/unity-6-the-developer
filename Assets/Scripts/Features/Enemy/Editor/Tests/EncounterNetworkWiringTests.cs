// 실제 Scene 파일을 바꾸지 않고 정식 네트워크 루트와 별도 Encounter 객체의 연결을 검사합니다.
// 격리 Preview Scene에서 명시 참조 우선·옛 재로드 호환·읽기 상태의 수명을 확인합니다.
using System;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Infrastructure;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class EncounterNetworkWiringTests
{
    private Scene _scene;
    private NetworkManager _manager;
    private EncounterSceneBinding _authored;

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        _manager = Create("App Network Root").AddComponent<NetworkManager>();
        _authored = Create("Scene Encounter").AddComponent<EncounterSceneBinding>();
    }

    [TearDown]
    public void TearDown() => EditorSceneManager.ClosePreviewScene(_scene);

    [Test]
    public void SeparateAuthoredConsumer_WinsOverLegacyConsumerOnNetworkRoot()
    {
        _manager.gameObject.AddComponent<EncounterSceneBinding>();
        Assert.That(Select(_authored), Is.SameAs(_authored));
        Assert.That(_authored.GetComponent<NetworkManager>(), Is.Null);
    }

    [Test]
    public void MissingAuthoredConsumer_UsesLegacyRootForExistingScenes()
    {
        var legacy = _manager.gameObject.AddComponent<EncounterSceneBinding>();
        Assert.That(Select(null), Is.SameAs(legacy));
    }

    [Test]
    public void MissingBothConsumers_IsRejectedInsteadOfPickingAnotherScene()
    {
        Assert.Throws<InvalidOperationException>(() => Select(null));
    }

    [Test]
    public void InactiveExplicitConsumer_IsRejectedEvenWhenLegacyIsAvailable()
    {
        _manager.gameObject.AddComponent<EncounterSceneBinding>();
        _authored.enabled = false;
        Assert.Throws<InvalidOperationException>(() => Select(_authored));
    }

    [Test]
    public void SupersededSceneNetworkRoot_UsesCurrentAppConsumer()
    {
        var legacy = _manager.gameObject.AddComponent<EncounterSceneBinding>();
        var discarded = _authored.gameObject;
        discarded.SetActive(false);
        Assert.That(Select(_authored, discarded), Is.SameAs(legacy));
    }

    [Test]
    public void DisposedStatusAdapter_IsOfflineAndDoesNotOwnTheSuppliedManager()
    {
        var session = new NgoNetworkSession(_manager);
        Assert.That(session.IsListening || session.IsServer || session.IsClient || session.IsHost, Is.False);
        Assert.That(session.ConnectedClientCount, Is.Zero);
        session.Dispose();
        Assert.That(_manager, Is.Not.Null);
        Assert.That(session.IsListening || session.IsServer || session.IsClient || session.IsHost, Is.False);
        Assert.That(session.ConnectedClientCount, Is.Zero);
    }

    private GameObject Create(string name)
    {
        var obj = new GameObject(name);
        SceneManager.MoveGameObjectToScene(obj, _scene);
        return obj;
    }

    private MonoBehaviour Select(MonoBehaviour authored, GameObject discarded = null)
    {
        var method = typeof(InitialStageNetworkBinding).GetMethod("SelectEncounterConsumer",
            BindingFlags.Static | BindingFlags.NonPublic);
        try { return (MonoBehaviour)method.Invoke(null, new object[] { authored, _manager, discarded }); }
        catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
    }
}
