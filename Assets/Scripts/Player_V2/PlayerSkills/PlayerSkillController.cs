// 스킬 준비 상태와 Q/E 입력을 연결합니다. Scene이 전투/준비 가능 여부를 명시적으로 전달합니다.
using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputHandler), typeof(PlayerBomb), typeof(PlayerTurretSkill))]
[DefaultExecutionOrder(100)]
public sealed class PlayerSkillController : MonoBehaviour
{
    [SerializeField] private Sprite bombIcon;
    [SerializeField] private Sprite turretIcon;
    [SerializeField] private TMP_FontAsset uiFont;
    private PlayerInputHandler _input;
    private PlayerBomb _bomb;
    private PlayerTurretSkill _turret;
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
    }

    private void Start()
    {
        var hudObject = new GameObject("Player Skill HUD", typeof(RectTransform));
        hudObject.transform.SetParent(transform, false);
        _hud = hudObject.AddComponent<PlayerSkillHud>();
        _hud.Bind(this, bombIcon, turretIcon, uiFont);
    }

    public void BindSceneState(Func<bool> canPrepare, Func<bool> canUse)
    {
        _canPrepare = canPrepare;
        _canUse = canUse;
    }

    public void CloseSelection() { if (_hud != null) _hud.Close(); }

    public bool TrySelect(PlayerSkillId skill) => CanPrepare && IsAvailable(skill) && Loadout.TrySelect(skill);
    public void ClearSelection() { if (CanPrepare) Loadout.Clear(); }
    public bool IsAvailable(PlayerSkillId skill) => skill == PlayerSkillId.Bomb ? _bomb.HasPrefab :
        skill == PlayerSkillId.TemporaryTurret && _turret.HasPrefab;
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
    public float RemainingCooldown(PlayerSkillId skill) => skill == PlayerSkillId.Bomb ? _bomb.RemainingCooldown :
        skill == PlayerSkillId.TemporaryTurret ? _turret.CurrentCooldownTimer : 0f;
    public float CooldownProgress(PlayerSkillId skill)
    {
        float duration = skill == PlayerSkillId.Bomb ? _bomb.CooldownDuration :
            skill == PlayerSkillId.TemporaryTurret ? _turret.SkillCooldown : 0f;
        return duration > 0f ? Mathf.Clamp01(RemainingCooldown(skill) / duration) : 0f;
    }

    public bool TryUse(PlayerSkillId skill)
    {
        if (!CanUse || !Loadout.Contains(skill)) return false;
        switch (skill)
        {
            case PlayerSkillId.Bomb: return _bomb.TryUseSkill();
            case PlayerSkillId.TemporaryTurret: return _turret.TryUseSkill();
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
