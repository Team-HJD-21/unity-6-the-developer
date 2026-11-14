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
    private PlayerPortalSkill _portal;
    public bool PortalConsumesR { get; private set; }
    private Func<bool> _canPrepare;
    private Func<bool> _canUse;
    private PlayerSkillHud _hud;
    private PlayerControlState _control;
    private PlayerSkillResearch _research;
    public PlayerSkillResearch Research => _research != null ? _research : (_research = GetComponent<PlayerSkillResearch>());
    public PlayerSkillId Candidate(int slot) => Research != null ? Research.Candidate(slot) : slot >= 0 && slot < 4 ? (PlayerSkillId)(slot + 1) : PlayerSkillId.None;
    public bool IsCandidate(PlayerSkillId id) => Research != null ? Research.Contains(id) : id >= PlayerSkillId.Bomb && id <= PlayerSkillId.LureBeacon;
    public string UpgradeStatName(PlayerSkillId id) => id switch
    {
        PlayerSkillId.Bomb or PlayerSkillId.PlayerTurret => "피해량",
        PlayerSkillId.DualWield => "양손 피해 배율",
        PlayerSkillId.EnergyBarrier => "최대 연결 거리",
        PlayerSkillId.LureBeacon => "유인 범위",
        PlayerSkillId.Portal => "설치 범위",
        _ => ""
    };
    public float UpgradeStatValue(PlayerSkillId id) => id switch
    {
        PlayerSkillId.Bomb => _bomb != null ? _bomb.EffectDamage : 0,
        PlayerSkillId.PlayerTurret => _turret != null ? _turret.EffectDamage : 0,
        PlayerSkillId.EnergyBarrier => _barrier != null ? _barrier.ConnectionRange : 0,
        PlayerSkillId.LureBeacon => _beacon != null ? _beacon.EffectRadius : 0,
        PlayerSkillId.Portal => _portal != null ? _portal.PlacementRadius : 0,
        PlayerSkillId.DualWield => 100f * (Research?.Multiplier(id) ?? 1),
        _ => 0
    };
    internal void OnCandidatesChanged()
    {
        CancelPlacements();
        Loadout.Restore(IsCandidate(Loadout.Q) ? Loadout.Q : PlayerSkillId.None,
            IsCandidate(Loadout.E) ? Loadout.E : PlayerSkillId.None);
    }
    private readonly System.Collections.Generic.Dictionary<PlayerSkillId, PlayerSkillBase> _skills = new();

    public PlayerSkillLoadout Loadout { get; } = new PlayerSkillLoadout();
    public event Action<int, PlayerSkillId, bool> SlotUseAttempted;
    public event Action<string> PlacementRejected;
    internal void NotifyPlacementRejected(string reason)
    { PlacementRejected?.Invoke(string.IsNullOrEmpty(reason) ? "이 위치에 설치할 수 없습니다." : reason); }
    public bool IsSelectionOpen => _hud != null && _hud.IsOpen;
    public bool CanPrepare => isActiveAndEnabled && (_control == null || _control.CanPrepare) && (_control != null && _control.HasMatchBinding || _canPrepare == null || _canPrepare());
    public bool CanUse => isActiveAndEnabled && !IsSelectionOpen && (_control == null || _control.CanAct) && (_control != null && _control.HasMatchBinding || _canUse == null || _canUse());
    public bool CanRecover => isActiveAndEnabled && !IsSelectionOpen && !IsPlacingSkill && (CanPrepare || CanUse);
    public bool IsPlacingTurret => _turret != null && _turret.IsPlacing;
    public bool IsSelectingTurret => _turret != null && _turret.IsSelecting;
    public bool IsPlacingSkill => (_turret != null && _turret.IsPending) || ActivePlacement != null;
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
        _portal = GetComponent<PlayerPortalSkill>();
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

    public bool TrySelect(PlayerSkillId skill) => CanPrepare && IsAvailable(skill) && IsCandidate(skill) && Loadout.TrySelect(skill);
    /// <summary>HUD 드롭 확정 경로입니다. 원본 준비 권한과 설정을 검사하고 기존 설치 의도를 정리한 뒤 장착합니다.</summary>
    public bool TryEquip(int slot, PlayerSkillId skill)
    {
        if (!CanPrepare || !IsAvailable(skill) || !IsCandidate(skill) || slot < 0 || slot > 1 ||
            (slot == 0 ? Loadout.Q : Loadout.E) == skill) return false;
        CancelPlacements();
        return Loadout.TryEquip(slot, skill);
    }
    public void ClearSelection()
    {
        if (!CanPrepare) return;
        CancelPlacements();
        Loadout.Clear();
    }
    private PlayerSkillBase FindSkill(PlayerSkillId id) => _skills.TryGetValue(id, out var skill) ? skill : null;
    public bool IsConfiguredSkill(PlayerSkillId id) => FindSkill(id) is PlayerSkillBase skill && skill.HasPrefab;
    public bool IsAvailable(PlayerSkillId id) => IsConfiguredSkill(id) && (Research == null || Research.IsUnlocked(id));
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
            FindRecoveryTarget(out var turret, out _);
            return turret != null ? turret.transform : null;
        }
    }
    public PlayerPortalEndpoint RecoverablePortal
    {
        get { FindRecoveryTarget(out _, out var portal); return portal; }
    }

    // F 안내와 실행은 같은 후보를 사용합니다. 겹친 범위에서도 한 번에 하나만 회수합니다.
    private void FindRecoveryTarget(out PlayerTurret turret, out PlayerPortalEndpoint portal)
    {
        turret = null; portal = null;
        if (!CanRecover) return;
        turret = _turret != null ? _turret.RecoverableTurret : null;
        portal = _portal != null ? _portal.NearestInteractable : null;
        if (turret == null || portal == null) return;
        var feet = GetComponent<PlayerDirectionalColliders>();
        Vector2 origin = feet != null && feet.isActiveAndEnabled ? feet.GroundPosition : (Vector2)transform.position;
        float turretDistance = (turret.Health.ApproachPoint(origin) - origin).sqrMagnitude;
        float portalDistance = ((Vector2)portal.FootprintAt(portal.transform.position).ClosestPoint(origin) - origin).sqrMagnitude;
        if (portalDistance <= turretDistance) turret = null;
        else portal = null;
    }

    public bool TryRecoverNearest()
    {
        FindRecoveryTarget(out var turret, out var portal);
        return portal != null ? _portal.TryRecoverNearest() : turret != null && turret.TryRecover(_turret);
    }
    public float RemainingCooldown(PlayerSkillId id) => FindSkill(id)?.RemainingCooldown ?? 0;
    public float CooldownProgress(PlayerSkillId id)
    {
        var skill = FindSkill(id);
        return skill != null && skill.CooldownDuration > 0 ? Mathf.Clamp01(skill.RemainingCooldown / skill.CooldownDuration) : 0;
    }
    public bool TryUse(PlayerSkillId id)
    {
        if (!CanUse || !Loadout.Contains(id) || !IsAvailable(id) || !IsCandidate(id)) return false;
        var skill = FindSkill(id);
        if (skill == null || !skill.IsReady) return false;
        CancelPlacements(id);
        return skill.TryUseSkill();
    }

    /// <summary>Q/E 입력 경로. 비전투/메뉴 잠금은 사용 요청으로 송출하지 않습니다.</summary>
    public bool TryUseSlot(int slot)
    {
        if (slot < 0 || slot > 1 || !CanUse) return false;
        PlayerSkillId id = slot == 0 ? Loadout.Q : Loadout.E;
        bool accepted = TryUse(id);
        SlotUseAttempted?.Invoke(slot, id, accepted);
        return accepted;
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
        if (!GameInput.IsPressed(GameKey.R)) PortalConsumesR = false;
        if (_input.WasReloadPressed && CanRecover && _portal != null && _portal.NearestInteractable != null)
        {
            PortalConsumesR = true;
            bool accepted = _portal.TryTravel();
            if (!accepted) NotifyPlacementRejected(_portal.InteractionFailure);
        }
        else if (_input.WasRecoverPressed && CanRecover) TryRecoverNearest();
        if (!CanUse) return;
        if (_input.WasSkillQPressed) TryUseSlot(0);
        if (_input.WasBombPressed) TryUseSlot(1);
    }

    private void OnDisable()
    {
        CancelPlacements();
        if (_hud != null) _hud.Unbind();
    }
}
