using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 분대의 타깃 유지·무효화 후 재검색과 타깃 규칙을 검사한다.
/// </summary>
public sealed class EnemyAutoRetargetTests
{
    private readonly List<GameObject> _objects = new();
    private readonly List<(TargetableComponent Target, int Count)> _occupiedTargets = new();
    private Vector3 _origin;

    [SetUp]
    public void SetUp()
    {
        float maxX = 0f;
        foreach (TargetableComponent target in Object.FindObjectsByType<TargetableComponent>(FindObjectsSortMode.None))
        {
            maxX = Mathf.Max(maxX, target.transform.position.x);

            // 열려 있는 씬의 타깃이 테스트 검색 후보에 섞이지 않게 남은 인원만 채운다.
            int remaining = target.MaxAttackers - target.AssignedAttackerCount;
            if (remaining > 0 && target.TryAssignAttackers(remaining))
                _occupiedTargets.Add((target, remaining));
        }

        _origin = new Vector3(maxX + 1000f, 0f, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        // 타깃을 파괴하기 전에 테스트 분대의 구독과 배정을 먼저 정리한다.
        foreach (GameObject created in _objects)
        {
            if (created != null && created.TryGetComponent(out EnemyAutoRetargetTestController controller))
                controller.LeaveSquad();
        }

        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i] != null)
                Object.DestroyImmediate(_objects[i]);
        _objects.Clear();

        foreach (var occupied in _occupiedTargets)
            if (occupied.Target != null)
                occupied.Target.UnassignAttackers(occupied.Count);
        _occupiedTargets.Clear();
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
    public void SharedSquad_TargetabilityLossImmediatelyReassignsAllMembers()
    {
        TargetableComponent first = CreateTarget("First", 4f);
        TargetableComponent second = CreateTarget("Second", 1f);
        TargetableComponent third = CreateTarget("Third", 2f);
        var leader = CreateEnemy();
        var follower = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared, EnemyTargetRule.PlayerOrTurret, first.transform);
        Assert.That(leader.JoinSquad(squad), Is.True);
        Assert.That(follower.JoinSquad(squad), Is.True);
        Assert.That(first.AssignedAttackerCount, Is.EqualTo(2));

        leader.RefreshTarget();
        Assert.That(leader.AssignedTarget, Is.EqualTo(first.transform));
        Assert.That(leader.TargetChanges, Is.EqualTo(1));

        first.SetHealth(0, 100);
        Assert.That(first.AssignedAttackerCount, Is.Zero);
        Assert.That(second.AssignedAttackerCount, Is.EqualTo(2));
        Assert.That(leader.AssignedTarget, Is.EqualTo(second.transform), "타깃 불가 이벤트에서 즉시 재검색한다.");
        Assert.That(follower.AssignedTarget, Is.EqualTo(second.transform));

        int targetChanges = leader.TargetChanges;
        follower.RefreshTarget();
        leader.RefreshTarget();
        Assert.That(leader.TargetChanges, Is.EqualTo(targetChanges), "타깃이 있으면 반복 갱신으로 재배정하지 않는다.");

        DisableTarget(second);
        Assert.That(leader.AssignedTarget, Is.SameAs(third.transform));
        Assert.That(follower.AssignedTarget, Is.SameAs(third.transform));
        Object.DestroyImmediate(second.gameObject);
        Assert.That(third.AssignedAttackerCount, Is.EqualTo(2));

        leader.LeaveSquad();
        Assert.That(third.AssignedAttackerCount, Is.EqualTo(1));
        TargetableComponent fourth = CreateTarget("Fourth", 3f);
        third.SetHealth(0, 100);
        Assert.That(follower.AssignedTarget, Is.EqualTo(fourth.transform), "구성원 제거 후에도 남은 분대에 이벤트를 전달한다.");
        Assert.That(third.AssignedAttackerCount, Is.Zero);
        Assert.That(fourth.AssignedAttackerCount, Is.EqualTo(1));

        follower.LeaveSquad();
        Assert.That(squad.IsEmpty, Is.True);
        Assert.That(fourth.AssignedAttackerCount, Is.Zero);
    }

    [Test]
    public void PlayerPriority_WhenFullKeepsTurretAndTransfersOnlyWhenWholeSquadFits()
    {
        TargetableComponent turret = CreateTarget("CurrentTurret", 1f);
        TargetableComponent player = CreateTarget("DetectedPlayer", 2f);
        player.SetTargetType(TargetType.Player);
        Assert.That(player.TryAssignAttackers(9), Is.True);

        var leader = CreateEnemy();
        var follower = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared,
            EnemyTargetRule.PlayerOrTurret, turret.transform);
        Assert.That(leader.JoinSquad(squad), Is.True);
        Assert.That(follower.JoinSquad(squad), Is.True);

        Assert.That(squad.TryPrioritizePlayer(leader, player), Is.False);
        Assert.That(leader.AssignedTarget, Is.EqualTo(turret.transform));
        Assert.That(follower.AssignedTarget, Is.EqualTo(turret.transform));
        Assert.That(turret.AssignedAttackerCount, Is.EqualTo(2));
        Assert.That(player.AssignedAttackerCount, Is.EqualTo(9));

        player.UnassignAttackers(1);
        Assert.That(squad.TryPrioritizePlayer(leader, player), Is.True);
        Assert.That(turret.AssignedAttackerCount, Is.Zero);
        Assert.That(player.AssignedAttackerCount, Is.EqualTo(10));
        Assert.That(leader.AssignedTarget, Is.EqualTo(player.transform));
        Assert.That(follower.AssignedTarget, Is.EqualTo(player.transform));

        Assert.That(squad.TryPrioritizePlayer(follower, player), Is.False);
        Assert.That(player.AssignedAttackerCount, Is.EqualTo(10));
        leader.LeaveSquad();
        leader.LeaveSquad();
        Assert.That(player.AssignedAttackerCount, Is.EqualTo(9));
        follower.LeaveSquad();
        Assert.That(player.AssignedAttackerCount, Is.EqualTo(8));
    }

    [Test]
    public void NewMember_WhenTargetCannotFit_ClearsPreviousAssignmentsBeforeJoining()
    {
        TargetableComponent fullTarget = CreateTarget("FullTarget", 1f);
        TargetableComponent replacement = CreateTarget("Replacement", 2f);
        Assert.That(fullTarget.TryAssignAttackers(9), Is.True);

        var leader = CreateEnemy();
        var follower = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared,
            EnemyTargetRule.PlayerOrTurret, fullTarget.transform);
        Assert.That(leader.JoinSquad(squad), Is.True);
        Assert.That(fullTarget.AssignedAttackerCount, Is.EqualTo(10));

        Assert.That(follower.JoinSquad(squad), Is.True);
        Assert.That(fullTarget.AssignedAttackerCount, Is.EqualTo(9));
        Assert.That(leader.AssignedTarget, Is.Null);
        Assert.That(follower.AssignedTarget, Is.Null);

        Assert.That(squad.SetTarget(), Is.True);
        Assert.That(leader.AssignedTarget, Is.EqualTo(replacement.transform));
        Assert.That(follower.AssignedTarget, Is.EqualTo(replacement.transform));
        Assert.That(replacement.AssignedAttackerCount, Is.EqualTo(2));

        leader.LeaveSquad();
        follower.LeaveSquad();
        Assert.That(replacement.AssignedAttackerCount, Is.Zero);
        Assert.That(fullTarget.AssignedAttackerCount, Is.EqualTo(9));
    }

    [Test]
    public void ExcludedMembers_JoinFullTargetWithoutChangingItsAssignments()
    {
        TargetableComponent target = CreateTarget("FullTarget", 1f);
        Assert.That(target.TryAssignAttackers(10), Is.True);
        var first = CreateEnemy();
        var second = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared,
            EnemyTargetRule.PlayerOrTurret, target.transform);

        Assert.That(first.JoinSquad(squad, false), Is.True);
        Assert.That(second.JoinSquad(squad, false), Is.True);
        Assert.That(first.AssignedTarget, Is.SameAs(target.transform));
        Assert.That(second.AssignedTarget, Is.SameAs(target.transform));
        Assert.That(target.AssignedAttackerCount, Is.EqualTo(10));

        first.LeaveSquad();
        second.LeaveSquad();
        Assert.That(target.AssignedAttackerCount, Is.EqualTo(10));
    }

    [Test]
    public void MixedSquad_TransfersOnlyIncludedMembersAndSharesTargetWithExcludedMembers()
    {
        TargetableComponent turret = CreateTarget("Turret", 1f);
        TargetableComponent player = CreateTarget("Player", 2f);
        player.SetTargetType(TargetType.Player);
        Assert.That(player.TryAssignAttackers(8), Is.True);
        var first = CreateEnemy();
        var excluded = CreateEnemy();
        var second = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared,
            EnemyTargetRule.PlayerOrTurret, turret.transform);
        Assert.That(first.JoinSquad(squad), Is.True);
        Assert.That(excluded.JoinSquad(squad, false), Is.True);
        Assert.That(second.JoinSquad(squad), Is.True);
        Assert.That(turret.AssignedAttackerCount, Is.EqualTo(2));

        Assert.That(squad.TryPrioritizePlayer(first, player), Is.True);
        Assert.That(turret.AssignedAttackerCount, Is.Zero);
        Assert.That(player.AssignedAttackerCount, Is.EqualTo(10));
        Assert.That(first.AssignedTarget, Is.SameAs(player.transform));
        Assert.That(excluded.AssignedTarget, Is.SameAs(player.transform));
        Assert.That(second.AssignedTarget, Is.SameAs(player.transform));

        excluded.LeaveSquad();
        Assert.That(player.AssignedAttackerCount, Is.EqualTo(10));
        first.LeaveSquad();
        second.LeaveSquad();
        Assert.That(player.AssignedAttackerCount, Is.EqualTo(8));
    }

    [Test]
    public void ExcludedSquad_SearchesAndRetargetsWithoutCapacityRestriction()
    {
        TargetableComponent first = CreateTarget("NearestFullTarget", 1f);
        TargetableComponent second = CreateTarget("NextFullTarget", 2f);
        Assert.That(first.TryAssignAttackers(10), Is.True);
        Assert.That(second.TryAssignAttackers(10), Is.True);
        var member = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared, EnemyTargetRule.PlayerOrTurret);
        Assert.That(member.JoinSquad(squad, false), Is.True);

        Assert.That(squad.SetTarget(), Is.True);
        Assert.That(member.AssignedTarget, Is.SameAs(first.transform));
        Assert.That(first.AssignedAttackerCount, Is.EqualTo(10));

        first.SetHealth(0, 100);
        Assert.That(member.AssignedTarget, Is.SameAs(second.transform));
        Assert.That(first.AssignedAttackerCount, Is.EqualTo(10));
        Assert.That(second.AssignedAttackerCount, Is.EqualTo(10));
        member.LeaveSquad();
        Assert.That(second.AssignedAttackerCount, Is.EqualTo(10));
    }

    [Test]
    public void MixedSquad_RestoreUsesOnlyIncludedMembersWithoutDoubleAssignment()
    {
        TargetableComponent current = CreateTarget("Current", 1f);
        TargetableComponent replacement = CreateTarget("Replacement", 2f);
        Assert.That(replacement.TryAssignAttackers(9), Is.True);
        var included = CreateEnemy();
        var excluded = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared,
            EnemyTargetRule.PlayerOrTurret, current.transform);
        Assert.That(included.JoinSquad(squad), Is.True);
        Assert.That(excluded.JoinSquad(squad, false), Is.True);

        MethodInfo restore = typeof(EnemySquad).GetMethod(
            "RestoreCheckpointTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        restore.Invoke(squad, new object[] { replacement.transform });
        restore.Invoke(squad, new object[] { replacement.transform });
        Assert.That(current.AssignedAttackerCount, Is.Zero);
        Assert.That(replacement.AssignedAttackerCount, Is.EqualTo(10));
        Assert.That(included.AssignedTarget, Is.SameAs(replacement.transform));
        Assert.That(excluded.AssignedTarget, Is.SameAs(replacement.transform));

        excluded.LeaveSquad();
        included.LeaveSquad();
        Assert.That(replacement.AssignedAttackerCount, Is.EqualTo(9));
    }

    [Test]
    public void RegisteredMember_RejectsAnotherSquadAndKeepsOriginalInclusionSetting()
    {
        TargetableComponent firstTarget = CreateTarget("FirstTarget", 1f);
        TargetableComponent secondTarget = CreateTarget("SecondTarget", 2f);
        var member = CreateEnemy();
        var firstSquad = new EnemySquad(EnemySquadMode.Shared,
            EnemyTargetRule.PlayerOrTurret, firstTarget.transform);
        var secondSquad = new EnemySquad(EnemySquadMode.Shared,
            EnemyTargetRule.PlayerOrTurret, secondTarget.transform);
        Assert.That(member.JoinSquad(firstSquad), Is.True);

        Assert.That(member.JoinSquad(firstSquad, false), Is.True);
        Assert.That(member.JoinSquad(secondSquad, false), Is.False);
        Assert.That(member.IsIncludedInAttackerLimit, Is.True);
        Assert.That(member.AssignedTarget, Is.SameAs(firstTarget.transform));
        Assert.That(firstTarget.AssignedAttackerCount, Is.EqualTo(1));
        Assert.That(secondTarget.AssignedAttackerCount, Is.Zero);

        member.LeaveSquad();
        Assert.That(firstTarget.AssignedAttackerCount, Is.Zero);
    }

    [Test]
    public void RestoreTarget_DoesNotDoubleAssignAndClearsWhenReplacementIsFull()
    {
        TargetableComponent current = CreateTarget("Current", 1f);
        TargetableComponent fullTarget = CreateTarget("FullReplacement", 2f);
        Assert.That(fullTarget.TryAssignAttackers(10), Is.True);
        var member = CreateEnemy();
        var squad = new EnemySquad(EnemySquadMode.Shared,
            EnemyTargetRule.PlayerOrTurret, current.transform);
        Assert.That(member.JoinSquad(squad), Is.True);

        MethodInfo restore = typeof(EnemySquad).GetMethod(
            "RestoreCheckpointTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        restore.Invoke(squad, new object[] { current.transform });
        Assert.That(current.AssignedAttackerCount, Is.EqualTo(1));

        restore.Invoke(squad, new object[] { fullTarget.transform });
        Assert.That(member.AssignedTarget, Is.Null);
        Assert.That(current.AssignedAttackerCount, Is.Zero);
        Assert.That(fullTarget.AssignedAttackerCount, Is.EqualTo(10));
        member.LeaveSquad();
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
        DisableTarget(target);
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
        _objects.Add(gameObject);
        gameObject.transform.position = _origin;
        return gameObject;
    }

    private static void DisableTarget(TargetableComponent target)
    {
        target.enabled = false;

        // EditMode에서는 일반 MonoBehaviour의 비활성화 콜백을 명시적으로 호출한다.
        typeof(TargetableComponent).GetMethod(
            "OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
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
