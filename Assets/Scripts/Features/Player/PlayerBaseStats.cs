using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>기본 스탯과 레벨 보상 원본입니다. 현재는 로컬/Host 쓰기만 허용하며 Client 명령 전송은 Core 연결 대상으로 남깁니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerExperience))]
public sealed partial class PlayerBaseStats : MonoBehaviour, IPlayerMatchBinding, IPlayerNetworkMatchBinding
{
    [SerializeField] private PlayerBaseStatsSettings _settings;
    private PlayerBaseStatsState _state;
    private PlayerExperience _experience;
    private PlayerControlState _controls;
    private MatchSession _session;
    private MatchId _matchId;
    private bool _matchBound, _authority;
    private IRandomSource _criticalRandom;
    private IDisposable _randomLease;
    private PlayerBaseStatsState State => _state ??= new PlayerBaseStatsState(_experience.MaximumLevel,
        _settings.CriticalPointLimit, _settings.PointsPerLevelReward);
    public event Action Changed;
    public bool IsConfigured => _settings != null && _settings.IsValid && _experience != null;
    public bool CanModify => isActiveAndEnabled && IsConfigured && (_controls == null || !_controls.IsBlocked) &&
        (!_matchBound || (_authority && _session != null && _session.IsGameplayRunning));
    public int AvailablePoints => IsConfigured ? State.AvailablePoints : 0;
    public int PointsPerLevelReward => _settings != null ? _settings.PointsPerLevelReward : 0;
    public float AttackMultiplier => IsConfigured ? 1 + State.Invested(PlayerBaseStat.Attack) * _settings.AttackPerPoint : 1;
    public float MovementMultiplier => IsConfigured ? 1 + State.Invested(PlayerBaseStat.Movement) * _settings.MovementPerPoint : 1;
    public float CriticalChance => IsConfigured ? Mathf.Min(_settings.CriticalChanceCap,
        State.Invested(PlayerBaseStat.CriticalChance) * _settings.CriticalChancePerPoint) : 0;
    public bool HasCriticalRandom => _criticalRandom != null && _criticalRandom.IsAvailable;

    private void Awake()
    {
        _experience = GetComponent<PlayerExperience>(); _controls = GetComponent<PlayerControlState>();
        if (!IsConfigured) Debug.LogWarning("[PlayerBaseStats] Connect the authored base stats settings.", this);
    }
    public int Invested(PlayerBaseStat stat) => IsConfigured ? State.Invested(stat) : 0;
    public bool IsRewardClaimed(int level) => IsConfigured && State.IsClaimed(level);
    public bool CanClaimReward(int level) => CanModify && State.CanClaim(level, _experience.Level);
    public bool CanInvest(PlayerBaseStat stat) => CanModify && State.CanInvest(stat);
    public bool CanRefund(PlayerBaseStat stat) => CanModify && State.Invested(stat) > 0;
    public bool TryClaimReward(int level) => Publish(CanModify && State.TryClaim(level, _experience.Level));
    public bool TryInvest(PlayerBaseStat stat) => Publish(CanModify && State.TryInvest(stat));
    public bool TryRefund(PlayerBaseStat stat) => Publish(CanModify && State.TryRefund(stat));
    public bool TryResetInvestments() => Publish(CanModify && State.ResetInvestments());
    private bool Publish(bool changed)
    {
        if (changed) { Changed?.Invoke(); GrowthChanged?.Invoke(GrowthSaveUrgency.Immediate); }
        return changed;
    }

    /// <summary>일반 피해는 표시/계산 공통, 치명타는 실제 공격에서만 권위 Match 난수를 사용합니다.</summary>
    public float ScaleWeaponDamage(float damage) => Mathf.Max(0, damage) * AttackMultiplier;
    public float ResolveWeaponDamage(float damage)
    {
        float scaled = ScaleWeaponDamage(damage);
        // 비주입 시험 Scene/Client에서 임의의 전역 난수로 게임 판정을 대신하지 않습니다.
        if (CriticalChance > 0 && (!_matchBound || _authority) && HasCriticalRandom && _criticalRandom.Chance(CriticalChance))
            scaled *= _settings.CriticalDamageMultiplier;
        return scaled;
    }

    public void BindMatch(MatchSession session)
    {
        ReleaseRandom();
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _matchBound = _authority = true; _matchId = session.Config.MatchId;
        // Player는 Scene 난수 자동 수집 이후 생성되므로 자신의 명시적 Match 바인딩에서 연결합니다.
        if (session.Random != null) _randomLease = RandomBinding.Bind(session.Random, "combat/player-critical", ref _criticalRandom);
    }
    public void BindNetworkMatch(NetworkMatchCoordinator match, PlayerId localPlayerId)
    {
        if (match?.Descriptor == null || localPlayerId.IsEmpty) throw new ArgumentException("Bind the prepared Player match.");
        ReleaseRandom(); _session = null; _matchBound = true; _authority = false;
        _matchId = match.Descriptor.Config.MatchId;
    }
    public void UnbindMatch() { ReleaseRandom(); _session = null; _matchBound = true; _authority = false; }
    public void UnbindNetworkMatch() => UnbindMatch();
    private void ReleaseRandom() { _randomLease?.Dispose(); _randomLease = null; _criticalRandom = null; }
    private void OnDestroy() => ReleaseRandom();
}
