using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>타깃의 공격 인원 배정 한계와 해제, 타깃 불가 상태를 검사한다.</summary>
public sealed class EnemyTargetAttackerLimitTests
{
    private Scene _scene;
    private TargetableComponent _component;
    private ITargetable _target;

    /// <summary>현재 게임 씬과 분리된 Preview Scene에 최대 인원 10의 타깃을 준비한다.</summary>
    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        var targetObject = new GameObject("AttackerLimitTestTarget");
        SceneManager.MoveGameObjectToScene(targetObject, _scene);
        _component = targetObject.AddComponent<TargetableComponent>();

        var settings = new SerializedObject(_component);
        settings.FindProperty("maxAttackers").intValue = 10;
        settings.ApplyModifiedPropertiesWithoutUndo();

        _component.SetHealth(100, 100);
        _target = _component;
    }

    /// <summary>테스트용 Preview Scene과 그 안의 오브젝트를 정리한다.</summary>
    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.ClosePreviewScene(_scene);
    }

    /// <summary>새 Definition과 기본 슬라임은 집계하고 봄버 에셋만 제외하도록 설정되어 있다.</summary>
    [Test]
    public void Definition_DefaultIncludesAttackersAndBomberAssetIsExcluded()
    {
        var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        try
        {
            Assert.That(definition.IsIncludedInAttackerLimit, Is.True);
            var slime = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/Data/Enemies/Spawning/Definitions/Slime.asset");
            Assert.That(slime, Is.Not.Null);
            Assert.That(slime.IsIncludedInAttackerLimit, Is.True);
            var bomber = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/Data/Enemies/Spawning/Definitions/BomberSlime.asset");
            Assert.That(bomber, Is.Not.Null);
            Assert.That(bomber.IsIncludedInAttackerLimit, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    /// <summary>전체 인원을 배정할 수 있을 때만 수락하고 반환된 여유를 다시 사용할 수 있다.</summary>
    [Test]
    public void Assignments_RespectLimitAndReuseReleasedCapacity()
    {
        Assert.That(_target.AssignedAttackerCount, Is.Zero);
        Assert.That(_target.TryAssignAttackers(6), Is.True);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(6));

        Assert.That(_target.TryAssignAttackers(5), Is.False);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(6));

        Assert.That(_target.TryAssignAttackers(4), Is.True);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(10));
        Assert.That(_target.TryAssignAttackers(1), Is.False);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(10));

        _target.UnassignAttackers(3);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(7));
        Assert.That(_target.TryAssignAttackers(3), Is.True);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(10));
    }

    /// <summary>0이나 음수의 배정 요청은 기존 인원을 변경하지 않는다.</summary>
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Assign_NonPositiveCountDoesNotChangeAssignments(int count)
    {
        Assert.That(_target.TryAssignAttackers(6), Is.True);

        Assert.That(_target.TryAssignAttackers(count), Is.False);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(6));
    }

    /// <summary>남은 자리보다 큰 요청은 일부만 배정하지 않고 전체를 거부한다.</summary>
    [TestCase(5)]
    [TestCase(int.MaxValue)]
    public void Assign_OverCapacityDoesNotPartiallyAssign(int count)
    {
        Assert.That(_target.TryAssignAttackers(6), Is.True);

        Assert.That(_target.TryAssignAttackers(count), Is.False);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(6));
    }

    /// <summary>0이나 음수의 반환 요청은 기존 배정을 변경하지 않는다.</summary>
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Unassign_NonPositiveCountDoesNotChangeAssignments(int count)
    {
        Assert.That(_target.TryAssignAttackers(6), Is.True);

        _target.UnassignAttackers(count);

        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(6));
    }

    /// <summary>현재 인원보다 많이 반환해도 음수가 되거나 이후 배정을 막지 않는다.</summary>
    [TestCase(10)]
    [TestCase(int.MaxValue)]
    public void Unassign_ExcessCountDoesNotMakeAssignmentsNegative(int count)
    {
        Assert.That(_target.TryAssignAttackers(6), Is.True);

        _target.UnassignAttackers(count);

        Assert.That(_target.AssignedAttackerCount, Is.Zero);
        Assert.That(_target.TryAssignAttackers(10), Is.True);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(10));
    }

    /// <summary>체력 0, 컴포넌트 비활성, 오브젝트 비활성 상태에서는 새 배정을 거부한다.</summary>
    [TestCase(0, true, true)]
    [TestCase(100, false, true)]
    [TestCase(100, true, false)]
    public void Assign_UntargetableStateRejectsAdditionalAttackers(
        int health, bool componentEnabled, bool objectActive)
    {
        Assert.That(_target.TryAssignAttackers(3), Is.True);
        _component.SetHealth(health, 100);
        _component.enabled = componentEnabled;
        _component.gameObject.SetActive(objectActive);

        Assert.That(_target.CanBeTargeted, Is.False);
        Assert.That(_target.TryAssignAttackers(1), Is.False);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(3));
    }

    /// <summary>타깃 불가 상태에서도 반환할 수 있고 회복 후 다시 배정할 수 있다.</summary>
    [Test]
    public void LostTarget_CanReleaseAssignmentsAndAcceptNewOnesAfterRecovery()
    {
        Assert.That(_target.TryAssignAttackers(4), Is.True);
        _component.SetHealth(0, 100);

        // 타깃 불가 상태에서도 분대가 기존 배정을 반환할 수 있어야 한다.
        _target.UnassignAttackers(4);
        Assert.That(_target.AssignedAttackerCount, Is.Zero);

        _component.SetHealth(100, 100);
        Assert.That(_target.TryAssignAttackers(10), Is.True);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(10));
    }
}
