// Stage1 진행 소유자의 요청만 기존 컷씬·HUD·카메라에 연결합니다.
// Scene 이동·보스 생성·승패·게임 시계와 다른 소유자의 입력 lease는 변경하지 않습니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class StageBossBattlePresentation : MonoBehaviour, IStageBossBattlePresentation, ILocalInputConsumer
{
    [Header("Existing Stage1 views")]
    public InitialStagePlayerHUD StageHud;
    public PlayerSkillHud SkillHud;
    public PlayerBossCutscenePreview Entrance;
    public FieldEntryAnnouncement AnnouncementPrefab;
    [Header("Existing announcement and camera motion")]
    [Range(.1f, 1)] public float VictoryZoomRatio = .45f;
    [Min(0)] public float VictoryZoomSeconds = .75f;
    [Min(0)] public float VictoryFadeSeconds = .22f;
    private IInputBlocker _input;
    private IDisposable _inputLease, _hudLease, _cameraLease;
    private CancellationTokenSource _operation;
    private FieldEntryAnnouncement _victory;
    private CombatBossView _bossView;
    private CombatHudHeader _header;
    private PlayerSkillController _battleOwner;
    private PlayerSkillHudSceneBinding _skillBinding;
    private bool _skillBindingEnabled, _skillBindingCaptured, _entranceOwned, _replayOwned, _battleBound;
    private TacticalCommandController _tactical;
    private bool _tacticalCaptured, _tacticalEnabled, _tacticalViewActive;
    public bool IsBusy => _operation != null;

    public void BindInputBlocker(IInputBlocker input) => _input = input ?? throw new ArgumentNullException(nameof(input));

    private void ResolveViews()
    {
        if (StageHud == null) foreach (var hud in FindObjectsByType<InitialStagePlayerHUD>(FindObjectsSortMode.None))
            if (hud.gameObject.scene == gameObject.scene) { StageHud = hud; break; }
        if (SkillHud == null) foreach (var hud in FindObjectsByType<PlayerSkillHud>(FindObjectsSortMode.None))
            if (hud.gameObject.scene == gameObject.scene) { SkillHud = hud; break; }
        if (Entrance == null) foreach (var preview in FindObjectsByType<PlayerBossCutscenePreview>(FindObjectsSortMode.None))
            if (preview.gameObject.scene == gameObject.scene) { Entrance = preview; break; }
        if (_input == null)
        {
            var app = FindFirstObjectByType<AppRoot>();
            if (app != null && app.IsInitialized) _input = app.LocalInput;
        }
        if (StageHud == null || SkillHud == null || _input == null)
            throw new InvalidOperationException("Boss presentation requires the existing local Stage1 HUD and input binding.");
        if (AnnouncementPrefab == null) AnnouncementPrefab = SkillHud.GetComponent<CombatHudIntro>()?.AnnouncementPrefab;
        _bossView = SkillHud.GetComponentInChildren<CombatBossView>(true);
        _header = SkillHud.GetComponentInChildren<CombatHudHeader>(true);
    }
    private PlayerInfo LocalActor()
    {
        var actor = StageHud.LocalPlayer;
        var follow = CameraManager.Current != null ? CameraManager.Current.GetComponent<MatchCameraFollow>() : null;
        if (actor == null || actor.IsDestroyed || !actor.gameObject.scene.isLoaded || follow == null || follow.Target != actor.transform)
            throw new InvalidOperationException("Use the Stage HUD's existing local actor and current follow camera.");
        return actor;
    }
    private CancellationTokenSource Begin(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!isActiveAndEnabled || IsBusy) throw new InvalidOperationException("Boss presentation is disabled or already running.");
        try { ResolveViews(); }
        catch { ResetBattle(); throw; }
        return _operation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
    }
    private void End(CancellationTokenSource operation)
    {
        if (ReferenceEquals(_operation, operation)) _operation = null;
        operation.Dispose();
    }
    private void HoldInputAndHud()
    {
        if (_inputLease == null) _inputLease = _input.Acquire("StageBossBattlePresentation");
        if (_hudLease == null) _hudLease = HudPresentationVisibility.Suppress();
        SkillHud.Close();
        if (_tacticalCaptured || StageHud.Tactical == null) return;
        _tactical = StageHud.Tactical; _tacticalCaptured = true;
        _tacticalEnabled = _tactical.enabled;
        _tacticalViewActive = _tactical.View != null && _tactical.View.gameObject.activeSelf;
        _tactical.enabled = false; // 기존 OnDisable이 지도와 자신의 카메라/입력 lease를 정리합니다.
        if (_tactical.View != null) _tactical.View.gameObject.SetActive(false);
    }
    private void ReleaseHud() { _hudLease?.Dispose(); _hudLease = null; }
    private void ReleaseInput() { _inputLease?.Dispose(); _inputLease = null; }
    public async Task PlayEntranceAsync(CancellationToken cancellation)
    {
        var operation = Begin(cancellation);
        try
        {
            if (Entrance == null || !Entrance.IsConfigured) throw new InvalidOperationException("Assign the authored boss entrance preview.");
            LocalActor(); HoldInputAndHud(); Entrance.BindInputBlocker(_input); _entranceOwned = true;
            await Entrance.PlayScriptedAsync(operation.Token);
            // 마지막 암전/포착 화면과 HUD 숨김은 Core의 Fade 인계가 끝날 때까지 유지합니다.
        }
        catch { ResetBattle(); throw; }
        finally { End(operation); }
    }
    public async Task ShowBattleIntroAsync(GameObject boss, string displayName, CancellationToken cancellation)
    {
        var operation = Begin(cancellation);
        try
        {
            var health = boss != null ? boss.GetComponentInChildren<EnemyHealth>(true) : null;
            if (health == null || string.IsNullOrWhiteSpace(displayName) || _bossView == null || _header == null)
                throw new InvalidOperationException("Boss intro requires the explicitly spawned EnemyHealth, name and existing HUD views.");
            var actor = LocalActor();
            StageHud.BindBattleActor(actor.gameObject);
            if (!_skillBindingCaptured)
            {
                _skillBinding = SkillHud.GetComponent<PlayerSkillHudSceneBinding>();
                _skillBindingCaptured = true; _skillBindingEnabled = _skillBinding != null && _skillBinding.enabled;
                if (_skillBinding != null) _skillBinding.enabled = false;
            }
            _battleOwner = actor.GetComponent<PlayerSkillController>();
            if (_battleOwner == null || !_battleOwner.BindBattleHud(SkillHud)) throw new InvalidOperationException("Cannot bind the existing local skill HUD to the additive battle actor.");
            if (!StageHud.HasFieldActor || !SkillHud.IsFieldVisible) throw new InvalidOperationException("The boss actor must retain its field presentation context.");
            HoldInputAndHud();
            _header.SetBattleMode(true); _bossView.BindBattle(health, displayName); _battleBound = true;
            var skills = SkillHud.GetComponent<CombatHudIntro>(); var vitals = StageHud.GetComponent<CombatHudIntro>();
            if (skills == null || vitals == null) throw new InvalidOperationException("Both authored combat HUD scans are required.");
            _replayOwned = true; skills.ReplayForBattle("보스를 처치하세요"); vitals.ReplayForBattle("보스를 처치하세요");
            if (_entranceOwned && Entrance != null) Entrance.StopPreview(); _entranceOwned = false;
            ReleaseHud();
            while (!skills.Completed || !vitals.Completed)
            {
                operation.Token.ThrowIfCancellationRequested();
                if (actor == null || actor.IsDestroyed || !SkillHud.IsFieldVisible || !StageHud.HasFieldActor || !skills.isActiveAndEnabled || !vitals.isActiveAndEnabled)
                    throw new OperationCanceledException("The local battle HUD was released.", operation.Token);
                await Task.Yield();
            }
            operation.Token.ThrowIfCancellationRequested(); _replayOwned = false; ReleaseInput();
        }
        catch { ResetBattle(); throw; }
        finally { End(operation); }
    }
    public async Task PlayVictoryAsync(string message, float holdSeconds, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(message) || !float.IsFinite(holdSeconds) || holdSeconds < 0)
            throw new ArgumentException("Use a victory message and a finite nonnegative hold time.");
        if (_cameraLease != null) throw new InvalidOperationException("Reset the previous victory after its Fade handoff.");
        var operation = Begin(cancellation);
        try
        {
            var actor = LocalActor(); HoldInputAndHud();
            if (AnnouncementPrefab == null) throw new InvalidOperationException("Assign the existing field announcement prefab.");
            if (_victory == null)
            {
                _victory = Instantiate(AnnouncementPrefab);
                SceneManager.MoveGameObjectToScene(_victory.gameObject, gameObject.scene);
            }
            _victory.SetMessage(message); _victory.Render(0, VictoryFadeSeconds, holdSeconds, .45f);
            var manager = CameraManager.Current;
            _cameraLease = manager.PushView(actor.transform, VictoryZoomSeconds,
                Mathf.Max(.5f, manager.OutputCamera.orthographicSize * VictoryZoomRatio));
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < VictoryFadeSeconds + holdSeconds)
            {
                operation.Token.ThrowIfCancellationRequested();
                if (actor == null || actor.IsDestroyed || CameraManager.Current != manager)
                    throw new OperationCanceledException("The victory actor or camera was released.", operation.Token);
                _victory.Render(Time.unscaledTime - started, VictoryFadeSeconds, holdSeconds, .45f);
                await Task.Yield();
            }
            operation.Token.ThrowIfCancellationRequested();
            // 문구·확대·숨김을 유지해 Core Fade 직전에 HUD나 넓은 시점이 번쩍이지 않습니다.
        }
        catch { ResetBattle(); throw; }
        finally { End(operation); }
    }
    public void ResetBattle()
    {
        _operation?.Cancel();
        if (_entranceOwned && Entrance != null) Entrance.StopPreview(); _entranceOwned = false;
        if (_replayOwned)
        {
            if (SkillHud != null) SkillHud.GetComponent<CombatHudIntro>()?.CancelBattleReplay();
            if (StageHud != null) StageHud.GetComponent<CombatHudIntro>()?.CancelBattleReplay();
        }
        _replayOwned = false;
        if (_victory != null) _victory.Group.alpha = 0;
        _cameraLease?.Dispose(); _cameraLease = null;
        ReleaseHud(); ReleaseInput();
        if (_battleBound && _bossView != null) _bossView.Bind(null, "");
        if (_battleBound && _header != null) _header.SetBattleMode(false);
        _battleBound = false;
        if (_battleOwner != null && SkillHud != null && SkillHud.Owner == _battleOwner && _battleOwner.gameObject.scene != SkillHud.gameObject.scene)
            SkillHud.Unbind();
        _battleOwner = null;
        if (_skillBindingCaptured && _skillBinding != null) _skillBinding.enabled = _skillBindingEnabled;
        _skillBinding = null; _skillBindingCaptured = false;
        if (_tacticalCaptured && _tactical != null)
        {
            _tactical.enabled = _tacticalEnabled;
            if (_tactical.View != null) _tactical.View.gameObject.SetActive(_tacticalViewActive);
        }
        _tactical = null; _tacticalCaptured = false;
    }
    private void OnDisable() => ResetBattle();
    private void OnDestroy() { ResetBattle(); if (_victory != null) Destroy(_victory.gameObject); }
}
