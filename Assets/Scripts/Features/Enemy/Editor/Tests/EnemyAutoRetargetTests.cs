using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EnemyAutoRetargetTests
{
    private Scene _scene;
    private Scene _previousActiveScene;
    private Vector3 _origin;

    [SetUp]
    public void SetUp()
    {
        float maxX = 0f;
        foreach (TargetableComponent target in Object.FindObjectsByType<TargetableComponent>(FindObjectsSortMode.None))
            maxX = Mathf.Max(maxX, target.transform.position.x);

        _origin = new Vector3(maxX + 1000f, 0f, 0f);
        _previousActiveScene = SceneManager.GetActiveScene();
        _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
    }

    [TearDown]
    public void TearDown()
    {
        if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(_previousActiveScene);

        EditorSceneManager.CloseScene(_scene, true);
    }

    [Test]
    public void UnboundController_DoesNotOverrideExternallyAssignedTarget()
    {
        var controller = CreateEnemy();
        TargetableComponent target = CreateTarget("Assigned", 1f);
        controller.AssignTarget(target.transform);
        target.SetHealth(0, 100);

        controller.RefreshTarget();

        Assert.That(controller.AssignedTarget, Is.EqualTo(target.transform));
        Assert.That(controller.TargetChanges, Is.EqualTo(1));
    }

    [Test]
    public void SharedSquad_OnlyRepresentativeReassignsAllMembers()
    {
        TargetableComponent first = CreateTarget("First", 4f);
        TargetableComponent second = CreateTarget("Second", 1f);
        TargetableComponent third = CreateTarget("Third", 2f);
        var leader = CreateEnemy();
        var follower = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared, EnemyTargetRule.PlayerOrTurret, first.transform);
        Assert.That(leader.JoinSquad(squad), Is.True);
        Assert.That(follower.JoinSquad(squad), Is.True);

        leader.RefreshTarget();
        Assert.That(leader.AssignedTarget, Is.EqualTo(first.transform));
        Assert.That(leader.TargetChanges, Is.EqualTo(1));

        first.SetHealth(0, 100);
        follower.RefreshTarget();
        Assert.That(follower.AssignedTarget, Is.EqualTo(first.transform), "대표가 아닌 구성원은 검색하지 않는다.");

        leader.RefreshTarget();
        Assert.That(leader.AssignedTarget, Is.EqualTo(second.transform));
        Assert.That(follower.AssignedTarget, Is.EqualTo(second.transform));

        Object.DestroyImmediate(second.gameObject);
        SetSearchTime(squad, 0f);
        leader.RefreshTarget();
        Assert.That(leader.AssignedTarget, Is.EqualTo(third.transform));
        Assert.That(follower.AssignedTarget, Is.EqualTo(third.transform));

        leader.LeaveSquad();
        third.SetHealth(0, 100);
        TargetableComponent fourth = CreateTarget("Fourth", 3f);
        SetSearchTime(squad, 0f);
        follower.RefreshTarget();
        Assert.That(follower.AssignedTarget, Is.EqualTo(fourth.transform), "대표가 제거되면 남은 구성원이 갱신한다.");

        follower.LeaveSquad();
        Assert.That(squad.IsEmpty, Is.True);
    }

    [Test]
    public void RetryDelay_IsSharedAndClearsEveryMembersDestination()
    {
        TargetableComponent target = CreateTarget("Invalid", 1f);
        var leader = CreateEnemy();
        var follower = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared, EnemyTargetRule.PlayerOrTurret, target.transform);
        leader.JoinSquad(squad);
        follower.JoinSquad(squad);
        target.enabled = false;
        SetSearchTime(squad, Time.time + 10f);

        leader.RefreshTarget();
        Assert.That(leader.AssignedTarget, Is.Null);
        Assert.That(follower.AssignedTarget, Is.Null);
        Assert.That(follower.GetComponent<EnemyTargeting>().TargetColliders, Is.Null);

        TargetableComponent replacement = CreateTarget("NewCandidate", 2f);
        leader.RefreshTarget();
        follower.RefreshTarget();
        Assert.That(leader.TargetChanges, Is.EqualTo(2));
        Assert.That(follower.TargetChanges, Is.EqualTo(2));

        SetSearchTime(squad, 0f);
        follower.RefreshTarget();
        Assert.That(follower.AssignedTarget, Is.Null);
        leader.RefreshTarget();
        Assert.That(leader.AssignedTarget, Is.EqualTo(replacement.transform));
        Assert.That(follower.AssignedTarget, Is.EqualTo(replacement.transform));
    }

    [Test]
    public void PlayerOnlySquad_RejectsInheritedTurretAndChoosesPlayer()
    {
        TargetableComponent turret = CreateTarget("Turret", 1f);
        TargetableComponent player = CreateTarget("Player", 2f);
        player.SetTargetType(TargetType.Player);
        var controller = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared, EnemyTargetRule.PlayerOnly, turret.transform);
        controller.JoinSquad(squad);

        controller.RefreshTarget();

        Assert.That(controller.AssignedTarget, Is.EqualTo(player.transform));
    }

    private EnemyAutoRetargetTestController CreateEnemy()
    {
        GameObject enemy = Create("Enemy");
        enemy.AddComponent<EnemyTargeting>();
        var controller = enemy.AddComponent<EnemyAutoRetargetTestController>();
        controller.InitializeReferences();
        return controller;
    }

    private TargetableComponent CreateTarget(string name, float distance)
    {
        GameObject target = Create(name);
        target.transform.position = _origin + Vector3.right * distance;
        target.AddComponent<BoxCollider2D>();
        var targetable = target.AddComponent<TargetableComponent>();
        targetable.SetTargetType(TargetType.Turret);
        targetable.SetHealth(100, 100);
        return targetable;
    }

    private GameObject Create(string name)
    {
        var gameObject = new GameObject(name);
        SceneManager.MoveGameObjectToScene(gameObject, _scene);
        gameObject.transform.position = _origin;
        return gameObject;
    }

    private static void SetSearchTime(EnemySquad squad, float time)
    {
        typeof(EnemySquad).GetField("_nextSearchTime", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(squad, time);
    }
}

public sealed class EnemyAutoRetargetTestController : EnemyController
{
    public Transform AssignedTarget { get; private set; }
    public int TargetChanges { get; private set; }

    public void InitializeReferences() => base.Awake();
    public override void ExecuteBehavior() { }

    protected override void OnTargetChanged(Transform target)
    {
        AssignedTarget = target;
        TargetChanges++;
    }
}
