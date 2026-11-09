using System;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>Player 성장 원본입니다. 같은 전장에 확정된 처치 경험치를 공유하고 변경을 알립니다.</summary>
[DisallowMultipleComponent]
public sealed partial class PlayerExperience : MonoBehaviour, IPlayerMatchBinding, IPlayerNetworkMatchBinding
{
    [SerializeField, Min(1)] private int _firstLevelExperience = 100;
    [SerializeField, Min(0)] private int _experienceIncreasePerLevel;
    [SerializeField, Range(2, 1000)] private int _maximumLevel = 100;
    private PlayerExperienceState _state;
    private bool _matchBound, _authority;
    private MatchId _matchId;
    private MatchSession _session;
    private NetworkMatchCoordinator _networkMatch;
    private PlayerMatchPresence _presence;
    private static readonly HashSet<PlayerExperience> ActivePlayers = new();
    private PlayerMatchPresence Presence => _presence != null ? _presence : (_presence = GetComponent<PlayerMatchPresence>());
    private PlayerExperienceState State => _state ??= new PlayerExperienceState(
        Mathf.Max(1, _firstLevelExperience), Mathf.Max(0, _experienceIncreasePerLevel), Mathf.Clamp(_maximumLevel, 2, 1000));

    public event Action Changed;
    /// <summary>여러 레벨 상승도 이전/이후 레벨로 알립니다. 후속 성장 보상은 호출부에서 판단합니다.</summary>
    public event Action<int, int> LevelChanged;
    public int Level => State.Level;
    public long TotalExperience => State.TotalExperience;
    public long ExperienceInLevel => State.ExperienceInLevel;
    public long RequiredExperience => State.RequiredExperience;
    public float Progress => State.Progress;
    public bool IsMaximumLevel => State.IsMaximumLevel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRegistry() => ActivePlayers.Clear();
    private void Awake() { _ = State; }
    private void OnEnable() => ActivePlayers.Add(this);
    private void OnDisable() => ActivePlayers.Remove(this);

    public bool AddExperience(int amount)
    {
        if (!isActiveAndEnabled || (_matchBound && !_authority) || !HasCurrentMatch ||
            !FieldSimulationTime.CanAdvance(this)) return false;
        return ApplyExperience(amount);
    }

    private bool HasCurrentMatch => !_matchBound || (_authority
        ? _session != null && _session.IsGameplayRunning
        : _networkMatch != null && _networkMatch.IsGameplayRunning);

    private bool ApplyExperience(int amount)
    {
        int previousLevel = Level;
        if (!State.AddExperience(amount)) return false;
        Changed?.Invoke();
        if (previousLevel != Level) LevelChanged?.Invoke(previousLevel, Level);
        return true;
    }

    /// <summary>Enemy 보상 어댑터의 로컬 확정/서버 전용 RPC 수신 경로입니다. UI 입력으로 호출하지 않습니다.</summary>
    internal static void ShareEnemyExperience(GameObject enemy, int amount, string matchId, bool fromServer)
    {
        if (enemy == null || amount <= 0) return;
        // 알림 구독자가 Player를 비활성화해도 현재 보상 전달 순회를 깨지 않습니다.
        var recipients = new List<PlayerExperience>(ActivePlayers);
        foreach (var player in recipients)
        {
            if (player == null || !player.isActiveAndEnabled || !player.HasCurrentMatch ||
                player.gameObject.scene != enemy.scene) continue;
            var presence = player.Presence;
            if (presence != null && (presence.GameplayCancellation.IsCancellationRequested ||
                presence.Location != MatchPlayerLocation.Field)) continue;
            var time = presence?.TimeSource ?? player.GetComponent<FieldSimulationActor>()?.TimeSource;
            string playerMatch = player._matchBound ? player._matchId.Value : time?.Snapshot.MatchId.Value ?? "";
            if (!string.Equals(playerMatch, matchId ?? "", StringComparison.Ordinal)) continue;
            if (!fromServer && player._matchBound && !player._authority) continue;
            try { player.ApplyExperience(amount); }
            catch (Exception error) { Debug.LogException(error, player); }
        }
    }

    public void BindMatch(MatchSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _networkMatch = null; _matchId = session.Config.MatchId; _matchBound = _authority = true;
    }
    public void BindNetworkMatch(NetworkMatchCoordinator match, PlayerId localPlayerId)
    {
        if (match?.Descriptor == null || localPlayerId.IsEmpty) throw new ArgumentException("Bind the prepared Player match.");
        _networkMatch = match; _session = null; _matchId = match.Descriptor.Config.MatchId;
        _matchBound = true; _authority = false;
    }
    public void UnbindMatch() { _session = null; _networkMatch = null; _authority = false; _matchBound = true; }
    public void UnbindNetworkMatch() => UnbindMatch();
}
