using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>제작된 무기고 UI를 실제 전투 계산과 같은 스탯으로 갱신합니다.</summary>
public sealed class PlayerArmoryView : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _prompt, _tokens, _loadout, _comparison;
    [SerializeField] private Button _close, _upgrade, _equipFirst, _equipSecond;
    [SerializeField] private Button[] _weapons;
    [SerializeField] private TMP_Text[] _weaponLabels;
    [SerializeField] private Image _selectedIcon;
    [SerializeField] private Sprite[] _weaponIcons;
    private PlayerArmory _owner;
    private PlayerArmoryStation _station;
    private WeaponType _selected = WeaponType.DefaultGun;
    private static readonly WeaponType[] Guns = { WeaponType.DefaultGun, WeaponType.CryoBlaster, WeaponType.Sniper };
    public bool IsConfigured => _panel != null && _prompt != null && _tokens != null && _loadout != null && _comparison != null &&
        _close != null && _upgrade != null && _equipFirst != null && _equipSecond != null && _weapons != null && _weapons.Length == 3 &&
        System.Array.TrueForAll(_weapons, b => b != null) && _weaponLabels != null && _weaponLabels.Length == 3 &&
        System.Array.TrueForAll(_weaponLabels, t => t != null) && _selectedIcon != null && _weaponIcons != null && _weaponIcons.Length == 3;
    public bool IsOpen => _panel != null && _panel.activeSelf;
    public string ComparisonText => _comparison != null ? _comparison.text : "";
    private void Awake()
    {
        Hide();
        _close.onClick.AddListener(() => _station?.Close());
        _upgrade.onClick.AddListener(() => { if (_owner != null) _owner.TryUpgrade(_selected); Refresh(); });
        _equipFirst.onClick.AddListener(() => { if (_owner != null) _owner.TryEquip(0, _selected); Refresh(); });
        _equipSecond.onClick.AddListener(() => { if (_owner != null) _owner.TryEquip(1, _selected); Refresh(); });
        for (int i = 0; i < Guns.Length; i++) { WeaponType weapon = Guns[i]; _weapons[i].onClick.AddListener(() => Select(weapon)); }
    }
    public void Bind(PlayerArmory owner, PlayerArmoryStation station)
    {
        Hide(); _owner = owner; _station = station; _selected = owner.State.First;
        _owner.Changed += Refresh; _panel.SetActive(true); ShowPrompt(false); Refresh();
    }
    public void Select(WeaponType weapon) { if (PlayerArmoryState.IsGun(weapon)) { _selected = weapon; Refresh(); } }
    public void ShowPrompt(bool visible) { if (_prompt != null) _prompt.gameObject.SetActive(visible); }
    public void Hide()
    {
        if (_owner != null) _owner.Changed -= Refresh;
        _owner = null; _station = null;
        if (_panel != null) _panel.SetActive(false); ShowPrompt(false);
    }
    public static string Name(WeaponType weapon) => weapon switch
    { WeaponType.DefaultGun => "기본총", WeaponType.CryoBlaster => "냉각 스프레이", WeaponType.Sniper => "스나이퍼", _ => "스패너" };
    public void Refresh()
    {
        if (_owner == null || !_owner.IsConfigured) return;
        var state = _owner.State; int level = state.Level(_selected); bool max = level == PlayerArmorySettings.MaximumLevel;
        _tokens.text = $"강화 토큰  {state.Tokens}개";
        _loadout.text = $"고정  스패너     /     총기 1  {Name(state.First)}     /     총기 2  {Name(state.Second)}";
        for (int i = 0; i < Guns.Length; i++)
        {
            var weapon = Guns[i]; string equipped = state.First == weapon ? "총기 1 장착" : state.Second == weapon ? "총기 2 장착" : "미장착";
            _weaponLabels[i].text = $"<line-height=34>{Name(weapon)}\n{state.Level(weapon)}레벨 / {equipped}";
            _weapons[i].image.color = weapon == _selected ? new Color(.3f, .3f, .23f) : new Color(.1f, .13f, .12f);
        }
        _selectedIcon.sprite = _weaponIcons[System.Array.IndexOf(Guns, _selected)]; _selectedIcon.preserveAspect = true;
        _equipFirst.interactable = state.First != _selected;
        _equipSecond.interactable = state.Second != _selected;
        _upgrade.interactable = _owner.CanUpgrade(_selected);
        _upgrade.GetComponentInChildren<TMP_Text>().text = max ? "최고 레벨" : "강화   /   토큰 1개";
        if (!_owner.TryGetStats(_selected, false, out var before) || !_owner.TryGetStats(_selected, true, out var after))
        { _comparison.text = "무기 설정을 확인할 수 없습니다."; return; }
        string row(string name, string current, string next) => $"{name}<pos=300>{current}<pos=410>><pos=450>{next}\n";
        _comparison.text = $"<line-height=34>{Name(_selected)}\n" +
            row("능력치", "현재", max ? "최고 단계" : "강화 후") +
            row("무기 레벨", level.ToString(), Mathf.Min(level + 1, PlayerArmorySettings.MaximumLevel).ToString()) +
            row("피해량", before.Damage.ToString("0.#"), after.Damage.ToString("0.#")) +
            row("공격 간격", before.Interval.ToString("0.###") + "초", after.Interval.ToString("0.###") + "초") +
            row("사거리", before.Range.ToString("0.#"), after.Range.ToString("0.#"));
        int nextLevel = Mathf.Min(level + 1, PlayerArmorySettings.MaximumLevel);
        if (_selected == WeaponType.DefaultGun) _comparison.text += row("발사 탄환 수", before.ProjectileCount.ToString(), after.ProjectileCount.ToString());
        if (_selected == WeaponType.Sniper) _comparison.text += row("최대 적중 적 수", _owner.Settings.SniperTargets(level).ToString(), _owner.Settings.SniperTargets(nextLevel).ToString());
        if (_selected == WeaponType.CryoBlaster)
            _comparison.text += row("둔화율 (예정)", $"{(1 - _owner.Settings.SlowMultiplier(level)) * 100:0}%", $"{(1 - _owner.Settings.SlowMultiplier(nextLevel)) * 100:0}%") +
                "\n<size=20>둔화 효과는 추후 적용됩니다.\n현재는 피해만 적용됩니다.</size>";
        if (max) _comparison.text += "\n<size=20>최고 무기 레벨에 도달했습니다.</size>";
        else if (state.Tokens == 0) _comparison.text += "\n<size=20>레벨을 올리면 강화 토큰을 받을 수 있습니다.</size>";
    }
    private void OnDisable() { if (_owner != null) _station?.Close(); Hide(); }
}
