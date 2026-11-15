using System;
using System.IO;
using UnityEngine;

/// <summary>현재 PlayerExperience 수명의 임시 해금 재화와 하단 후보 네 칸을 소유합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerExperience))]
public sealed class PlayerSkillResearch : MonoBehaviour
{
    private PlayerExperience _experience;
    private PlayerSkillController _controller;
    private PlayerControlState _controls;
    private int _rewardedLevel = 1, _tokens = 1, _unlocked = 1 << (int)PlayerSkillId.Bomb;
    private bool _levelRewardsPending;
    private readonly PlayerSkillId[] _candidates = { PlayerSkillId.Bomb, PlayerSkillId.None, PlayerSkillId.None, PlayerSkillId.None };
    private readonly int[] _upgrades = new int[7];
    public const int MaximumLevel = 3;
    public static float MultiplierAtLevel(int level) => 1f + .15f * Mathf.Clamp(level - 1, 0, MaximumLevel - 1);
    public int Level(PlayerSkillId id) => IsUnlocked(id) ? 1 + _upgrades[(int)id] : 0;
    public float Multiplier(PlayerSkillId id) => MultiplierAtLevel(Level(id));
    public bool CanUpgrade(PlayerSkillId id) => CanModify && IsUnlocked(id) && Level(id) < MaximumLevel && _tokens > 0 && _controller.IsConfiguredSkill(id);
    public bool TryUpgrade(PlayerSkillId id)
    { if (!CanUpgrade(id)) return false; _tokens--; _upgrades[(int)id]++; Changed?.Invoke(); return true; }
    public event Action Changed;
    public int Tokens => _tokens;
    public PlayerSkillId Candidate(int slot) => slot >= 0 && slot < 4 ? _candidates[slot] : PlayerSkillId.None;
    public bool IsUnlocked(PlayerSkillId id) => IsKnown(id) && (_unlocked & (1 << (int)id)) != 0;
    public bool Contains(PlayerSkillId id) => id != PlayerSkillId.None && Array.IndexOf(_candidates, id) >= 0;
    public static bool IsKnown(PlayerSkillId id) => id >= PlayerSkillId.Bomb && id <= PlayerSkillId.DualWield;
    public static PlayerSkillId Prerequisite(PlayerSkillId id) => id switch
    {
        PlayerSkillId.DualWield or PlayerSkillId.PlayerTurret => PlayerSkillId.Bomb,
        PlayerSkillId.EnergyBarrier => PlayerSkillId.PlayerTurret,
        PlayerSkillId.LureBeacon => PlayerSkillId.EnergyBarrier,
        PlayerSkillId.Portal => PlayerSkillId.LureBeacon,
        _ => PlayerSkillId.None
    };
    public bool CanModify => isActiveAndEnabled && Time.timeScale > 0 && _controller != null &&
        (_controls == null || !_controls.IsBlocked) &&
        (_controls == null || _controls.CanPrepare || _controls.IsInSpaceship) &&
        GetComponent<PlayerInfo>() is PlayerInfo health && !health.IsDestroyed && health.curHp > 0 &&
        (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening ||
            Unity.Netcode.NetworkManager.Singleton.IsServer);
    public bool CanChangeCandidates => CanModify && _controls != null && _controls.IsInSpaceship;
    private void Awake()
    { _experience = GetComponent<PlayerExperience>(); _controller = GetComponent<PlayerSkillController>(); _controls = GetComponent<PlayerControlState>(); }
    private void OnEnable()
    {
        if (_experience == null) return;
        _experience.LevelChanged += OnLevelChanged; _experience.Changed += QueueLevelRewards;
        ReconcileLevelRewards();
    }
    private void OnDisable()
    {
        if (_experience == null) return;
        _experience.LevelChanged -= OnLevelChanged; _experience.Changed -= QueueLevelRewards;
    }
    private void OnLevelChanged(int before, int after) => ReconcileLevelRewards();
    private void QueueLevelRewards() => _levelRewardsPending = true;
    // 복원은 LevelChanged를 발생시키지 않습니다. 같은 프레임의 Apply/Rollback이
    // 모두 끝난 실제 레벨을 정산해 중간 복원 상태에 토큰을 지급하지 않습니다.
    private void LateUpdate() { if (_levelRewardsPending) ReconcileLevelRewards(); }
    private void ReconcileLevelRewards()
    {
        _levelRewardsPending = false;
        if (_experience == null || (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening && !Unity.Netcode.NetworkManager.Singleton.IsServer)) return;
        int after = _experience.Level;
        if (after <= _rewardedLevel) return;
        _tokens += after - _rewardedLevel; _rewardedLevel = after; Changed?.Invoke();
    }
    public bool CanUnlock(PlayerSkillId id) => CanModify && IsKnown(id) && !IsUnlocked(id) && _tokens > 0 &&
        IsUnlocked(Prerequisite(id)) && _controller.IsConfiguredSkill(id);
    public bool TryUnlock(PlayerSkillId id)
    {
        if (!CanUnlock(id)) return false;
        _tokens--; _unlocked |= 1 << (int)id; Changed?.Invoke(); return true;
    }
    public bool TrySetCandidate(int slot, PlayerSkillId id)
    {
        if (!CanChangeCandidates || slot < 0 || slot >= 4 || !IsUnlocked(id) || !_controller.IsConfiguredSkill(id) || _candidates[slot] == id) return false;
        int previous = Array.IndexOf(_candidates, id);
        if (previous >= 0) _candidates[previous] = _candidates[slot];
        _candidates[slot] = id;
        _controller.OnCandidatesChanged(); Changed?.Invoke(); return true;
    }
    internal Snapshot Capture() => new(_tokens, _rewardedLevel, _unlocked, (PlayerSkillId[])_candidates.Clone(), _upgrades);
    internal void Restore(Snapshot state)
    {
        _tokens = state.Tokens; _rewardedLevel = state.RewardedLevel; _unlocked = state.Unlocked;
        Array.Copy(state.Candidates, _candidates, 4);
        Array.Copy(state.Upgrades, _upgrades, 7);
        QueueLevelRewards();
        Changed?.Invoke();
    }
    internal sealed class Snapshot
    {
        public readonly int Tokens, RewardedLevel, Unlocked;
        public readonly PlayerSkillId[] Candidates;
        public readonly int[] Upgrades;
        public Snapshot(int tokens, int rewarded, int unlocked, PlayerSkillId[] candidates, int[] upgrades, bool legacy = false)
        {
            int knownMask = 0; for (int i=1;i<=(int)PlayerSkillId.DualWield;i++) knownMask |= 1<<i;
            if (tokens < 0 || rewarded < 1 || rewarded > 1000 || tokens > rewarded - (legacy ? 1 : 0) ||
                (unlocked & ~knownMask) != 0 || (unlocked & (1 << (int)PlayerSkillId.Bomb)) == 0 || candidates == null || candidates.Length != 4 || upgrades == null || upgrades.Length != 7 || upgrades[0] != 0)
                throw new InvalidDataException("Invalid skill research state.");
            int spent = 0;
            for (int i=2;i<=(int)PlayerSkillId.DualWield;i++)
                if ((unlocked & (1<<i)) != 0)
                {
                    spent++;
                    if ((unlocked & (1 << (int)Prerequisite((PlayerSkillId)i))) == 0) throw new InvalidDataException("Missing skill prerequisite.");
                }
            for(int i=1;i<7;i++)
            {
                if (upgrades[i] < 0 || upgrades[i] >= MaximumLevel || upgrades[i] > 0 && (unlocked & (1<<i)) == 0)
                    throw new InvalidDataException("Invalid skill upgrade level.");
                spent += upgrades[i];
            }
            if ((long)tokens + spent != rewarded - (legacy ? 1 : 0)) throw new InvalidDataException("Invalid skill token accounting.");
            int seen = 0;
            foreach (var id in candidates)
            {
                if (id == PlayerSkillId.None) continue;
                if (!IsKnown(id) || (unlocked & (1 << (int)id)) == 0 || (seen & (1 << (int)id)) != 0)
                    throw new InvalidDataException("Invalid skill candidates.");
                seen |= 1 << (int)id;
            }
            Tokens=tokens+(legacy ? 1 : 0); RewardedLevel=rewarded; Unlocked=unlocked; Candidates=(PlayerSkillId[])candidates.Clone();
            Upgrades=(int[])upgrades.Clone();
        }
        public void Write(BinaryWriter writer)
        { writer.Write(Tokens); writer.Write(RewardedLevel); writer.Write(Unlocked); foreach(var id in Candidates)writer.Write((int)id); foreach(var value in Upgrades)writer.Write(value); }
        public static Snapshot Read(BinaryReader reader, bool legacy = false) => new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
            new[]{(PlayerSkillId)reader.ReadInt32(),(PlayerSkillId)reader.ReadInt32(),(PlayerSkillId)reader.ReadInt32(),(PlayerSkillId)reader.ReadInt32()},
            new[]{reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32()}, legacy);
    }
}
