// 스킬 준비 상태와 Q/E 입력을 연결합니다. Scene이 전투/준비 가능 여부를 명시적으로 전달합니다.
using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler), typeof(PlayerBomb), typeof(PlayerTurretSkill))]
[RequireComponent(typeof(PlayerBarrierSkill), typeof(PlayerBeaconSkill))]
[DefaultExecutionOrder(100)]
public sealed partial class PlayerSkillController : MonoBehaviour
{
    private PlayerInputHandler _input;
    private PlayerBomb _bomb;
    private PlayerTurretSkill _turret;
    private PlayerBarrierSkill _barrier;
    private PlayerBeaconSkill _beacon;
    private Func<bool> _canPrepare;
    private Func<bool> _canUse;
    private PlayerSkillHud _hud;
    private PlayerControlState _control;
    private readonly System.Collections.Generic.Dictionary<PlayerSkillId, PlayerSkillBase> _skills = new();

    public PlayerSkillLoadout Loadout { get; } = new PlayerSkillLoadout();
    public bool IsSelectionOpen => _hud != null && _hud.IsOpen;
    public bool CanPrepare => isActiveAndEnabled && (_control == null || _control.CanPrepare) && (_control != null && _control.HasMatchBinding || _canPrepare == null || _canPrepare());
    public bool CanUse => isActiveAndEnabled && !IsSelectionOpen && (_control == null || _control.CanAct) && (_control != null && _control.HasMatchBinding || _canUse == null || _canUse());
    public bool CanRecover => isActiveAndEnabled && !IsSelectionOpen && !IsPlacingSkill && (CanPrepare || CanUse);
    public bool IsPlacingTurret => _turret != null && _turret.IsPlacing;
    public bool IsPlacingSkill => IsPlacingTurret || ActivePlacement != null;
    private PlayerPlacementSkillBase ActivePlacement
    {
        get
        {
            foreach (var skill in _skills.Values)
                if (skill is PlayerPlacementSkillBase placement && placement.IsPlacing) return placement;
            return null;
        }
    }

    private void Awake()
    {
        _control = GetComponent<PlayerControlState>();
        if (_control == null) _control = gameObject.AddComponent<PlayerControlState>();
        foreach (var skill in GetComponents<PlayerSkillBase>()) _skills[skill.Id] = skill;
        _input = GetComponent<PlayerInputHandler>();
        _bomb = GetComponent<PlayerBomb>();
        _turret = GetComponent<PlayerTurretSkill>();
        _barrier = GetComponent<PlayerBarrierSkill>();
        _beacon = GetComponent<PlayerBeaconSkill>();
    }

    /// <summary>씬에 미리 배치한 HUD를 연결합니다. 로컬 플레이어 선택은 씬 바인더가 담당합니다.</summary>
    public bool BindHud(PlayerSkillHud hud)
    {
        if (!isActiveAndEnabled || hud == null || !hud.isActiveAndEnabled
            || hud.gameObject.scene != gameObject.scene || !hud.IsConfigured) return false;
        if (_hud != null && _hud != hud) _hud.Unbind();
        if (!hud.Bind(this)) return false;
        _hud = hud;
        return true;
    }

    public void DetachHud(PlayerSkillHud hud)
    {
        if (_hud == hud) _hud = null;
    }

    public void BindSceneState(Func<bool> canPrepare, Func<bool> canUse)
    {
        _canPrepare = canPrepare;
        _canUse = canUse;
    }

    public void CloseSelection() { if (_hud != null) _hud.Close(); }

    public bool TrySelect(PlayerSkillId skill) => CanPrepare && IsAvailable(skill) && Loadout.TrySelect(skill);
    public void ClearSelection()
    {
        if (!CanPrepare) return;
        CancelPlacements();
        Loadout.Clear();
    }
    private PlayerSkillBase FindSkill(PlayerSkillId id) => _skills.TryGetValue(id, out var skill) ? skill : null;
    public bool IsAvailable(PlayerSkillId id) => FindSkill(id) is PlayerSkillBase skill && skill.HasPrefab;
    public int SkillCount(PlayerSkillId id) => FindSkill(id)?.AvailableCount ?? 0;
    public bool IsSkillReady(PlayerSkillId id) => FindSkill(id)?.IsReady ?? false;
    public string BarrierPlacementHint => _barrier != null ? _barrier.PlacementHint : "";
    public string PlacementHint => IsPlacingTurret ? _turret.PlacementHint : ActivePlacement?.PlacementHint ?? "";
    public PlayerTurretPlacementResult TurretPlacementFeedback =>
        _turret != null ? _turret.PlacementFeedback : PlayerTurretPlacementResult.Allowed;
    public string PlacementFailure
    {
        get
        {
            if (IsPlacingTurret) return PlayerSkillDescriptions.TurretPlacementFailure(TurretPlacementFeedback);
            if (ActivePlacement != null) return ActivePlacement.PlacementFailure;
            if (IsPlacingSkill || _bomb == null || !Loadout.Contains(PlayerSkillId.Bomb)) return "";
            return PlayerSkillDescriptions.BombPlacementFailure(_bomb.PlacementFeedback);
        }
    }
    public int BombCount => _bomb.CurrentBombCount;
    public int TurretCount => _turret.CurrentTurretCount;
    public Transform RecoverableTurret
    {
        get
        {
            var turret = _turret.RecoverableTurret;
            return turret != null ? turret.transform : null;
        }
    }
    public float RemainingCooldown(PlayerSkillId id) => FindSkill(id)?.RemainingCooldown ?? 0;
    public float CooldownProgress(PlayerSkillId id)
    {
        var skill = FindSkill(id);
        return skill != null && skill.CooldownDuration > 0 ? Mathf.Clamp01(skill.RemainingCooldown / skill.CooldownDuration) : 0;
    }
    public bool TryUse(PlayerSkillId id)
    {
        if (!CanUse || !Loadout.Contains(id)) return false;
        var skill = FindSkill(id);
        if (skill == null || !skill.IsReady) return false;
        CancelPlacements(id);
        return skill.TryUseSkill();
    }

    private void CancelPlacements(PlayerSkillId except = PlayerSkillId.None)
    {
        if (_turret != null && except != _turret.Id) _turret.CancelPlacement();
        foreach (var skill in _skills.Values)
            if (skill.Id != except && skill is PlayerPlacementSkillBase placement) placement.CancelPlacement();
    }

    private void Update()
    {
        if (_input == null) return;
        if (_input.WasRecoverPressed && CanRecover) _turret.TryRecoverNearest();
        if (!CanUse) return;
        if (_input.WasSkillQPressed) TryUse(Loadout.Q);
        if (_input.WasBombPressed) TryUse(Loadout.E);
    }

    private void OnDisable()
    {
        CancelPlacements();
        if (_hud != null) _hud.Unbind();
    }
}
