using TeamHJD.Game.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>HUD에 주입된 로컬 Player의 상태를 미리 배치한 패널에 표시합니다. 게임은 정지하지 않습니다.</summary>
[DisallowMultipleComponent]
// AppRoot.Update(0)의 메뉴 ESC 처리보다 먼저 닫기 입력을 소비합니다.
// LateUpdate에서는 Core 메뉴 입력이 이미 처리되므로 닫기 검사를 옮기지 않습니다.
[DefaultExecutionOrder(-300)]
public sealed class PlayerStatusPanel : MonoBehaviour
{
    [SerializeField] private StagePlayerVitalsView _vitals;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _levelValue;
    [SerializeField] private TMP_Text _experienceValue;
    [SerializeField] private TMP_Text _healthValue;
    [SerializeField] private TMP_Text _speedValue;
    [SerializeField] private TMP_Text _weaponValue;
    [SerializeField] private TMP_Text _ammoValue;
    [SerializeField] private TMP_Text _dashValue;
    [SerializeField] private Image _healthFill;
    [SerializeField] private Image _experienceFill;
    [Header("Weapon details")]
    [SerializeField] private Button _previousWeaponButton;
    [SerializeField] private Button _nextWeaponButton;
    [SerializeField] private TMP_Text _weaponTitle;
    [SerializeField] private TMP_Text _damageValue;
    [SerializeField] private TMP_Text _intervalValue;
    [SerializeField] private TMP_Text _projectilesValue;
    [SerializeField] private TMP_Text _projectileSpeedValue;
    [SerializeField] private TMP_Text _rangeValue;
    [SerializeField] private TMP_Text _reloadValue;
    [Header("Base stats and progression")]
    [SerializeField] private TMP_Text _attackMultiplierValue;
    [SerializeField] private TMP_Text _criticalChanceValue;
    [SerializeField] private TMP_Text _availablePointsValue;
    [SerializeField] private Button[] _investButtons;
    [SerializeField] private Button[] _refundButtons;
    [SerializeField] private PlayerLevelRewardsView _levelRewards;
    [SerializeField] private PlayerStatusCharacterPreview _characterPreview;
    [SerializeField] private PlayerProgressionDebugPanel _debugPanel;
    [SerializeField] private Image _weaponIcon;
    [SerializeField] private Sprite[] _weaponIcons;
    [SerializeField] private PlayerSkillTreePanel _skillTree;
    private PlayerInfo _owner;
    private PlayerControlState _controls;
    private PlayerExperience _experience;
    private PlayerMovement _movement;
    private PlayerSlowEffects _slow;
    private PlayerWeaponSelection _selection;
    private PlayerWeaponAmmo _ammo;
    private PlayerDash _dash;
    private PlayerAttack _attack;
    private PlayerBaseStats _baseStats;

    public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;
    public PlayerInfo Owner => _owner;
    private bool IsConfigured => _vitals != null && _panelRoot != null &&
        _panelRoot != gameObject && _panelRoot.transform.IsChildOf(transform) &&
        _closeButton != null && _levelValue != null && _experienceValue != null &&
        _healthValue != null && _speedValue != null && _weaponValue != null &&
        _ammoValue != null && _dashValue != null && _healthFill != null && _experienceFill != null &&
        _previousWeaponButton != null && _nextWeaponButton != null && _weaponTitle != null &&
        _damageValue != null && _intervalValue != null && _projectilesValue != null &&
        _projectileSpeedValue != null && _rangeValue != null && _reloadValue != null &&
        _attackMultiplierValue != null && _criticalChanceValue != null && _availablePointsValue != null &&
        _investButtons != null && _investButtons.Length == 3 && System.Array.TrueForAll(_investButtons, button => button != null) &&
        _refundButtons != null && _refundButtons.Length == 3 && System.Array.TrueForAll(_refundButtons, button => button != null) &&
        _levelRewards != null && _levelRewards.IsConfigured && _characterPreview != null && _characterPreview.IsConfigured &&
        _weaponIcon != null && _weaponIcons != null && _weaponIcons.Length == 4 &&
        _skillTree != null && _skillTree.IsConfigured;

    private void OnEnable()
    {
        Close();
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        if (_previousWeaponButton != null) _previousWeaponButton.onClick.AddListener(SelectPreviousWeapon);
        if (_nextWeaponButton != null) _nextWeaponButton.onClick.AddListener(SelectNextWeapon);
        BindStatButtons(true);
    }

    private void OnDisable()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_previousWeaponButton != null) _previousWeaponButton.onClick.RemoveListener(SelectPreviousWeapon);
        if (_nextWeaponButton != null) _nextWeaponButton.onClick.RemoveListener(SelectNextWeapon);
        BindStatButtons(false);
        Close();
        BindOwner(null);
    }

    private void Update()
    {
        RefreshOwner();
        if (IsOpen && UIEscapeInput.TryConsume())
        {
            if (_skillTree == null || !_skillTree.TryClose()) Close();
            return;
        }
        if (_owner == null || (_controls != null && _controls.IsBlocked)) { Close(); return; }
        if (!GameInput.WasPressedThisFrame(GameKey.I) || IsEditingText()) return;
        if (IsOpen) Close();
        else TryOpen();
    }

    private void LateUpdate()
    {
        RefreshOwner();
        if (!IsOpen) return;
        if (_controls != null && _controls.IsBlocked) { Close(); return; }
        RefreshValues();
    }

    public bool TryOpen()
    {
        RefreshOwner();
        if (!isActiveAndEnabled || !IsConfigured || _owner == null ||
            (_controls != null && _controls.IsBlocked)) return false;
        RefreshValues();
        _panelRoot.SetActive(true);
        return true;
    }

    public void Close()
    {
        if (_skillTree != null) _skillTree.Close();
        if (_panelRoot == null || _panelRoot == gameObject || !_panelRoot.activeSelf) return;
        // X로 닫은 클릭이 같은 프레임에 월드 공격/선택으로 새지 않게 합니다.
        GameInput.TryConsumePrimaryPointerPress();
        _panelRoot.SetActive(false);
    }

    private void RefreshOwner()
    {
        var player = _vitals != null && _vitals.HasPlayer ? _vitals.Player : null;
        if (ReferenceEquals(player, _owner)) return;
        Close();
        BindOwner(player);
    }

    private void BindOwner(PlayerInfo player)
    {
        _owner = player;
        _controls = player != null ? player.GetComponent<PlayerControlState>() : null;
        _experience = player != null ? player.GetComponent<PlayerExperience>() : null;
        _movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        _slow = player != null ? player.GetComponent<PlayerSlowEffects>() : null;
        _selection = player != null ? player.GetComponent<PlayerWeaponSelection>() : null;
        _ammo = player != null ? player.GetComponent<PlayerWeaponAmmo>() : null;
        _dash = player != null ? player.GetComponent<PlayerDash>() : null;
        _attack = player != null ? player.GetComponent<PlayerAttack>() : null;
        _baseStats = player != null ? player.GetComponent<PlayerBaseStats>() : null;
        if (_levelRewards != null) _levelRewards.Bind(player);
        if (_characterPreview != null) _characterPreview.Bind(player);
        if (_debugPanel != null) _debugPanel.Bind(player);
    }

    private void RefreshValues()
    {
        _levelValue.text = _experience != null ? $"LV. {_experience.Level:00}" : "LV. —";
        _experienceValue.text = _experience == null ? "XP —" : _experience.IsMaximumLevel ? "XP MAX" :
            $"{_experience.ExperienceInLevel} / {_experience.RequiredExperience} XP";
        SetBar(_experienceFill, _experience != null ? _experience.Progress : 0);
        _healthValue.text = $"{_owner.CurrentHealth} / {_owner.MaxHealth}";
        SetBar(_healthFill, _owner.MaxHealth > 0 ? (float)_owner.CurrentHealth / _owner.MaxHealth : 0);
        _speedValue.text = _movement != null ? (_movement.MoveSpeed * (_slow != null ? _slow.SpeedMultiplier : 1f)).ToString("0.0") : "—";
        _weaponValue.text = _selection == null ? "—" : _selection.CurrentWeapon switch
        {
            WeaponType.Spanner => "스패너",
            WeaponType.DefaultGun => "기본총",
            WeaponType.CryoBlaster => "냉각총",
            WeaponType.Sniper => "스나이퍼",
            _ => "—"
        };
        _ammoValue.text = _ammo == null ? "—" : _ammo.IsInfinite ? "∞" : _ammo.IsReloading
            ? $"Reload · {_ammo.ReloadProgress:P0}" : _ammo.UsesPercentage ? $"{_ammo.CurrentPercentage}%" : $"{_ammo.CurrentRounds} / {_ammo.CurrentCapacity}";
        _dashValue.text = _dash == null ? "—" : _dash.IsDashing ? "대시 중" : _dash.CooldownRemaining > 0
            ? $"{_dash.CooldownRemaining:0.0}초" : _dash.IsReady ? "준비 완료" : "사용 불가";
        _attackMultiplierValue.text = _baseStats != null ? $"{_baseStats.AttackMultiplier:P0}" : "100%";
        _criticalChanceValue.text = _baseStats != null ? $"{_baseStats.CriticalChance:P0}" : "0%";
        _availablePointsValue.text = $"스탯 포인트  {(_baseStats != null ? _baseStats.AvailablePoints : 0)}";
        for (int i = 0; i < 3; i++)
        {
            _investButtons[i].interactable = _baseStats != null && _baseStats.CanInvest((PlayerBaseStat)i);
            _refundButtons[i].interactable = _baseStats != null && _baseStats.CanRefund((PlayerBaseStat)i);
        }
        _levelRewards.Bind(_owner);
        _characterPreview.Bind(_owner);
        if (_debugPanel != null) _debugPanel.Bind(_owner);
        RefreshWeaponStats();
    }

    private void RefreshWeaponStats()
    {
        _weaponTitle.text = _weaponValue.text;
        int slot = _selection != null ? PlayerWeaponSlotMapping.GetSlot(_selection.CurrentWeapon) - 1 : -1;
        _weaponIcon.sprite = slot >= 0 && slot < _weaponIcons.Length ? _weaponIcons[slot] : null;
        _weaponIcon.enabled = _weaponIcon.sprite != null;
        bool canSwitch = _selection != null && _selection.CanSelectWeapon;
        _previousWeaponButton.interactable = _nextWeaponButton.interactable = canSwitch;
        if (_attack == null || !_attack.TryGetCurrentWeaponStats(out var stats))
        {
            _damageValue.text = _intervalValue.text = _projectilesValue.text =
                _projectileSpeedValue.text = _rangeValue.text = _reloadValue.text = "—";
            return;
        }
        _damageValue.text = stats.Damage.ToString("0.#");
        _intervalValue.text = $"{stats.Interval:0.##}초";
        _projectilesValue.text = stats.IsMelee ? "근접 타격" : stats.IsSpray ? "연속 분사" : $"{stats.ProjectileCount}발";
        _projectileSpeedValue.text = stats.IsMelee || stats.IsSpray ? "—" : stats.ProjectileSpeed.ToString("0.#");
        _rangeValue.text = stats.Range.ToString("0.#");
        _reloadValue.text = stats.IsMelee ? "필요 없음" : _ammo != null && _selection != null
            ? $"{_ammo.GetReloadDuration(_selection.CurrentWeapon):0.##}초" : "—";
    }

    private void SelectPreviousWeapon() => SelectWeapon(-1);
    private void SelectNextWeapon() => SelectWeapon(1);
    private void InvestAttack() => ChangeStat(PlayerBaseStat.Attack, true);
    private void InvestMovement() => ChangeStat(PlayerBaseStat.Movement, true);
    private void InvestCritical() => ChangeStat(PlayerBaseStat.CriticalChance, true);
    private void RefundAttack() => ChangeStat(PlayerBaseStat.Attack, false);
    private void RefundMovement() => ChangeStat(PlayerBaseStat.Movement, false);
    private void RefundCritical() => ChangeStat(PlayerBaseStat.CriticalChance, false);
    private void BindStatButtons(bool bind)
    {
        if (_investButtons == null || _refundButtons == null || _investButtons.Length != 3 || _refundButtons.Length != 3) return;
        UnityEngine.Events.UnityAction[] invest = { InvestAttack, InvestMovement, InvestCritical };
        UnityEngine.Events.UnityAction[] refund = { RefundAttack, RefundMovement, RefundCritical };
        for (int i = 0; i < 3; i++)
        {
            if (_investButtons[i] != null)
            {
                if (bind) _investButtons[i].onClick.AddListener(invest[i]);
                else _investButtons[i].onClick.RemoveListener(invest[i]);
            }
            if (_refundButtons[i] != null)
            {
                if (bind) _refundButtons[i].onClick.AddListener(refund[i]);
                else _refundButtons[i].onClick.RemoveListener(refund[i]);
            }
        }
    }
    private void ChangeStat(PlayerBaseStat stat, bool invest)
    {
        GameInput.TryConsumePrimaryPointerPress();
        RefreshOwner();
        if (!IsOpen || _baseStats == null) return;
        if (invest) _baseStats.TryInvest(stat); else _baseStats.TryRefund(stat);
        RefreshValues();
    }
    private void SelectWeapon(int direction)
    {
        GameInput.TryConsumePrimaryPointerPress();
        RefreshOwner();
        if (!IsOpen || _selection == null) return;
        _selection.SelectAdjacentWeapon(direction);
        RefreshValues();
    }

    private static bool IsEditingText()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return false;
        var tmp = selected.GetComponentInParent<TMP_InputField>();
        var legacy = selected.GetComponentInParent<InputField>();
        return (tmp != null && tmp.isFocused) || (legacy != null && legacy.isFocused);
    }

    private static void SetBar(Image image, float value) =>
        image.rectTransform.localScale = new Vector3(Mathf.Clamp01(value), 1, 1);
}
