using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>로컬 무기고 요청을 검증하고 같은 PlayerExperience actor의 무기 성장 상태를 소유합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerWeaponSelection), typeof(PlayerExperience))]
public sealed partial class PlayerArmory : MonoBehaviour, ILocalInputConsumer, IHubPlayerBinding
{
    [SerializeField] private PlayerArmorySettings _settings;
    private PlayerArmoryState _state = new();
    private PlayerExperience _experience;
    private PlayerWeaponSelection _selection;
    private PlayerControlState _controls;
    private IInputBlocker _input;
    private IDisposable _menuLock;
    private PlayerArmoryStation _station;
    private bool _hub;
    // 시작 허브 -> 첫 출격에서 장착 선택만 한 번 전달합니다. 레벨·토큰·XP를 전역으로 보관하지 않습니다.
    private static readonly Dictionary<IInputBlocker, (WeaponType first, WeaponType second)> DepartureLoadouts = new();
    internal static readonly HashSet<PlayerArmory> LocalActors = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime() { DepartureLoadouts.Clear(); LocalActors.Clear(); }
    public event Action Changed;
    public PlayerArmorySettings Settings => _settings;
    public PlayerArmoryState State => _state;
    public bool IsConfigured => _settings != null && _settings.IsValid;
    public bool IsOpen => _station != null && _menuLock != null;
    public bool IsInSpaceship => _controls != null && _controls.IsInSpaceship;
    public bool IsAlive => GetComponent<PlayerInfo>() is PlayerInfo info && info.curHp > 0 && !info.IsDestroyed;
    public bool CanOpen => isActiveAndEnabled && IsConfigured && _input != null && !_input.IsBlocked &&
        IsInSpaceship && IsAlive && _controls != null && !_controls.IsBlocked;
    private bool CanModify => IsOpen && isActiveAndEnabled && IsInSpaceship && IsAlive && Time.timeScale > 0 &&
        _station.IsInRange(this) && !IsRemoteClient;
    private bool IsRemoteClient => Unity.Netcode.NetworkManager.Singleton != null &&
        Unity.Netcode.NetworkManager.Singleton.IsListening && !Unity.Netcode.NetworkManager.Singleton.IsServer;
    private void Awake()
    {
        _experience = GetComponent<PlayerExperience>(); _selection = GetComponent<PlayerWeaponSelection>();
        _controls = GetComponent<PlayerControlState>();
    }
    private void OnEnable()
    { if (_experience != null) _experience.LevelChanged += OnLevelChanged; if (_input != null) LocalActors.Add(this); }
    private void OnLevelChanged(int before, int after)
    {
        if (IsConfigured && !IsRemoteClient && _state.GrantLevelRewards(after, _settings.TokensPerLevel)) Changed?.Invoke();
    }
    public void BindInputBlocker(IInputBlocker input)
    {
        Close(); _input = input ?? throw new ArgumentNullException(nameof(input)); LocalActors.Add(this);
        if (!_hub && DepartureLoadouts.Remove(input, out var loadout))
        { _state.Equip(0, loadout.first); _state.Equip(1, loadout.second); NotifyLoadout(); }
    }
    public void BindHub(IInputBlocker input)
    { _hub = true; BindInputBlocker(input); SaveDeparture(); }
    public void UnbindHub() { Close(); _hub = false; LocalActors.Remove(this); _input = null; }
    private void SaveDeparture() { if (_hub && _input != null) DepartureLoadouts[_input] = (_state.First, _state.Second); }
    public bool TryOpen(PlayerArmoryStation station)
    {
        if (station == null || !CanOpen || !station.IsInRange(this)) return false;
        _station = station; _menuLock = _input.Acquire("PlayerArmory");
        GetComponent<PlayerInputHandler>()?.CancelAttackGesture();
        return true;
    }
    public void Close()
    {
        _menuLock?.Dispose(); _menuLock = null; _station = null;
        GameInput.TryConsumePrimaryPointerPress();
    }
    public bool TryEquip(int slot, WeaponType weapon)
    {
        if (!CanModify || !_selection.IsWeaponAuthored(weapon) || !_state.Equip(slot, weapon)) return false;
        NotifyLoadout(); SaveDeparture(); return true;
    }
    private void NotifyLoadout()
    { _selection.RefreshLoadout(); Changed?.Invoke(); }
    public bool CanUpgrade(WeaponType weapon) => CanModify && IsConfigured && _selection.IsWeaponAuthored(weapon) && _state.CanUpgrade(weapon);
    public bool TryUpgrade(WeaponType weapon)
    {
        if (!CanUpgrade(weapon) || !_state.Upgrade(weapon)) return false;
        Changed?.Invoke(); return true;
    }
    public int Level(WeaponType weapon) => _state.Level(weapon);
    public float DamageMultiplier(WeaponType weapon, int level = 0) => IsConfigured && PlayerArmoryState.IsGun(weapon)
        ? _settings.DamageMultiplier(level > 0 ? level : Level(weapon)) : 1;
    public bool TryGetStats(WeaponType weapon, bool next, out PlayerWeaponStats stats)
    {
        stats = default; var attack = GetComponent<PlayerAttack>();
        return attack != null && attack.TryGetWeaponStats(weapon,
            next ? Mathf.Min(Level(weapon) + 1, PlayerArmorySettings.MaximumLevel) : Level(weapon), out stats);
    }
    private void OnDisable()
    {
        Close(); LocalActors.Remove(this);
        if (_experience != null) _experience.LevelChanged -= OnLevelChanged;
    }
}
