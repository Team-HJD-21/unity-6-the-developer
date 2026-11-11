using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 폭발 능력의 발동·범위 피해와 중복 적용 방지를 검사한다.
/// </summary>
public sealed class EnemyExplosionAbilityTests
{
    private readonly List<GameObject> _objects = new();
    private static readonly Vector3 Origin = new(10000f, 10000f, 0f);

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject instance in _objects)
            if (instance != null)
                Object.DestroyImmediate(instance);

        _objects.Clear();
    }

    [Test]
    public void BomberPrefab_UsesExplosionAbilityAndPreservesControllerReference()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemies/Runtime/Special/BomberSlime.prefab");

        Assert.That(prefab, Is.Not.Null);
        foreach (Component component in prefab.GetComponents<Component>())
            Assert.That(component, Is.Not.Null, "Bomber contains a missing script.");

        var ability = prefab.GetComponent<EnemyExplosionAbility>();
        Assert.That(ability, Is.Not.Null);
        Assert.That(prefab.GetComponent<EnemyCloseRangeAbility>(), Is.Null);
        Assert.That(prefab.GetComponents<EnemyAbility>().Length, Is.EqualTo(1));
        var settings = new SerializedObject(ability);
        Assert.That(settings.FindProperty("_bodyCollider").objectReferenceValue,
            Is.EqualTo(prefab.GetComponent<CapsuleCollider2D>()));
        Assert.That(prefab.GetComponent<CapsuleCollider2D>().isTrigger, Is.True);
        var controller = new SerializedObject(prefab.GetComponent<MovingEnemyController>());
        Assert.That(controller.FindProperty("enemyAbility").objectReferenceValue,
            Is.EqualTo(ability));
    }

    [TestCase("Down")]
    [TestCase("Left")]
    [TestCase("Right")]
    [TestCase("Up")]
    public void BomberAttackClip_ReportsCompletionAtEnd(string direction)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            $"Assets/Art/Animations/Enemies/Runtime/Special/BomberSlime/Attack/BomberSlime_{direction}Attack.anim");

        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.isLooping, Is.False);
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        Assert.That(events.Length, Is.EqualTo(1));
        Assert.That(events[0].functionName, Is.EqualTo("OnExplosionAnimationFinished"));
        Assert.That(events[0].time, Is.EqualTo(clip.length).Within(0.001f));
    }

    [Test]
    public void Explosion_DamagesInterfaceTargetsOnceAndRequestsRemovalOnlyAfterAnimation()
    {
        GameObject enemy = Create("Bomber", Origin);
        enemy.AddComponent<BoxCollider2D>();
        enemy.AddComponent<CapsuleCollider2D>().isTrigger = true;
        var health = enemy.AddComponent<EnemyHealth>();
        health.Initialize();
        var targeting = enemy.AddComponent<EnemyTargeting>();
        var ability = enemy.AddComponent<EnemyExplosionAbility>();
        InvokeAwake(ability);

        var primary = CreateTarget("Primary", Origin + Vector3.right * 1.1f);
        GameObject extraCollider = new("ExtraCollider");
        extraCollider.transform.SetParent(primary.transform, false);
        extraCollider.AddComponent<CircleCollider2D>();
        var nearby = CreateTarget("Nearby", Origin + Vector3.up * 0.8f);
        var distant = CreateTarget("Distant", Origin + Vector3.right * 3f);
        var invalid = CreateTarget("Invalid", Origin + Vector3.left * 0.5f);
        invalid.Available = false;
        targeting.SetTarget(primary.transform);
        Physics2D.SyncTransforms();

        int removals = 0;
        int deaths = 0;
        ability.RemovalRequested += () => removals++;
        health.Died += _ => deaths++;

        Assert.That(ability.CheckCondition(), Is.True);
        Assert.That(ability.TryUse(), Is.True);
        Assert.That(primary.CurrentHealth, Is.EqualTo(80));
        Assert.That(nearby.CurrentHealth, Is.EqualTo(80));
        Assert.That(distant.CurrentHealth, Is.EqualTo(100));
        Assert.That(invalid.CurrentHealth, Is.EqualTo(100));
        Assert.That(ability.IsExecuting, Is.True);
        Assert.That(ability.TryUse(), Is.False);
        Assert.That(primary.CurrentHealth, Is.EqualTo(80));
        Assert.That(removals, Is.Zero);

        health.TakeDamage(health.MaxHealth);
        Assert.That(deaths, Is.Zero);
        ability.OnExplosionAnimationFinished();
        ability.OnExplosionAnimationFinished();
        Assert.That(removals, Is.EqualTo(1));
        Assert.That(deaths, Is.Zero);
    }

    [Test]
    public void Explosion_UsesTriggerCapsuleForActivationAndBlastCenter()
    {
        GameObject enemy = Create("Bomber", Origin);
        enemy.AddComponent<BoxCollider2D>();
        var body = enemy.AddComponent<CapsuleCollider2D>();
        body.isTrigger = true;
        body.direction = CapsuleDirection2D.Horizontal;
        body.offset = new Vector2(0f, -0.8f);
        body.size = new Vector2(0.4f, 0.2f);
        var targeting = enemy.AddComponent<EnemyTargeting>();
        var ability = enemy.AddComponent<EnemyExplosionAbility>();
        InvokeAwake(ability);

        var target = CreateTarget("Target", Origin + Vector3.down * 1.6f);
        targeting.SetTarget(target.transform);
        Physics2D.SyncTransforms();

        Vector2 closestPoint = target.GetComponent<Collider2D>()
            .ClosestPoint(enemy.transform.position);
        Assert.That(Vector2.Distance(enemy.transform.position, closestPoint),
            Is.GreaterThan(1f), "기존 피벗 중심 반경에서는 타깃이 제외되는 배치다.");
        Assert.That(ability.CheckCondition(), Is.True);
        Assert.That(ability.TryUse(), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(80));
    }

    [Test]
    public void MovingController_RemainsStoppedWhileExplosionIsExecuting()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemies/Runtime/Special/BomberSlime.prefab");
        GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        _objects.Add(enemy);
        enemy.transform.position = Origin;
        var controller = enemy.GetComponent<MovingEnemyController>();
        var movement = enemy.GetComponent<EnemyMovement>();
        var ability = enemy.GetComponent<EnemyExplosionAbility>();
        InvokeAwake(controller);
        InvokeAwake(movement);
        InvokeAwake(enemy.GetComponent<EnemyAnimation>());
        InvokeAwake(ability);
        enemy.GetComponent<Animator>().Rebind();

        var target = CreateTarget("Target", Origin);
        enemy.GetComponent<EnemyTargeting>().SetTarget(target.transform);
        Physics2D.SyncTransforms();
        Assert.That(ability.TryUse(), Is.True);

        // 폭발 후 타깃이 멀어져도 이동을 다시 시작하지 않아야 한다.
        target.transform.position = Origin + Vector3.right * 10f;
        typeof(EnemyMovement).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(movement, new[] { target.transform.position });
        movement.Move(Vector2.right);
        controller.ExecuteBehavior();
        Assert.That(enemy.GetComponent<Rigidbody2D>().linearVelocity,
            Is.EqualTo(Vector2.zero));
    }

    private GameObject Create(string name, Vector3 position)
    {
        var instance = new GameObject(name);
        instance.transform.position = position;
        _objects.Add(instance);
        return instance;
    }

    private EnemyExplosionTargetProbe CreateTarget(string name, Vector3 position)
    {
        GameObject instance = Create(name, position);
        instance.AddComponent<BoxCollider2D>();
        return instance.AddComponent<EnemyExplosionTargetProbe>();
    }

    private static void InvokeAwake(MonoBehaviour component)
    {
        component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(component, null);
    }
}

public sealed class EnemyExplosionTargetProbe : MonoBehaviour, ITargetable, IDamageableTarget
{
    public bool Available = true;
    public Transform TargetTransform => transform;
    public TargetType TargetType => global::TargetType.Turret;
    public int MaxAttackers => 10;
    public float HealthRatio => CurrentHealth / (float)MaxHealth;
    public float FirepowerRatio => 0f;
    public bool CanBeTargeted => Available && !IsDestroyed;
    public int CurrentHealth { get; private set; } = 100;
    public int MaxHealth => 100;
    public bool IsDestroyed => CurrentHealth <= 0;

    public bool ApplyDamage(int damage)
    {
        CurrentHealth -= damage;
        return true;
    }
}
