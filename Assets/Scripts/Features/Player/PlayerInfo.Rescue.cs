// Host 구조 조율부가 Player의 실제 생존·위치와 복귀를 사용하는 어댑터입니다.
// 구조 자격/유지 시간/팀 예산은 Core가 검증하며 여기서는 HP·생명 잠금 원본만 적용합니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
public partial class PlayerInfo : IPlayerRescueActor, IPlayerRescueConsumer
{
    private PlayerId _rescuePlayer;
    private IPlayerRescueService _rescueRequests;
    /// <summary>F 상호작용 중재용 실제 구조 대상입니다. UI가 새 구조 자격을 만들지 않습니다.</summary>
    public bool HasRescueTarget => _rescueRequests?.IsReady == true && _rescueRequests.TryFindTarget(_rescuePlayer, out _);
    private bool HasHeldRescueTarget => _rescueRequests?.IsReady == true && GameInput.IsPressed(GameKey.F) &&
        _rescueRequests.TryFindTarget(_rescuePlayer, out _);

    public void BindRescue(PlayerId player, IPlayerRescueService service)
    {
        if (player.IsEmpty || service == null) throw new ArgumentException("Bind the current local rescue requester.");
        UnbindRescue(); _rescuePlayer = player; _rescueRequests = service;
    }
    public void UnbindRescue()
    {
        _rescueRequests?.SetRescueHeld(_rescuePlayer, default, false);
        _rescueRequests = null; _rescuePlayer = default;
    }
    private void PumpRescueInput()
    {
        if (_rescueRequests == null) return;
        PlayerId target = default;
        bool held = GameInput.IsPressed(GameKey.F) && _rescueRequests.TryFindTarget(_rescuePlayer, out target);
        if (held) _rescueRequests.SetRescueHeld(_rescuePlayer, target, true);
        else _rescueRequests.SetRescueHeld(_rescuePlayer, default, false);
    }

    bool IPlayerRescueActor.TryCaptureRescue(out PlayerInteractionSnapshot snapshot)
    {
        snapshot = default;
        var controls = GetComponent<PlayerControlState>();
        if (!isActiveAndEnabled || controls == null || !controls.TryCaptureInteraction(out var current)) return false;
        snapshot = new PlayerInteractionSnapshot(current.Position, !IsDestroyed, current.CanInteract && !IsDestroyed);
        return true;
    }

    bool IPlayerRescueActor.TryRevive(double maximumHealthRatio)
    {
        if (!_matchBound || !CanWriteHealth || !isActiveAndEnabled || !IsDestroyed || maxHp <= 0 ||
            double.IsNaN(maximumHealthRatio) || double.IsInfinity(maximumHealthRatio) || maximumHealthRatio <= 0 || maximumHealthRatio > 1)
            return false;
        _isDead = false;
        _recovering = false;
        curHp = Math.Max(1, Math.Min(maxHp, (int)Math.Ceiling(maxHp * maximumHealthRatio)));
        GetComponent<PlayerControlState>()?.SetLifeBlocked(false);
        GetComponent<PlayerMovement>()?.StopImmediately();
        onHpChange.Invoke(curHp, maxHp);
        return true;
    }
}
