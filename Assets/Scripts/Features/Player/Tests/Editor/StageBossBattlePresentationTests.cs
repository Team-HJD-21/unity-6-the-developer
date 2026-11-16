// 실제 제작 HUD와 additive actor/EnemyHealth를 사용해 연출·잠금·취소 경계를 검사합니다.
// Scene 이동 진행·보스 생성 규칙이나 승패 처리는 이 UI fixture가 구현하지 않습니다.
using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Enemy.Contracts;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Presentation.Scene;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class StageBossBattlePresentationTests
{
    private Scene _stageScene, _bossScene;
    private GameObject _actor, _cameraObject, _skillsObject, _stageObject, _presenterObject;
    private InitialStagePlayerHUD _stage;
    private PlayerSkillHud _skills;
    private StageBossBattlePresentation _presentation;
    private InputLockService _input;
    private Camera _camera;
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        yield return null;
        _stageScene = SceneManager.CreateScene("Stage boss UI fixture");
        _bossScene = SceneManager.CreateScene("Additive boss UI fixture");
        _input = new InputLockService();
        _actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        SceneManager.MoveGameObjectToScene(_actor, _stageScene); _actor.transform.position = new Vector3(70000, 70000);
        _actor.GetComponent<PlayerInputHandler>().enabled = false; _actor.GetComponent<PlayerAttack>().enabled = false;
        var id = new PlayerId("local-player");
        (_actor.GetComponent<PlayerActorIdentity>() ?? _actor.AddComponent<PlayerActorIdentity>()).Bind(id);
        _actor.GetComponent<PlayerControlState>().BindInputBlocker(_input);
        _cameraObject = new GameObject("Boss UI camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(_cameraObject, _stageScene);
        _camera = _cameraObject.GetComponent<Camera>(); _camera.orthographic = true; _camera.orthographicSize = 10;
        _cameraObject.transform.position = _actor.transform.position + Vector3.back * 10;
        _cameraObject.AddComponent<MatchCameraFollow>().Bind(_actor.transform);
        _skillsObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab"));
        SceneManager.MoveGameObjectToScene(_skillsObject, _stageScene); _skills = _skillsObject.GetComponent<PlayerSkillHud>();
        Assert.That(_actor.GetComponent<PlayerSkillController>().BindHud(_skills), Is.True);
        _stageObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab"));
        SceneManager.MoveGameObjectToScene(_stageObject, _stageScene); _stage = _stageObject.GetComponent<InitialStagePlayerHUD>();
        _stage.BindLocalActor(new MatchId("boss-ui"), id, _actor);
        _presenterObject = new GameObject("Boss UI request adapter"); SceneManager.MoveGameObjectToScene(_presenterObject, _stageScene);
        _presentation = _presenterObject.AddComponent<StageBossBattlePresentation>();
        _presentation.StageHud = _stage; _presentation.SkillHud = _skills; _presentation.BindInputBlocker(_input);
        yield return null;
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_presenterObject != null) Object.Destroy(_presenterObject);
        if (_skillsObject != null) Object.Destroy(_skillsObject);
        if (_stageObject != null) Object.Destroy(_stageObject);
        if (_actor != null) Object.Destroy(_actor);
        if (_cameraObject != null) Object.Destroy(_cameraObject);
        yield return null; _input?.Dispose();
        if (_bossScene.isLoaded) yield return SceneManager.UnloadSceneAsync(_bossScene);
        if (_stageScene.isLoaded) yield return SceneManager.UnloadSceneAsync(_stageScene);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    private static IEnumerator Completed(Task task, float seconds = 8)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + seconds;
        while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Assert.That(task.IsCompleted, Is.True, "Presentation did not finish in its expected timeline.");
    }
    [UnityTest]
    public IEnumerator AdditiveBattleReplaysBothScansUnderCoreLockAndBindsTheRealBossWithoutRelaxingNormalBindings()
    {
        using var core = _input.Acquire("Core preparation");
        SceneManager.MoveGameObjectToScene(_actor, _bossScene);
        Assert.That(_actor.GetComponent<PlayerSkillController>().BindHud(_skills), Is.False);
        var boss = new GameObject("Explicit boss", typeof(EnemyHealth)); SceneManager.MoveGameObjectToScene(boss, _bossScene);
        var health = boss.GetComponent<EnemyHealth>(); health.Initialize();
        var bar = _skills.GetComponentInChildren<CombatBossView>(true);
        bar.Bind(health, "Test boss"); Assert.That(bar.Source, Is.Null, "Normal binding remains scoped to its Scene.");
        const string bossName = "감염된 트리언트";
        var task = _presentation.ShowBattleIntroAsync(boss, bossName, CancellationToken.None);
        Assert.That(_stage.GetComponent<CombatHudIntro>().Completed, Is.False);
        yield return Completed(task);
        Assert.That(task.IsCompletedSuccessfully, Is.True, task.Exception?.ToString());
        Assert.That(_input.Count, Is.EqualTo(1), "Only the Core's own lock remains after the HUD scan.");
        Assert.That(_skills.Owner.gameObject, Is.SameAs(_actor));
        Assert.That(_skills.gameObject.scene, Is.EqualTo(_stageScene));
        Assert.That(_actor.scene, Is.EqualTo(_bossScene));
        Assert.That(bar.Source, Is.SameAs(health)); Assert.That(bar.Name.text, Is.EqualTo(bossName));
        Assert.That(bar.Name.font, Is.SameAs(bar.Style.BodyFont));
        Assert.That(bar.Name.fontSharedMaterial, Is.SameAs(bar.Style.BodyFont.material));
        foreach (char character in bossName)
            Assert.That(bar.Name.font.HasCharacter(character, true, true), Is.True, "Missing boss-name glyph: " + character);
        Assert.That(health.TryTakeDamage(5, AttackSourceType.Player), Is.True, "The real health source must accept the test hit.");
        Assert.That(health.CurrentHealth, Is.EqualTo(15)); Assert.That(bar.isActiveAndEnabled, Is.True);
        double healthDeadline = Time.realtimeSinceStartupAsDouble + 1;
        while (Mathf.Abs(bar.Health.fillAmount - .75f) > .001f && Time.realtimeSinceStartupAsDouble < healthDeadline) yield return null;
        Assert.That(bar.Health.fillAmount, Is.EqualTo(15f / 20).Within(.001));
        var intro = _skills.GetComponent<CombatHudIntro>();
        var announcement = (FieldEntryAnnouncement)typeof(CombatHudIntro).GetField("_announcement", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(intro);
        Assert.That(announcement.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("보스를 처치하세요"));
        var header = _skills.GetComponentInChildren<CombatHudHeader>(true); header.SetConfirmedOccupancy(100, 100);
        Assert.That(header.OccupancyLabel.gameObject.activeSelf, Is.False);
        Assert.That(_skills.GetComponent<PlayerSkillHudSceneBinding>().enabled, Is.False);
        _presentation.ResetBattle();
        Assert.That(_input.Count, Is.EqualTo(1)); Assert.That(bar.Source, Is.Null);
        Assert.That(_skills.GetComponent<PlayerSkillHudSceneBinding>().enabled, Is.True);
    }
    [UnityTest]
    public IEnumerator VictoryHoldsItsMessageZoomAndHudSuppressionUntilFadeHandoffAndCancellationReleasesOnlyOwnedLeases()
    {
        using var core = _input.Acquire("Core return");
        SceneManager.MoveGameObjectToScene(_actor, _bossScene);
        var task = _presentation.PlayVictoryAsync("보스를 처치하는데에 성공했습니다", .2f, CancellationToken.None);
        yield return Completed(task);
        Assert.That(task.IsCompletedSuccessfully, Is.True, task.Exception?.ToString());
        Assert.That(_input.Count, Is.EqualTo(2)); Assert.That(HudPresentationVisibility.IsSuppressed, Is.True);
        Assert.That(CameraManager.Current.HasOverride, Is.True);
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(_camera.orthographicSize, Is.EqualTo(4.5f).Within(.05));
        var message = (FieldEntryAnnouncement)typeof(StageBossBattlePresentation).GetField("_victory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_presentation);
        var label = message.GetComponentInChildren<TMP_Text>(); label.ForceMeshUpdate(true, true);
        Assert.That(message.Group.alpha, Is.EqualTo(1)); Assert.That(label.isTextTruncated, Is.False);
        Assert.That(label.isTextOverflowing, Is.False); Assert.That(label.textInfo.lineCount, Is.EqualTo(1));
        _presentation.ResetBattle();
        Assert.That(_input.Count, Is.EqualTo(1)); Assert.That(CameraManager.Current.HasOverride, Is.False);
        Assert.That(HudPresentationVisibility.IsSuppressed, Is.False); Assert.That(message.Group.alpha, Is.Zero);
        using var other = HudPresentationVisibility.Suppress();
        using var cancel = new CancellationTokenSource();
        task = _presentation.PlayVictoryAsync("보스를 처치하는데에 성공했습니다", 5, cancel.Token);
        cancel.Cancel(); yield return Completed(task);
        Assert.That(task.IsCanceled, Is.True); Assert.That(_input.Count, Is.EqualTo(1));
        Assert.That(HudPresentationVisibility.IsSuppressed, Is.True, "Another presentation's suppression survives our cancellation.");
        Assert.That(CameraManager.Current.HasOverride, Is.False);
    }
}
