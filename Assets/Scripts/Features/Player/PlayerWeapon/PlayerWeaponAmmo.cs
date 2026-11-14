using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>현재 Player의 탄창·R/빈 탄창 자동 재장전을 관리합니다. 수리 입력이 재장전보다 우선합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler), typeof(PlayerWeaponSelection))]
// 선택(200)·수리(250)가 같은 프레임의 R 용도를 결정한 뒤, 공격 LateUpdate(300) 전에 차단합니다.
[DefaultExecutionOrder(275)]
public sealed class PlayerWeaponAmmo : MonoBehaviour
{
    [SerializeField, Min(1)] private int _defaultGunCapacity = 15;
    [SerializeField, Min(1)] private int _cryoCapacity = 15;
    [SerializeField, Min(1)] private int _sniperCapacity = 5;
    [SerializeField, Min(.01f)] private float _defaultGunReloadSeconds = .75f;
    [SerializeField, Min(.01f)] private float _cryoReloadSeconds = .75f;
    [SerializeField, Min(.01f)] private float _sniperReloadSeconds = .75f;
    private PlayerWeaponAmmoState _state;
    private PlayerWeaponSelection _selection;
    private PlayerInputHandler _input;
    private PlayerControlState _controls;
    private PlayerSkillController _skills;
    private PlayerTurretRepair _repair;
    private PlayerMatchPresence _presence;
    private double _lastReloadTime;
    private double GameSeconds => FieldSimulationTime.Seconds(this);
    private PlayerMatchPresence Presence => _presence != null ? _presence :
        (_presence = GetComponent<PlayerMatchPresence>());

    private PlayerWeaponAmmoState State => _state ??= new PlayerWeaponAmmoState(
        Mathf.Max(1, _defaultGunCapacity), Mathf.Max(1, _cryoCapacity), Mathf.Max(1, _sniperCapacity));
    public bool IsInfinite => _selection != null && _selection.CurrentWeapon == WeaponType.Spanner;
    public bool IsReloading => State.IsReloading;
    public float ReloadProgress => State.ReloadProgress;
    public int CurrentRounds => _selection != null ? GetRounds(_selection.CurrentWeapon) : 0;
    public int CurrentCapacity => _selection != null ? GetCapacity(_selection.CurrentWeapon) : 0;
    public bool UsesPercentage => _selection != null && _selection.CurrentWeapon == WeaponType.CryoBlaster;
    public int CurrentPercentage => CurrentCapacity > 0 ? Mathf.CeilToInt(100f * CurrentRounds / CurrentCapacity) : 0;
    public int GetRounds(WeaponType weapon) => State.GetRounds(weapon);
    public int GetCapacity(WeaponType weapon) => State.GetCapacity(weapon);
    public float GetReloadDuration(WeaponType weapon) => weapon switch
    {
        WeaponType.Spanner => 0,
        WeaponType.CryoBlaster => _cryoReloadSeconds,
        WeaponType.Sniper => _sniperReloadSeconds,
        _ => _defaultGunReloadSeconds
    };
    public bool CanFire(WeaponType weapon) => isActiveAndEnabled && State.CanFire(weapon);
    internal bool TryConsumeRound(WeaponType weapon)
    {
        if (!isActiveAndEnabled || !State.TryConsumeRound(weapon)) return false;
        // 마지막 탄환도 정상 발사한 뒤, 다음 공격 전에 재장전을 시작합니다.
        if (_selection != null && weapon == _selection.CurrentWeapon && weapon != WeaponType.Spanner &&
            State.GetRounds(weapon) == 0) TryReload();
        return true;
    }

    private void Awake()
    {
        _selection = GetComponent<PlayerWeaponSelection>();
        _input = GetComponent<PlayerInputHandler>();
        _controls = GetComponent<PlayerControlState>();
        _skills = GetComponent<PlayerSkillController>();
        _repair = GetComponent<PlayerTurretRepair>();
        _ = State;
    }

    private void OnEnable()
    {
        if (_selection != null) _selection.WeaponChanged += OnWeaponChanged;
    }

    private void OnDisable()
    {
        if (_selection != null) _selection.WeaponChanged -= OnWeaponChanged;
        State.CancelReload();
    }

    private void OnWeaponChanged(WeaponType _) => State.CancelReload();

    private bool IsInterrupted => _selection == null || !_selection.isActiveAndEnabled ||
        _input == null || !_input.isActiveAndEnabled ||
        (_controls != null && _controls.IsBlocked) ||
        (_skills != null && _skills.IsPlacingSkill) ||
        (_repair != null && (_repair.IsRepairing || _repair.ConsumesRepairInput));

    private bool CanOperate => isActiveAndEnabled && !IsInterrupted && _selection.CanSelectWeapon &&
        FieldSimulationTime.CanAdvance(this) &&
        (Presence == null || Presence.CanReport);

    private void Update()
    {
        if (IsInterrupted) { State.CancelReload(); return; }
        // 필드 정지는 진행 중인 재장전을 취소하지 않고 남은 시간을 보존합니다.
        if (_controls != null && _controls.IsFieldPaused) return;
        if (!CanOperate) { State.CancelReload(); return; }
        AdvanceReloadToNow();
        // 빈 총으로 돌아오거나 수리/설치가 끝난 경우에도 자동 재장전을 다시 시도합니다.
        if (_input.WasReloadPressed || (!IsInfinite && CurrentRounds == 0)) TryReload();
    }

    public bool TryReload()
    {
        if (!CanOperate) return false;
        float duration = GetReloadDuration(_selection.CurrentWeapon);
        if (!State.TryStartReload(_selection.CurrentWeapon, duration)) return false;
        _lastReloadTime = GameSeconds;
        return true;
    }

    private void AdvanceReloadToNow()
    {
        double now = GameSeconds;
        State.AdvanceReload(now - _lastReloadTime);
        _lastReloadTime = now;
    }

    internal PlayerWeaponAmmoState.CheckpointState CaptureCheckpoint()
    {
        // 저장 요청이 Update보다 먼저 와도 실제 필드 시각까지의 진행을 기록합니다.
        AdvanceReloadToNow();
        return State.CaptureCheckpoint();
    }
    internal void ValidateCheckpoint(PlayerWeaponAmmoState.CheckpointState state, WeaponType selected)
    {
        State.ValidateCheckpoint(state);
        if (state.Remaining > 0 && state.Weapon != selected)
            throw new System.IO.InvalidDataException("The reloading weapon must be equipped.");
    }
    internal void RestoreCheckpoint(PlayerWeaponAmmoState.CheckpointState state)
    {
        if (state == null)
            _state = new PlayerWeaponAmmoState(Mathf.Max(1, _defaultGunCapacity), Mathf.Max(1, _cryoCapacity), Mathf.Max(1, _sniperCapacity));
        else State.RestoreCheckpoint(state);
        _lastReloadTime = GameSeconds;
    }
}
