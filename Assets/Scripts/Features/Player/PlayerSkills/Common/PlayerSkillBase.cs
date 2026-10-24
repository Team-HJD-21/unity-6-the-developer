using UnityEngine;

/// <summary>스킬의 사용 조건·보유 상태·쿨타임과 입력 진입점을 통일합니다.</summary>
public abstract class PlayerSkillBase : MonoBehaviour
{
    private PlayerSkillController _controller;
    private PlayerControlState _control;
    public abstract PlayerSkillId Id { get; }
    public abstract bool HasPrefab { get; }
    public abstract int AvailableCount { get; }
    public abstract float RemainingCooldown { get; }
    public abstract float CooldownDuration { get; }
    public virtual bool IsReady => HasPrefab && AvailableCount > 0 && RemainingCooldown <= 0;
    protected PlayerSkillController Controller => _controller != null ? _controller : _controller = GetComponent<PlayerSkillController>();
    protected PlayerControlState ControlState => _control != null ? _control : _control = GetComponent<PlayerControlState>();
    protected virtual void Awake() { _controller = GetComponent<PlayerSkillController>(); _control = GetComponent<PlayerControlState>(); }
    public bool TryUseSkill()
    {
        if (!isActiveAndEnabled || !IsReady) return false;
        if (Controller != null)
        {
            if (!Controller.CanUse || !Controller.Loadout.Contains(Id)) return false;
        }
        else if (ControlState != null && !ControlState.CanAct) return false;
        return TryActivate();
    }
    protected abstract bool TryActivate();
}
