using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.FieldTurrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public sealed class FieldCanonCombatTests
{
    private readonly List<GameObject> _objects = new();
    private Random.State _randomState;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        _randomState = Random.state;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int index = _objects.Count - 1; index >= 0; index--)
            if (_objects[index] != null) Object.DestroyImmediate(_objects[index]);
        _objects.Clear();
        Random.state = _randomState;
        yield return new ExitPlayMode();
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void RepeatedSalvosPreserveAimPointsAndUseFreshSpread(int level)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Prefabs/FieldTurrets/FieldTurrets/FieldCanonTurret/stage1/FieldCanonTurret LV{level}.prefab");
        Assert.That(prefab, Is.Not.Null);
        var staging = Track(new GameObject("Cannon combat test staging"));
        staging.SetActive(false);
        var turret = Object.Instantiate(prefab, staging.transform).GetComponent<DefaultFieldCanonTurret>();
        turret.GetType().GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(turret, null);
        Transform[] spawnPoints = GetTransforms(turret, "bulletSpawnPoint");
        Transform[] aimPoints = GetTransforms(turret, level == 1 ? "bulletFirePoint" : "bulletFireDirection");
        Vector3[] initialPositions = aimPoints.Select(point => point.position).ToArray();
        Assert.That(spawnPoints.Length, Is.EqualTo(level));
        Assert.That(turret.ProjectilesPerShot, Is.EqualTo(level));
        Assert.That(turret.TheoreticalDps,
            Is.EqualTo(turret.EffectiveDamage * turret.Definition.FireRate * level).Within(0.001f));
        Assert.That(turret.DpsPerPower,
            Is.EqualTo(turret.TheoreticalDps / turret.EffectivePower).Within(0.001f));

        for (int shot = 0; shot < 12; shot++)
        {
            int seed = 4231 + shot;
            Random.InitState(seed);
            Vector3[] expectedDirections = aimPoints.Select((point, index) =>
            {
                Vector3 aim = new Vector3(initialPositions[index].x + Random.Range(-0.5f, 0.5f),
                    initialPositions[index].y, 0f);
                return (aim - spawnPoints[index].position).normalized;
            }).ToArray();
            Random.InitState(seed);
            turret.GetType().GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(turret, null);

            var bullets = Object.FindObjectsByType<FieldTurretBullet>(FindObjectsSortMode.None).ToList();
            foreach (var bullet in bullets) Track(bullet.gameObject);
            Assert.That(bullets.Count, Is.EqualTo(level));
            for (int index = 0; index < aimPoints.Length; index++)
            {
                Assert.That(aimPoints[index].position, Is.EqualTo(initialPositions[index]), "Spread must not move the authored aim point.");
                var bullet = bullets.Single(item => Vector3.Distance(item.transform.position, spawnPoints[index].position) < 0.001f);
                Vector3 direction = (Vector3)typeof(FieldTurretBullet)
                    .GetField("_direction", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bullet);
                Assert.That(Vector3.Distance(direction, expectedDirections[index]), Is.LessThan(0.0001f));
            }
            foreach (var bullet in bullets) Object.DestroyImmediate(bullet.gameObject);
        }
    }

    [Test]
    public void LegacyBulletInitializationStillUsesTargetPosition()
    {
        var target = Track(new GameObject("Legacy bullet aim"));
        target.transform.position = new Vector3(3f, 4f, 0f);
        var bullet = Track(new GameObject("Legacy bullet")).AddComponent<FieldTurretBullet>();
        bullet.Initialize(target.transform, 10);
        Vector3 direction = (Vector3)typeof(FieldTurretBullet)
            .GetField("_direction", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bullet);
        Assert.That(Vector3.Distance(direction, target.transform.position.normalized), Is.LessThan(0.0001f));
    }

    private static Transform[] GetTransforms(DefaultFieldCanonTurret turret, string fieldName)
    {
        object value = turret.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(turret);
        return value is Transform point ? new[] { point } : (Transform[])value;
    }

    private GameObject Track(GameObject value)
    {
        _objects.Add(value);
        return value;
    }
}
