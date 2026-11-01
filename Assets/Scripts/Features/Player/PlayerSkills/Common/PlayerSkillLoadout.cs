// 선택 순서대로 Q/E 슬롯에 스킬을 배정하는 Player Feature의 실행 중 준비 데이터입니다.
using System;

public enum PlayerSkillId { None, Bomb, TemporaryTurret, EnergyBarrier, LureBeacon }

public sealed class PlayerSkillLoadout
{
    public PlayerSkillId Q { get; private set; }
    public PlayerSkillId E { get; private set; }
    public event Action Changed;

    public bool Contains(PlayerSkillId skill) => skill != PlayerSkillId.None && (Q == skill || E == skill);

    public bool TrySelect(PlayerSkillId skill)
    {
        if ((int)skill < (int)PlayerSkillId.Bomb || (int)skill > (int)PlayerSkillId.LureBeacon || Contains(skill)) return false;
        if (Q == PlayerSkillId.None) Q = skill;
        else if (E == PlayerSkillId.None) E = skill;
        else return false;
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        if (Q == PlayerSkillId.None && E == PlayerSkillId.None) return;
        Q = E = PlayerSkillId.None;
        Changed?.Invoke();
    }

    /// <summary>Player 스킬 writer가 검증된 Q/E 선택을 한 번에 복원합니다. 선택 제한·사용 비용을 다시 실행하지 않습니다.</summary>
    public void Restore(PlayerSkillId q, PlayerSkillId e)
    {
        if (!Enum.IsDefined(typeof(PlayerSkillId), q) || !Enum.IsDefined(typeof(PlayerSkillId), e) ||
            (q == PlayerSkillId.None && e != PlayerSkillId.None) || (q != PlayerSkillId.None && q == e))
            throw new ArgumentException("Invalid saved Q/E skill selection.");
        if (Q == q && E == e) return;
        Q = q; E = e; Changed?.Invoke();
    }
}
