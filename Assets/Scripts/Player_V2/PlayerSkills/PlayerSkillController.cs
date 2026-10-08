// 스킬 준비 상태와 Q/E 입력을 연결합니다. Scene이 전투/준비 가능 여부를 명시적으로 전달합니다.
using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler), typeof(PlayerBomb), typeof(PlayerTurretSkill))]
[RequireComponent(typeof(PlayerBarrierSkill), typeof(PlayerBeaconSkill))]
[DefaultExecutionOrder(100)]
public sealed class PlayerSkillController : MonoBehaviour
{
    [SerializeField] private Sprite bombIcon;
    [SerializeField] private Sprite turretIcon;
    [SerializeField] private Sprite _barrierIcon;
    [SerializeField] private Sprite _beaconIcon;
    [SerializeField] private Sprite _recoveryKeyIcon;
    [SerializeField] private TMP_FontAsset uiFont;
    private PlayerInputHandler _input;
    private PlayerBomb _bomb;
    private PlayerTurretSkill _turret;
    private PlayerBarrierSkill _barrier;
    private PlayerBeaconSkill _beacon;
    private Func<bool> _canPrepare;
    private Func<bool> _canUse;
    private PlayerSkillHud _hud;

    public PlayerSkillLoadout Loadout { get; } = new PlayerSkillLoadout();
    public bool IsSelectionOpen => _hud != null && _hud.IsOpen;
    public bool CanPrepare => isActiveAndEnabled && (_canPrepare == null || _canPrepare());
    public bool CanUse => isActiveAndEnabled && !IsSelectionOpen && (_canUse == null || _canUse());
    public bool CanRecover => isActiveAndEnabled && !IsSelectionOpen && (CanPrepare || CanUse);

    private void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
        _bomb = GetComponent<PlayerBomb>();
        _turret = GetComponent<PlayerTurretSkill>();
        _barrier = GetComponent<PlayerBarrierSkill>();
        _beacon = GetComponent<PlayerBeaconSkill>();
    }

    private void Start()
    {
        var hudObject = new GameObject("Player Skill HUD", typeof(RectTransform));
        hudObject.transform.SetParent(transform, false);
        _hud = hudObject.AddComponent<PlayerSkillHud>();
        _hud.Bind(this, bombIcon, turretIcon, uiFont, _barrierIcon, _beaconIcon, _recoveryKeyIcon);
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
        if (_barrier != null) _barrier.CancelPlacement();
        Loadout.Clear();
    }
    public bool IsAvailable(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => _bomb != null && _bomb.HasPrefab,
        PlayerSkillId.TemporaryTurret => _turret != null && _turret.HasPrefab,
        PlayerSkillId.EnergyBarrier => _barrier != null && _barrier.HasPrefab,
        PlayerSkillId.LureBeacon => _beacon != null && _beacon.HasPrefab,
        _ => false
    };
    public int SkillCount(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => BombCount,
        PlayerSkillId.TemporaryTurret => TurretCount,
        PlayerSkillId.EnergyBarrier => _barrier != null ? _barrier.Charges : 0,
        PlayerSkillId.LureBeacon => _beacon != null && _beacon.CanDeploy ? 1 : 0,
        _ => 0
    };
    public bool IsSkillReady(PlayerSkillId skill) => IsAvailable(skill) && SkillCount(skill) > 0 &&
        (skill == PlayerSkillId.EnergyBarrier || RemainingCooldown(skill) <= 0);
    public string BarrierPlacementHint => _barrier != null ? _barrier.PlacementHint : "";
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
    public float RemainingCooldown(PlayerSkillId skill) => skill switch
    {
        PlayerSkillId.Bomb => _bomb != null ? _bomb.RemainingCooldown : 0,
        PlayerSkillId.TemporaryTurret => _turret != null ? _turret.CurrentCooldownTimer : 0,
        PlayerSkillId.EnergyBarrier => _barrier != null ? _barrier.RechargeRemaining : 0,
        PlayerSkillId.LureBeacon => _beacon != null ? _beacon.RemainingCooldown : 0,
        _ => 0
    };
    public float CooldownProgress(PlayerSkillId skill)
    {
        float duration = skill switch
        {
            PlayerSkillId.Bomb => _bomb != null ? _bomb.CooldownDuration : 0,
            PlayerSkillId.TemporaryTurret => _turret != null ? _turret.SkillCooldown : 0,
            PlayerSkillId.EnergyBarrier => _barrier != null ? _barrier.RechargeSeconds : 0,
            PlayerSkillId.LureBeacon => _beacon != null ? _beacon.Cooldown : 0,
            _ => 0
        };
        return duration > 0f ? Mathf.Clamp01(RemainingCooldown(skill) / duration) : 0f;
    }

    public bool TryUse(PlayerSkillId skill)
    {
        if (!CanUse || !Loadout.Contains(skill)) return false;
        switch (skill)
        {
            case PlayerSkillId.Bomb: return _bomb.TryUseSkill();
            case PlayerSkillId.TemporaryTurret: return _turret.TryUseSkill();
            case PlayerSkillId.EnergyBarrier: return _barrier != null && _barrier.TryUseSkill();
            case PlayerSkillId.LureBeacon: return _beacon != null && _beacon.TryUseSkill();
            default: return false;
        }
    }

    private void Update()
    {
        if (_input == null) return;
        if (_input.WasRecoverPressed && CanRecover) _turret.TryRecoverNearest();
        if (!CanUse) return;
        if (_input.WasSkillQPressed) TryUse(Loadout.Q);
        if (_input.WasBombPressed) TryUse(Loadout.E);
    }

    private void OnDisable() { if (_hud != null) _hud.gameObject.SetActive(false); }
    private void OnEnable() { if (_hud != null) _hud.gameObject.SetActive(true); }
}
