using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Tilemaps;

/// <summary>Unity 생명주기로 초기화된 스킬과 HUD의 동작을 검증합니다.</summary>
public sealed class PlayerSkillRuntimeTests
{
    private GameObject _player;
    private PlayerSkillController _controller;
    private PlayerSkillHud _hud;
    private GameObject _events;
    private GameObject _groundGrid;
    private Tile _groundTile;
    private bool _preparing;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _preparing = true;
        _player = new GameObject("Skill runtime test player");
        _player.SetActive(false);
        _player.transform.position = new Vector3(10000, 10000, 0);
        _player.layer = LayerMask.NameToLayer("PlayerFeetBox");
        _controller = _player.AddComponent<PlayerSkillController>();
        Assign(_player.GetComponent<PlayerBomb>(), "bombPrefab", "Assets/Prefabs/Player/bomb.prefab");
        Assign(_player.GetComponent<PlayerTurretSkill>(), "turretPrefab", "Assets/Prefabs/Player/PlayerTurret.prefab");
        var turretData = new SerializedObject(_player.GetComponent<PlayerTurretSkill>());
        turretData.FindProperty("_previewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/PlayerTurretPreview.prefab").GetComponent<PlayerTurretPlacementPreview>();
        _groundGrid = new GameObject("Skill test ground", typeof(Grid));
        _groundGrid.transform.position = _player.transform.position;
        var mapObject = new GameObject("Ground", typeof(Tilemap), typeof(TilemapCollider2D));
        mapObject.transform.SetParent(_groundGrid.transform, false);
        var map = mapObject.GetComponent<Tilemap>();
        _groundTile = ScriptableObject.CreateInstance<Tile>();
        _groundTile.colliderType = Tile.ColliderType.None;
        for (int y = -10; y <= 10; y++)
        for (int x = -10; x <= 10; x++) map.SetTile(new Vector3Int(x, y, 0), _groundTile);
        turretData.FindProperty("_placementGround").objectReferenceValue = map;
        turretData.ApplyModifiedPropertiesWithoutUndo();
        Assign(_player.GetComponent<PlayerBarrierSkill>(), "_prefab", "Assets/Prefabs/Player/EnergyBarrier.prefab", true);
        Assign(_player.GetComponent<PlayerBeaconSkill>(), "_prefab", "Assets/Prefabs/Player/LureBeacon.prefab", true);
        BindPlacement(_player.GetComponent<PlayerBomb>(), "PlayerBombPreview", map);
        BindPlacement(_player.GetComponent<PlayerBeaconSkill>(), "PlayerBeaconPreview", map);
        BindPlacement(_player.GetComponent<PlayerBarrierSkill>(), "PlayerBarrierPreview", map);
        _controller.BindSceneState(() => _preparing, () => true);
        _player.SetActive(true); // Awake/OnEnable을 Unity가 한 번씩 실행합니다.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        _hud = Object.Instantiate(prefab).GetComponent<PlayerSkillHud>();
        // 씬 바인더의 자동 탐색 대신 테스트 대상 플레이어를 명시적으로 연결합니다.
        _hud.GetComponent<PlayerSkillHudSceneBinding>().enabled = false;
        Assert.That(_controller.BindHud(_hud), Is.True);
        if (EventSystem.current == null)
            _events = new GameObject("Skill test EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        yield return null;
    }

    private static void Assign(MonoBehaviour component, string field, string path, bool device = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var data = new SerializedObject(component);
        data.FindProperty(field).objectReferenceValue = device ? (Object)prefab.GetComponent<PlayerFieldDevice>() : prefab;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BindPlacement(PlayerPlacementSkillBase skill, string preview, Tilemap ground)
    {
        var data = new SerializedObject(skill);
        data.FindProperty("_previewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/" + preview + ".prefab").GetComponent<PlayerSkillPlacementPreview>();
        data.FindProperty("_placementGround").objectReferenceValue = ground;
        data.FindProperty("_placementRadius").floatValue = 12;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_player != null) Object.DestroyImmediate(_player);
        if (_hud != null) Object.DestroyImmediate(_hud.gameObject);
        if (_events != null) Object.DestroyImmediate(_events);
        if (_groundGrid != null) Object.DestroyImmediate(_groundGrid);
        if (_groundTile != null) Object.DestroyImmediate(_groundTile);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator Hud_RebindingReusesViewsAndPreservesSelection()
    {
        _controller.TrySelect(PlayerSkillId.Bomb);
        int count = _hud.GetComponentsInChildren<Transform>(true).Length;
        for (int i = 0; i < 3; i++)
        {
            _hud.Unbind();
            Assert.That(_hud.Owner, Is.Null);
            Assert.That(_hud.GetComponent<Canvas>().enabled, Is.False);
            Assert.That(_controller.BindHud(_hud), Is.True);
        }
        Assert.That(_hud.transform.parent, Is.Null);
        Assert.That(_hud.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        Assert.That(_controller.Loadout.Q, Is.EqualTo(PlayerSkillId.Bomb));
        _hud.Open();
        var reset = _hud.transform.Find("Skill Selection/Panel Border/Skill Selection Panel/Reset Selection").GetComponent<Button>();
        int changes = 0;
        void CountChange() => changes++;
        _controller.Loadout.Changed += CountChange;
        reset.onClick.Invoke();
        _controller.Loadout.Changed -= CountChange;
        Assert.That(changes, Is.EqualTo(1));
        yield break;
    }

    [UnityTest]
    public IEnumerator Hud_PlayerDisableHidesAndDetachesSceneCanvas()
    {
        _hud.Open();
        _player.SetActive(false);
        Assert.That(_hud.Owner, Is.Null);
        Assert.That(_hud.gameObject.activeSelf, Is.True);
        Assert.That(_hud.GetComponent<Canvas>().enabled, Is.False);
        Assert.That(_controller.IsSelectionOpen, Is.False);
        _player.SetActive(true);
        Assert.That(_controller.BindHud(_hud), Is.True);
        Assert.That(_hud.GetComponent<Canvas>().enabled, Is.True);
        yield break;
    }

    [UnityTest]
    public IEnumerator Hud_MissingOptionalViewsAndIconKeepSelectionUsable()
    {
        _hud.Unbind();
        var data = new SerializedObject(_hud);
        foreach (string field in new[] { "_tooltip", "_recovery", "_placementHint", "_placementFailure", "_beaconIcon" })
            data.FindProperty(field).objectReferenceValue = null;
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(_hud.IsConfigured, Is.True);
        Assert.That(_hud.IsFullyConfigured, Is.False);
        Assert.That(_controller.BindHud(_hud), Is.True);
        _hud.Open();
        var panel = _hud.transform.Find("Skill Selection/Panel Border/Skill Selection Panel");
        var beacon = panel.Find("LureBeacon");
        Assert.That(beacon.Find("Icon").GetComponent<Image>().enabled, Is.False);
        beacon.GetComponent<Button>().onClick.Invoke();
        Assert.That(_controller.Loadout.Q, Is.EqualTo(PlayerSkillId.LureBeacon));
        _hud.ShowTooltip(_player, PlayerSkillId.LureBeacon);
        _hud.Close();
        yield return null;
        Assert.That(_hud.GetComponent<Canvas>().enabled, Is.True);
        Assert.That(_controller.CanUse, Is.True);
    }

    [UnityTest]
    public IEnumerator Hud_IncompleteTooltipKeepsSlotsAndPanelUsable()
    {
        _hud.Unbind();
        var tooltip = _hud.GetComponentInChildren<PlayerSkillTooltipView>(true);
        var data = new SerializedObject(tooltip);
        data.FindProperty("_title").objectReferenceValue = null;
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(_controller.BindHud(_hud), Is.True);
        _hud.Open();
        _hud.ShowTooltip(_player, PlayerSkillId.Bomb);
        Assert.That(_hud.IsOpen, Is.True);
        Assert.That(tooltip.gameObject.activeSelf, Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator Hud_MissingRequiredSlotRejectsBinding()
    {
        _hud.Unbind();
        var data = new SerializedObject(_hud);
        data.FindProperty("_qSlot").objectReferenceValue = null;
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(_hud.IsConfigured, Is.False);
        Assert.That(_controller.BindHud(_hud), Is.False);
        Assert.That(_hud.Owner, Is.Null);
        Assert.That(_hud.GetComponent<Canvas>().enabled, Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator SelectionPanel_ReopeningShowsSelectionChangedWhileClosed()
    {
        _hud.Open();
        _hud.Close();
        Assert.That(_controller.TrySelect(PlayerSkillId.LureBeacon), Is.True);
        yield return null;
        _hud.Open();
        var panel = _hud.transform.Find("Skill Selection/Panel Border/Skill Selection Panel");
        Assert.That(panel.Find("Q Selection").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("유도 비콘"));
        Assert.That(panel.Find("LureBeacon/State").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Q 등록됨"));
        Assert.That(panel.Find("LureBeacon").GetComponent<Button>().interactable, Is.False);
        Assert.That(panel.Find("Q Skill Slot/Background/Skill Icon").GetComponent<Image>().sprite,
            Is.EqualTo(_hud.Icon(PlayerSkillId.LureBeacon)));
    }

    [UnityTest]
    public IEnumerator Hud_SceneBindingRejectsRemoteIdentity()
    {
        var identity = _player.AddComponent<TeamHJD.Game.Presentation.Scene.PlayerActorIdentity>();
        identity.Bind(new TeamHJD.Game.Domain.PlayerId("remote-player"));
        var binding = _hud.GetComponent<PlayerSkillHudSceneBinding>();
        _hud.Unbind();
        binding.enabled = true;
        Assert.That(binding.BindLocalPlayer(_controller), Is.False);
        yield return null;
        Assert.That(_hud.Owner == _controller, Is.False);
        binding.enabled = false;
    }

    [UnityTest]
    public IEnumerator Hud_SceneBindingWaitsForPlayerAndReusesCanvas()
    {
        var originalScene = _player.scene;
        var testScene = UnityEngine.SceneManagement.SceneManager.CreateScene("Skill HUD binding test");
        var binding = _hud.GetComponent<PlayerSkillHudSceneBinding>();
        try
        {
            _player.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_player, testScene);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_hud.gameObject, testScene);
            binding.enabled = true;
            yield return null;
            Assert.That(_hud.Owner, Is.Null);
            Assert.That(_hud.GetComponent<Canvas>().enabled, Is.False);
            _player.SetActive(true);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(_hud.Owner, Is.SameAs(_controller));
            Assert.That(_hud.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(_player.GetComponentInChildren<PlayerSkillHud>(), Is.Null);
        }
        finally
        {
            binding.enabled = false;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_player, originalScene);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_hud.gameObject, originalScene);
            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(testScene);
        }
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
        _hud.Open();
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
        _controller.TrySelect(PlayerSkillId.PlayerTurret);
        _hud.Close(); _preparing = false;
        yield return null;
        var slot = _hud.transform.Find("Q Skill Slot").gameObject;
        ExecuteEvents.Execute(slot, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
        var tooltip = _hud.transform.Find("Skill Tooltip");
        Assert.That(tooltip.gameObject.activeSelf, Is.True);
        Assert.That(tooltip.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("플레이어 터렛"));
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
        Assert.That(skill.HasFirstEmitter, Is.False);
        Assert.That(skill.TryConfirmPlacement(_player.transform.position + Vector3.right * 4), Is.True);
        Assert.That(skill.HasFirstEmitter, Is.True);
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
        var skill = _player.GetComponent<PlayerBarrierSkill>();
        Assert.That(skill.TryConfirmPlacement(_player.transform.position + Vector3.right * 4), Is.True);
        Vector3 first = skill.FirstEmitterPosition;
        _player.transform.position += Vector3.right * 20;
        Assert.That(skill.TryConfirmPlacement(_player.transform.position + Vector3.right * 4), Is.False);
        Assert.That(skill.IsPending, Is.True);
        Assert.That(skill.FirstEmitterPosition, Is.EqualTo(first));
        Assert.That(_controller.PlacementFailure, Does.Contain("범위"));
        Assert.That(skill.Charges, Is.EqualTo(3));
        yield break;
    }

    [UnityTest]
    public IEnumerator Barrier_CompletedConnectionConsumesOneCharge()
    {
        _controller.TrySelect(PlayerSkillId.EnergyBarrier);
        _controller.TryUse(PlayerSkillId.EnergyBarrier);
        var skill = _player.GetComponent<PlayerBarrierSkill>();
        Assert.That(skill.TryConfirmPlacement(_player.transform.position + Vector3.right * 4), Is.True);
        Assert.That(skill.TryConfirmPlacement(_player.transform.position + Vector3.up * 4), Is.True);
        Assert.That(skill.IsPending, Is.False);
        Assert.That(skill.Charges, Is.EqualTo(2));
        yield break;
    }

    [UnityTest]
    public IEnumerator Barrier_TerrainBlocksConnectionWithoutConsumingCharge()
    {
        _controller.TrySelect(PlayerSkillId.EnergyBarrier); _controller.TryUse(PlayerSkillId.EnergyBarrier);
        var skill = _player.GetComponent<PlayerBarrierSkill>();
        Assert.That(skill.TryConfirmPlacement(_player.transform.position + Vector3.right * 4), Is.True);
        Vector3 first = skill.FirstEmitterPosition;
        var obstacle = new GameObject("Impassable terrain", typeof(BoxCollider2D));
        try
        {
            obstacle.transform.position = _player.transform.position + new Vector3(2, 2, 0);
            for (int layer = 0; layer < 32; layer++)
                if (!Physics2D.GetIgnoreLayerCollision(_player.layer, layer)) { obstacle.layer = layer; break; }
            Physics2D.SyncTransforms();
            Assert.That(skill.TryConfirmPlacement(_player.transform.position + Vector3.up * 4), Is.False);
            Assert.That(skill.Charges, Is.EqualTo(3));
            Assert.That(skill.HasFirstEmitter, Is.True);
            Assert.That(skill.FirstEmitterPosition, Is.EqualTo(first));
            Assert.That(_controller.PlacementFailure, Is.Not.Empty);
            obstacle.transform.position += Vector3.right * 30;
            Assert.That(skill.TryConfirmPlacement(_player.transform.position + Vector3.up * 4), Is.True);
            Assert.That(skill.IsPending, Is.False);
            Assert.That(skill.Charges, Is.EqualTo(2));
            Assert.That(_controller.PlacementFailure, Is.Empty);
        }
        finally { Object.DestroyImmediate(obstacle); }
        yield break;
    }

    [UnityTest]
    public IEnumerator Barrier_FirstPulseStaysFixedUntilExplicitCancellation()
    {
        _controller.TrySelect(PlayerSkillId.EnergyBarrier); _controller.TryUse(PlayerSkillId.EnergyBarrier);
        var skill = _player.GetComponent<PlayerBarrierSkill>();
        Assert.That(skill.TryConfirmPlacement(_player.transform.position + Vector3.right * 4), Is.True);
        Vector3 first = skill.FirstEmitterPosition;
        skill.Tick(60);
        _player.transform.position += Vector3.up;
        Assert.That(skill.IsPending, Is.True);
        Assert.That(skill.FirstEmitterPosition, Is.EqualTo(first));
        Assert.That(skill.Charges, Is.EqualTo(3));
        yield break;
    }

    [UnityTest]
    public IEnumerator Turret_RecoveryDoesNotBypassInstallationCooldown()
    {
        _controller.TrySelect(PlayerSkillId.PlayerTurret);
        var skill = _player.GetComponent<PlayerTurretSkill>();
        Assert.That(DeployTurret(), Is.True);
        Assert.That(skill.CurrentTurretCount, Is.EqualTo(2));
        Assert.That(skill.TryRecoverNearest(), Is.True);
        Assert.That(skill.CurrentTurretCount, Is.EqualTo(3));
        Assert.That(_controller.TryUse(PlayerSkillId.PlayerTurret), Is.True);
        Assert.That(skill.IsSelecting, Is.True);
        Assert.That(skill.TrySelectSlot(0), Is.False, "Opening status must not bypass the installation cooldown.");
        yield break;
    }

    [UnityTest]
    public IEnumerator Turret_DamagedRecoveryKeepsItsHealthForRedeployment()
    {
        _controller.TrySelect(PlayerSkillId.PlayerTurret);
        Assert.That(DeployTurret(), Is.True);
        var skill = _player.GetComponent<PlayerTurretSkill>();
        var health = skill.RecoverableTurret.Health;
        Assert.That(health.MaxHealth, Is.EqualTo(100).Within(.001f));
        var blocker = health.GetComponent<BoxCollider2D>();
        Assert.That(blocker, Is.Not.Null);
        Assert.That(blocker.isTrigger, Is.False);
        health.TakeDamage(50);
        Assert.That(skill.TryRecoverNearest(), Is.True);
        Assert.That(skill.CurrentTurretCount, Is.EqualTo(3));
        Assert.That(skill.RepairRemaining, Is.Zero);
        Assert.That(skill.GetSlot(0).HealthRatio, Is.EqualTo(.5f));
        skill.Tick(skill.SkillCooldown);
        Assert.That(DeployTurret(), Is.True);
        Assert.That(skill.RecoverableTurret.Health.Health, Is.EqualTo(50).Within(.001f));
        yield break;
    }

    [UnityTest]
    public IEnumerator Turret_DestroyedReturnsAfterThirtySeconds()
    {
        _controller.TrySelect(PlayerSkillId.PlayerTurret);
        Assert.That(DeployTurret(), Is.True);
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
        _controller.TrySelect(PlayerSkillId.PlayerTurret); Assert.That(DeployTurret(), Is.True);
        _player.transform.position += Vector3.right * 100;
        Assert.That(_player.GetComponent<PlayerTurretSkill>().TryRecoverNearest(), Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator Turret_CannotBeRecoveredByAnotherOwner()
    {
        _controller.TrySelect(PlayerSkillId.PlayerTurret); Assert.That(DeployTurret(), Is.True);
        var other = new GameObject("Other owner", typeof(PlayerTurretSkill));
        try
        {
            var turret = _player.GetComponent<PlayerTurretSkill>().RecoverableTurret;
            Assert.That(turret.TryRecover(other.GetComponent<PlayerTurretSkill>()), Is.False);
        }
        finally { Object.DestroyImmediate(other); }
        yield break;
    }
    [UnityTest]
    public IEnumerator Turret_SelectionUiCancelsPreviewWithoutConsuming()
    {
        Assert.That(_controller.TrySelect(PlayerSkillId.PlayerTurret), Is.True);
        Assert.That(_controller.TryUse(PlayerSkillId.PlayerTurret), Is.True);
        var skill = _player.GetComponent<PlayerTurretSkill>();
        Assert.That(skill.IsSelecting, Is.True);
        Assert.That(skill.TrySelectSlot(0), Is.True);
        Assert.That(_controller.IsPlacingTurret, Is.True);
        Assert.That(_controller.CanRecover, Is.False);
        _hud.Open();
        Assert.That(skill.TryConfirmPlacement(_player.transform.position), Is.False);
        yield return null;
        Assert.That(skill.IsPlacing, Is.False);
        Assert.That(skill.AvailableCount, Is.EqualTo(3));
        Assert.That(skill.CurrentCooldownTimer, Is.Zero);
    }

    [UnityTest]
    public IEnumerator Bomb_EnemyOverlapPreservesInventoryCooldownAndShowsFailure()
    {
        Assert.That(_controller.TrySelect(PlayerSkillId.Bomb), Is.True);
        var enemy = new GameObject("Bomb placement enemy", typeof(BoxCollider2D));
        GameObject deployed = null;
        try
        {
            enemy.layer = LayerMask.NameToLayer("Enemy");
            var skill = _player.GetComponent<PlayerBomb>();
            Vector3 pointer = _player.transform.position + Vector3.down;
            Vector3 candidate = skill.GetPlacementPosition(pointer);
            enemy.transform.position = candidate;
            Assert.That(_controller.TryUse(PlayerSkillId.Bomb), Is.True);
            Assert.That(skill.TryConfirmPlacement(pointer), Is.False);
            Assert.That(skill.CurrentBombCount, Is.EqualTo(5));
            Assert.That(skill.RemainingCooldown, Is.Zero);
            Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerBombPlacementResult.OverlapsEnemy));
            yield return null;
            var failure = (TMPro.TMP_Text)new SerializedObject(_hud).FindProperty("_placementFailure").objectReferenceValue;
            Assert.That(failure.gameObject.activeSelf, Is.True);
            Assert.That(failure.text, Does.Contain("몬스터"));

            enemy.transform.position += Vector3.right * 10;
            Assert.That(skill.TryConfirmPlacement(pointer), Is.True);
            Assert.That(skill.CurrentBombCount, Is.EqualTo(4));
            Assert.That(skill.RemainingCooldown, Is.EqualTo(skill.CooldownDuration));
            Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerBombPlacementResult.Allowed));
            foreach (var bomb in Object.FindObjectsByType<PlayerBombExplosion>(FindObjectsSortMode.None))
                if (Vector3.Distance(bomb.transform.position, candidate) < .01f) deployed = bomb.gameObject;
            Assert.That(deployed, Is.Not.Null);
            Assert.That(deployed.GetComponent<UnityEngine.Rendering.SortingGroup>(), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(enemy);
            if (deployed != null) Object.DestroyImmediate(deployed);
        }
    }

    [UnityTest]
    public IEnumerator Bomb_ObstacleOverlapDoesNotConsumeInventoryOrCooldown()
    {
        Assert.That(_controller.TrySelect(PlayerSkillId.Bomb), Is.True);
        var wall = new GameObject("Bomb placement wall", typeof(BoxCollider2D));
        try
        {
            var skill = _player.GetComponent<PlayerBomb>();
            Vector3 pointer = _player.transform.position + Vector3.down;
            wall.transform.position = skill.GetPlacementPosition(pointer);
            wall.GetComponent<BoxCollider2D>().includeLayers = 1 << _player.layer;
            Assert.That(_controller.TryUse(PlayerSkillId.Bomb), Is.True);
            Assert.That(skill.TryConfirmPlacement(pointer), Is.False);
            Assert.That(skill.PlacementFeedback, Is.EqualTo(PlayerBombPlacementResult.BlockedByObstacle));
            Assert.That(skill.CurrentBombCount, Is.EqualTo(5));
            Assert.That(skill.RemainingCooldown, Is.Zero);
        }
        finally { Object.DestroyImmediate(wall); }
        yield break;
    }

    [UnityTest]
    public IEnumerator Beacon_RangeFollowsPlayerAndRejectsOutsideCursorWithoutClamping()
    {
        Assert.That(_controller.TrySelect(PlayerSkillId.LureBeacon), Is.True);
        var skill = _player.GetComponent<PlayerBeaconSkill>();
        Assert.That(_controller.TryUse(PlayerSkillId.LureBeacon), Is.True);
        PlayerSkillPlacementPreview preview = null;
        foreach (var item in Object.FindObjectsByType<PlayerSkillPlacementPreview>(FindObjectsSortMode.None))
            if (item.name.StartsWith("PlayerBeaconPreview")) preview = item;
        Assert.That(preview, Is.Not.Null);
        var range = preview.transform.Find("Placement Range");
        Assert.That(range.position, Is.EqualTo(_player.transform.position));
        Assert.That(range.localScale.x, Is.EqualTo(skill.PlacementRadius));
        Vector3 outside = _player.transform.position + Vector3.right * (skill.PlacementRadius + .5f);
        Assert.That(skill.GetPlacementPosition(outside), Is.EqualTo(outside));
        Assert.That(skill.TryConfirmPlacement(outside), Is.False);
        Assert.That(skill.IsPlacing, Is.True);
        Assert.That(_controller.PlacementFailure, Does.Contain("범위"));
        Assert.That(skill.RemainingCooldown, Is.Zero);
        Assert.That(preview.transform.position, Is.EqualTo(outside));
        Assert.That(range.position, Is.EqualTo(_player.transform.position), "The circle follows the player, not the candidate.");
        _player.transform.position += Vector3.right;
        yield return null;
        Assert.That(range.position, Is.EqualTo(_player.transform.position));
        Vector3 inside = _player.transform.position + new Vector3(2.25f, 1.75f, 0);
        Assert.That(skill.TryConfirmPlacement(inside), Is.True);
        bool found = false;
        foreach (var device in Object.FindObjectsByType<PlayerFieldDevice>(FindObjectsSortMode.None))
            if (device.Kind == PlayerFieldDeviceKind.Beacon && Vector3.Distance(device.Position, inside) < .001f) found = true;
        Assert.That(found, Is.True, "Placement preserves the exact cursor coordinates.");
    }

    [UnityTest]
    public IEnumerator Barrier_SecondRangeStaysOnFirstEmitterAfterPlayerMoves()
    {
        Assert.That(_controller.TrySelect(PlayerSkillId.EnergyBarrier), Is.True);
        var skill = _player.GetComponent<PlayerBarrierSkill>();
        Assert.That(_controller.TryUse(PlayerSkillId.EnergyBarrier), Is.True);
        Vector3 first = _player.transform.position + Vector3.right * 4;
        Assert.That(skill.TryConfirmPlacement(first), Is.True);
        PlayerSkillPlacementPreview preview = null;
        foreach (var item in Object.FindObjectsByType<PlayerSkillPlacementPreview>(FindObjectsSortMode.None))
            if (item.name.StartsWith("PlayerBarrierPreview")) preview = item;
        Assert.That(preview, Is.Not.Null);
        var range = preview.transform.Find("Placement Range");
        Assert.That(skill.PlacementCenter, Is.EqualTo(first));
        Assert.That(skill.PlacementRadius, Is.EqualTo(13.5f));
        Assert.That(range.position, Is.EqualTo(first), "The first click immediately relocates the circle.");
        Assert.That(range.localScale.x, Is.EqualTo(13.5f));
        Assert.That(skill.TryConfirmPlacement(first + Vector3.up * 13.6f), Is.False);
        Assert.That(_controller.PlacementFailure, Does.Contain("범위"));
        Assert.That(skill.FirstEmitterPosition, Is.EqualTo(first));
        Assert.That(skill.HasFirstEmitter, Is.True);
        Assert.That(skill.Charges, Is.EqualTo(3));
        _player.transform.position += Vector3.right * 25;
        yield return null;
        Assert.That(range.position, Is.EqualTo(first));
        Assert.That(range.localScale.x, Is.EqualTo(13.5f));
        Assert.That(skill.PlacementCenter, Is.EqualTo(first));
        Assert.That(skill.TryConfirmPlacement(first + Vector3.up * 4), Is.True,
            "The second endpoint is measured from the first, even when the player is far away.");
        Assert.That(skill.Charges, Is.EqualTo(2));
        Assert.That(skill.IsPlacing, Is.False);
        Assert.That(skill.PlacementCenter, Is.EqualTo(_player.transform.position));
        Assert.That(skill.PlacementRadius, Is.EqualTo(12));
    }

    [UnityTest]
    public IEnumerator SwitchingPlacementAndResettingLoadoutCancelEveryPreviewWithoutSpending()
    {
        Assert.That(_controller.TrySelect(PlayerSkillId.Bomb), Is.True);
        Assert.That(_controller.TrySelect(PlayerSkillId.LureBeacon), Is.True);
        var bomb = _player.GetComponent<PlayerBomb>();
        var beacon = _player.GetComponent<PlayerBeaconSkill>();
        Assert.That(_controller.TryUse(PlayerSkillId.Bomb), Is.True);
        Assert.That(_controller.IsPlacingSkill, Is.True);
        Assert.That(_controller.TryUse(PlayerSkillId.LureBeacon), Is.True);
        Assert.That(bomb.IsPlacing, Is.False);
        Assert.That(beacon.IsPlacing, Is.True);
        Assert.That(bomb.CurrentBombCount, Is.EqualTo(5));
        Assert.That(bomb.RemainingCooldown, Is.Zero);
        Assert.That(_controller.CanRecover, Is.False);
        _controller.ClearSelection();
        Assert.That(_controller.IsPlacingSkill, Is.False);
        Assert.That(beacon.RemainingCooldown, Is.Zero);
        yield break;
    }

    [UnityTest]
    public IEnumerator Beacon_BlockedEndpointCanBeRetriedAndConsumesOnlyOnSuccess()
    {
        Assert.That(_controller.TrySelect(PlayerSkillId.LureBeacon), Is.True);
        var skill = _player.GetComponent<PlayerBeaconSkill>();
        Vector3 pointer = _player.transform.position + Vector3.down;
        Vector3 position = skill.GetPlacementPosition(pointer);
        var actor = new GameObject("Beacon occupied cell", typeof(BoxCollider2D));
        PlayerFieldDevice deployed = null;
        try
        {
            actor.layer = LayerMask.NameToLayer("PlayerFeetBox");
            actor.transform.position = position;
            Assert.That(_controller.TryUse(PlayerSkillId.LureBeacon), Is.True);
            Assert.That(skill.TryConfirmPlacement(pointer), Is.False);
            Assert.That(_controller.PlacementFailure, Does.Contain("플레이어"));
            Assert.That(skill.IsPlacing, Is.True);
            Assert.That(skill.RemainingCooldown, Is.Zero);
            actor.transform.position += Vector3.right * 30;
            Assert.That(skill.TryConfirmPlacement(pointer), Is.True);
            Assert.That(skill.IsPlacing, Is.False);
            Assert.That(skill.RemainingCooldown, Is.EqualTo(skill.Cooldown));
            foreach (var device in Object.FindObjectsByType<PlayerFieldDevice>(FindObjectsSortMode.None))
                if (device.Kind == PlayerFieldDeviceKind.Beacon && Vector3.Distance(device.Position, position) < .01f) deployed = device;
            Assert.That(deployed, Is.Not.Null);
            Assert.That(deployed.gameObject.scene, Is.EqualTo(_player.scene));
            Assert.That(skill.TryConfirmPlacement(pointer), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(actor);
            if (deployed != null) Object.DestroyImmediate(deployed.gameObject);
        }
        yield break;
    }

    private bool DeployTurret() => _controller.TryUse(PlayerSkillId.PlayerTurret)
        && _player.GetComponent<PlayerTurretSkill>().TrySelectSlot(0)
        && _player.GetComponent<PlayerTurretSkill>().TryConfirmPlacement(_player.transform.position + Vector3.down);

    [UnityTest]
    public IEnumerator RecoveryPrompt_UsesFinalCameraPositionBeforeDrawing()
    {
        Assert.That(_controller.TrySelect(PlayerSkillId.PlayerTurret), Is.True);
        Assert.That(DeployTurret(), Is.True);
        var prompt = (PlayerTurretRecoveryPromptView)new SerializedObject(_hud)
            .FindProperty("_recovery").objectReferenceValue;
        var cameraObject = new GameObject("Recovery prompt test camera", typeof(Camera));
        try
        {
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 18;
            camera.transform.position = _player.transform.position + Vector3.back * 10;
            typeof(PlayerTurretRecoveryPromptView).GetField("_worldCamera",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(prompt, camera);
            prompt.Refresh();
            Assert.That(prompt.gameObject.activeSelf, Is.True);

            // Simulate the follow camera moving after the HUD's LateUpdate.
            camera.transform.position += new Vector3(1, .5f);
            Canvas.ForceUpdateCanvases();
            var turret = _controller.RecoverableTurret;
            var bottom = turret.position;
            bottom.y = turret.GetComponent<SpriteRenderer>().bounds.min.y;
            var screen = camera.WorldToScreenPoint(bottom);
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_hud.transform,
                screen, null, out var expected), Is.True);
            Vector2 offset = new SerializedObject(prompt).FindProperty("_screenOffset").vector2Value;
            Assert.That(Vector2.Distance(((RectTransform)prompt.transform).anchoredPosition, expected + offset),
                Is.LessThan(.01f), "The prompt must use the same final camera position as the turret.");
        }
        finally { Object.DestroyImmediate(cameraObject); }
        yield break;
    }

}
