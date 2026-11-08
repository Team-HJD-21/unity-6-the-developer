using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Content;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class FieldMissileHeatTests
{
    private readonly List<Object> _objects = new();
    private readonly List<Transform> _enemies = new();
    private FieldMissileTurretLV2 _turret;
    private FieldTurretDefinition _definition;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();

        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/FieldTurrets/FieldTurrets/FieldMissileTurret/Stage1/FieldMissileTurret LV2.prefab");
        Assert.That(prefabAsset, Is.Not.Null);
        var prefab = prefabAsset.GetComponent<FieldMissileTurretLV2>();
        _definition = Track(Object.Instantiate(prefab.Definition));
        SetField(_definition, "_id", "missile-heat-regression");
        var staging = Track(new GameObject("Inactive missile test template"));
        staging.SetActive(false);
        var template = Track(Object.Instantiate(prefab, staging.transform));
        SetField(template, "_definition", _definition);
        var controller = Track(new GameObject("Missile heat controller")).AddComponent<FieldTurretController>();
        _turret = (FieldMissileTurretLV2)FieldTurretFactory.Create(template,
            FieldTurretInitializationData.CreateDefault(_definition), controller,
            new Vector3(5000, 5000, 0), Quaternion.identity);
        Track(_turret.gameObject);
        SetField(_turret, "enemyMask", (LayerMask)1);
        yield return null;

        for (int index = 0; index < 4; index++)
        {
            var enemy = Track(new GameObject("Missile heat target " + index));
            enemy.transform.position = _turret.transform.position + new Vector3(index * 3, 15, 0);
            enemy.AddComponent<MovingEnemyController>();
            enemy.AddComponent<CircleCollider2D>();
            _enemies.Add(enemy.transform);
        }
        Physics2D.SyncTransforms();
        Assert.That(_turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        DestroyMissiles();
        for (int index = _objects.Count - 1; index >= 0; index--)
            if (_objects[index] != null) Object.DestroyImmediate(_objects[index]);
        _objects.Clear();
        _enemies.Clear();
        yield return new ExitPlayMode();
    }

    [Test]
    public void FinalSalvoFiresAllFourMissilesThenCoolsImmediately()
    {
        SetField(_turret, "_currentMissileCount", _definition.OverHeatMissileCount - 1f);
        Invoke(_turret, "NoTargetInRange");
        Invoke(_turret, "Shoot");

        Assert.That(FindMissiles().Length, Is.EqualTo(4));
        Assert.That(_turret.IsActivated, Is.True);
        Assert.That(_turret.IsCoolingDown, Is.True);
        Assert.That(_turret.IsOperational, Is.False);
        Assert.That(GetField<float>(_turret, "_currentMissileCount"), Is.EqualTo(_definition.OverHeatMissileCount));
        foreach (Transform enemy in _enemies)
            Assert.That(MissileTargetReservation.TryAcquire(enemy, out _), Is.False,
                "발사체가 소유한 예약은 터렛 냉각으로 해제하지 않는다.");

        Invoke(_turret, "Shoot");
        Assert.That(FindMissiles().Length, Is.EqualTo(4));
    }

    [Test]
    public void CoolingReleasesUnlaunchedReservationsWithoutAnInSightTarget()
    {
        Invoke(_turret, "NoTargetInRange");
        SetField(_turret, "_currentMissileCount", (float)_definition.OverHeatMissileCount);
        SetField(_definition, "_targetingAngle", 0f);
        SetField(_turret, "turretRotationPoint", _turret.transform);
        _turret.transform.rotation = Quaternion.Euler(0, 0, 180);

        Invoke(_turret, "OverHeatAnimationController");

        Assert.That(_turret.IsCoolingDown, Is.True);
        Assert.That(FindMissiles(), Is.Empty);
        foreach (Transform enemy in _enemies)
        {
            bool acquired = MissileTargetReservation.TryAcquire(enemy, out var claim);
            claim?.Dispose();
            Assert.That(acquired, Is.True, "냉각 중인 터렛이 미발사 타깃을 점유하면 안 된다.");
        }
    }

    [Test]
    public void ReacquiringValidTargetsDoesNotCoolAccumulatedSalvos()
    {
        SetField(_turret, "_currentMissileCount", 3f);
        Invoke(_turret, "NoTargetInRange");
        Assert.That(GetField<float>(_turret, "_currentMissileCount"), Is.EqualTo(3f));

        Invoke(_turret, "ReleaseUnlaunchedTargets");
        Invoke(_turret, "NoTargetInRange");
        Assert.That(GetField<float>(_turret, "_currentMissileCount"), Is.EqualTo(3f));
    }

    [Test]
    public void LaunchWithoutReservationsDoesNotCountAsASalvo()
    {
        Invoke(_turret, "Shoot");
        Assert.That(GetField<float>(_turret, "_currentMissileCount"), Is.Zero);
        Assert.That(_turret.IsCoolingDown, Is.False);
        Assert.That(FindMissiles(), Is.Empty);
    }

    [UnityTest]
    public IEnumerator RepeatedSalvosReachThresholdAndResumeAfterCooling()
    {
        SetField(_definition, "_overHeatMissileCount", 2);
        SetField(_definition, "_coolTime", 0.1f);
        int reservedPower = _turret.ReservedPower;
        Invoke(_turret, "NoTargetInRange");
        Invoke(_turret, "Shoot");
        Assert.That(_turret.IsCoolingDown, Is.False);
        Assert.That(GetField<float>(_turret, "_currentMissileCount"), Is.EqualTo(1f));

        DestroyMissiles();
        Invoke(_turret, "NoTargetInRange");
        Invoke(_turret, "Shoot");
        Assert.That(_turret.IsCoolingDown, Is.True);
        Assert.That(_turret.ReservedPower, Is.EqualTo(reservedPower));
        float deadline = Time.realtimeSinceStartup + 3f;
        while (_turret.IsCoolingDown && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return null;

        Assert.That(_turret.IsCoolingDown, Is.False);
        Assert.That(_turret.IsOperational, Is.True);
        Assert.That(GetField<float>(_turret, "_currentMissileCount"), Is.Zero);
        Assert.That(_turret.ReservedPower, Is.EqualTo(reservedPower));
    }

    private static void DestroyMissiles()
    {
        foreach (var missile in FindMissiles())
            if (missile != null) Object.DestroyImmediate(missile.gameObject);
    }

    private static FieldTurretMissile[] FindMissiles() => Array.FindAll(
        Object.FindObjectsByType<FieldTurretMissile>(FindObjectsSortMode.None),
        missile => missile.transform.parent == null);

    private T Track<T>(T value) where T : Object
    {
        _objects.Add(value);
        return value;
    }

    private static FieldInfo FindField(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }

    private static void SetField(object target, string field, object value) =>
        FindField(target.GetType(), field).SetValue(target, value);

    private static T GetField<T>(object target, string field) =>
        (T)FindField(target.GetType(), field).GetValue(target);

    private static void Invoke(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method == null) continue;
            method.Invoke(target, null);
            return;
        }
        throw new MissingMethodException(name);
    }
}
