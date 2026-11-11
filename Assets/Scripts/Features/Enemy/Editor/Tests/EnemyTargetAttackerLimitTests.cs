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

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.ClosePreviewScene(_scene);
    }

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

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Assign_NonPositiveCountDoesNotChangeAssignments(int count)
    {
        Assert.That(_target.TryAssignAttackers(6), Is.True);

        Assert.That(_target.TryAssignAttackers(count), Is.False);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(6));
    }

    [TestCase(5)]
    [TestCase(int.MaxValue)]
    public void Assign_OverCapacityDoesNotPartiallyAssign(int count)
    {
        Assert.That(_target.TryAssignAttackers(6), Is.True);

        Assert.That(_target.TryAssignAttackers(count), Is.False);
        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(6));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void Unassign_NonPositiveCountDoesNotChangeAssignments(int count)
    {
        Assert.That(_target.TryAssignAttackers(6), Is.True);

        _target.UnassignAttackers(count);

        Assert.That(_target.AssignedAttackerCount, Is.EqualTo(6));
    }

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
