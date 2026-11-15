using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>로컬 무기고 요청을 검증하고 같은 플레이 세션에서 무기 성장 상태를 전달합니다.</summary>
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
    private bool _levelRewardsPending;
    // App의 같은 로컬 입력 scope에서 새 허브/필드 actor로 무기고 상태를 전달합니다.
    // 이전 actor가 늦게 정리되어도 새 소유자의 잔액을 덮어쓰지 않습니다.
    private static readonly Dictionary<IInputBlocker, (PlayerArmory owner, PlayerArmoryState state)> SessionStates = new();
    internal static readonly HashSet<PlayerArmory> LocalActors = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime() { SessionStates.Clear(); LocalActors.Clear(); }
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
    {
        if (_experience != null)
        { _experience.LevelChanged += OnLevelChanged; _experience.Changed += QueueLevelRewards; ReconcileLevelRewards(); }
        if (_input != null) LocalActors.Add(this);
    }
    private void QueueLevelRewards() => _levelRewardsPending = true;
    private void LateUpdate() { if (_levelRewardsPending) ReconcileLevelRewards(); }
    private void OnLevelChanged(int before, int after) => ReconcileLevelRewards();
    private void ReconcileLevelRewards()
    {
        _levelRewardsPending = false;
        if (_experience != null && IsConfigured && !IsRemoteClient &&
            _state.GrantLevelRewards(_experience.Level, _settings.TokensPerLevel))
        { SaveSession(); Changed?.Invoke(); }
    }
    public void BindInputBlocker(IInputBlocker input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        Close();
        if (!ReferenceEquals(_input, input) || !SessionStates.TryGetValue(input, out var current) || !ReferenceEquals(current.owner, this))
        {
            SaveSession(); _input = input;
            if (!IsRemoteClient)
            {
                if (SessionStates.TryGetValue(input, out var saved))
                { _state = saved.state.Clone(); NotifyLoadout(); }
                SessionStates[input] = (this, _state.Clone());
            }
        }
        LocalActors.Add(this); ReconcileLevelRewards(); SaveSession();
    }
    public void BindHub(IInputBlocker input)
    { BindInputBlocker(input); }
    public void UnbindHub() { Close(); SaveSession(); LocalActors.Remove(this); _input = null; }
    private void SaveSession()
    {
        if (_input != null && !IsRemoteClient && SessionStates.TryGetValue(_input, out var current) && ReferenceEquals(current.owner, this))
            SessionStates[_input] = (this, _state.Clone());
    }
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
        SaveSession(); NotifyLoadout(); return true;
    }
    private void NotifyLoadout()
    { _selection.RefreshLoadout(); Changed?.Invoke(); }
    public bool CanUpgrade(WeaponType weapon) => CanModify && IsConfigured && _selection.IsWeaponAuthored(weapon) && _state.CanUpgrade(weapon);
    public bool TryUpgrade(WeaponType weapon)
    {
        if (!CanUpgrade(weapon) || !_state.Upgrade(weapon)) return false;
        SaveSession(); Changed?.Invoke(); return true;
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
        Close(); SaveSession(); LocalActors.Remove(this);
        if (_experience != null)
        { _experience.LevelChanged -= OnLevelChanged; _experience.Changed -= QueueLevelRewards; }
    }
}
