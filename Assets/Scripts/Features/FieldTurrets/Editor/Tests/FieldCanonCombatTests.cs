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

    [Test]
    public void FlightCastPlaysSharedImpactAtObstacleContact()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FieldTurrets/Weapons/bullet.prefab");
        var playerBullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerBullet.prefab");
        var playerImpact = new SerializedObject(playerBullet.GetComponent<PlayerProjectileView>())
            .FindProperty("_impactPrefab").objectReferenceValue;
        Assert.That(playerImpact, Is.Not.Null);
        Assert.That(new SerializedObject(prefab.GetComponent<FieldTurretBullet>())
            .FindProperty("_impactPrefab").objectReferenceValue, Is.EqualTo(playerImpact));

        var wall = Track(new GameObject("Cannon bullet obstacle"));
        wall.tag = "FieldTurretBulletObstacle";
        wall.transform.position = Vector3.right * 3f;
        var wallCollider = wall.AddComponent<BoxCollider2D>();
        var bulletObject = Track(Object.Instantiate(prefab, new Vector3(0f, 0f, 2f), Quaternion.identity));
        var bullet = bulletObject.GetComponent<FieldTurretBullet>();
        bullet.Initialize(wall.transform, 10f);
        Physics2D.SyncTransforms();

        var cast = typeof(FieldTurretBullet).GetMethod("TryHitFlightPath", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(cast.Invoke(bullet, new object[] { Vector2.right * 5f, 0f }), Is.True);
        var impact = Track(Object.FindObjectsByType<PlayerSpriteEffect>(FindObjectsSortMode.None).Single().gameObject);
        Assert.That(impact.scene, Is.EqualTo(bulletObject.scene));
        Assert.That(impact.transform.position.x,
            Is.EqualTo(wallCollider.bounds.min.x).Within(Physics2D.defaultContactOffset));
        Assert.That(impact.transform.position.z, Is.EqualTo(2f));
        var bulletRenderer = bulletObject.GetComponent<SpriteRenderer>();
        var impactRenderer = impact.GetComponent<SpriteRenderer>();
        Assert.That(impactRenderer.sortingLayerID, Is.EqualTo(bulletRenderer.sortingLayerID));
        Assert.That(impactRenderer.sortingOrder, Is.EqualTo(bulletRenderer.sortingOrder + 1));
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
