using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EnemyDetectionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private Scene _scene;

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
    }

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.ClosePreviewScene(_scene);
    }

    [Test]
    public void MultipleDetectors_NotifyOnlyFirstRegistrationAndLastRemoval()
    {
        EnemyDetection detection = CreateDetection();
        var changes = new List<bool>();
        detection.DetectionChanged += changes.Add;

        detection.RegisterDetector(1);
        detection.RegisterDetector(1);
        detection.RegisterDetector(2);
        detection.UnregisterDetector(1);
        detection.UnregisterDetector(99);
        Assert.That(detection.IsDetected, Is.True);
        Assert.That(changes, Is.EqualTo(new[] { true }));

        detection.UnregisterDetector(2);
        detection.UnregisterDetector(2);
        Assert.That(detection.IsDetected, Is.False);
        Assert.That(changes, Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public void Disable_ClearsRegistrationsAndDoesNotDuplicateUndetectedEvent()
    {
        EnemyDetection detection = CreateDetection();
        var changes = new List<bool>();
        detection.DetectionChanged += changes.Add;
        detection.RegisterDetector(1);

        MethodInfo disable = typeof(EnemyDetection).GetMethod("OnDisable", PrivateInstance);
        disable.Invoke(detection, null);
        disable.Invoke(detection, null);
        detection.UnregisterDetector(1);
        Assert.That(detection.IsDetected, Is.False);
        Assert.That(changes, Is.EqualTo(new[] { true, false }));

        detection.RegisterDetector(1);
        Assert.That(detection.IsDetected, Is.True);
        Assert.That(changes, Is.EqualTo(new[] { true, false, true }));
    }

    [Test]
    public void ClientAdapter_AppliesInitialAndChangedStateAndUnsubscribesOnDespawn()
    {
        EnemyDetection detection = CreateDetection();
        var adapter = detection.gameObject.AddComponent<EnemyNetworkDetection>();
        typeof(EnemyNetworkDetection).GetMethod("Awake", PrivateInstance).Invoke(adapter, null);

        typeof(EnemyDetection).GetMethod("ApplyDetectionState", PrivateInstance)
            .Invoke(detection, new object[] { true });
        var state = (NetworkVariable<bool>)typeof(EnemyNetworkDetection)
            .GetField("_isDetected", PrivateInstance).GetValue(adapter);

        // 실제 전송 대신 Client 수신 콜백과 구독 수명을 검증한다.
        adapter.OnNetworkSpawn();
        Assert.That(detection.IsDetected, Is.False);
        Assert.That(state.OnValueChanged, Is.Not.Null);

        state.OnValueChanged.Invoke(false, true);
        Assert.That(detection.IsDetected, Is.True);
        state.OnValueChanged.Invoke(true, false);
        Assert.That(detection.IsDetected, Is.False);

        adapter.OnNetworkDespawn();
        Assert.That(state.OnValueChanged, Is.Null);
    }

    [Test]
    public void BlackWolfPrefab_HasOneDetectionAdapterWithMatchingReference()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemies/Runtime/Assassin/BlackWolf.prefab");
        Assert.That(prefab.GetComponents<EnemyNetworkDetection>().Length, Is.EqualTo(1));
        var adapter = prefab.GetComponent<EnemyNetworkDetection>();
        var settings = new SerializedObject(adapter);
        Assert.That(settings.FindProperty("enemyDetection").objectReferenceValue,
            Is.EqualTo(prefab.GetComponent<EnemyDetection>()));
    }

    private EnemyDetection CreateDetection()
    {
        var enemy = new GameObject("DetectionTest");
        SceneManager.MoveGameObjectToScene(enemy, _scene);
        return enemy.AddComponent<EnemyDetection>();
    }
}
