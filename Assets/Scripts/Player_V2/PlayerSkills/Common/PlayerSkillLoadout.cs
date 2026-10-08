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
}
