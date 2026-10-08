using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>Unity 생명주기로 초기화된 스킬과 HUD의 동작을 검증합니다.</summary>
public sealed class PlayerSkillRuntimeTests
{
    private GameObject _player;
    private PlayerSkillController _controller;
    private PlayerSkillHud _hud;
    private bool _preparing;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _preparing = true;
        _player = new GameObject("Skill runtime test player");
        _player.SetActive(false);
        _player.transform.position = new Vector3(10000, 10000, 0);
        _player.layer = LayerMask.NameToLayer("Player");
        _controller = _player.AddComponent<PlayerSkillController>();
        Assign(_player.GetComponent<PlayerBomb>(), "bombPrefab", "Assets/Prefabs/Player/bomb.prefab");
        Assign(_player.GetComponent<PlayerTurretSkill>(), "turretPrefab", "Assets/Prefabs/Player/TemporaryTurret.prefab");
        Assign(_player.GetComponent<PlayerBarrierSkill>(), "_prefab", "Assets/Prefabs/Player/EnergyBarrier.prefab", true);
        Assign(_player.GetComponent<PlayerBeaconSkill>(), "_prefab", "Assets/Prefabs/Player/LureBeacon.prefab", true);
        var source = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab").GetComponent<PlayerSkillController>());
        var data = new SerializedObject(_controller);
        foreach (string field in new[] { "bombIcon", "turretIcon", "_barrierIcon", "_beaconIcon", "uiFont", "_recoveryKeyIcon" })
            data.FindProperty(field).objectReferenceValue = source.FindProperty(field).objectReferenceValue;
        data.ApplyModifiedPropertiesWithoutUndo();
        _controller.BindSceneState(() => _preparing, () => true);
        _player.SetActive(true); // Awake/OnEnable을 Unity가 한 번씩 실행합니다.
        yield return null; // Start에서 HUD가 생성될 때까지 기다립니다.
        _hud = _player.GetComponentInChildren<PlayerSkillHud>();
    }

    private static void Assign(MonoBehaviour component, string field, string path, bool device = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var data = new SerializedObject(component);
        data.FindProperty(field).objectReferenceValue = device ? (Object)prefab.GetComponent<PlayerFieldDevice>() : prefab;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_player != null) Object.DestroyImmediate(_player);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SelectionPanel_BlocksSkillUseWhileOpen()
    {
        _hud.Open();
        Assert.That(_hud.IsOpen, Is.True);
        Assert.That(_controller.CanUse, Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator SelectionPanel_ClosesWhenPreparationEnds()
    {
        _hud.Open(); _preparing = false;
        yield return null;
        Assert.That(_hud.IsOpen, Is.False);
        _hud.Open();
        Assert.That(_hud.IsOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator SelectionPanel_DevelopmentCardsCannotBeSelected()
    {
        var panel = _hud.transform.Find("Skill Selection/Panel Border/Skill Selection Panel");
        for (int i = 5; i <= 8; i++)
        {
            var card = panel.Find("Development Slot " + i);
            Assert.That(card.GetComponent<Button>(), Is.Null);
            Assert.That(card.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("개발 중"));
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator Tooltip_IsAvailableAfterPanelCloses()
    {
        _controller.TrySelect(PlayerSkillId.TemporaryTurret);
        _hud.Close(); _preparing = false;
        yield return null;
        var slot = _hud.transform.Find("Q Skill Slot").gameObject;
        ExecuteEvents.Execute(slot, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
        var tooltip = _hud.transform.Find("Skill Tooltip");
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        Assert.That(tooltip.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("보조 포탑"));
        foreach (var graphic in tooltip.GetComponentsInChildren<Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
        ExecuteEvents.Execute(slot, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
        Assert.That(tooltip.gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator Barrier_FirstPulseAndCancellationDoNotConsumeCharge()
    {
        _controller.TrySelect(PlayerSkillId.EnergyBarrier);
        var skill = _player.GetComponent<PlayerBarrierSkill>();
        Assert.That(_controller.TryUse(PlayerSkillId.EnergyBarrier), Is.True);
        Assert.That(skill.IsPending, Is.True);
        Assert.That(skill.Charges, Is.EqualTo(3));
        skill.CancelPlacement();
        Assert.That(skill.IsPending, Is.False);
        Assert.That(skill.Charges, Is.EqualTo(3));
        yield break;
    }

    [UnityTest]
    public IEnumerator Barrier_OutOfRangePulsePreservesPendingPlacement()
    {
        _controller.TrySelect(PlayerSkillId.EnergyBarrier);
        _controller.TryUse(PlayerSkillId.EnergyBarrier);
        _player.transform.position += Vector3.right * 7;
        Assert.That(_controller.TryUse(PlayerSkillId.EnergyBarrier), Is.False);
        var skill = _player.GetComponent<PlayerBarrierSkill>();
        Assert.That(skill.IsPending, Is.True);
        Assert.That(skill.Charges, Is.EqualTo(3));
        yield break;
    }

    [UnityTest]
    public IEnumerator Barrier_CompletedConnectionConsumesOneCharge()
    {
        _controller.TrySelect(PlayerSkillId.EnergyBarrier);
        _controller.TryUse(PlayerSkillId.EnergyBarrier);
        _player.transform.position += Vector3.right * 3;
        Assert.That(_controller.TryUse(PlayerSkillId.EnergyBarrier), Is.True);
        var skill = _player.GetComponent<PlayerBarrierSkill>();
        Assert.That(skill.IsPending, Is.False);
        Assert.That(skill.Charges, Is.EqualTo(2));
        yield break;
    }

    [UnityTest]
    public IEnumerator Barrier_TerrainBlocksConnectionWithoutConsumingCharge()
    {
        _controller.TrySelect(PlayerSkillId.EnergyBarrier); _controller.TryUse(PlayerSkillId.EnergyBarrier);
        var obstacle = new GameObject("Impassable terrain", typeof(BoxCollider2D));
        try
        {
            obstacle.transform.position = _player.transform.position + Vector3.right * 1.5f;
            for (int layer = 0; layer < 32; layer++)
                if (!Physics2D.GetIgnoreLayerCollision(_player.layer, layer)) { obstacle.layer = layer; break; }
            _player.transform.position += Vector3.right * 3;
            Physics2D.SyncTransforms();
            Assert.That(_controller.TryUse(PlayerSkillId.EnergyBarrier), Is.False);
            Assert.That(_player.GetComponent<PlayerBarrierSkill>().Charges, Is.EqualTo(3));
        }
        finally { Object.DestroyImmediate(obstacle); }
        yield break;
    }

    [UnityTest]
    public IEnumerator Barrier_ExpiredPulseDoesNotConsumeCharge()
    {
        _controller.TrySelect(PlayerSkillId.EnergyBarrier); _controller.TryUse(PlayerSkillId.EnergyBarrier);
        var skill = _player.GetComponent<PlayerBarrierSkill>(); skill.Tick(15);
        Assert.That(skill.IsPending, Is.False);
        Assert.That(skill.Charges, Is.EqualTo(3));
        yield break;
    }

    [UnityTest]
    public IEnumerator Turret_RecoveryDoesNotBypassInstallationCooldown()
    {
        _controller.TrySelect(PlayerSkillId.TemporaryTurret);
        var skill = _player.GetComponent<PlayerTurretSkill>();
        Assert.That(_controller.TryUse(PlayerSkillId.TemporaryTurret), Is.True);
        Assert.That(skill.CurrentTurretCount, Is.EqualTo(2));
        Assert.That(skill.TryRecoverNearest(), Is.True);
        Assert.That(skill.CurrentTurretCount, Is.EqualTo(3));
        Assert.That(_controller.TryUse(PlayerSkillId.TemporaryTurret), Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator Turret_DamagedRecoveryRequiresRepair()
    {
        _controller.TrySelect(PlayerSkillId.TemporaryTurret);
        _controller.TryUse(PlayerSkillId.TemporaryTurret);
        var skill = _player.GetComponent<PlayerTurretSkill>();
        var health = skill.RecoverableTurret.Health;
        Assert.That(health.MaxHealth, Is.EqualTo(100).Within(.001f));
        var blocker = health.GetComponent<CircleCollider2D>();
        Assert.That(blocker, Is.Not.Null);
        Assert.That(blocker.isTrigger, Is.False);
        health.TakeDamage(50);
        Assert.That(skill.TryRecoverNearest(), Is.True);
        Assert.That(skill.CurrentTurretCount, Is.EqualTo(2));
        Assert.That(skill.RepairRemaining, Is.EqualTo(15).Within(.001f));
        skill.Tick(15);
        Assert.That(skill.CurrentTurretCount, Is.EqualTo(3));
        yield break;
    }

    [UnityTest]
    public IEnumerator Turret_DestroyedReturnsAfterThirtySeconds()
    {
        _controller.TrySelect(PlayerSkillId.TemporaryTurret);
        _controller.TryUse(PlayerSkillId.TemporaryTurret);
        var skill = _player.GetComponent<PlayerTurretSkill>();
        skill.RecoverableTurret.Health.TakeDamage(100);
        yield return null; // Destroy completes through Unity's lifecycle.
        Assert.That(skill.CurrentTurretCount, Is.EqualTo(2));
        Assert.That(skill.RepairRemaining, Is.InRange(29f, 30f));
        skill.Tick(30);
        Assert.That(skill.CurrentTurretCount, Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator Turret_CannotBeRecoveredOutsideRecoveryRange()
    {
        _controller.TrySelect(PlayerSkillId.TemporaryTurret); _controller.TryUse(PlayerSkillId.TemporaryTurret);
        _player.transform.position += Vector3.right * 100;
        Assert.That(_player.GetComponent<PlayerTurretSkill>().TryRecoverNearest(), Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator Turret_CannotBeRecoveredByAnotherOwner()
    {
        _controller.TrySelect(PlayerSkillId.TemporaryTurret); _controller.TryUse(PlayerSkillId.TemporaryTurret);
        var other = new GameObject("Other owner", typeof(PlayerTurretSkill));
        try
        {
            var turret = _player.GetComponent<PlayerTurretSkill>().RecoverableTurret;
            Assert.That(turret.TryRecover(other.GetComponent<PlayerTurretSkill>()), Is.False);
        }
        finally { Object.DestroyImmediate(other); }
        yield break;
    }
}
