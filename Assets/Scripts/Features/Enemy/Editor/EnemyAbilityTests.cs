using NUnit.Framework;
using System.Reflection;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EnemyAbilityTests
{
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
    public void TargetlessAbility_UsesWithoutTargetOrDamageInput()
    {
        var ability = Create("Ability").AddComponent<EnemyAbilityTestProbe>();

        Assert.That(ability.CheckCondition(), Is.True);
        Assert.That(ability.TryUse(), Is.True);
        Assert.That(ability.Activations, Is.EqualTo(1));
        Assert.That(ability.CheckCondition(), Is.True, "쿨타임 중에도 발동 조건 자체는 유지된다.");
        Assert.That(ability.TryUse(), Is.False);
        Assert.That(ability.Activations, Is.EqualTo(1));
    }

    [Test]
    public void FailedActivation_DoesNotConsumeCooldown()
    {
        var ability = Create("Ability").AddComponent<EnemyAbilityTestProbe>();
        ability.Succeeds = false;
        Assert.That(ability.TryUse(), Is.False);
        Assert.That(ability.CheckCondition(), Is.True);

        ability.Succeeds = true;
        Assert.That(ability.TryUse(), Is.True);
        Assert.That(ability.TryUse(), Is.False);
    }

    [Test]
    public void UseCondition_IsIndependentOfCooldown()
    {
        var ability = Create("Ability").AddComponent<EnemyAbilityTestProbe>();
        Assert.That(ability.TryUse(), Is.True);
        ability.Condition = false;

        Assert.That(ability.CheckCondition(), Is.False);
        Assert.That(ability.TryUse(), Is.False);
        Assert.That(ability.Activations, Is.EqualTo(1));
    }

    [Test]
    public void DisabledAbilityOrUnmetCondition_DoesNotActivate()
    {
        var ability = Create("Ability").AddComponent<EnemyAbilityTestProbe>();
        ability.Condition = false;
        Assert.That(ability.TryUse(), Is.False);

        ability.Condition = true;
        ability.enabled = false;
        Assert.That(ability.TryUse(), Is.False);
        Assert.That(ability.Activations, Is.Zero);
    }

    [Test]
    public void MovingController_UsesTargetlessAbilityAndMovesOnlyWhenConditionIsUnmet()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemies/Runtime/Melee/Slime.prefab");
        var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
        var controller = enemy.GetComponent<MovingEnemyController>();
        var movement = enemy.GetComponent<EnemyMovement>();
        var animation = enemy.GetComponent<EnemyAnimation>();
        var body = enemy.GetComponent<Rigidbody2D>();
        var ability = enemy.AddComponent<EnemyAbilityTestProbe>();

        typeof(MovingEnemyController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(controller, null);
        typeof(EnemyMovement).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(movement, null);
        typeof(EnemyAnimation).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(animation, null);
        enemy.GetComponent<Animator>().Rebind();
        var references = new SerializedObject(controller);
        references.FindProperty("enemyAbility").objectReferenceValue = ability;
        references.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(enemy.GetComponent<EnemyTargeting>().Target, Is.Null);
        movement.Move(Vector2.right);
        controller.ExecuteBehavior();
        Assert.That(ability.Activations, Is.EqualTo(1));
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));

        movement.Move(Vector2.right);
        controller.ExecuteBehavior();
        Assert.That(ability.Activations, Is.EqualTo(1));
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));

        ability.Condition = false;
        GameObject target = Create("Target");
        target.transform.position = enemy.transform.position + Vector3.right * 5f;
        enemy.GetComponent<EnemyTargeting>().SetTarget(target.transform);
        typeof(EnemyMovement).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(movement, new[] { target.transform.position });
        controller.ExecuteBehavior();
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
        Assert.That(ability.Activations, Is.EqualTo(1));
    }

    [Test]
    public void CloseRangeAbility_ChecksColliderSurfaceDistanceAndAppliesDamageOnce()
    {
        GameObject enemy = Create("Enemy");
        enemy.AddComponent<BoxCollider2D>();
        var targeting = enemy.AddComponent<EnemyTargeting>();
        var ability = enemy.AddComponent<EnemyCloseRangeAbility>();
        typeof(EnemyCloseRangeAbility).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(ability, null);

        GameObject target = Create("Target");
        target.AddComponent<BoxCollider2D>();
        var receiver = target.AddComponent<EnemyAbilityDamageReceiver>();
        var targetable = target.AddComponent<TargetableComponent>();
        targetable.SetTargetType(TargetType.Turret);
        targetable.SetHealth(100, 100);
        targeting.SetTarget(target.transform);

        target.transform.position = new Vector3(3f, 0f, 0f);
        Physics2D.SyncTransforms();
        Assert.That(ability.CheckCondition(), Is.False);
        Assert.That(ability.TryUse(), Is.False);
        Assert.That(receiver.CurrentHealth, Is.EqualTo(100));

        target.transform.position = new Vector3(1.2f, 0f, 0f);
        Physics2D.SyncTransforms();
        Assert.That(ability.CheckCondition(), Is.True);
        Assert.That(ability.TryUse(), Is.True);
        Assert.That(receiver.CurrentHealth, Is.EqualTo(95));
        Assert.That(ability.CheckCondition(), Is.True);
        Assert.That(ability.TryUse(), Is.False);
        Assert.That(receiver.CurrentHealth, Is.EqualTo(95));

        targeting.SetTarget(null);
        Assert.That(ability.CheckCondition(), Is.False);
    }

    [TestCase("Assets/Prefabs/Enemies/Base/MeleeEnemy.prefab", 0.4f)]
    [TestCase("Assets/Prefabs/Enemies/Runtime/Melee/Slime.prefab", 1f)]
    [TestCase("Assets/Prefabs/Enemies/Runtime/Slime.prefab", 1f)]
    [TestCase("Assets/Prefabs/Enemies/Testing/SlimeTest.prefab", 0.4f)]
    public void PrefabMigration_PreservesReferencesAndAttackSettings(string path, float expectedRange)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null);
        foreach (Component component in prefab.GetComponents<Component>())
            Assert.That(component, Is.Not.Null, path + " contains a missing script.");

        var ability = prefab.GetComponent<EnemyCloseRangeAbility>();
        var controller = prefab.GetComponent<MovingEnemyController>();
        Assert.That(ability, Is.Not.Null);
        Assert.That(controller, Is.Not.Null);
        Assert.That(new SerializedObject(controller).FindProperty("enemyAbility").objectReferenceValue,
            Is.EqualTo(ability));

        var settings = new SerializedObject(ability);
        Assert.That(settings.FindProperty("activationRange").floatValue, Is.EqualTo(expectedRange).Within(0.0001f));
        Assert.That(settings.FindProperty("cooldown").floatValue, Is.EqualTo(1f));
        Assert.That(settings.FindProperty("attackDamage").intValue, Is.EqualTo(5));
    }

    private GameObject Create(string name)
    {
        var gameObject = new GameObject(name);
        SceneManager.MoveGameObjectToScene(gameObject, _scene);
        return gameObject;
    }
}

public sealed class EnemyAbilityTestProbe : EnemyAbility
{
    public bool Condition = true;
    public bool Succeeds = true;
    public int Activations;

    public override bool CheckCondition() => Condition;

    protected override bool Activate()
    {
        Activations++;
        return Succeeds;
    }
}

public sealed class EnemyAbilityDamageReceiver : MonoBehaviour, IDamageableTarget
{
    public int CurrentHealth { get; private set; } = 100;
    public int MaxHealth => 100;
    public bool IsDestroyed => CurrentHealth <= 0;

    public bool ApplyDamage(int damage)
    {
        CurrentHealth -= damage;
        return true;
    }
}
